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

        internal DateConstructor(
            Engine engine,
            Realm realm,
            FunctionPrototype functionPrototype,
            ObjectPrototype objectPrototype)
            : base(engine, realm, _functionName)
        {
            _prototype = functionPrototype;
            PrototypeObject = new DatePrototype(engine, realm, this, objectPrototype);
            _length = new PropertyDescriptor(7, PropertyFlag.Configurable);
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
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
                    if (!DateTime.TryParse(date, Engine.Options.Culture, DateTimeStyles.AdjustToUniversal, out result))
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
            double dv;
            if (arguments.Length == 0 || newTarget.IsUndefined())
            {
                dv = FromDateTime(DateTime.UtcNow);
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

                dv = TimeClip(TypeConverter.ToNumber(v));
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

                dv = TimeClip(PrototypeObject.Utc(finalDate));
            }

            var o = OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Date.PrototypeObject,
                static (engine, realm, _) => new DateInstance(engine));
            o.PrimitiveValue = dv;
            return o;
        }

        public DatePrototype PrototypeObject { get; private set; }

        public DateInstance Construct(DateTimeOffset value)
        {
            return Construct(value.UtcDateTime);
        }

        public DateInstance Construct(DateTime value)
        {
            return Construct(FromDateTime(value));
        }

        public DateInstance Construct(double time)
        {
            var instance = new DateInstance(_engine)
            {
                _prototype = PrototypeObject,
                PrimitiveValue = TimeClip(time)
            };

            return instance;
        }

        private static double TimeClip(double time)
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

        private double FromDateTime(DateTime dt)
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
