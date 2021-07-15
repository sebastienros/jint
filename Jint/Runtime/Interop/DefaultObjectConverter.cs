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
                }
                else
                {
                    if (value is Delegate d)
                    {
                        result = new DelegateWrapper(engine, d);
                    }
                    else
                    {
                        var t = value.GetType();
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
            }

            return result is not null;
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