using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.BigInt;
using Jint.Native.Boolean;
using Jint.Native.Global;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Proxy;
using Jint.Native.String;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Json
{
    public class JsonSerializer
    {
        private readonly Engine _engine;
        private ObjectTraverseStack _stack = null!;
        private string? _indent;
        private string? _gap;
        private List<JsValue>? _propertyList;
        private JsValue _replacerFunction = Undefined.Instance;

        private static readonly JsString toJsonProperty = new("toJSON");

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

            if (replacer is ObjectInstance oi)
            {
                if (oi.IsCallable)
                {
                    _replacerFunction = replacer;
                }
                else
                {
                    if (oi.IsArray())
                    {
                        _propertyList = new List<JsValue>();
                        var len = oi.Length;
                        var k = 0;
                        while (k < len)
                        {
                            var prop = JsString.Create(k);
                            var v = replacer.Get(prop);
                            var item = JsValue.Undefined;
                            if (v.IsString())
                            {
                                item = v;
                            }
                            else if (v.IsNumber())
                            {
                                item = TypeConverter.ToString(v);
                            }
                            else if (v.IsObject())
                            {
                                if (v is StringInstance or NumberInstance)
                                {
                                    item = TypeConverter.ToString(v);
                                }
                            }

                            if (!item.IsUndefined() && !_propertyList.Contains(item))
                            {
                                _propertyList.Add(item);
                            }

                            k++;
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

            return SerializeJSONProperty(JsString.Empty, wrapper);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-serializejsonproperty
        /// </summary>
        private JsValue SerializeJSONProperty(JsValue key, JsValue holder)
        {
            var value = holder.Get(key, holder);
            var isBigInt = value is BigIntInstance || value.IsBigInt();
            if (value.IsObject() || isBigInt)
            {
                var toJson = value.GetV(_engine.Realm, toJsonProperty);
                if (toJson.IsUndefined() && isBigInt)
                {
                    toJson = _engine.Realm.Intrinsics.BigInt.PrototypeObject.Get(toJsonProperty);
                }
                if (toJson.IsObject())
                {
                    if (toJson.AsObject() is ICallable callableToJson)
                    {
                        value = callableToJson.Call(value, Arguments.From(TypeConverter.ToPropertyKey(key)));
                    }
                }
            }

            if (!_replacerFunction.IsUndefined())
            {
                var replacerFunctionCallable = (ICallable) _replacerFunction.AsObject();
                value = replacerFunctionCallable.Call(holder, Arguments.From(TypeConverter.ToPropertyKey(key), value));
            }

            if (value.IsObject())
            {
                value = value switch
                {
                    NumberInstance => TypeConverter.ToNumber(value),
                    StringInstance => TypeConverter.ToString(value),
                    BooleanInstance booleanInstance => booleanInstance.BooleanData,
                    BigIntInstance bigIntInstance => bigIntInstance.BigIntData,
                    _ => value
                };
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
                return QuoteJSONString(value.ToString());
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

            if (value.IsBigInt())
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm, "Do not know how to serialize a BigInt");
            }

            if (value is ObjectInstance { IsCallable: false } objectInstance)
            {
                if (SerializesAsArray(objectInstance))
                {
                    return SerializeJSONArray(objectInstance);
                }

                if (objectInstance is IObjectWrapper wrapper
                    && _engine.Options.Interop.SerializeToJson is { } serialize)
                {
                    return serialize(wrapper.Target);
                }

                return SerializeJSONObject(objectInstance);
            }

            return JsValue.Undefined;
        }

        private static bool SerializesAsArray(ObjectInstance value)
        {
            if (value is ArrayInstance)
            {
                return true;
            }

            if (value is ProxyInstance proxyInstance && SerializesAsArray(proxyInstance._target))
            {
                return true;
            }

            if (value is ObjectWrapper { IsArrayLike: true })
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-quotejsonstring
        /// </summary>
        private static string QuoteJSONString(string value)
        {
            using var stringBuilder = StringBuilderPool.Rent();
            var sb = stringBuilder.Builder;
            sb.Append('"');

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
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
                        if (char.IsSurrogatePair(value, i))
                        {
                            sb.Append(value[i]);
                            i++;
                            sb.Append(value[i]);
                        }
                        else if (c < 0x20 || char.IsSurrogate(c))
                        {
                            sb.Append("\\u");
                            sb.Append(((int) c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-serializejsonarray
        /// </summary>
        private string SerializeJSONArray(ObjectInstance value)
        {
            _stack.Enter(value);
            var stepback = _indent;
            _indent += _gap;
            var partial = new List<string>();
            var len = TypeConverter.ToUint32(value.Get(CommonProperties.Length, value));
            for (int i = 0; i < len; i++)
            {
                var strP = SerializeJSONProperty(i, value);
                if (strP.IsUndefined())
                {
                    strP = JsString.NullString;
                }
                partial.Add(strP.ToString());
            }

            if (partial.Count == 0)
            {
                _stack.Exit();
                _indent = stepback;
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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-serializejsonobject
        /// </summary>
        private string SerializeJSONObject(ObjectInstance value)
        {
            string final;

            _stack.Enter(value);
            var stepback = _indent;
            _indent += _gap;

            var k = (IEnumerable<JsValue>?) _propertyList ?? value.EnumerableOwnPropertyNames(ObjectInstance.EnumerableOwnPropertyNamesKind.Key);

            var partial = new List<string>();
            foreach (var p in k)
            {
                var strP = SerializeJSONProperty(p, value);
                if (!strP.IsUndefined())
                {
                    var member = QuoteJSONString(p.ToString()) + ":";
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
