using System;
using System.Globalization;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Date
{
    public sealed class DateConstructor : FunctionInstance, IConstructor
    {
        internal static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly string[] DefaultFormats = {
            "yyyy-MM-ddTHH:mm:ss.FFF",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm",
            "yyyy-MM-dd",
            "yyyy-MM",
            "yyyy"
        };

        private static readonly string[] SecondaryFormats = {
            // Formats used in DatePrototype toString methods
            "ddd MMM dd yyyy HH:mm:ss 'GMT'K",
            "ddd MMM dd yyyy",
            "HH:mm:ss 'GMT'K",

            // standard formats
            "yyyy-M-dTH:m:s.FFFK",
            "yyyy/M/dTH:m:s.FFFK",
            "yyyy-M-dTH:m:sK",
            "yyyy/M/dTH:m:sK",
            "yyyy-M-dTH:mK",
            "yyyy/M/dTH:mK",
            "yyyy-M-d H:m:s.FFFK",
            "yyyy/M/d H:m:s.FFFK",
            "yyyy-M-d H:m:sK",
            "yyyy/M/d H:m:sK",
            "yyyy-M-d H:mK",
            "yyyy/M/d H:mK",
            "yyyy-M-dK",
            "yyyy/M/dK",
            "yyyy-MK",
            "yyyy/MK",
            "yyyyK",
            "THH:mm:ss.FFFK",
            "THH:mm:ssK",
            "THH:mmK",
            "THHK"
        };

        private static readonly JsString _functionName = new JsString("Date");

        public DateConstructor(Engine engine) : base(engine, _functionName, strict: false)
        {
        }

        public static DateConstructor CreateDateConstructor(Engine engine)
        {
            var obj = new DateConstructor(engine)
            {
                Extensible = true,
                Prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Date constructor is the Function prototype object
            obj.PrototypeObject = DatePrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(7, PropertyFlag.AllForbidden);

            // The initial value of Date.prototype is the Date prototype object
            obj._prototype = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(3)
            {
                ["parse"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "parse", Parse, 1), true, false, true),
                ["UTC"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "utc", Utc, 7), true, false, true),
                ["now"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "now", Now, 0), true, false, true)
            };
        }

        private JsValue Parse(JsValue thisObj, JsValue[] arguments)
        {
            var date = TypeConverter.ToString(arguments.At(0));

            if (!DateTime.TryParseExact(date, DefaultFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var result))
            {
                if (!DateTime.TryParseExact(date, SecondaryFormats, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
                {
                    if (!DateTime.TryParse(date, Engine.Options._Culture, DateTimeStyles.AdjustToUniversal, out result))
                    {
                        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out result))
                        {
                            // unrecognized dates should return NaN (15.9.4.2)
                            return JsNumber.DoubleNaN;
                        }
                    }
                }
            }

            return FromDateTime(result);
        }

        private JsValue Utc(JsValue thisObj, JsValue[] arguments)
        {
            return TimeClip(ConstructTimeValue(arguments, useUtc: true));
        }

        private static JsValue Now(JsValue thisObj, JsValue[] arguments)
        {
            return System.Math.Floor((DateTime.UtcNow - Epoch).TotalMilliseconds);
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
                return Construct(DateTime.UtcNow);
            }
            else if (arguments.Length == 1)
            {
                var v = TypeConverter.ToPrimitive(arguments[0]);
                if (v.IsString())
                {
                    return Construct(((JsNumber) Parse(Undefined, Arguments.From(v)))._value);
                }

                return Construct(TypeConverter.ToNumber(v));
            }
            else
            {
                return Construct(ConstructTimeValue(arguments, useUtc: false));
            }
        }

        private double ConstructTimeValue(JsValue[] arguments, bool useUtc)
        {
            if (arguments.Length < 2)
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(arguments), "There must be at least two arguments.");
            }

            int SafeInteger(JsValue value)
            {
                var integer = TypeConverter.ToInteger(value);
                if (integer > int.MaxValue || integer < int.MinValue)
                {
                    ExceptionHelper.ThrowTypeError(_engine, "Invalid Date.");
                }

                return (int) integer;
            }

            var y = TypeConverter.ToNumber(arguments[0]);
            var m = SafeInteger(arguments[1]);
            var dt = arguments.Length > 2 ? SafeInteger(arguments[2]) : 1;
            var h = arguments.Length > 3 ? SafeInteger(arguments[3]) : 0;
            var min = arguments.Length > 4 ? SafeInteger(arguments[4]) : 0;
            var s = arguments.Length > 5 ? SafeInteger(arguments[5]) : 0;
            var milli = arguments.Length > 6 ? SafeInteger(arguments[6]) : 0;

            for (int i = 2; i < arguments.Length; i++)
            {
                if (double.IsNaN(TypeConverter.ToNumber(arguments[i])))
                {
                    return double.NaN;
                }
            }

            if (!double.IsNaN(y) && 0 <= TypeConverter.ToInteger(y) && TypeConverter.ToInteger(y) <= 99)
            {
                y += 1900;
            }

            var finalDate = DatePrototype.MakeDate(
                DatePrototype.MakeDay(y, m, dt),
                DatePrototype.MakeTime(h, min, s, milli));

            return TimeClip(useUtc ? finalDate : PrototypeObject.Utc(finalDate));
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

        public double FromDateTime(DateTime dt)
        {
            var convertToUtcAfter = (dt.Kind == DateTimeKind.Unspecified);

            var dateAsUtc = dt.Kind == DateTimeKind.Local
                ? dt.ToUniversalTime()
                : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            var result = (dateAsUtc - Epoch).TotalMilliseconds;

            if (convertToUtcAfter)
            {
                result = PrototypeObject.Utc(result);
            }

            return System.Math.Floor(result);
        }
    }
}
