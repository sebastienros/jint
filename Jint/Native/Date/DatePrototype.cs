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
    /// https://tc39.es/ecma262/#sec-properties-of-the-date-prototype-object
    /// </summary>
    internal sealed class DatePrototype : Prototype
    {
        // ES6 section 20.3.1.1 Time Values and Time Range
        private const double MinYear = -1000000.0;
        private const double MaxYear = -MinYear;
        private const double MinMonth = -10000000.0;
        private const double MaxMonth = -MinMonth;

        private readonly DateConstructor _constructor;
        private readonly ITimeSystem _timeSystem;

        internal DatePrototype(
            Engine engine,
            DateConstructor constructor,
            ObjectPrototype objectPrototype)
            : base(engine, engine.Realm)
        {
            _prototype = objectPrototype;
            _constructor = constructor;
            _timeSystem = engine.Options.TimeSystem;
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
                ["toJSON"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toJSON", ToJson, 1, lengthFlags), propertyFlags)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.ToPrimitive] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.toPrimitive]", ToPrimitive, 1, PropertyFlag.Configurable), PropertyFlag.Configurable),
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype-@@toprimitive
        /// </summary>
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

        private JsValue ValueOf(JsValue thisObject, JsValue[] arguments)
        {
            return ThisTimeValue(thisObject).ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#thistimevalue
        /// </summary>
        private DatePresentation ThisTimeValue(JsValue thisObject)
        {
            if (thisObject is JsDate dateInstance)
            {
                return dateInstance._dateValue;
            }

            ExceptionHelper.ThrowTypeError(_realm, "this is not a Date object");
            return default;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.tostring
        /// </summary>
        internal JsValue ToString(JsValue thisObject, JsValue[] arg2)
        {
            var tv = ThisTimeValue(thisObject);
            return ToDateString(tv);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.todatestring
        /// </summary>
        private JsValue ToDateString(JsValue thisObject, JsValue[] arguments)
        {
            var tv = ThisTimeValue(thisObject);

            if (tv.IsNaN)
            {
                return "Invalid Date";
            }

            var t = LocalTime(tv);
            return DateString(t);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-todatestring
        /// </summary>
        private JsValue ToDateString(DatePresentation tv)
        {
            if (tv.IsNaN)
            {
                return "Invalid Date";
            }

            var t = LocalTime(tv);
            return DateString(t) + " " + TimeString(t) + TimeZoneString(tv);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.totimestring
        /// </summary>
        private JsValue ToTimeString(JsValue thisObject, JsValue[] arguments)
        {
            var tv = ThisTimeValue(thisObject);

            if (tv.IsNaN)
            {
                return "Invalid Date";
            }

            var t = LocalTime(tv);

            return TimeString(t) + TimeZoneString(tv);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.tolocalestring
        /// </summary>
        private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
        {
            var dateInstance = ThisTimeValue(thisObject);

            if (dateInstance.IsNaN)
            {
                return "Invalid Date";
            }

            return ToLocalTime(dateInstance).ToString("F", Engine.Options.Culture);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.tolocaledatestring
        /// </summary>
        private JsValue ToLocaleDateString(JsValue thisObject, JsValue[] arguments)
        {
            var dateInstance = ThisTimeValue(thisObject);

            if (dateInstance.IsNaN)
            {
                return "Invalid Date";
            }

            return ToLocalTime(dateInstance).ToString("D", Engine.Options.Culture);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.tolocaletimestring
        /// </summary>
        private JsValue ToLocaleTimeString(JsValue thisObject, JsValue[] arguments)
        {
            var dateInstance = ThisTimeValue(thisObject);

            if (dateInstance.IsNaN)
            {
                return "Invalid Date";
            }

            return ToLocalTime(dateInstance).ToString("T", Engine.Options.Culture);
        }

        private JsValue GetTime(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return t.ToJsValue();
        }

        private JsValue GetFullYear(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return YearFromTime(LocalTime(t));
        }

        private JsValue GetYear(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return YearFromTime(LocalTime(t)) - 1900;
        }

        private JsValue GetUTCFullYear(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return YearFromTime(t);
        }

        private JsValue GetMonth(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return MonthFromTime(LocalTime(t));
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.getutcmonth
        /// </summary>
        private JsValue GetUTCMonth(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return MonthFromTime(t);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.getdate
        /// </summary>
        private JsValue GetDate(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return DateFromTime(LocalTime(t));
        }

        private JsValue GetUTCDate(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return DateFromTime(t);
        }

        private JsValue GetDay(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return WeekDay(LocalTime(t));
        }

        private JsValue GetUTCDay(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return WeekDay(t);
        }

        private JsValue GetHours(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return HourFromTime(LocalTime(t));
        }

        private JsValue GetUTCHours(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return HourFromTime(t);
        }

        private JsValue GetMinutes(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return MinFromTime(LocalTime(t));
        }

        private JsValue GetUTCMinutes(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return MinFromTime(t);
        }

        private JsValue GetSeconds(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return SecFromTime(LocalTime(t));
        }

        private JsValue GetUTCSeconds(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return SecFromTime(t);
        }

        private JsValue GetMilliseconds(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return MsFromTime(LocalTime(t));
        }

        private JsValue GetUTCMilliseconds(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return MsFromTime(t);
        }

        private JsValue GetTimezoneOffset(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }
            return (int) ((double) t.Value - LocalTime(t).Value)/MsPerMinute;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.settime
        /// </summary>
        private JsValue SetTime(JsValue thisObject, JsValue[] arguments)
        {
            ThisTimeValue(thisObject);
            var t = TypeConverter.ToNumber(arguments.At(0));
            var v = t.TimeClip();

            ((JsDate) thisObject)._dateValue = t;
            return v.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setmilliseconds
        /// </summary>
        private JsValue SetMilliseconds(JsValue thisObject, JsValue[] arguments)
        {
            var t = LocalTime(ThisTimeValue(thisObject));
            var ms = TypeConverter.ToNumber(arguments.At(0));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var time = MakeTime(HourFromTime(t), MinFromTime(t), SecFromTime(t), ms);
            var u = Utc(MakeDate(Day(t), time)).TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setutcmilliseconds
        /// </summary>
        private JsValue SetUTCMilliseconds(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            var milli = TypeConverter.ToNumber(arguments.At(0));

            if (t.IsNaN)
            {
                return double.NaN;
            }

            var time = MakeTime(HourFromTime(t), MinFromTime(t), SecFromTime(t), milli);
            var u = MakeDate(Day(t), time).TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setseconds
        /// </summary>
        private JsValue SetSeconds(JsValue thisObject, JsValue[] arguments)
        {
            var t = LocalTime(ThisTimeValue(thisObject));
            var s = TypeConverter.ToNumber(arguments.At(0));
            var milli = arguments.Length <= 1 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(1));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var date = MakeDate(Day(t), MakeTime(HourFromTime(t), MinFromTime(t), s, milli));
            var u = Utc(date).TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setutcseconds
        /// </summary>
        private JsValue SetUTCSeconds(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            var s = TypeConverter.ToNumber(arguments.At(0));
            var milli = arguments.Length <= 1 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(1));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var date = MakeDate(Day(t), MakeTime(HourFromTime(t), MinFromTime(t), s, milli));
            var u = date.TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setminutes
        /// </summary>
        private JsValue SetMinutes(JsValue thisObject, JsValue[] arguments)
        {
            var t = LocalTime(ThisTimeValue(thisObject));
            var m = TypeConverter.ToNumber(arguments.At(0));
            var s = arguments.Length <= 1 ? SecFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var milli = arguments.Length <= 2 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(2));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var date = MakeDate(Day(t), MakeTime(HourFromTime(t), m, s, milli));
            var u = Utc(date).TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setutcminutes
        /// </summary>
        private JsValue SetUTCMinutes(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            var m = TypeConverter.ToNumber(arguments.At(0));
            var s = arguments.Length <= 1 ? SecFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var milli = arguments.Length <= 2 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(2));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var date = MakeDate(Day(t), MakeTime(HourFromTime(t), m, s, milli));
            var u = date.TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.sethours
        /// </summary>
        private JsValue SetHours(JsValue thisObject, JsValue[] arguments)
        {
            var t = LocalTime(ThisTimeValue(thisObject));
            var h = TypeConverter.ToNumber(arguments.At(0));
            var m = arguments.Length <= 1 ? MinFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var s = arguments.Length <= 2 ? SecFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var milli = arguments.Length <= 3 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(3));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var date = MakeDate(Day(t), MakeTime(h, m, s, milli));
            var u = Utc(date).TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setutchours
        /// </summary>
        private JsValue SetUTCHours(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            var h = TypeConverter.ToNumber(arguments.At(0));
            var m = arguments.Length <= 1 ? MinFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var s = arguments.Length <= 2 ? SecFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var milli = arguments.Length <= 3 ? MsFromTime(t) : TypeConverter.ToNumber(arguments.At(3));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var newDate = MakeDate(Day(t), MakeTime(h, m, s, milli));
            var v = newDate.TimeClip();
            ((JsDate) thisObject)._dateValue = v;
            return v.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setdate
        /// </summary>
        private JsValue SetDate(JsValue thisObject, JsValue[] arguments)
        {
            var t = LocalTime(ThisTimeValue(thisObject));
            var dt = TypeConverter.ToNumber(arguments.At(0));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var (year, month, __) = YearMonthDayFromTime(t);
            var newDate = MakeDate(MakeDay(year, month, dt), TimeWithinDay(t));
            var u = Utc(newDate).TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setutcdate
        /// </summary>
        private JsValue SetUTCDate(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            var dt = TypeConverter.ToNumber(arguments.At(0));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var newDate = MakeDate(MakeDay(YearFromTime(t), MonthFromTime(t), dt), TimeWithinDay(t));
            var u = newDate.TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setmonth
        /// </summary>
        private JsValue SetMonth(JsValue thisObject, JsValue[] arguments)
        {
            var t = LocalTime(ThisTimeValue(thisObject));
            var m = TypeConverter.ToNumber(arguments.At(0));
            var dt = arguments.Length <= 1 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(1));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var newDate = MakeDate(MakeDay(YearFromTime(t), m, dt), TimeWithinDay(t));
            var u = Utc(newDate).TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setutcmonth
        /// </summary>
        private JsValue SetUTCMonth(JsValue thisObject, JsValue[] arguments)
        {
            var t = ThisTimeValue(thisObject);
            var m = TypeConverter.ToNumber(arguments.At(0));
            var dt = arguments.Length <= 1 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(1));

            if (t.IsNaN)
            {
                return JsNumber.DoubleNaN;
            }

            var newDate = MakeDate(MakeDay(YearFromTime(t), m, dt), TimeWithinDay(t));
            var u = newDate.TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setfullyear
        /// </summary>
        private JsValue SetFullYear(JsValue thisObject, JsValue[] arguments)
        {
            var thisTime = ThisTimeValue(thisObject);
            var t = thisTime.IsNaN ? 0 : LocalTime(thisTime);
            var y = TypeConverter.ToNumber(arguments.At(0));
            var m = arguments.Length <= 1 ? MonthFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var dt = arguments.Length <= 2 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(2));

            var newDate = MakeDate(MakeDay(y, m, dt), TimeWithinDay(t));
            var u = Utc(newDate).TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setyear
        /// </summary>
        private JsValue SetYear(JsValue thisObject, JsValue[] arguments)
        {
            var thisTime = ThisTimeValue(thisObject);
            var t = thisTime.IsNaN ? 0 : LocalTime(thisTime);
            var y = TypeConverter.ToNumber(arguments.At(0));
            if (double.IsNaN(y))
            {
                ((JsDate) thisObject)._dateValue = double.NaN;
                return JsNumber.DoubleNaN;
            }

            var fy = TypeConverter.ToInteger(y);
            if (y >= 0 && y <= 99)
            {
                fy += 1900;
            }

            var newDate = MakeDay(fy, MonthFromTime(t), DateFromTime(t));
            var u = Utc(MakeDate(newDate, TimeWithinDay(t)));
            ((JsDate) thisObject)._dateValue = u.TimeClip();
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.setutcfullyear
        /// </summary>
        private JsValue SetUTCFullYear(JsValue thisObject, JsValue[] arguments)
        {
            var thisTime = ThisTimeValue(thisObject);
            var t = thisTime.IsNaN ? 0 : thisTime;
            var y = TypeConverter.ToNumber(arguments.At(0));
            var m = arguments.Length <= 1 ? MonthFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var dt = arguments.Length <= 2 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(2));
            var newDate = MakeDate(MakeDay(y, m, dt), TimeWithinDay(t));
            var u = newDate.TimeClip();
            ((JsDate) thisObject)._dateValue = u;
            return u.ToJsValue();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.toutcstring
        /// </summary>
        private JsValue ToUtcString(JsValue thisObject, JsValue[] arguments)
        {
            var tv = ThisTimeValue(thisObject);
            if (!IsFinite(tv))
            {
                return "Invalid Date";
            }

            var weekday = _dayNames[WeekDay(tv)];
            var month = _monthNames[MonthFromTime(tv)];
            var day = DateFromTime(tv).ToString("00");
            var yv = YearFromTime(tv);
            var paddedYear = yv.ToString("0000");

            return $"{weekday}, {day} {month} {paddedYear} {TimeString(tv)}";
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-date.prototype.toisostring
        /// </summary>
        private JsValue ToISOString(JsValue thisObject, JsValue[] arguments)
        {
            var thisTime = ThisTimeValue(thisObject);
            var t = thisTime;
            if (t.IsNaN)
            {
                ExceptionHelper.ThrowRangeError(_realm);
            }

            if (((JsDate) thisObject).DateTimeRangeValid)
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
            month++;

            var formatted = $"{year:0000}-{month:00}-{day:00}T{h:00}:{m:00}:{s:00}.{ms:000}Z";
            if (year > 9999)
            {
                formatted = "+" + formatted;
            }

            return formatted;
        }

        private JsValue ToJson(JsValue thisObject, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObject);
            var tv = TypeConverter.ToPrimitive(o, Types.Number);
            if (tv.IsNumber() && !IsFinite(((JsNumber) tv)._value))
            {
                return Null;
            }

            return Invoke(o, "toISOString", Arguments.Empty);
        }

        private const int HoursPerDay = 24;
        private const int MinutesPerHour = 60;
        private const int SecondsPerMinute = 60;
        private const int MsPerSecond = 1000;
        private const int MsPerMinute = 60000;
        private const int MsPerHour = 3600000;
        private const long MsPerDay = 86400000;

        /// <summary>
        /// https://tc39.es/ecma262/#eqn-Day
        /// </summary>
        private static int Day(DatePresentation t)
        {
            return (int) System.Math.Floor((double) t.Value / MsPerDay);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#eqn-Day
        /// </summary>
        private static long TimeWithinDay(DatePresentation t)
        {
            var result = t.Value % MsPerDay;

            if (result < 0)
            {
                result += MsPerDay;
            }

            return result;
        }

        /// <summary>
        /// The number of days in a year
        /// </summary>
        private static int DaysInYear(double y)
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
        private static int DayFromYear(DatePresentation y)
        {
            return (int) (365*(y.Value - 1970)
                          + System.Math.Floor((y.Value - 1969)/4d)
                          - System.Math.Floor((y.Value - 1901)/100d)
                          + System.Math.Floor((y.Value - 1601)/400d));
        }

        /// <summary>
        /// The time value of the start of the year
        /// </summary>
        private static long TimeFromYear(DatePresentation y)
        {
            return MsPerDay*DayFromYear(y);
        }

        /// <summary>
        /// The year of a time value.
        /// </summary>
        private static int YearFromTime(DatePresentation t)
        {
            var (year, _, _) = YearMonthDayFromTime(t);
            return year;
        }

        /// <summary>
        /// <value>true</value> if the time is within a leap year, <value>false</value> otherwise
        /// </summary>
        private static int InLeapYear(DatePresentation t)
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
        private static int MonthFromTime(DatePresentation t)
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

        private static int DayWithinYear(DatePresentation t)
        {
            return Day(t) - DayFromYear(YearFromTime(t));
        }

        private static int DateFromTime(DatePresentation t)
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
        /// https://tc39.es/ecma262/#sec-week-day
        /// </summary>
        private static int WeekDay(DatePresentation t)
        {
            var result = (Day(t) + 4) % 7;
            return result >= 0 ? result : result + 7;
        }

        private DateTime ToLocalTime(DatePresentation t)
        {
            var utcOffset = _timeSystem.GetUtcOffset(t.Value).TotalMilliseconds;
            return (t + utcOffset).ToDateTime();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-localtime
        /// </summary>
        private DatePresentation LocalTime(DatePresentation t)
        {
            if (t.IsNaN)
            {
                return DatePresentation.NaN;
            }

            var offset = _timeSystem.GetUtcOffset(t.Value).TotalMilliseconds;
            return t + offset;
        }

        internal DatePresentation Utc(DatePresentation t)
        {
            var offset = _timeSystem.GetUtcOffset(t.Value).TotalMilliseconds;
            return t - offset;
        }

        private static int HourFromTime(DatePresentation t)
        {
            var hours = System.Math.Floor((double) t.Value / MsPerHour) % HoursPerDay;

            if (hours < 0)
            {
                hours += HoursPerDay;
            }

            return (int) hours;
        }

        private static int MinFromTime(DatePresentation t)
        {
            var minutes = System.Math.Floor((double) t.Value / MsPerMinute) % MinutesPerHour;

            if (minutes < 0)
            {
                minutes += MinutesPerHour;
            }

            return (int) minutes;
        }

        private static int SecFromTime(DatePresentation t)
        {
            var seconds = System.Math.Floor((double) t.Value / MsPerSecond) % SecondsPerMinute;

            if (seconds < 0)
            {
                seconds += SecondsPerMinute;
            }

            return (int) seconds;
        }

        private static int MsFromTime(DatePresentation t)
        {
            var milli = t.Value % MsPerSecond;

            if (milli < 0)
            {
                milli += MsPerSecond;
            }

            return (int) milli;
        }

        internal static double MakeTime(double hour, double min, double sec, double ms)
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

        private static readonly int[] _dayFromMonth = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
        private static readonly int[] _dayFromMonthLeapYear = { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335 };

        internal static double MakeDay(double year, double month, double date)
        {
            if (year < MinYear || year > MaxYear || month < MinMonth || month > MaxMonth || !AreFinite(year, month, date))
            {
                return double.NaN;
            }

            var y = (long) TypeConverter.ToInteger(year);
            var m = (long) TypeConverter.ToInteger(month);
            var dt = (long) TypeConverter.ToInteger(date);

            y += m / 12;
            m %= 12;
            if (m < 0)
            {
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
            const long kBaseDay =
                365 * (1970 + kYearDelta) + (1970 + kYearDelta) / 4 -
                (1970 + kYearDelta) / 100 + (1970 + kYearDelta) / 400;

            var dayFromYear = 365 * (y + kYearDelta) + (y + kYearDelta) / 4 -
                (y + kYearDelta) / 100 + (y + kYearDelta) / 400 - kBaseDay;

            if (y % 4 != 0 || (y % 100 == 0 && y % 400 != 0))
            {
                dayFromYear += _dayFromMonth[m];
            }
            else
            {
                dayFromYear += _dayFromMonthLeapYear[m];
            }

            return dayFromYear + dt - 1;
        }

        internal static DatePresentation MakeDate(double day, double time)
        {
            if (!AreFinite(day, time))
            {
                return DatePresentation.NaN;
            }

            return new DatePresentation((long) (day * MsPerDay + time), DateFlags.None);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool AreFinite(double value1, double value2) => IsFinite(value1) && IsFinite(value2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(DatePresentation value) => value.IsFinite;

        private static bool AreFinite(double value1, double value2, double value3)
            => IsFinite(value1) && IsFinite(value2) && IsFinite(value3);

        private static bool AreFinite(double value1, double value2, double value3, double value4)
            => IsFinite(value1) && IsFinite(value2) &&  IsFinite(value3) && IsFinite(value4);

        private readonly record struct Date(int Year, int Month, int Day);

        private static readonly int[] kDaysInMonths = {31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};

        private static Date YearMonthDayFromTime(DatePresentation t) => YearMonthDayFromDays(t.Value / 1000 / 60 / 60 / 24);

        private static Date YearMonthDayFromDays(long days)
        {
            const int kDaysIn4Years = 4 * 365 + 1;
            const int kDaysIn100Years = 25 * kDaysIn4Years - 1;
            const int kDaysIn400Years = 4 * kDaysIn100Years + 1;
            const int kDays1970to2000 = 30 * 365 + 7;
            const int kDaysOffset = 1000 * kDaysIn400Years + 5 * kDaysIn400Years - kDays1970to2000;
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

        private static readonly string[] _dayNames =
        {
            "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
        };

        private static readonly string[] _monthNames =
        {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
        };

        /// <summary>
        /// https://tc39.es/ecma262/#sec-datestring
        /// </summary>
        private static string DateString(DatePresentation tv)
        {
            var weekday = _dayNames[WeekDay(tv)];
            var month = _monthNames[MonthFromTime(tv)];

            var dateFromTime = DateFromTime(tv);
            var day = System.Math.Max(1, dateFromTime).ToString("00");
            var yv = YearFromTime(tv);
            var yearSign = yv < 0 ? "-" : "";
            var year = System.Math.Abs(yv);
            var paddedYear = year.ToString("0000");

            return weekday + " " + month + " " + day + " " + yearSign + paddedYear;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-timestring
        /// </summary>
        private static string TimeString(DatePresentation t)
        {
            var hour = HourFromTime(t).ToString("00");
            var minute = MinFromTime(t).ToString("00");
            var second = SecFromTime(t).ToString("00");

            return hour + ":" + minute + ":" + second + " GMT";
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-timezoneestring
        /// </summary>
        private string TimeZoneString(DatePresentation tv)
        {
            var offset = _timeSystem.GetUtcOffset(tv.Value).TotalMilliseconds;

            string offsetSign;
            double absOffset;
            if (offset >= 0)
            {
                offsetSign = "+";
                absOffset = offset;
            }
            else
            {
                offsetSign = "-";
                absOffset = -1 * offset;
            }

            var offsetMin = MinFromTime(absOffset).ToString("00");
            var offsetHour = HourFromTime(absOffset).ToString("00");

            var tzName = " (" + _timeSystem.DefaultTimeZone.StandardName + ")";

            return offsetSign + offsetHour + offsetMin + tzName;
        }

        public override string ToString()
        {
            return "Date.prototype";
        }
    }
}
