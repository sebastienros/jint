using System.Collections.Generic;
using System.Linq;
using Jint.Collections;
using Jint.Native.Global;
using Jint.Native.Object;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Json
{
    public class JsonSerializer
    {
        private readonly Engine _engine;
        private ObjectTraverseStack _stack;
        private string _indent, _gap;
        private List<JsValue> _propertyList;
        private JsValue _replacerFunction = Undefined.Instance;

        public JsonSerializer(Engine engine)
        {
            _engine = engine;
        }

        public JsValue Serialize(JsValue value, JsValue replacer, JsValue space)
        {
            _stack = new ObjectTraverseStack(_engine);

            // for JSON.stringify(), any function passed as the first argument will return undefined
            // if the replacer is not defined. The function is not called either.
            if (value.IsCallable && ReferenceEquals(replacer, Undefined.Instance))
            {
                return Undefined.Instance;
            }

            if (replacer.IsObject())
            {
                if (replacer is ICallable)
                {
                    _replacerFunction = replacer;
                }
                else
                {
                    var replacerObj = replacer.AsObject();
                    if (replacerObj.Class == ObjectClass.Array)
                    {
                        _propertyList = new List<JsValue>();
                    }

                    foreach (var property in replacerObj.GetOwnProperties().Select(x => x.Value))
                    {
                        JsValue v = _engine.GetValue(property, false);
                        string item = null;
                        if (v.IsString())
                        {
                            item = v.ToString();
                        }
                        else if (v.IsNumber())
                        {
                            item = TypeConverter.ToString(v);
                        }
                        else if (v.IsObject())
                        {
                            var propertyObj = v.AsObject();
                            if (propertyObj.Class == ObjectClass.String || propertyObj.Class == ObjectClass.Number)
                            {
                                item = TypeConverter.ToString(v);
                            }
                        }

                        if (item != null && !_propertyList.Contains(item))
                        {
                            _propertyList.Add(item);
                        }

                    }
                }
            }

            if (space.IsObject())
            {
                var spaceObj = space.AsObject();
                if (spaceObj.Class == ObjectClass.Number)
                {
                    space = TypeConverter.ToNumber(spaceObj);
                }
                else if (spaceObj.Class == ObjectClass.String)
                {
                    space = TypeConverter.ToJsString(spaceObj);
                }
            }

            // defining the gap
            if (space.IsNumber())
            {
                var number = ((JsNumber) space)._value;
                if (number > 0)
                {
                    _gap = new string(' ', (int) System.Math.Min(10, number));
                }
                else
                {
                    _gap = string.Empty;
                }
            }
            else if (space.IsString())
            {
                var stringSpace = space.ToString();
                _gap = stringSpace.Length <= 10 ? stringSpace : stringSpace.Substring(0, 10);
            }
            else
            {
                _gap = string.Empty;
            }

            var wrapper = _engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
            wrapper.DefineOwnProperty(JsString.Empty, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));

            return Str(JsString.Empty, wrapper);
        }

        private JsValue Str(JsValue key, JsValue holder)
        {
            var value = holder.Get(key, holder);
            if (value.IsObject())
            {
                var toJson = value.AsObject().Get("toJSON", value);
                if (toJson.IsObject())
                {
                    if (toJson.AsObject() is ICallable callableToJson)
                    {
                        value = callableToJson.Call(value, Arguments.From(key));
                    }
                }
            }

            if (!ReferenceEquals(_replacerFunction, Undefined.Instance))
            {
                var replacerFunctionCallable = (ICallable)_replacerFunction.AsObject();
                value = replacerFunctionCallable.Call(holder, Arguments.From(key, value));
            }

            if (value.IsObject())
            {
                var valueObj = value.AsObject();
                switch (valueObj.Class)
                {
                    case ObjectClass.Number:
                        value = TypeConverter.ToNumber(value);
                        break;
                    case ObjectClass.String:
                        value = TypeConverter.ToString(value);
                        break;
                    case ObjectClass.Boolean:
                        value = TypeConverter.ToPrimitive(value);
                        break;
                    default:
                        value = SerializesAsArray(value)
                            ? SerializeArray(value)
                            : SerializeObject(value.AsObject());
                        return value;
                }
            }

            if (ReferenceEquals(value, Null.Instance))
            {
                return JsString.NullString;
            }

            if (value.IsBoolean())
            {
                return ((JsBoolean) value)._value ? JsString.TrueString : JsString.FalseString;
            }

            if (value.IsString())
            {
                return Quote(value.ToString());
            }

            if (value.IsNumber())
            {
                var isFinite = GlobalObject.IsFinite(Undefined.Instance, Arguments.From(value));
                if (((JsBoolean) isFinite)._value)
                {
                    return TypeConverter.ToJsString(value);
                }

                return JsString.NullString;
            }

            var isCallable = value.IsObject() && value.AsObject() is ICallable;

            if (value.IsObject() && isCallable == false)
            {
                return SerializesAsArray(value)
                    ? SerializeArray(value)
                    : SerializeObject(value.AsObject());
            }

            return JsValue.Undefined;
        }

        private static bool SerializesAsArray(JsValue value)
        {
            return value.AsObject().Class == ObjectClass.Array || value is ObjectWrapper { IsArrayLike: true };
        }

        private static string Quote(string value)
        {
            using var stringBuilder = StringBuilderPool.Rent();
            var sb = stringBuilder.Builder;
            sb.Append("\"");

            foreach (var c in value)
            {
                switch (c)
                {
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int) c).ToString("x4"));
                        }
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append("\"");
            return sb.ToString();
        }

        private string SerializeArray(JsValue value)
        {
            _stack.Enter(value);
            var stepback = _indent;
            _indent = _indent + _gap;
            var partial = new List<string>();
            var len = TypeConverter.ToUint32(value.Get(CommonProperties.Length, value));
            for (int i = 0; i < len; i++)
            {
                var strP = Str(i, value);
                if (strP.IsUndefined())
                {
                    strP = JsString.NullString;
                }
                partial.Add(strP.ToString());
            }

            if (partial.Count == 0)
            {
                _stack.Exit();
                return "[]";
            }

            string final;
            if (_gap == "")
            {
                const string separator = ",";
                var properties = string.Join(separator, partial);
                final = "[" + properties + "]";
            }
            else
            {
                var separator = ",\n" + _indent;
                var properties = string.Join(separator, partial);
                final = "[\n" + _indent + properties + "\n" + stepback + "]";
            }

            _stack.Exit();
            _indent = stepback;
            return final;
        }

        private string SerializeObject(ObjectInstance value)
        {
            string final;

            _stack.Enter(value);
            var stepback = _indent;
            _indent += _gap;

            var k = _propertyList ?? value.GetOwnProperties()
                .Where(x => x.Value.Enumerable)
                .Select(x => x.Key);

            var partial = new List<string>();
            foreach (var p in k)
            {
                var strP = Str(p, value);
                if (!strP.IsUndefined())
                {
                    var member = Quote(p.ToString()) + ":";
                    if (_gap != "")
                    {
                        member += " ";
                    }
                    member += strP.AsString(); // TODO:This could be undefined
                    partial.Add(member);
                }
            }
            if (partial.Count == 0)
            {
                final = "{}";
            }
            else
            {
                if (_gap == "")
                {
                    const string separator = ",";
                    var properties = string.Join(separator, partial);
                    final = "{" + properties + "}";
                }
                else
                {
                    var separator = ",\n" + _indent;
                    var properties = string.Join(separator, partial);
                    final = "{\n" + _indent + properties + "\n" + stepback + "}";
                }
            }
            _stack.Exit();
            _indent = stepback;
            return final;
        }
    }
}
