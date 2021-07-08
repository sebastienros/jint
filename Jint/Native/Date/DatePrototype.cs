using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Date
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.9.5
    /// </summary>
    public sealed class DatePrototype : ObjectInstance
    {
        // ES6 section 20.3.1.1 Time Values and Time Range
        private const double MinYear = -1000000.0;
        private const double MaxYear = -MinYear;
        private const double MinMonth = -10000000.0;
        private const double MaxMonth = -MinMonth;

        private readonly Realm _realm;
        private readonly DateConstructor _constructor;

        internal DatePrototype(
            Engine engine,
            Realm realm,
            DateConstructor constructor,
            ObjectPrototype objectPrototype)
            : base(engine)
        {
            _prototype = objectPrototype;
            _realm = realm;
            _constructor = constructor;
        }

        protected override  void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;

            var properties = new PropertyDictionary(50, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToString, 0, lengthFlags), propertyFlags),
                ["toDateString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toDateString", ToDateString, 0, lengthFlags), propertyFlags),
                ["toTimeString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toTimeString", ToTimeString, 0, lengthFlags), propertyFlags),
                ["toLocaleString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, lengthFlags), propertyFlags),
                ["toLocaleDateString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleDateString", ToLocaleDateString, 0, lengthFlags), propertyFlags),
                ["toLocaleTimeString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleTimeString", ToLocaleTimeString, 0, lengthFlags), propertyFlags),
                ["valueOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, lengthFlags), propertyFlags),
                ["getTime"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getTime", GetTime, 0, lengthFlags), propertyFlags),
                ["getFullYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getFullYear", GetFullYear, 0, lengthFlags), propertyFlags),
                ["getYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getYear", GetYear, 0, lengthFlags), propertyFlags),
                ["getUTCFullYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCFullYear", GetUTCFullYear, 0, lengthFlags), propertyFlags),
                ["getMonth"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getMonth", GetMonth, 0, lengthFlags), propertyFlags),
                ["getUTCMonth"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCMonth", GetUTCMonth, 0, lengthFlags), propertyFlags),
                ["getDate"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getDate", GetDate, 0, lengthFlags), propertyFlags),
                ["getUTCDate"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCDate", GetUTCDate, 0, lengthFlags), propertyFlags),
                ["getDay"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getDay", GetDay, 0, lengthFlags), propertyFlags),
                ["getUTCDay"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCDay", GetUTCDay, 0, lengthFlags), propertyFlags),
                ["getHours"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getHours", GetHours, 0, lengthFlags), propertyFlags),
                ["getUTCHours"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCHours", GetUTCHours, 0, lengthFlags), propertyFlags),
                ["getMinutes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getMinutes", GetMinutes, 0, lengthFlags), propertyFlags),
                ["getUTCMinutes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCMinutes", GetUTCMinutes, 0, lengthFlags), propertyFlags),
                ["getSeconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getSeconds", GetSeconds, 0, lengthFlags), propertyFlags),
                ["getUTCSeconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCSeconds", GetUTCSeconds, 0, lengthFlags), propertyFlags),
                ["getMilliseconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getMilliseconds", GetMilliseconds, 0, lengthFlags), propertyFlags),
                ["getUTCMilliseconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCMilliseconds", GetUTCMilliseconds, 0, lengthFlags), propertyFlags),
                ["getTimezoneOffset"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getTimezoneOffset", GetTimezoneOffset, 0, lengthFlags), propertyFlags),
                ["setTime"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setTime", SetTime, 1, lengthFlags), propertyFlags),
                ["setMilliseconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setMilliseconds", SetMilliseconds, 1, lengthFlags), propertyFlags),
                ["setUTCMilliseconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCMilliseconds", SetUTCMilliseconds, 1, lengthFlags), propertyFlags),
                ["setSeconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setSeconds", SetSeconds, 2, lengthFlags), propertyFlags),
                ["setUTCSeconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCSeconds", SetUTCSeconds, 2, lengthFlags), propertyFlags),
                ["setMinutes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setMinutes", SetMinutes, 3, lengthFlags), propertyFlags),
                ["setUTCMinutes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCMinutes", SetUTCMinutes, 3, lengthFlags), propertyFlags),
                ["setHours"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setHours", SetHours, 4, lengthFlags), propertyFlags),
                ["setUTCHours"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCHours", SetUTCHours, 4, lengthFlags), propertyFlags),
                ["setDate"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setDate", SetDate, 1, lengthFlags), propertyFlags),
                ["setUTCDate"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCDate", SetUTCDate, 1, lengthFlags), propertyFlags),
                ["setMonth"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setMonth", SetMonth, 2, lengthFlags), propertyFlags),
                ["setUTCMonth"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCMonth", SetUTCMonth, 2, lengthFlags), propertyFlags),
                ["setFullYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setFullYear", SetFullYear, 3, lengthFlags), propertyFlags),
                ["setYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setYear", SetYear, 1, lengthFlags), propertyFlags),
                ["setUTCFullYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCFullYear", SetUTCFullYear, 3, lengthFlags), propertyFlags),
                ["toUTCString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toUTCString", ToUtcString, 0, lengthFlags), propertyFlags),
                ["toISOString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toISOString", ToISOString, 0, lengthFlags), propertyFlags),
                ["toJSON"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toJSON", ToJSON, 1, lengthFlags), propertyFlags)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.ToPrimitive] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.toPrimitive]", ToPrimitive, 1, PropertyFlag.Configurable), PropertyFlag.Configurable),
            };
            SetSymbols(symbols);
        }

        private JsValue ToPrimitive(JsValue thisObject, JsValue[] arguments)
        {
            var oi = thisObject as ObjectInstance;
            if (oi is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var hint = arguments.At(0);
            if (!hint.IsString())
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var hintString = hint.ToString();
            var tryFirst = Types.None;
            if (hintString == "default" || hintString == "string")
            {
                tryFirst = Types.String;
            }
            else  if (hintString == "number")
            {
                tryFirst = Types.Number;
            }
            else
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return TypeConverter.OrdinaryToPrimitive(oi, tryFirst);
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            return EnsureDateInstance(thisObj).PrimitiveValue;
        }

        /// <summary>
        /// Converts a value to a <see cref="DateInstance"/> or throws a TypeError exception.
        /// c.f., http://www.ecma-international.org/ecma-262/5.1/#sec-15.9.5
        /// </summary>
        private DateInstance EnsureDateInstance(JsValue thisObj)
        {
            if (thisObj is DateInstance dateInstance)
            {
                return dateInstance;
            }

            ExceptionHelper.ThrowTypeError(_realm, "this is not a Date object");
            return null;
        }

        public JsValue ToString(JsValue thisObj, JsValue[] arg2)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            var t = ToLocalTime(dateInstance.ToDateTime(), Engine.Options.TimeZone);
            return t.ToString("ddd MMM dd yyyy HH:mm:ss ", CultureInfo.InvariantCulture) + TimeZoneString(t);
        }

        internal JsValue ToDateString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime(), Engine.Options.TimeZone).ToString("ddd MMM dd yyyy", CultureInfo.InvariantCulture);
        }

        private JsValue ToTimeString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            var t = ToLocalTime(dateInstance.ToDateTime(), Engine.Options.TimeZone);

            var timeString = t.ToString("HH:mm:ss ", CultureInfo.InvariantCulture);
            var timeZoneString = TimeZoneString(t);
            return timeString + timeZoneString;
        }

        private static string TimeZoneString(DateTimeOffset t)
        {
            return t.ToString("'GMT'K", CultureInfo.InvariantCulture).Replace(":", "");
        }

        private JsValue ToLocaleString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime(), Engine.Options.TimeZone).ToString("F", Engine.Options.Culture);
        }

        private JsValue ToLocaleDateString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime(), Engine.Options.TimeZone).ToString("D", Engine.Options.Culture);
        }

        private JsValue ToLocaleTimeString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime(), Engine.Options.TimeZone).ToString("T", Engine.Options.Culture);
        }

        private JsValue GetTime(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return t;
        }

        private JsValue GetFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return YearFromTime(LocalTime(t));
        }

        private JsValue GetYear(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return YearFromTime(LocalTime(t)) - 1900;
        }

        private JsValue GetUTCFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return YearFromTime(t);
        }

        private JsValue GetMonth(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return MonthFromTime(LocalTime(t));
        }

        private JsValue GetUTCMonth(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return MonthFromTime(t);
        }

        private JsValue GetDate(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return DateFromTime(LocalTime(t));
        }

        private JsValue GetUTCDate(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return DateFromTime(t);
        }

        private JsValue GetDay(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return WeekDay(LocalTime(t));
        }

        private JsValue GetUTCDay(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return WeekDay(t);
        }

        private JsValue GetHours(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return HourFromTime(LocalTime(t));
        }

        private JsValue GetUTCHours(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return HourFromTime(t);
        }

        private JsValue GetMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return MinFromTime(LocalTime(t));
        }

        private JsValue GetUTCMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return MinFromTime(t);
        }

        private JsValue GetSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return SecFromTime(LocalTime(t));
        }

        private JsValue GetUTCSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return SecFromTime(t);
        }

        private JsValue GetMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return MsFromTime(LocalTime(t));
        }

        private JsValue GetUTCMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return MsFromTime(t);
        }

        private JsValue GetTimezoneOffset(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            return (int) (t - LocalTime(t))/MsPerMinute;
        }

        private JsValue SetTime(JsValue thisObj, JsValue[] arguments)
        {
            return EnsureDateInstance(thisObj).PrimitiveValue = TimeClip(TypeConverter.ToNumber(arguments.At(0)));
        }

        private JsValue SetMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = LocalTime(EnsureDateInstance(thisObj).PrimitiveValue);
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var time = MakeTime(HourFromTime(t), MinFromTime(t), SecFromTime(t), TypeConverter.ToNumber(arguments.At(0)));
            var u = TimeClip(Utc(MakeDate(Day(t), time)));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;

            if (!IsFinite(t))
            {
                return double.NaN;
            }

            var time = MakeTime(HourFromTime(t), MinFromTime(t), SecFromTime(t), TypeConverter.ToNumber(arguments.At(0)));
            var u = TimeClip(MakeDate(Day(t), time));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = LocalTime(EnsureDateInstance(thisObj).PrimitiveValue);
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var s = TypeConverter.ToNumber(arguments.At(0));
            var milli = arguments.Length <= 1 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var date = MakeDate(Day(t), MakeTime(HourFromTime(t), MinFromTime(t), s, milli));
            var u = TimeClip(Utc(date));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var s = TypeConverter.ToNumber(arguments.At(0));
            var milli = arguments.Length <= 1 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var date = MakeDate(Day(t), MakeTime(HourFromTime(t), MinFromTime(t), s, milli));
            var u = TimeClip(date);
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var t = LocalTime(EnsureDateInstance(thisObj).PrimitiveValue);
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var m = TypeConverter.ToNumber(arguments.At(0));
            var s = arguments.Length <= 1 ? SecFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var milli = arguments.Length <= 2 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var date = MakeDate(Day(t), MakeTime(HourFromTime(t), m, s, milli));
            var u = TimeClip(Utc(date));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var m = TypeConverter.ToNumber(arguments.At(0));
            var s = arguments.Length <= 1 ? SecFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var milli = arguments.Length <= 2 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var date = MakeDate(Day(t), MakeTime(HourFromTime(t), m, s, milli));
            var u = TimeClip(date);
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetHours(JsValue thisObj, JsValue[] arguments)
        {
            var t = LocalTime(EnsureDateInstance(thisObj).PrimitiveValue);
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var h = TypeConverter.ToNumber(arguments.At(0));
            var m = arguments.Length <= 1 ? MinFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var s = arguments.Length <= 2 ? SecFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var milli = arguments.Length <= 3 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(3));
            var date = MakeDate(Day(t), MakeTime(h, m, s, milli));
            var u = TimeClip(Utc(date));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCHours(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var h = TypeConverter.ToNumber(arguments.At(0));
            var m = arguments.Length <= 1 ? MinFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var s = arguments.Length <= 2 ? SecFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var milli = arguments.Length <= 3 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(3));
            var date = MakeDate(Day(t), MakeTime(h, m, s, milli));
            var u = TimeClip(date);
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetDate(JsValue thisObj, JsValue[] arguments)
        {
            var t = LocalTime(EnsureDateInstance(thisObj).PrimitiveValue);
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var dt = TypeConverter.ToNumber(arguments.At(0));
            var (year, month, __) = YearMonthDayFromTime(t);
            var newDate = MakeDate(MakeDay(year, month, dt), TimeWithinDay(t));
            var u = TimeClip(Utc(newDate));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCDate(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var dt = TypeConverter.ToNumber(arguments.At(0));
            var newDate = MakeDate(MakeDay(YearFromTime(t), MonthFromTime(t), dt), TimeWithinDay(t));
            var u = TimeClip(newDate);
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetMonth(JsValue thisObj, JsValue[] arguments)
        {
            var t = LocalTime(EnsureDateInstance(thisObj).PrimitiveValue);
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var m = TypeConverter.ToNumber(arguments.At(0));
            var dt = arguments.Length <= 1 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var newDate = MakeDate(MakeDay(YearFromTime(t), m, dt), TimeWithinDay(t));
            var u = TimeClip(Utc(newDate));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCMonth(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (!IsFinite(t))
            {
                return JsNumber.DoubleNaN;
            }
            var m = TypeConverter.ToNumber(arguments.At(0));
            var dt = arguments.Length <= 1 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var newDate = MakeDate(MakeDay(YearFromTime(t), m, dt), TimeWithinDay(t));
            var u = TimeClip(newDate);
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var thisTime = EnsureDateInstance(thisObj).PrimitiveValue;
            var t = double.IsNaN(thisTime) ? 0 : LocalTime(thisTime);
            var y = TypeConverter.ToNumber(arguments.At(0));
            var m = arguments.Length <= 1 ? MonthFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var dt = arguments.Length <= 2 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var newDate = MakeDate(MakeDay(y, m, dt), TimeWithinDay(t));
            var u = TimeClip(Utc(newDate));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetYear(JsValue thisObj, JsValue[] arguments)
        {
            var thisTime = EnsureDateInstance(thisObj).PrimitiveValue;
            var t = double.IsNaN(thisTime) ? 0 : LocalTime(thisTime);
            var y = TypeConverter.ToNumber(arguments.At(0));
            if (double.IsNaN(y))
            {
                EnsureDateInstance(thisObj).PrimitiveValue = double.NaN;
                return JsNumber.DoubleNaN;
            }

            var fy = TypeConverter.ToInteger(y);
            if (y >= 0 && y <= 99)
            {
                fy += 1900;
            }

            var newDate = MakeDay(fy, MonthFromTime(t), DateFromTime(t));
            var u = Utc(MakeDate(newDate, TimeWithinDay(t)));
            EnsureDateInstance(thisObj).PrimitiveValue = TimeClip(u);
            return u;
        }

        private JsValue SetUTCFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var thisTime = EnsureDateInstance(thisObj).PrimitiveValue;
            var t = (long) (double.IsNaN(thisTime) ? 0 : thisTime);
            var y = TypeConverter.ToNumber(arguments.At(0));
            var m = arguments.Length <= 1 ? MonthFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var dt = arguments.Length <= 2 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var newDate = MakeDate(MakeDay(y, m, dt), TimeWithinDay(t));
            var u = TimeClip(newDate);
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue ToUtcString(JsValue thisObj, JsValue[] arguments)
        {
            var thisTime = EnsureDateInstance(thisObj);
            if (!IsFinite(thisTime.PrimitiveValue))
            {
                return "Invalid Date";
            }
            return thisTime.ToDateTime().ToUniversalTime().ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
        }

        private JsValue ToISOString(JsValue thisObj, JsValue[] arguments)
        {
            var thisTime = EnsureDateInstance(thisObj);
            var t = thisTime.PrimitiveValue;
            if (!IsFinite(t))
            {
                ExceptionHelper.ThrowRangeError(_realm);
            }

            if (thisTime.DateTimeRangeValid)
            {
                // shortcut
                var dt = thisTime.ToDateTime();
                return $"{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}T{dt.Hour:00}:{dt.Minute:00}:{dt.Second:00}.{dt.Millisecond:000}Z";
            }

            var h = HourFromTime(t);
            var m = MinFromTime(t);
            var s = SecFromTime(t);
            var ms = MsFromTime(t);
            if (h < 0) { h += HoursPerDay; }
            if (m < 0) { m += MinutesPerHour; }
            if (s < 0) { s += SecondsPerMinute; }
            if (ms < 0) { ms += MsPerSecond; }

            var (year, month, day) = YearMonthDayFromTime(t);
            return $"{year:0000}-{month:00}-{day:00}T{h:00}:{m:00}:{s:00}.{ms:000}Z";
        }

        private JsValue ToJSON(JsValue thisObj, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObj);
            var tv = TypeConverter.ToPrimitive(o, Types.Number);
            if (tv.IsNumber() && !IsFinite(((JsNumber) tv)._value))
            {
                return Null;
            }

            var toIso = o.Get("toISOString", o);
            var callable = toIso as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return callable.Call(o, Arguments.Empty);
        }

        public const int HoursPerDay = 24;
        public const int MinutesPerHour = 60;
        public const int SecondsPerMinute = 60;
        public const int MsPerSecond = 1000;
        public const int MsPerMinute = 60000;
        public const int MsPerHour = 3600000;
        public const long MsPerDay = 86400000;

        /// <summary>
        /// 15.9.1.2
        /// </summary>
        public static int Day(double t)
        {
            return (int) System.Math.Floor(t / MsPerDay);
        }

        /// <summary>
        /// 15.9.1.2
        /// </summary>
        public static double TimeWithinDay(double t)
        {
            var result = t % MsPerDay;

            if (result < 0)
            {
                result += MsPerDay;
            }

            return result;
        }

        /// <summary>
        /// The number of days in a year
        /// </summary>
        public static int DaysInYear(double y)
        {
            if (y%4 != 0)
            {
                return 365;
            }

            if (y%4 == 0 && y%100 != 0)
            {
                return 366;
            }

            if (y%100 == 0 && y%400 != 0)
            {
                return 365;
            }

            if (y%400 == 0)
            {
                return 366;
            }

            return 365;
        }

        /// <summary>
        /// The day number of the first day of the year.
        /// </summary>
        public static int DayFromYear(double y)
        {
            return (int) (365*(y - 1970)
                          + System.Math.Floor((y - 1969)/4)
                          - System.Math.Floor((y - 1901)/100)
                          + System.Math.Floor((y - 1601)/400));
        }

        /// <summary>
        /// The time value of the start of the year
        /// </summary>
        public static long TimeFromYear(double y)
        {
            return MsPerDay*DayFromYear(y);
        }

        /// <summary>
        /// The year of a time value.
        /// </summary>
        public static int YearFromTime(double t)
        {
            var (year, _, _) = YearMonthDayFromTime(t);
            return year;
        }

        /// <summary>
        /// <value>true</value> if the time is within a leap year, <value>false</value> otherwise
        /// </summary>
        public static int InLeapYear(double t)
        {
            var daysInYear = DaysInYear(YearFromTime(t));

            if (daysInYear == 365)
            {
                return 0;
            }

            if (daysInYear == 366)
            {
                return 1;
            }

            ExceptionHelper.ThrowArgumentException();
            return 0;
        }

        /// <summary>
        /// The month number of a time value.
        /// </summary>
        public static int MonthFromTime(double t)
        {
            var dayWithinYear = DayWithinYear(t);
            var inLeapYear = InLeapYear(t);

            if (dayWithinYear < 31)
            {
                return 0;
            }

            if (dayWithinYear < 59 + inLeapYear)
            {
                return 1;
            }

            if (dayWithinYear < 90 + inLeapYear)
            {
                return 2;
            }

            if (dayWithinYear < 120 + inLeapYear)
            {
                return 3;
            }

            if (dayWithinYear < 151 + inLeapYear)
            {
                return 4;
            }

            if (dayWithinYear < 181 + inLeapYear)
            {
                return 5;
            }

            if (dayWithinYear < 212 + inLeapYear)
            {
                return 6;
            }

            if (dayWithinYear < 243 + inLeapYear)
            {
                return 7;
            }

            if (dayWithinYear < 273 + inLeapYear)
            {
                return 8;
            }

            if (dayWithinYear < 304 + inLeapYear)
            {
                return 9;
            }

            if (dayWithinYear < 334 + inLeapYear)
            {
                return 10;
            }

            if (dayWithinYear < 365 + inLeapYear)
            {
                return 11;
            }

            ExceptionHelper.ThrowInvalidOperationException();
            return 0;
        }

        public static int DayWithinYear(double t)
        {
            return Day(t) - DayFromYear(YearFromTime(t));
        }

        public static int DateFromTime(double t)
        {
            var monthFromTime = MonthFromTime(t);
            var dayWithinYear = DayWithinYear(t);

            if (monthFromTime == 0)
            {
                return dayWithinYear + 1;
            }

            if (monthFromTime== 1)
            {
                return dayWithinYear - 30;
            }

            if (monthFromTime == 2)
            {
                return dayWithinYear - 58 - InLeapYear(t);
            }

            if (monthFromTime == 3)
            {
                return dayWithinYear - 89 - InLeapYear(t);
            }

            if (monthFromTime == 4)
            {
                return dayWithinYear - 119 - InLeapYear(t);
            }

            if (monthFromTime == 5)
            {
                return dayWithinYear - 150 - InLeapYear(t);
            }

            if (monthFromTime == 6)
            {
                return dayWithinYear - 180 - InLeapYear(t);
            }

            if (monthFromTime == 7)
            {
                return dayWithinYear - 211 - InLeapYear(t);
            }

            if (monthFromTime == 8)
            {
                return dayWithinYear - 242 - InLeapYear(t);
            }

            if (monthFromTime == 9)
            {
                return dayWithinYear - 272 - InLeapYear(t);
            }

            if (monthFromTime == 10)
            {
                return dayWithinYear - 303 - InLeapYear(t);
            }

            if (monthFromTime == 11)
            {
                return dayWithinYear - 333 - InLeapYear(t);
            }

            ExceptionHelper.ThrowInvalidOperationException();
            return 0;
        }

        /// <summary>
        /// The weekday for a particular time value.
        /// </summary>
        public static int WeekDay(double t)
        {
            return (Day(t) + 4)%7;
        }

        public long LocalTza => (long) Engine.Options.TimeZone.BaseUtcOffset.TotalMilliseconds;

        public double DaylightSavingTa(double t)
        {
            if (double.IsNaN(t))
            {
                return t;
            }

            var year = YearFromTime(t);
            var timeInYear = t - TimeFromYear(year);

            if (double.IsInfinity(timeInYear) || double.IsNaN(timeInYear))
            {
                return 0;
            }

            if (year < 9999 && year > -9999 && year != 0)
            {
                // in DateTimeOffset range so we can use it
            }
            else
            {
                // use similar leap-ed year
                var isLeapYear = InLeapYear((long) t) == 1;
                year = isLeapYear ? 2000 : 1999;
            }

            var dateTime = new DateTime(year, 1, 1).AddMilliseconds(timeInYear);

            return Engine.Options.TimeZone.IsDaylightSavingTime(dateTime) ? MsPerHour : 0;
        }

        private static DateTimeOffset ToLocalTime(DateTime t, TimeZoneInfo timeZone)
        {
            return t.Kind switch
            {
                DateTimeKind.Local => new DateTimeOffset(TimeZoneInfo.ConvertTime(t.ToUniversalTime(), timeZone), timeZone.GetUtcOffset(t)),
                DateTimeKind.Utc => new DateTimeOffset(TimeZoneInfo.ConvertTime(t, timeZone), timeZone.GetUtcOffset(t)),
                _ => t
            };
        }

        public double LocalTime(double t)
        {
            if (!IsFinite(t))
            {
                return double.NaN;
            }

            return (long) (t + LocalTza + DaylightSavingTa((long) t));
        }

        public double Utc(double t)
        {
            return t - LocalTza - DaylightSavingTa(t - LocalTza);
        }

        public static int HourFromTime(double t)
        {
            var hours = System.Math.Floor(t / MsPerHour) % HoursPerDay;

            if (hours < 0)
            {
                hours += HoursPerDay;
            }

            return (int) hours;
        }

        public static int MinFromTime(double t)
        {
            var minutes = System.Math.Floor(t / MsPerMinute) % MinutesPerHour;

            if (minutes < 0)
            {
                minutes += MinutesPerHour;
            }

            return (int) minutes;
        }

        public static int SecFromTime(double t)
        {
            var seconds = System.Math.Floor(t / MsPerSecond) % SecondsPerMinute;

            if (seconds < 0)
            {
                seconds += SecondsPerMinute;
            }

            return (int) seconds;
        }

        public static int MsFromTime(double t)
        {
            var milli = t % MsPerSecond;

            if (milli < 0)
            {
                milli += MsPerSecond;
            }

            return (int) milli;
        }

        public static double MakeTime(double hour, double min, double sec, double ms)
        {
            if (!AreFinite(hour, min, sec, ms))
            {
                return double.NaN;
            }

            var h = TypeConverter.ToInteger(hour);
            var m = TypeConverter.ToInteger(min);
            var s = TypeConverter.ToInteger(sec);
            var milli = TypeConverter.ToInteger(ms);
            var t = h*MsPerHour + m*MsPerMinute + s*MsPerSecond + milli;

            return t;
        }

        public static double MakeDay(double year, double month, double date)
        {
            if ((year < MinYear || year > MaxYear) || month < MinMonth  || month > MaxMonth || !AreFinite(year, month, date))
            {
                return double.NaN;
            }

            var y = (long) TypeConverter.ToInteger(year);
            var m = (long) TypeConverter.ToInteger(month);
            y += m / 12;
            m %= 12;
            if (m < 0) {
                m += 12;
                y -= 1;
            }

            // kYearDelta is an arbitrary number such that:
            // a) kYearDelta = -1 (mod 400)
            // b) year + kYearDelta > 0 for years in the range defined by
            //    ECMA 262 - 15.9.1.1, i.e. upto 100,000,000 days on either side of
            //    Jan 1 1970. This is required so that we don't run into integer
            //    division of negative numbers.
            // c) there shouldn't be an overflow for 32-bit integers in the following
            //    operations.
            const int kYearDelta = 399999;
            const int kBaseDay =
                365 * (1970 + kYearDelta) + (1970 + kYearDelta) / 4 -
                (1970 + kYearDelta) / 100 + (1970 + kYearDelta) / 400;

            long dayFromYear = 365 * (y + kYearDelta) + (y + kYearDelta) / 4 -
                                (y + kYearDelta) / 100 + (y + kYearDelta) / 400 -  kBaseDay;

            if ((y % 4 != 0) || (y % 100 == 0 && y % 400 != 0)) {
                var dayFromMonth = new [] {0,   31,  59,  90,  120, 151,  181, 212, 243, 273, 304, 334};
                dayFromYear += dayFromMonth[m];
            } else {
                var dayFromMonthLeapYear = new [] {0,   31,  60,  91,  121, 152, 182, 213, 244, 274, 305, 335};
                dayFromYear += dayFromMonthLeapYear[m];
            }
            return dayFromYear - 1 + TypeConverter.ToInteger(date);
        }

        public static double MakeDate(double day, double time)
        {
            if (!AreFinite(day, time))
            {
                return double.NaN;
            }

            return day * MsPerDay + time;
        }

        public static double TimeClip(double time)
        {
            if (!IsFinite(time))
            {
                return double.NaN;
            }

            if (System.Math.Abs(time) > 8640000000000000)
            {
                return double.NaN;
            }

            return (long) time + 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool AreFinite(double value1, double value2)
        {
            return IsFinite(value1) && IsFinite(value2);
        }

        private static bool AreFinite(double value1, double value2, double value3)
        {
            return IsFinite(value1) && IsFinite(value2) &&  IsFinite(value3);
        }

        private static bool AreFinite(double value1, double value2, double value3, double value4)
        {
            return IsFinite(value1) && IsFinite(value2) &&  IsFinite(value3) && IsFinite(value4);
        }
        private readonly struct Date
        {
            public Date(int year, int month, int day)
            {
                Year = year;
                Month = month;
                Day = day;
            }

            public readonly int Year;
            public readonly int Month;
            public readonly int Day;

            public void Deconstruct(out int year, out int month, out int day)
            {
                year = Year;
                month = Month;
                day = Day;
            }
        }

        private static readonly int[] kDaysInMonths = {31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};

        private static Date YearMonthDayFromTime(double t) => YearMonthDayFromDays((long) (t / 1000 / 60 / 60 / 24));

        private static Date YearMonthDayFromDays(long days)
        {
            const int kDaysIn4Years = 4 * 365 + 1;
            const int kDaysIn100Years = 25 * kDaysIn4Years - 1;
            const int kDaysIn400Years = 4 * kDaysIn100Years + 1;
            const int kDays1970to2000 = 30 * 365 + 7;
            const int kDaysOffset =
                1000 * kDaysIn400Years + 5 * kDaysIn400Years - kDays1970to2000;
            const int kYearsOffset = 400000;


            days += kDaysOffset;
            var year = 400 * (days / kDaysIn400Years) - kYearsOffset;
            days %= kDaysIn400Years;

            days--;
            var yd1 = days / kDaysIn100Years;
            days %= kDaysIn100Years;
            year += 100 * yd1;

            days++;
            var yd2 = days / kDaysIn4Years;
            days %= kDaysIn4Years;
            year += 4 * yd2;

            days--;
            var yd3 = days / 365;
            days %= 365;
            year += yd3;

            var is_leap = (yd1 == 0 || yd2 != 0) && yd3 == 0;

            days += is_leap ? 1 : 0;
            var month = 0;
            var day = 0;

            // Check if the date is after February.
            if (days >= 31 + 28 + (is_leap ? 1 : 0))
            {
                days -= 31 + 28 + (is_leap ? 1 : 0);
                // Find the date starting from March.
                for (int i = 2; i < 12; i++)
                {
                    if (days < kDaysInMonths[i])
                    {
                        month = i;
                        day = (int) (days + 1);
                        break;
                    }

                    days -= kDaysInMonths[i];
                }
            }
            else
            {
                // Check January and February.
                if (days < 31)
                {
                    month = 0;
                    day = (int) (days + 1);
                }
                else
                {
                    month = 1;
                    day = (int) (days - 31 + 1);
                }
            }

            return new Date((int) year, month, day);
        }

        public override string ToString()
        {
            return "Date.prototype";
        }
    }
}
