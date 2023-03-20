using System.Runtime.CompilerServices;
using System.Text;
using Jint.Collections;
using Jint.Native.BigInt;
using Jint.Native.Boolean;
using Jint.Native.Number;
using Jint.Native.Number.Dtoa;
using Jint.Native.Object;
using Jint.Native.Proxy;
using Jint.Native.String;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Json
{
    public sealed class JsonSerializer
    {
        private readonly Engine _engine;
        private ObjectTraverseStack _stack = null!;
        private string? _indent;
        private string _gap = string.Empty;
        private List<JsValue>? _propertyList;
        private JsValue _replacerFunction = JsValue.Undefined;

        private static readonly JsString toJsonProperty = new("toJSON");

        public JsonSerializer(Engine engine)
        {
            _engine = engine;
        }

        public JsValue Serialize(JsValue value)
        {
            return Serialize(value, JsValue.Undefined, JsValue.Undefined);
        }

        public JsValue Serialize(JsValue value, JsValue replacer, JsValue space)
        {
            _stack = new ObjectTraverseStack(_engine);

            // for JSON.stringify(), any function passed as the first argument will return undefined
            // if the replacer is not defined. The function is not called either.
            if (value.IsCallable && ReferenceEquals(replacer, JsValue.Undefined))
            {
                return JsValue.Undefined;
            }

            SetupReplacer(replacer);
            _gap = BuildSpacingGap(space);

            var wrapper = _engine.Realm.Intrinsics.Object.Construct(Arguments.Empty);
            wrapper.DefineOwnProperty(JsString.Empty, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));

            SerializeTarget target = new SerializeTarget();
            try
            {
                if (SerializeJSONProperty(JsString.Empty, wrapper, ref target) == SerializeResult.Undefined)
                {
                    return JsValue.Undefined;
                }
                return new JsString(target.Json.ToString());
            }
            finally
            {
                target.Return();
            }
        }

        private void SetupReplacer(JsValue replacer)
        {
            if (replacer is not ObjectInstance oi)
            {
                return;
            }

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

        private static string BuildSpacingGap(JsValue space)
        {
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
                    return new string(' ', (int) System.Math.Min(10, number));
                }

                return string.Empty;
            }

            if (space.IsString())
            {
                var stringSpace = space.ToString();
                return stringSpace.Length <= 10 ? stringSpace : stringSpace.Substring(0, 10);
            }

            return string.Empty;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-serializejsonproperty
        /// </summary>
        private SerializeResult SerializeJSONProperty(JsValue key, JsValue holder, ref SerializeTarget target)
        {
            var value = ReadUnwrappedValue(key, holder);

            if (ReferenceEquals(value, JsValue.Null))
            {
                target.Json.Append(JsString.NullString.ToString());
                return SerializeResult.NotUndefined;
            }

            if (value.IsBoolean())
            {
                target.Json.Append(((JsBoolean) value)._value
                    ? JsString.TrueString.ToString()
                    : JsString.FalseString.ToString());
                return SerializeResult.NotUndefined;
            }

            if (value.IsString())
            {
                QuoteJSONString(value.ToString(), ref target);
                return SerializeResult.NotUndefined;
            }

            if (value.IsNumber())
            {
                var doubleValue = TypeConverter.ToNumber(value);
                var isFinite = !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue);
                if (isFinite)
                {
                    if (TypeConverter.CanBeStringifiedAsLong(doubleValue))
                    {
                        target.Json.Append(TypeConverter.ToString((long) doubleValue));
                        return SerializeResult.NotUndefined;
                    }

                    target.NumberBuffer.Clear();
                    target.DtoaBuilder.Reset();
                    target.Json.Append(NumberPrototype.NumberToString(doubleValue, target.DtoaBuilder, target.NumberBuffer));
                    return SerializeResult.NotUndefined;
                }

                target.Json.Append(JsString.NullString);
                return SerializeResult.NotUndefined;
            }

            if (value.IsBigInt())
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm, "Do not know how to serialize a BigInt");
            }

            if (value is ObjectInstance { IsCallable: false } objectInstance)
            {
                if (CanSerializesAsArray(objectInstance))
                {
                    SerializeJSONArray(objectInstance, ref target);
                    return SerializeResult.NotUndefined;
                }

                if (objectInstance is IObjectWrapper wrapper
                    && _engine.Options.Interop.SerializeToJson is { } serialize)
                {
                    target.Json.Append(serialize(wrapper.Target));
                    return SerializeResult.NotUndefined;
                }

                SerializeJSONObject(objectInstance, ref target);
                return SerializeResult.NotUndefined;
            }

            return SerializeResult.Undefined;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JsValue ReadUnwrappedValue(JsValue key, JsValue holder)
        {
            var value = holder.Get(key);
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

            return value;
        }

        private static bool CanSerializesAsArray(ObjectInstance value)
        {
            if (value is JsArray)
            {
                return true;
            }

            if (value is ProxyInstance proxyInstance && CanSerializesAsArray(proxyInstance._target))
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
        private static void QuoteJSONString(string value, ref SerializeTarget target)
        {
            int len = value.Length;
            if (len == 0)
            {
                target.Json.Append("\"\"");
                return;
            }

            target.Json.Append('"');
            for (var i = 0; i < len; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\"':
                        target.Json.Append("\\\"");
                        break;
                    case '\\':
                        target.Json.Append("\\\\");
                        break;
                    case '\b':
                        target.Json.Append("\\b");
                        break;
                    case '\f':
                        target.Json.Append("\\f");
                        break;
                    case '\n':
                        target.Json.Append("\\n");
                        break;
                    case '\r':
                        target.Json.Append("\\r");
                        break;
                    case '\t':
                        target.Json.Append("\\t");
                        break;
                    default:
                        if (char.IsSurrogatePair(value, i))
                        {
                            target.Json.Append(value[i]);
                            i++;
                            target.Json.Append(value[i]);
                        }
                        else if (c < 0x20 || char.IsSurrogate(c))
                        {
                            target.Json.Append("\\u");
                            target.Json.Append(((int) c).ToString("x4"));
                        }
                        else
                        {
                            target.Json.Append(c);
                        }

                        break;
                }
            }

            target.Json.Append('"');
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-serializejsonarray
        /// </summary>
        private void SerializeJSONArray(ObjectInstance value, ref SerializeTarget target)
        {
            var len = TypeConverter.ToUint32(value.Get(CommonProperties.Length));
            if (len == 0)
            {
                target.Json.Append("[]");
                return;
            }

            _stack.Enter(value);
            var stepback = _indent;
            if (_gap.Length > 0)
            {
                _indent += _gap;
            }

            const string separator = ",";;
            bool hasPrevious = false;

            for (int i = 0; i < len; i++)
            {
                if (hasPrevious)
                {
                    target.Json.Append(separator);
                }
                else
                {
                    target.Json.Append('[');
                }

                if (_gap.Length > 0)
                {
                    target.Json.Append('\n');
                    target.Json.Append(_indent);
                }

                if (SerializeJSONProperty(i, value, ref target) == SerializeResult.Undefined)
                {
                    target.Json.Append(JsString.NullString);
                }

                hasPrevious = true;
            }

            if (!hasPrevious)
            {
                _stack.Exit();
                _indent = stepback;
                target.Json.Append("[]");
                return;
            }

            if (_gap.Length > 0)
            {
                target.Json.Append('\n');
                target.Json.Append(stepback);
            }
            target.Json.Append(']');

            _stack.Exit();
            _indent = stepback;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-serializejsonobject
        /// </summary>
        private void SerializeJSONObject(ObjectInstance value, ref SerializeTarget target)
        {
            var enumeration = _propertyList is null
                ? PropertyEnumeration.FromObjectInstance(value)
                : PropertyEnumeration.FromList(_propertyList);
            if (enumeration.IsEmpty)
            {
                target.Json.Append("{}");
                return;
            }

            _stack.Enter(value);
            var stepback = _indent;
            if (_gap.Length > 0)
            {
                _indent += _gap;
            }

            const string separator = ",";

            bool hasPrevious = false;
            foreach (var p in enumeration.Keys)
            {
                int position = target.Json.Length;

                if (hasPrevious)
                {
                    target.Json.Append(separator);
                }
                else
                {
                    target.Json.Append('{');
                }

                if (_gap.Length > 0)
                {
                    target.Json.Append('\n');
                    target.Json.Append(_indent);
                }

                QuoteJSONString(p.ToString(), ref target);
                target.Json.Append(':');
                if (_gap.Length > 0)
                {
                    target.Json.Append(' ');
                }

                if (SerializeJSONProperty(p, value, ref target) == SerializeResult.Undefined)
                {
                    target.Json.Length = position;
                }
                else
                {
                    hasPrevious = true;
                }
            }

            if (!hasPrevious)
            {
                _stack.Exit();
                _indent = stepback;
                target.Json.Append("{}");
                return;
            }

            if (_gap.Length > 0)
            {
                target.Json.Append('\n');
                target.Json.Append(stepback);
            }
            target.Json.Append('}');

            _stack.Exit();
            _indent = stepback;
        }

        private readonly ref struct SerializeTarget
        {
            private readonly StringBuilderPool.BuilderWrapper _jsonBuilder;
            private readonly StringBuilderPool.BuilderWrapper _numberBuilder;

            public SerializeTarget()
            {
                _jsonBuilder = StringBuilderPool.Rent();
                _numberBuilder = StringBuilderPool.Rent();
                Json = _jsonBuilder.Builder;
                NumberBuffer = _numberBuilder.Builder;
            }

            public StringBuilder Json { get; }

            public StringBuilder NumberBuffer { get; }

            public DtoaBuilder DtoaBuilder { get; } = TypeConverter.CreateDtoaBuilderForDouble();

            public void Return()
            {
                _jsonBuilder.Dispose();
                _numberBuilder.Dispose();
            }
        }

        private enum SerializeResult
        {
            NotUndefined,
            Undefined
        }

        private readonly struct PropertyEnumeration
        {
            private PropertyEnumeration(IEnumerable<JsValue> keys, bool isEmpty)
            {
                Keys = keys;
                IsEmpty = isEmpty;
            }

            public static PropertyEnumeration FromList(List<JsValue> keys)
                => new PropertyEnumeration(keys, keys.Count == 0);

            public static PropertyEnumeration FromObjectInstance(ObjectInstance instance)
            {
                List<JsValue> allKeys = instance.GetOwnPropertyKeys(Types.String);
                RemoveUnserializableProperties(instance, allKeys);
                return new PropertyEnumeration(allKeys, allKeys.Count == 0);
            }

            private static void RemoveUnserializableProperties(ObjectInstance instance, List<JsValue> keys)
            {
                keys.RemoveAll(key =>
                {
                    if (!key.IsString())
                    {
                        return true;
                    }

                    var desc = instance.GetOwnProperty(key);
                    return desc == PropertyDescriptor.Undefined || !desc.Enumerable;
                });
            }

            public IEnumerable<JsValue> Keys { get; }

            public bool IsEmpty { get; }
        }
    }
}
