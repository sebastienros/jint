using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Date
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.9.5
    /// </summary>
    public sealed class DatePrototype : DateInstance
    {
        private DateConstructor _dateConstructor;

        private DatePrototype(Engine engine)
            : base(engine)
        {
        }

        public static DatePrototype CreatePrototypeObject(Engine engine, DateConstructor dateConstructor)
        {
            var obj = new DatePrototype(engine)
            {
                Prototype = engine.Object.PrototypeObject,
                Extensible = true,
                PrimitiveValue = double.NaN,
                _dateConstructor = dateConstructor
            };

            return obj;
        }

        protected override  void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(50)
            {
                ["constructor"] = new PropertyDescriptor(_dateConstructor, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToString, 0), true, false, true),
                ["toDateString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toDateString", ToDateString, 0), true, false, true),
                ["toTimeString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toTimeString", ToTimeString, 0), true, false, true),
                ["toLocaleString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0), true, false, true),
                ["toLocaleDateString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleDateString", ToLocaleDateString, 0), true, false, true),
                ["toLocaleTimeString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleTimeString", ToLocaleTimeString, 0), true, false, true),
                ["valueOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0), true, false, true),
                ["getTime"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getTime", GetTime, 0), true, false, true),
                ["getFullYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getFullYear", GetFullYear, 0), true, false, true),
                ["getYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getYear", GetYear, 0), true, false, true),
                ["getUTCFullYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCFullYear", GetUTCFullYear, 0), true, false, true),
                ["getMonth"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getMonth", GetMonth, 0), true, false, true),
                ["getUTCMonth"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCMonth", GetUTCMonth, 0), true, false, true),
                ["getDate"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getDate", GetDate, 0), true, false, true),
                ["getUTCDate"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCDate", GetUTCDate, 0), true, false, true),
                ["getDay"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getDay", GetDay, 0), true, false, true),
                ["getUTCDay"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCDay", GetUTCDay, 0), true, false, true),
                ["getHours"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getHours", GetHours, 0), true, false, true),
                ["getUTCHours"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCHours", GetUTCHours, 0), true, false, true),
                ["getMinutes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getMinutes", GetMinutes, 0), true, false, true),
                ["getUTCMinutes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCMInutes", GetUTCMinutes, 0), true, false, true),
                ["getSeconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getSeconds", GetSeconds, 0), true, false, true),
                ["getUTCSeconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCSeconds", GetUTCSeconds, 0), true, false, true),
                ["getMilliseconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getMilliseconds", GetMilliseconds, 0), true, false, true),
                ["getUTCMilliseconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getUTCMilliseconds", GetUTCMilliseconds, 0), true, false, true),
                ["getTimezoneOffset"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "getTimezoneOffset", GetTimezoneOffset, 0), true, false, true),
                ["setTime"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setTime", SetTime, 1), true, false, true),
                ["setMilliseconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setMilliseconds", SetMilliseconds, 1), true, false, true),
                ["setUTCMilliseconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCMilliseconds", SetUTCMilliseconds, 1), true, false, true),
                ["setSeconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setSeconds", SetSeconds, 2), true, false, true),
                ["setUTCSeconds"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCSeconds", SetUTCSeconds, 2), true, false, true),
                ["setMinutes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setMinutes", SetMinutes, 3), true, false, true),
                ["setUTCMinutes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCMinutes", SetUTCMinutes, 3), true, false, true),
                ["setHours"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setHours", SetHours, 4), true, false, true),
                ["setUTCHours"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCHours", SetUTCHours, 4), true, false, true),
                ["setDate"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setDate", SetDate, 1), true, false, true),
                ["setUTCDate"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCDate", SetUTCDate, 1), true, false, true),
                ["setMonth"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setMonth", SetMonth, 2), true, false, true),
                ["setUTCMonth"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCMonth", SetUTCMonth, 2), true, false, true),
                ["setFullYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setFullYear", SetFullYear, 3), true, false, true),
                ["setYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setYear", SetYear, 1), true, false, true),
                ["setUTCFullYear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "setUTCFullYear     ", SetUTCFullYear, 3), true, false, true),
                ["toUTCString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toUTCString", ToUtcString, 0), true, false, true),
                ["toISOString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toISOString", ToISOString, 0), true, false, true),
                ["toJSON"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toJSON", ToJSON, 1), true, false, true)
            };
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
            return thisObj as DateInstance ?? ExceptionHelper.ThrowTypeError<DateInstance>(_engine, "Invalid Date");
        }

        public JsValue ToString(JsValue thisObj, JsValue[] arg2)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime()).ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'K", CultureInfo.InvariantCulture);
        }

        private JsValue ToDateString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime()).ToString("ddd MMM dd yyyy", CultureInfo.InvariantCulture);
        }

        private JsValue ToTimeString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime()).ToString("HH:mm:ss 'GMT'K", CultureInfo.InvariantCulture);
        }

        private JsValue ToLocaleString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime()).ToString("F", Engine.Options._Culture);
        }

        private JsValue ToLocaleDateString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime()).ToString("D", Engine.Options._Culture);
        }

        private JsValue ToLocaleTimeString(JsValue thisObj, JsValue[] arguments)
        {
            var dateInstance = EnsureDateInstance(thisObj);

            if (double.IsNaN(dateInstance.PrimitiveValue))
                return "Invalid Date";

            return ToLocalTime(dateInstance.ToDateTime()).ToString("T", Engine.Options._Culture);
        }

        private JsValue GetTime(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(EnsureDateInstance(thisObj).PrimitiveValue))
            {
                return JsNumber.DoubleNaN;
            }

            return EnsureDateInstance(thisObj).PrimitiveValue;
        }

        private JsValue GetFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return YearFromTime(LocalTime(t));
        }

        private JsValue GetYear(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return YearFromTime(LocalTime(t)) - 1900;
        }

        private JsValue GetUTCFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return YearFromTime(t);
        }

        private JsValue GetMonth(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return MonthFromTime(LocalTime(t));
        }

        private JsValue GetUTCMonth(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return MonthFromTime(t);
        }

        private JsValue GetDate(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return DateFromTime(LocalTime(t));
        }

        private JsValue GetUTCDate(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return DateFromTime(t);
        }

        private JsValue GetDay(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return WeekDay(LocalTime(t));
        }

        private JsValue GetUTCDay(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return WeekDay(t);
        }

        private JsValue GetHours(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return HourFromTime(LocalTime(t));
        }

        private JsValue GetUTCHours(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return HourFromTime(t);
        }

        private JsValue GetMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return MinFromTime(LocalTime(t));
        }

        private JsValue GetUTCMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return MinFromTime(t);
        }

        private JsValue GetSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = thisObj.TryCast<DateInstance>().PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return SecFromTime(LocalTime(t));
        }

        private JsValue GetUTCSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return SecFromTime(t);
        }

        private JsValue GetMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return MsFromTime(LocalTime(t));
        }

        private JsValue GetUTCMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
            {
                return JsNumber.DoubleNaN;
            }

            return MsFromTime(t);
        }

        private JsValue GetTimezoneOffset(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            if (double.IsNaN(t))
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
            var time = MakeTime(HourFromTime(t), MinFromTime(t), SecFromTime(t), TypeConverter.ToNumber(arguments.At(0)));
            var u = TimeClip(Utc(MakeDate(Day(t), time)));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = EnsureDateInstance(thisObj).PrimitiveValue;
            var time = MakeTime(HourFromTime(t), MinFromTime(t), SecFromTime(t), TypeConverter.ToNumber(arguments.At(0)));
            var u = TimeClip(MakeDate(Day(t), time));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var t = LocalTime(EnsureDateInstance(thisObj).PrimitiveValue);
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
            var dt = TypeConverter.ToNumber(arguments.At(0));
            var newDate = MakeDate(MakeDay(YearFromTime(t), MonthFromTime(t), dt), TimeWithinDay(t));
            var u = TimeClip(Utc(newDate));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCDate(JsValue thisObj, JsValue[] arguments)
        {
            var t = (long) EnsureDateInstance(thisObj).PrimitiveValue;
            var dt = TypeConverter.ToNumber(arguments.At(0));
            var newDate = MakeDate(MakeDay(YearFromTime(t), MonthFromTime(t), dt), TimeWithinDay(t));
            var u = TimeClip(newDate);
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetMonth(JsValue thisObj, JsValue[] arguments)
        {
            var t = LocalTime(EnsureDateInstance(thisObj).PrimitiveValue);
            var m = TypeConverter.ToNumber(arguments.At(0));
            var dt = arguments.Length <= 1 ? DateFromTime(t) : TypeConverter.ToNumber(arguments.At(1));
            var newDate = MakeDate(MakeDay(YearFromTime(t), m, dt), TimeWithinDay(t));
            var u = TimeClip(Utc(newDate));
            thisObj.As<DateInstance>().PrimitiveValue = u;
            return u;
        }

        private JsValue SetUTCMonth(JsValue thisObj, JsValue[] arguments)
        {
            var t = (long) EnsureDateInstance(thisObj).PrimitiveValue;
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
            return (thisObj as DateInstance?? ExceptionHelper.ThrowTypeError<DateInstance>(_engine))
            .ToDateTime().ToUniversalTime().ToString("ddd MMM dd yyyy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture);
        }

        private JsValue ToISOString(JsValue thisObj, JsValue[] arguments)
        {
            var t = (thisObj as DateInstance ?? ExceptionHelper.ThrowTypeError<DateInstance>(_engine)).PrimitiveValue;

            if (double.IsInfinity(t) || double.IsNaN(t))
            {
                ExceptionHelper.ThrowRangeError(_engine);
            }
            var h = HourFromTime(t);
            var m = MinFromTime(t);
            var s = SecFromTime(t);
            var ms = MsFromTime(t);
            if (h < 0) { h += HoursPerDay; }
            if (m < 0) { m += MinutesPerHour; }
            if (s < 0) { s += SecondsPerMinute; }
            if (ms < 0) { ms += MsPerSecond; }
            return $"{YearFromTime(t):0000}-{MonthFromTime(t) + 1:00}-{DateFromTime(t):00}T{h:00}:{m:00}:{s:00}.{ms:000}Z";
        }

        private JsValue ToJSON(JsValue thisObj, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var tv = TypeConverter.ToPrimitive(o, Types.Number);
            if (tv.IsNumber() && double.IsInfinity(((JsNumber) tv)._value))
            {
                return Null;
            }

            var toIso = o.Get("toISOString");
            if (!toIso.Is<ICallable>())
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return toIso.TryCast<ICallable>().Call(o, Arguments.Empty);
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
        public static long TimeWithinDay(long t)
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
            if (!(y%4).Equals(0))
            {
                return 365;
            }

            if ((y%4).Equals(0) && !(y%100).Equals(0))
            {
                return 366;
            }

            if ((y%100).Equals(0) && !(y%400).Equals(0))
            {
                return 365;
            }

            if ((y%400).Equals(0))
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
            var sign = (t < 0) ? -1 : 1;
            var year = (sign < 0) ? 1969 : 1970;
            for (var timeToTimeZero = (long) t; ;)
            {
                //  Subtract the current year's time from the time that's left.
                var timeInYear = DaysInYear(year) * MsPerDay;
                timeToTimeZero -= sign * timeInYear;

                //  If there's less than the current year's worth of time left, then break.
                if (sign < 0)
                {
                    if (sign * timeToTimeZero <= 0)
                    {
                        break;
                    }
                    else
                    {
                        year += sign;
                    }
                }
                else
                {
                    if (sign * timeToTimeZero < 0)
                    {
                        break;
                    }
                    else
                    {
                        year += sign;
                    }
                }
            }

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

            return ExceptionHelper.ThrowArgumentException<int>();
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

        public long LocalTza => (long) Engine.Options._LocalTimeZone.BaseUtcOffset.TotalMilliseconds;

        public long DaylightSavingTa(double t)
        {
            var timeInYear = t - TimeFromYear(YearFromTime(t));

            if (double.IsInfinity(timeInYear) || double.IsNaN(timeInYear))
            {
                return 0;
            }

            var year = YearFromTime(t);
            if (year < 9999 && year > -9999)
            {
                // in DateTimeOffset range so we can use it
            }
            else
            {
                // use similar leap-ed year
                var isLeapYear = InLeapYear(t) == 1;
                year = isLeapYear ? 2000 : 1999;
            }

            var dateTime = new DateTime((int)year, 1, 1).AddMilliseconds(timeInYear);

            return Engine.Options._LocalTimeZone.IsDaylightSavingTime(dateTime) ? MsPerHour : 0;
        }

        public DateTimeOffset ToLocalTime(DateTime t)
        {
            switch (t.Kind)
            {
                case DateTimeKind.Local:
                    return new DateTimeOffset(TimeZoneInfo.ConvertTime(t.ToUniversalTime(), Engine.Options._LocalTimeZone), Engine.Options._LocalTimeZone.GetUtcOffset(t));
                case DateTimeKind.Utc:
                    return new DateTimeOffset(TimeZoneInfo.ConvertTime(t, Engine.Options._LocalTimeZone), Engine.Options._LocalTimeZone.GetUtcOffset(t));
                default:
                    return t;
            }
        }

        public long LocalTime(double t)
        {
            return (long) (t + LocalTza + DaylightSavingTa(t));
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

        public static int DayFromMonth(int year, int month)
        {
            int day = month * 30;

            if (month >= 7)
            {
                day += month/2 - 1;
            }
            else if (month >= 2)
            {
                day += (month - 1)/2 - 1;
            }
            else
            {
                day += month;
            }

            if (month >= 2 && InLeapYear(year) == 1)
            {
                day++;
            }

            return day;
        }


        public static int DaysInMonth(int month, int leap)
        {
            month = month%12;

            switch ((long) month)
            {
                case 0:
                case 2:
                case 4:
                case 6:
                case 7:
                case 9:
                case 11:
                    return 31;
                case 3:
                case 5:
                case 8:
                case 10:
                    return 30;
                case 1:
                    return 28 + leap;
                default:
                    return ExceptionHelper.ThrowArgumentOutOfRangeException<int>(nameof(month), "invalid month");
            }
        }

        public static double MakeTime(double hour, double min, double sec, double ms)
        {
            if (!AreFinite(hour, min, sec, ms))
            {
                return double.NaN;
            }

            var h = (long) hour;
            var m = (long) min;
            var s = (long) sec;
            var milli = (long) ms;
            var t = h*MsPerHour + m*MsPerMinute + s*MsPerSecond + milli;

            return t;
        }

        public static double MakeDay(double year, double month, double date)
        {
            if (!AreFinite(year, month, date))
            {
                return double.NaN;
            }

            year = TypeConverter.ToInteger(year);
            month = TypeConverter.ToInteger(month);
            date = TypeConverter.ToInteger(date);

            if (month < 0)
            {
                var m = (long) month;
                year += (m - 11) / 12;
                month = (12 + m % 12) % 12;
            }

            var sign = (year < 1970) ? -1 : 1;
            long t = (year < 1970) ? 1 : 0;
            int y;

            if (sign == -1)
            {
                for (y = 1969; y >= year; y += sign)
                {
                    t += sign * DaysInYear(y) * MsPerDay;
                }
            }
            else
            {
                for (y = 1970; y < year; y += sign)
                {
                    t += sign * DaysInYear(y) * MsPerDay;
                }
            }

            for (var m = 0; m < month; m++)
            {
                t += DaysInMonth(m, InLeapYear(t)) * MsPerDay;
            }

            return Day(t) + date - 1;
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
            if (!AreFinite(time))
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
        private static bool AreFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool AreFinite(double value1, double value2)
        {
            return AreFinite(value1) && AreFinite(value2);
        }

        private static bool AreFinite(double value1, double value2, double value3)
        {
            return AreFinite(value1) && AreFinite(value2) &&  AreFinite(value3);
        }

        private static bool AreFinite(double value1, double value2, double value3, double value4)
        {
            return AreFinite(value1) && AreFinite(value2) &&  AreFinite(value3) && AreFinite(value4);
        }
    }
}
