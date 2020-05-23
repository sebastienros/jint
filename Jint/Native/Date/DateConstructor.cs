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

        public DateConstructor(Engine engine) : base(engine, _functionName)
        {
        }

        public static DateConstructor CreateDateConstructor(Engine engine)
        {
            var obj = new DateConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Date constructor is the Function prototype object
            obj.PrototypeObject = DatePrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(7, PropertyFlag.Configurable);

            // The initial value of Date.prototype is the Date prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;

            var properties = new PropertyDictionary(3, checkExistingKeys: false)
            {
                ["parse"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "parse", Parse, 1, lengthFlags), propertyFlags),
                ["UTC"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "UTC", Utc, 7, lengthFlags), propertyFlags),
                ["now"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "now", Now, 0, lengthFlags), propertyFlags)
            };
            SetProperties(properties);
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
            var y = TypeConverter.ToNumber(arguments.At(0));
            var m = TypeConverter.ToNumber(arguments.At(1, JsNumber.PositiveZero));
            var dt = TypeConverter.ToNumber(arguments.At(2, JsNumber.One));
            var h = TypeConverter.ToNumber(arguments.At(3, JsNumber.PositiveZero));
            var min = TypeConverter.ToNumber(arguments.At(4, JsNumber.PositiveZero));
            var s = TypeConverter.ToNumber(arguments.At(5, JsNumber.PositiveZero));
            var milli = TypeConverter.ToNumber(arguments.At(6, JsNumber.PositiveZero));

            var yInteger = TypeConverter.ToInteger(y);
            if (!double.IsNaN(y) && 0 <= yInteger && yInteger <= 99)
            {
                y  = yInteger + 1900;
            }

            var finalDate = DatePrototype.MakeDate(
                DatePrototype.MakeDay(y, m, dt),
                DatePrototype.MakeTime(h, min, s, milli));

            return TimeClip(finalDate);
        }

        private static JsValue Now(JsValue thisObj, JsValue[] arguments)
        {
            return System.Math.Floor((DateTime.UtcNow - Epoch).TotalMilliseconds);
        }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return PrototypeObject.ToString(Construct(Arguments.Empty, thisObject), Arguments.Empty);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.9.3
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            if (arguments.Length == 0)
            {
                return Construct(DateTime.UtcNow);
            }
            else if (arguments.Length == 1)
            {
                if (arguments[0] is DateInstance date)
                {
                    return Construct(date.PrimitiveValue);
                }
                
                var v = TypeConverter.ToPrimitive(arguments[0]);
                if (v.IsString())
                {
                    return Construct(((JsNumber) Parse(Undefined, Arguments.From(v)))._value);
                }

                return Construct(TimeClip(TypeConverter.ToNumber(v)));
            }
            else
            {
                var y = TypeConverter.ToNumber(arguments.At(0));
                var m = TypeConverter.ToNumber(arguments.At(1));
                var dt = TypeConverter.ToNumber(arguments.At(2, JsNumber.One));
                var h = TypeConverter.ToNumber(arguments.At(3, JsNumber.PositiveZero));
                var min = TypeConverter.ToNumber(arguments.At(4, JsNumber.PositiveZero));
                var s = TypeConverter.ToNumber(arguments.At(5, JsNumber.PositiveZero));
                var milli = TypeConverter.ToNumber(arguments.At(6, JsNumber.PositiveZero));

                var yInteger = TypeConverter.ToInteger(y);
                if (!double.IsNaN(y) && 0 <= yInteger && yInteger <= 99)
                {
                    y += 1900;
                }

                var finalDate = DatePrototype.MakeDate(
                    DatePrototype.MakeDay(y, m, dt),
                    DatePrototype.MakeTime(h, min, s, milli));

                return Construct(TimeClip(PrototypeObject.Utc(finalDate)));
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
                _prototype = PrototypeObject,
                PrimitiveValue = FromDateTime(value)
            };

            return instance;
        }

        public DateInstance Construct(double time)
        {
            var instance = new DateInstance(Engine)
            {
                _prototype = PrototypeObject,
                PrimitiveValue = TimeClip(time)
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

            return TypeConverter.ToInteger(time) + 0;
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
