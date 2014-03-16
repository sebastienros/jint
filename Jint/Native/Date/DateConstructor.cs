using System;
using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Date
{
    public sealed class DateConstructor : FunctionInstance, IConstructor
    {
        internal static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateConstructor(Engine engine) : base(engine, null, null, false)
        {
        }

        public static DateConstructor CreateDateConstructor(Engine engine)
        {
            var obj = new DateConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Date constructor is the Function prototype object 
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = DatePrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 7, false, false, false);

            // The initial value of Date.prototype is the Date prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("parse", new ClrFunctionInstance(Engine, Parse, 1), true, false, true);
            FastAddProperty("UTC", new ClrFunctionInstance(Engine, Utc, 7), true, false, true);
            FastAddProperty("now", new ClrFunctionInstance(Engine, Now, 0), true, false, true);
        }

        private JsValue Parse(JsValue thisObj, JsValue[] arguments)
        {
            DateTime result;
            var date = TypeConverter.ToString(arguments.At(0));

            if (!DateTime.TryParseExact(date, new[]
            {
                "yyyy/MM/ddTH:m:s.fff",
                "yyyy/MM/dd",
                "yyyy/MM",
                "yyyy-MM-ddTH:m:s.fff",
                "yyyy-MM-dd",
                "yyyy-MM",
                "yyyy",
                "THH:m:s.fff",
                "TH:mm:sm",
                "THH:mm",
                "THH"
            }, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
            {
                if (!DateTime.TryParseExact(date, new[]
                {
                    "yyyy/MM/ddTH:m:s.fffK",
                    "yyyy/MM/ddK",
                    "yyyy/MMK",
                    "yyyy-MM-ddTH:m:s.fffK",
                    "yyyy-MM-ddK",
                    "yyyy-MMK",
                    "yyyyK",
                    "THH:m:s.fffK",
                    "TH:mm:smK",
                    "THH:mmK",
                    "THHK"
                }, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
                {
                    if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal,out result))
                    {
                        throw new JavaScriptException(Engine.SyntaxError, "Invalid date");
                    }
                }
            }

            return Construct(result);
        }

        private JsValue Utc(JsValue thisObj, JsValue[] arguments)
        {
            var local = (DateInstance) Construct(arguments);
            return local.PrimitiveValue;
        }

        private JsValue Now(JsValue thisObj, JsValue[] arguments)
        {
            return (DateTime.UtcNow - Epoch).TotalMilliseconds;
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return PrototypeObject.ToString(Construct(Arguments.Empty), Arguments.Empty);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.9.3
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(JsValue[] arguments)
        {
            if (arguments.Length == 0)
            {
                return Construct(DateTime.Now);
            }
            else if (arguments.Length == 1)
            {
                var v = TypeConverter.ToPrimitive(arguments[0]);
                if (v.IsString())
                {
                    return Parse(Undefined.Instance, Arguments.From(v)).AsObject();
                }

                return Construct(DatePrototype.TimeClip(TypeConverter.ToNumber(v)));
            }
            else
            {
                var y = TypeConverter.ToNumber(arguments[0]);
                var m = (int)TypeConverter.ToInteger(arguments[1]);
                var dt = arguments.Length > 2 ? (int)TypeConverter.ToInteger(arguments[2]) : 1;
                var h = arguments.Length > 3 ? (int)TypeConverter.ToInteger(arguments[3]) : 0;
                var min = arguments.Length > 4 ? (int)TypeConverter.ToInteger(arguments[4]) : 0;
                var s = arguments.Length > 5 ? (int)TypeConverter.ToInteger(arguments[5]) : 0;
                var milli = arguments.Length > 6 ? (int)TypeConverter.ToInteger(arguments[6]) : 0;

                for (int i = 2; i < arguments.Length; i++)
                {
                    if (double.IsNaN(TypeConverter.ToNumber(arguments[i])))
                    {
                        return Construct(double.NaN);
                    }
                }

                if ((!double.IsNaN(y)) && (0 <= TypeConverter.ToInteger(y)) && (TypeConverter.ToInteger(y) <= 99))
                {
                    y += 1900;
                }

                var finalDate = DatePrototype.MakeDate(DatePrototype.MakeDay(y, m, dt),
                    DatePrototype.MakeTime(h, min, s, milli));

                return Construct(DatePrototype.TimeClip(DatePrototype.Utc(finalDate)));
            }
        }

        public DatePrototype PrototypeObject { get; private set; }

        public DateInstance Construct(DateTimeOffset value)
        {
            return Construct(value.UtcDateTime);
        }

        public DateInstance Construct(DateTime value)
        {
            var instance = new DateInstance(Engine)
                {
                    Prototype = PrototypeObject,
                    PrimitiveValue = FromDateTime(value),
                    Extensible = true
                };

            return instance;
        }

        public DateInstance Construct(double time)
        {
            var instance = new DateInstance(Engine)
                {
                    Prototype = PrototypeObject,
                    PrimitiveValue = TimeClip(time),
                    Extensible = true
                };

            return instance;
        }

        public static double TimeClip(double time)
        {
            if (double.IsInfinity(time) || double.IsNaN(time))
            {
                return double.NaN;
            }

            if (System.Math.Abs(time) > 8640000000000000)
            {
                return double.NaN;
            }

            return TypeConverter.ToInteger(time);
        }

        public static double FromDateTime(DateTime dt)
        {
            return System.Math.Floor((dt.ToUniversalTime() - Epoch).TotalMilliseconds);
        }
    }
}
