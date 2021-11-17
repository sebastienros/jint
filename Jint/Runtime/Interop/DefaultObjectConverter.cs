using System;
using System.Collections.Generic;
using System.Threading;
using Jint.Native;
using Jint.Native.Array;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint
{
    internal static class DefaultObjectConverter
    {
        private static Dictionary<Type, Func<Engine, object, JsValue>> _typeMappers = new()
        {
            { typeof(bool), (engine, v) => (bool)v ? JsBoolean.True : JsBoolean.False },
            { typeof(byte), (engine, v) => JsNumber.Create((byte)v) },
            { typeof(char), (engine, v) => JsString.Create((char)v) },
            { typeof(DateTime), (engine, v) => engine.Realm.Intrinsics.Date.Construct((DateTime)v) },
            { typeof(DateTimeOffset), (engine, v) => engine.Realm.Intrinsics.Date.Construct((DateTimeOffset)v) },
            { typeof(decimal), (engine, v) => (JsValue)(double)(decimal)v },
            { typeof(double), (engine, v) => (JsValue)(double)v },
            { typeof(short), (engine, v) => JsNumber.Create((short)v) },
            { typeof(int), (engine, v) => JsNumber.Create((int)v) },
            { typeof(long), (engine, v) => (JsValue)(long)v },
            { typeof(sbyte), (engine, v) => JsNumber.Create((sbyte)v) },
            { typeof(float), (engine, v) => (JsValue)(float)v },
            { typeof(string), (engine, v) => JsString.Create((string)v) },
            { typeof(ushort), (engine, v) => JsNumber.Create((ushort)v) },
            { typeof(uint), (engine, v) => JsNumber.Create((uint)v) },
            { typeof(ulong), (engine, v) => JsNumber.Create((ulong)v) },
            {
                typeof(System.Text.RegularExpressions.Regex),
                (engine, v) => engine.Realm.Intrinsics.RegExp.Construct((System.Text.RegularExpressions.Regex)v, "")
            }
        };

        public static bool TryConvert(Engine engine, object value, out JsValue result)
        {
            result = null;
            var valueType = value.GetType();

            var typeMappers = _typeMappers;

            if (typeMappers.TryGetValue(valueType, out var typeMapper))
            {
                result = typeMapper(engine, value);
            }
            else
            {
                if (value is Array a)
                {
                    // racy, we don't care, worst case we'll catch up later
                    Interlocked.CompareExchange(ref _typeMappers,
                        new Dictionary<Type, Func<Engine, object, JsValue>>(typeMappers)
                        {
                            [valueType] = ConvertArray
                        }, typeMappers);

                    result = ConvertArray(engine, a);
                    return result is not null;
                }

                if (value is IConvertible convertible)
                {
                    result = ConvertConvertible(engine, convertible);
                    if (result is not null)
                    {
                        return true;
                    }
                }

                if (value is Delegate d)
                {
                    result = new DelegateWrapper(engine, d);
                }
                else
                {
                    var t = value.GetType();

                    if (!engine.Options.Interop.AllowSystemReflection
                        && t.Namespace?.StartsWith("System.Reflection") == true)
                    {
                        const string message = "Cannot access System.Reflection namespace, check Engine's interop options";
                        ExceptionHelper.ThrowInvalidOperationException(message);
                    }

                    if (t.IsEnum)
                    {
                        var ut = Enum.GetUnderlyingType(t);

                        if (ut == typeof(ulong))
                        {
                            result = JsNumber.Create(Convert.ToDouble(value));
                        }
                        else
                        {
                            if (ut == typeof(uint) || ut == typeof(long))
                            {
                                result = JsNumber.Create(Convert.ToInt64(value));
                            }
                            else
                            {
                                result = JsNumber.Create(Convert.ToInt32(value));
                            }
                        }
                    }
                    else
                    {
                        result = engine.Options.Interop.WrapObjectHandler.Invoke(engine, value);
                    }

                    // if no known type could be guessed, use the default of wrapping using using ObjectWrapper.
                }
            }

            return result is not null;
        }

        private static JsValue ConvertConvertible(Engine engine, IConvertible convertible)
        {
            JsValue result = null;
            switch (convertible.GetTypeCode())
            {
                case TypeCode.Boolean:
                    result = convertible.ToBoolean(engine.Options.Culture) ? JsBoolean.True : JsBoolean.False;
                    break;
                case TypeCode.Byte:
                    result = JsNumber.Create(convertible.ToByte(engine.Options.Culture));
                    break;
                case TypeCode.Char:
                    result = JsString.Create(convertible.ToChar(engine.Options.Culture));
                    break;
                case TypeCode.Double:
                    result = JsNumber.Create(convertible.ToDouble(engine.Options.Culture));
                    break;
                case TypeCode.SByte:
                    result = JsNumber.Create(convertible.ToSByte(engine.Options.Culture));
                    break;
                case TypeCode.Int16:
                    result = JsNumber.Create(convertible.ToInt16(engine.Options.Culture));
                    break;
                case TypeCode.Int32:
                    result = JsNumber.Create(convertible.ToInt32(engine.Options.Culture));
                    break;
                case TypeCode.UInt16:
                    result = JsNumber.Create(convertible.ToUInt16(engine.Options.Culture));
                    break;
                case TypeCode.Int64:
                    result = JsNumber.Create(convertible.ToInt64(engine.Options.Culture));
                    break;
                case TypeCode.Single:
                    result = JsNumber.Create(convertible.ToSingle(engine.Options.Culture));
                    break;
                case TypeCode.String:
                    result = JsString.Create(convertible.ToString(engine.Options.Culture));
                    break;
                case TypeCode.UInt32:
                    result = JsNumber.Create(convertible.ToUInt32(engine.Options.Culture));
                    break;
                case TypeCode.UInt64:
                    result = JsNumber.Create(convertible.ToUInt64(engine.Options.Culture));
                    break;
            }

            return result;
        }

        private static JsValue ConvertArray(Engine e, object v)
        {
            var array = (Array)v;
            var arrayLength = (uint)array.Length;

            var jsArray = new ArrayInstance(e, arrayLength)
            {
                _prototype = e.Realm.Intrinsics.Array.PrototypeObject
            };

            for (uint i = 0; i < arrayLength; ++i)
            {
                var jsItem = JsValue.FromObject(e, array.GetValue(i));
                jsArray.WriteArrayValue(i, new PropertyDescriptor(jsItem, PropertyFlag.ConfigurableEnumerableWritable));
            }

            jsArray.SetOwnProperty(CommonProperties.Length,
                new PropertyDescriptor(arrayLength, PropertyFlag.OnlyWritable));
            return jsArray;
        }
    }
}