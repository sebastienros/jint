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

            using var jsonBuilder = StringBuilderPool.Rent();

            var target = new SerializerState(jsonBuilder.Builder);
            if (SerializeJSONProperty(JsString.Empty, wrapper, ref target) == SerializeResult.Undefined)
            {
                return JsValue.Undefined;
            }

            return new JsString(target.Json.ToString());
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
        private SerializeResult SerializeJSONProperty(JsValue key, JsValue holder, ref SerializerState target)
        {
            var value = ReadUnwrappedValue(key, holder);

            if (ReferenceEquals(value, JsValue.Null))
            {
                target.Json.Append("null");
                return SerializeResult.NotUndefined;
            }

            if (value.IsBoolean())
            {
                target.Json.Append(((JsBoolean) value)._value ? "true" : "false");
                return SerializeResult.NotUndefined;
            }

            if (value.IsString())
            {
                QuoteJSONString(value.ToString(), target.Json);
                return SerializeResult.NotUndefined;
            }

            if (value.IsNumber())
            {
                var doubleValue = ((JsNumber) value)._value;

                if (value.IsInteger())
                {
                    target.Json.Append((long) doubleValue);
                    return SerializeResult.NotUndefined;
                }

                var isFinite = !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue);
                if (isFinite)
                {
                    if (TypeConverter.CanBeStringifiedAsLong(doubleValue))
                    {
                        target.Json.Append((long) doubleValue);
                        return SerializeResult.NotUndefined;
                    }

                    target.DtoaBuilder.Reset();
                    NumberPrototype.NumberToString(doubleValue, target.DtoaBuilder, target.Json);
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

            if (value._type <= InternalTypes.Integer && _replacerFunction.IsUndefined())
            {
                return value;
            }

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

            if (value is JsProxy proxyInstance && CanSerializesAsArray(proxyInstance._target))
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
        /// <remarks>
        /// MethodImplOptions.AggressiveOptimization = 512 which is only exposed in .NET Core.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions)512)]
        private static unsafe void QuoteJSONString(string value, StringBuilder target)
        {
            if (value.Length == 0)
            {
                target.Append("\"\"");
                return;
            }

            target.Append('"');

#if NETCOREAPP1_0_OR_GREATER
            fixed (char* ptr = value)
            {
                int remainingLength = value.Length;
                int offset = 0;
                while (true)
                {
                    int index = System.Text.Encodings.Web.JavaScriptEncoder.Default.FindFirstCharacterToEncode(ptr + offset, remainingLength);
                    if (index < 0)
                    {
                        // append the remaining text which doesn't need any encoding.
                        target.Append(value.AsSpan(offset));
                        break;
                    }

                    index += offset;
                    if (index - offset > 0)
                    {
                        // append everything which does not need any encoding until the found index.
                        target.Append(value.AsSpan(offset, index - offset));
                    }

                    AppendJsonStringCharacter(value, ref index, target);

                    offset = index + 1;
                    remainingLength = value.Length - offset;
                    if (remainingLength == 0)
                    {
                        break;
                    }
                }
            }
#else
            for (var i = 0; i < value.Length; i++)
            {
                AppendJsonStringCharacter(value, ref i, target);
            }
#endif
            target.Append('"');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendJsonStringCharacter(string value, ref int index, StringBuilder target)
        {
            var c = value[index];
            switch (c)
            {
                case '\"':
                    target.Append("\\\"");
                    break;
                case '\\':
                    target.Append("\\\\");
                    break;
                case '\b':
                    target.Append("\\b");
                    break;
                case '\f':
                    target.Append("\\f");
                    break;
                case '\n':
                    target.Append("\\n");
                    break;
                case '\r':
                    target.Append("\\r");
                    break;
                case '\t':
                    target.Append("\\t");
                    break;
                default:
                    if (char.IsSurrogatePair(value, index))
                    {
#if NETCOREAPP1_0_OR_GREATER
                        target.Append(value.AsSpan(index, 2));
                        index++;
#else
                        target.Append(c);
                        index++;
                        target.Append(value[index]);
#endif
                    }
                    else if (c < 0x20 || char.IsSurrogate(c))
                    {
                        target.Append("\\u");
                        target.Append(((int) c).ToString("x4"));
                    }
                    else
                    {
                        target.Append(c);
                    }
                    break;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-serializejsonarray
        /// </summary>
        private void SerializeJSONArray(ObjectInstance value, ref SerializerState target)
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

            const char separator = ',';
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
        private void SerializeJSONObject(ObjectInstance value, ref SerializerState target)
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

            const char separator = ',';
            var hasPrevious = false;
            for (var i = 0; i < enumeration.Keys.Count; i++)
            {
                var p = enumeration.Keys[i];
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

                QuoteJSONString(p.ToString(), target.Json);
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

        private readonly ref struct SerializerState
        {
            public SerializerState(StringBuilder jsonBuilder)
            {
                Json = jsonBuilder;
                DtoaBuilder = TypeConverter.CreateDtoaBuilderForDouble();
            }

            public readonly StringBuilder Json;
            public readonly DtoaBuilder DtoaBuilder;
        }

        private enum SerializeResult
        {
            NotUndefined,
            Undefined
        }

        private readonly struct PropertyEnumeration
        {
            private PropertyEnumeration(List<JsValue> keys, bool isEmpty)
            {
                Keys = keys;
                IsEmpty = isEmpty;
            }

            public static PropertyEnumeration FromList(List<JsValue> keys)
                => new PropertyEnumeration(keys, keys.Count == 0);

            public static PropertyEnumeration FromObjectInstance(ObjectInstance instance)
            {
                var allKeys = instance.GetOwnPropertyKeys(Types.String);
                RemoveUnserializableProperties(instance, allKeys);
                return new PropertyEnumeration(allKeys, allKeys.Count == 0);
            }

            private static void RemoveUnserializableProperties(ObjectInstance instance, List<JsValue> keys)
            {
                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    var desc = instance.GetOwnProperty(key);
                    if (desc == PropertyDescriptor.Undefined || !desc.Enumerable)
                    {
                        keys.RemoveAt(i);
                        i--;
                    }
                }
            }

            public readonly List<JsValue> Keys;
            public readonly bool IsEmpty;
        }
    }
}
