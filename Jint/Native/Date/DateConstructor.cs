using System.Globalization;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Date
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-date-constructor
    /// </summary>
    public sealed class DateConstructor : FunctionInstance, IConstructor
    {
        internal static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly string[] DefaultFormats = {
            "yyyy-MM-dd",
            "yyyy-MM",
            "yyyy"
        };

        private static readonly string[] SecondaryFormats = {
            "yyyy-MM-ddTHH:mm:ss.FFF",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm",

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

        internal DatePrototype PrototypeObject { get; }

        protected override void Initialize()
        {
            const PropertyFlag LengthFlags = PropertyFlag.Configurable;
            const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;

            var properties = new PropertyDictionary(3, checkExistingKeys: false)
            {
                ["parse"] = new(new ClrFunctionInstance(Engine, "parse", Parse, 1, LengthFlags), PropertyFlags),
                ["UTC"] = new(new ClrFunctionInstance(Engine, "UTC", Utc, 7, LengthFlags), PropertyFlags),
                ["now"] = new(new ClrFunctionInstance(Engine, "now", Now, 0, LengthFlags), PropertyFlags)
            };
            SetProperties(properties);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.parse
        /// </summary>
        private JsValue Parse(JsValue thisObj, JsValue[] arguments)
        {
            var date = TypeConverter.ToString(arguments.At(0));
            var negative = date.StartsWith("-");
            if (negative)
            {
                date = date.Substring(1);
            }

            var startParen = date.IndexOf('(');
            if (startParen != -1)
            {
                // informative text
                date = date.Substring(0, startParen);
            }

            date = date.Trim();

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

            return FromDateTime(result, negative);
        }

        private static JsValue Utc(JsValue thisObj, JsValue[] arguments)
        {
            var y = TypeConverter.ToNumber(arguments.At(0));
            var m = TypeConverter.ToNumber(arguments.At(1, JsNumber.PositiveZero));
            var dt = TypeConverter.ToNumber(arguments.At(2, JsNumber.PositiveOne));
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

            return finalDate.TimeClip();
        }

        private static JsValue Now(JsValue thisObj, JsValue[] arguments)
        {
            return System.Math.Floor((DateTime.UtcNow - Epoch).TotalMilliseconds);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return PrototypeObject.ToString(Construct(Arguments.Empty, thisObject), Arguments.Empty);
        }

        ObjectInstance IConstructor.Construct(JsValue[] arguments, JsValue newTarget) => Construct(arguments, newTarget);

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            // fast path is building default, new Date()
            if (arguments.Length == 0 || newTarget.IsUndefined())
            {
                return OrdinaryCreateFromConstructor(
                    newTarget,
                    static intrinsics => intrinsics.Date.PrototypeObject,
                    static (engine, _, dateValue) => new DateInstance(engine, dateValue),
                    (DateTime.UtcNow - Epoch).TotalMilliseconds);
            }

            return ConstructUnlikely(arguments, newTarget);
        }

        private DateInstance ConstructUnlikely(JsValue[] arguments, JsValue newTarget)
        {
            double dv;
            if (arguments.Length == 1)
            {
                if (arguments[0] is DateInstance date)
                {
                    return Construct(date.DateValue);
                }

                var v = TypeConverter.ToPrimitive(arguments[0]);
                if (v.IsString())
                {
                    return Construct(((JsNumber) Parse(Undefined, Arguments.From(v)))._value);
                }

                dv = TypeConverter.ToNumber(v);
            }
            else
            {
                var y = TypeConverter.ToNumber(arguments.At(0));
                var m = TypeConverter.ToNumber(arguments.At(1));
                var dt = TypeConverter.ToNumber(arguments.At(2, JsNumber.PositiveOne));
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

                dv = PrototypeObject.Utc(finalDate);
            }

            return OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Date.PrototypeObject,
                static (engine, _, dateValue) => new DateInstance(engine, dateValue), dv);
        }

        public DateInstance Construct(DateTimeOffset value) => Construct(value.UtcDateTime);

        public DateInstance Construct(DateTime value) => Construct(FromDateTime(value));

        public DateInstance Construct(double time)
        {
            return OrdinaryCreateFromConstructor(
                Undefined,
                static intrinsics => intrinsics.Date.PrototypeObject,
                static (engine, _, dateValue) => new DateInstance(engine, dateValue), time);
        }

        private double FromDateTime(DateTime dt, bool negative = false)
        {
            var convertToUtcAfter = dt.Kind == DateTimeKind.Unspecified;

            var dateAsUtc = dt.Kind == DateTimeKind.Local
                ? dt.ToUniversalTime()
                : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            double result;
            if (negative)
            {
                var zero = (Epoch - DateTime.MinValue).TotalMilliseconds;
                result = zero - TimeSpan.FromTicks(dateAsUtc.Ticks).TotalMilliseconds;
                result *= -1;
            }
            else
            {
                result = (dateAsUtc - Epoch).TotalMilliseconds;
            }

            if (convertToUtcAfter)
            {
                result = PrototypeObject.Utc(result);
            }

            return System.Math.Floor(result);
        }
    }
}
