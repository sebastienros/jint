using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
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
                (engine, v) => engine.Realm.Intrinsics.RegExp.Construct((System.Text.RegularExpressions.Regex)v, ((System.Text.RegularExpressions.Regex)v).ToString(), "")
            }
        };

        public static bool TryConvert(Engine engine, object value, Type? type, [NotNullWhen(true)] out JsValue? result)
        {
            result = null;
            Type valueType = ObjectWrapper.GetClrType(value, type);

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

                if (value is IConvertible convertible && TryConvertConvertible(engine, convertible, out result))
                {
                    return true;
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
                        // check global cache, have we already wrapped the value?
                        if (engine._objectWrapperCache?.TryGetValue(value, out var cached) == true)
                        {
                            result = cached;
                        }
                        else
                        {
                            var wrapped = engine.Options.Interop.WrapObjectHandler.Invoke(engine, value, type);
                            result = wrapped;

                            if (engine.Options.Interop.TrackObjectWrapperIdentity && wrapped is not null)
                            {
                                engine._objectWrapperCache ??= new ConditionalWeakTable<object, ObjectInstance>();
                                engine._objectWrapperCache.Add(value, wrapped);
                            }
                        }
                    }

                    // if no known type could be guessed, use the default of wrapping using using ObjectWrapper.
                }
            }

            return result is not null;
        }

        private static bool TryConvertConvertible(Engine engine, IConvertible convertible, [NotNullWhen(true)] out JsValue? result)
        {
            result = convertible.GetTypeCode() switch
            {
                TypeCode.Boolean => convertible.ToBoolean(engine.Options.Culture) ? JsBoolean.True : JsBoolean.False,
                TypeCode.Byte => JsNumber.Create(convertible.ToByte(engine.Options.Culture)),
                TypeCode.Char => JsString.Create(convertible.ToChar(engine.Options.Culture)),
                TypeCode.Double => JsNumber.Create(convertible.ToDouble(engine.Options.Culture)),
                TypeCode.SByte => JsNumber.Create(convertible.ToSByte(engine.Options.Culture)),
                TypeCode.Int16 => JsNumber.Create(convertible.ToInt16(engine.Options.Culture)),
                TypeCode.Int32 => JsNumber.Create(convertible.ToInt32(engine.Options.Culture)),
                TypeCode.UInt16 => JsNumber.Create(convertible.ToUInt16(engine.Options.Culture)),
                TypeCode.Int64 => JsNumber.Create(convertible.ToInt64(engine.Options.Culture)),
                TypeCode.Single => JsNumber.Create(convertible.ToSingle(engine.Options.Culture)),
                TypeCode.String => JsString.Create(convertible.ToString(engine.Options.Culture)),
                TypeCode.UInt32 => JsNumber.Create(convertible.ToUInt32(engine.Options.Culture)),
                TypeCode.UInt64 => JsNumber.Create(convertible.ToUInt64(engine.Options.Culture)),
                TypeCode.DateTime => engine.Realm.Intrinsics.Date.Construct(convertible.ToDateTime(engine.Options.Culture)),
                TypeCode.Decimal => JsNumber.Create(convertible.ToDecimal(engine.Options.Culture)),
                TypeCode.DBNull => JsValue.Null,
                TypeCode.Empty => JsValue.Null,
                _ => null
            };

            return result is not null;
        }

        private static JsValue ConvertArray(Engine e, object v)
        {
            var array = (Array) v;
            var arrayLength = (uint) array.Length;

            var values = new JsValue[arrayLength];
            for (uint i = 0; i < arrayLength; ++i)
            {
                values[i] = JsValue.FromObject(e, array.GetValue(i));
            }

            return new JsArray(e, values);
        }
    }
}
