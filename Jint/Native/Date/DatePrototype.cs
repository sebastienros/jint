using System;
using System.Globalization;
using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Date
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.9.5
    /// </summary>
    public sealed class DatePrototype : DateInstance
    {
        private DatePrototype(Engine engine)
            : base(engine)
        {
        }

        public static DatePrototype CreatePrototypeObject(Engine engine, DateConstructor dateConstructor)
        {
            var obj = new DatePrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.Extensible = true;

            obj.FastAddProperty("constructor", dateConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToString, 0), true, false, true);
            FastAddProperty("toDateString", new ClrFunctionInstance(Engine, ToDateString, 0), true, false, true);
            FastAddProperty("toTimeString", new ClrFunctionInstance(Engine, ToTimeString, 0), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance(Engine, ToLocaleString, 0), true, false, true);
            FastAddProperty("toLocaleDateString", new ClrFunctionInstance(Engine, ToLocaleDateString, 0), true, false, true);
            FastAddProperty("toLocaleTimeString", new ClrFunctionInstance(Engine, ToLocaleTimeString, 0), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, ValueOf, 0), true, false, true);
            FastAddProperty("getTime", new ClrFunctionInstance(Engine, GetTime, 0), true, false, true);
            FastAddProperty("getFullYear", new ClrFunctionInstance(Engine, GetFullYear, 0), true, false, true);
            FastAddProperty("getUTCFullYear", new ClrFunctionInstance(Engine, GetUTCFullYear, 0), true, false, true);
            FastAddProperty("getMonth", new ClrFunctionInstance(Engine, GetMonth, 0), true, false, true);
            FastAddProperty("getUTCMonth", new ClrFunctionInstance(Engine, GetUTCMonth, 0), true, false, true);
            FastAddProperty("getDate", new ClrFunctionInstance(Engine, GetDate, 0), true, false, true);
            FastAddProperty("getUTCDate", new ClrFunctionInstance(Engine, GetUTCDate, 0), true, false, true);
            FastAddProperty("getDay", new ClrFunctionInstance(Engine, GetDay, 0), true, false, true);
            FastAddProperty("getUTCDay", new ClrFunctionInstance(Engine, GetUTCDay, 0), true, false, true);
            FastAddProperty("getHours", new ClrFunctionInstance(Engine, GetHours, 0), true, false, true);
            FastAddProperty("getUTCHours", new ClrFunctionInstance(Engine, GetUTCHours, 0), true, false, true);
            FastAddProperty("getMinutes", new ClrFunctionInstance(Engine, GetMinutes, 0), true, false, true);
            FastAddProperty("getUTCMinutes", new ClrFunctionInstance(Engine, GetUTCMinutes, 0), true, false, true);
            FastAddProperty("getSeconds", new ClrFunctionInstance(Engine, GetSeconds, 0), true, false, true);
            FastAddProperty("getUTCSeconds", new ClrFunctionInstance(Engine, GetUTCSeconds, 0), true, false, true);
            FastAddProperty("getMilliseconds", new ClrFunctionInstance(Engine, GetMilliseconds, 0), true, false, true);
            FastAddProperty("getUTCMilliseconds", new ClrFunctionInstance(Engine, GetUTCMilliseconds, 0), true, false, true);
            FastAddProperty("getTimezoneOffset", new ClrFunctionInstance(Engine, GetTimezoneOffset, 0), true, false, true);
            FastAddProperty("setTime", new ClrFunctionInstance(Engine, SetTime, 1), true, false, true);
            FastAddProperty("setMilliseconds", new ClrFunctionInstance(Engine, SetMilliseconds, 1), true, false, true);
            FastAddProperty("setUTCMilliseconds", new ClrFunctionInstance(Engine, SetUTCMilliseconds, 1), true, false, true);
            FastAddProperty("setSeconds", new ClrFunctionInstance(Engine, SetSeconds, 2), true, false, true);
            FastAddProperty("setUTCSeconds", new ClrFunctionInstance(Engine, SetUTCSeconds, 2), true, false, true);
            FastAddProperty("setMinutes", new ClrFunctionInstance(Engine, SetMinutes, 3), true, false, true);
            FastAddProperty("setUTCMinutes", new ClrFunctionInstance(Engine, SetUTCMinutes, 3), true, false, true);
            FastAddProperty("setHours", new ClrFunctionInstance(Engine, SetHours, 4), true, false, true);
            FastAddProperty("setUTCHours", new ClrFunctionInstance(Engine, SetUTCHours, 4), true, false, true);
            FastAddProperty("setDate", new ClrFunctionInstance(Engine, SetDate, 1), true, false, true);
            FastAddProperty("setUTCDate", new ClrFunctionInstance(Engine, SetUTCDate, 1), true, false, true);
            FastAddProperty("setMonth", new ClrFunctionInstance(Engine, SetMonth, 2), true, false, true);
            FastAddProperty("setUTCMonth", new ClrFunctionInstance(Engine, SetUTCMonth, 2), true, false, true);
            FastAddProperty("setFullYear", new ClrFunctionInstance(Engine, SetFullYear, 3), true, false, true);
            FastAddProperty("setUTCFullYear", new ClrFunctionInstance(Engine, SetUTCFullYear, 3), true, false, true);
            FastAddProperty("toUTCString", new ClrFunctionInstance(Engine, ToUTCString, 0), true, false, true);
            FastAddProperty("toISOString", new ClrFunctionInstance(Engine, ToISOString, 0), true, false, true);
            FastAddProperty("toJSON", new ClrFunctionInstance(Engine, ToJSON, 1), true, false, true);
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().PrimitiveValue;
        }

        public JsValue ToString(JsValue thisObj, JsValue[] arg2)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToString("F", CultureInfo.InvariantCulture);
        }

        private JsValue ToDateString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToString("D", CultureInfo.InvariantCulture);
        }

        private JsValue ToTimeString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToString("T", CultureInfo.InvariantCulture);
        }

        private JsValue ToLocaleString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToString("F");
        }

        private JsValue ToLocaleDateString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToString("D");
        }

        private JsValue ToLocaleTimeString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToString("T");
        }

        private JsValue GetTime(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().PrimitiveValue;
        }

        private JsValue GetFullYear(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().Year;
        }

        private JsValue GetUTCFullYear(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().Year;
        }

        private JsValue GetMonth(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().Month - 1;
        }

        private JsValue GetUTCMonth(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().Month;
        }

        private JsValue GetDate(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().Day;
        }

        private JsValue GetUTCDate(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().Day;
        }

        private JsValue GetDay(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return (int)thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().DayOfWeek;
        }

        private JsValue GetUTCDay(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return (int)thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().DayOfWeek;
        }

        private JsValue GetHours(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().Hour;
        }

        private JsValue GetUTCHours(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().Hour;
        }

        private JsValue GetMinutes(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().Minute;
        }

        private JsValue GetUTCMinutes(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().Minute;
        }

        private JsValue GetSeconds(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().Second;
        }

        private JsValue GetUTCSeconds(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().Second;
        }

        private JsValue GetMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().Millisecond;
        }

        private JsValue GetUTCMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().Second;
        }

        private JsValue GetTimezoneOffset(JsValue thisObj, JsValue[] arguments)
        {
            if (double.IsNaN(thisObj.TryCast<DateInstance>().PrimitiveValue))
            {
                return double.NaN;
            }

            return - TimeZoneInfo.Local.GetUtcOffset(thisObj.TryCast<DateInstance>().ToDateTime()).Hours*60;
        }

        private JsValue SetTime(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.TimeClip(TypeConverter.ToNumber(arguments.At(0)));
        }

        private JsValue SetMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, (int)TypeConverter.ToNumber(arguments.At(0)), DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, (int)TypeConverter.ToNumber(arguments.At(0)), DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            var ms = arguments.At(1) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(1));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, (int)TypeConverter.ToNumber(arguments.At(0)), (int)ms, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var ms = arguments.At(1) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(1));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, (int)TypeConverter.ToNumber(arguments.At(0)), (int)ms, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            var s = arguments.At(1) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(1));
            var ms = arguments.At(2) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(2));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (int)TypeConverter.ToNumber(arguments.At(0)), (int)s, (int)ms, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var s = arguments.At(1) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(1));
            var ms = arguments.At(2) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(2));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (int)TypeConverter.ToNumber(arguments.At(0)), (int)s, (int)ms, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetHours(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            var min = arguments.At(1) == Undefined.Instance ? dt.Minute : TypeConverter.ToNumber(arguments.At(1));
            var s = arguments.At(2) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(2));
            var ms = arguments.At(3) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(3));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, (int)TypeConverter.ToNumber(arguments.At(0)), (int)min, (int)s, (int)ms, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCHours(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var min = arguments.At(1) == Undefined.Instance ? dt.Minute : TypeConverter.ToNumber(arguments.At(1));
            var s = arguments.At(2) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(2));
            var ms = arguments.At(3) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(3));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, (int)TypeConverter.ToNumber(arguments.At(0)), (int)min, (int)s, (int)ms, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetDate(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            dt = new DateTime(dt.Year, dt.Month, (int)TypeConverter.ToNumber(arguments.At(0)), dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCDate(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            dt = new DateTime(dt.Year, dt.Month, (int)TypeConverter.ToNumber(arguments.At(0)), dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetMonth(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var date = arguments.At(1) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(1));
            dt = new DateTime(dt.Year, (int)TypeConverter.ToNumber(arguments.At(0)), (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCMonth(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var date = arguments.At(1) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(1));
            dt = new DateTime(dt.Year, (int)TypeConverter.ToNumber(arguments.At(0)), (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            var month = arguments.At(1) == Undefined.Instance ? dt.Month : TypeConverter.ToNumber(arguments.At(1));
            var date = arguments.At(2) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(2));
            dt = new DateTime((int)TypeConverter.ToNumber(arguments.At(0)), (int)month, (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var month = arguments.At(1) == Undefined.Instance ? dt.Month : TypeConverter.ToNumber(arguments.At(1));
            var date = arguments.At(2) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(2));
            dt = new DateTime((int)TypeConverter.ToNumber(arguments.At(0)), (int)month, (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue ToUTCString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>(x =>
            {
                throw new JavaScriptException(Engine.TypeError);
            } )
            .ToDateTime().ToUniversalTime().ToString("r");
        }

        private JsValue ToISOString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>(x =>
            {
                throw new JavaScriptException(Engine.TypeError);
            })
           .ToDateTime().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        private JsValue ToJSON(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>(x =>
            {
                throw new JavaScriptException(Engine.TypeError);
            })
           .ToDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        public static double MsPerDay = 86400000;
        public static double MsPerHour = 3600000;

        /// <summary>
        /// 15.9.1.2
        /// </summary>
        public static double Day(double t)
        {
            return System.Math.Floor(t / MsPerDay);
        }

        /// <summary>
        /// 15.9.1.2
        /// </summary>
        public static double TimeWithinDay(double t)
        {
            return t % MsPerDay;
        }

        /// <summary>
        /// The number of days in a year
        /// </summary>
        public static double DaysInYear(double y)
        {
            if (!(y%4).Equals(0))
            {
                return 365;
            }

            if ((y%4).Equals(0) && !(y%100).Equals(100))
            {
                return 365;
            }

            if ((y%100).Equals(0) && !(y%400).Equals(100))
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
        public static double DayFromYear(double y)
        {
            return 365*(y - 1970) + System.Math.Floor((y - 1969)/4) - System.Math.Floor((y - 1901)/100) +
                   System.Math.Floor((y - 1601)/400);
        }

        /// <summary>
        /// The time value of the start of the year
        /// </summary>
        public static double TimeFromYear(double y)
        {
            return MsPerDay*DayFromYear(y);
        }

        /// <summary>
        /// The year of a time value.
        /// </summary>
        public static double YearFromTime(double t)
        {
            double upper = double.PositiveInfinity;
            double lower = double.NegativeInfinity;
            while (upper > lower)
            {
                var current = System.Math.Floor(upper + lower / 2);

                if (TimeFromYear(current) <= t)
                {
                    lower = current;
                }
                else
                {
                    upper = current;
                }
            }

            return lower;
        }

        /// <summary>
        /// <value>true</value> if the time is within a leap year, <value>false</value> otherwise
        /// </summary>
        public static double InLeapYear(double t)
        {
            var daysInYear = DaysInYear(YearFromTime(t));

            if (daysInYear.Equals(365))
            {
                return 0;
            }            

            if (daysInYear.Equals(366))
            {
                return 1;
            }            

            throw new ArgumentException();
        }

        /// <summary>
        /// The month number of a time value.
        /// </summary>
        public static double MonthFromTime(double t)
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

            throw new InvalidOperationException();
        }

        public static double DayWithinYear(double t)
        {
            return Day(t) - DayFromYear(YearFromTime(t));
        }

        public static double DateFromTime(double t)
        {
            var monthFromTime = MonthFromTime(t);
            var dayWithinYear = DayWithinYear(t);

            if (monthFromTime.Equals(0))
            {
                return dayWithinYear + 1;
            }

            if (monthFromTime.Equals(1))
            {
                return dayWithinYear - 30;
            }

            if (monthFromTime.Equals(2))
            {
                return dayWithinYear - 58;
            }

            if (monthFromTime.Equals(3))
            {
                return dayWithinYear - 89;
            }

            if (monthFromTime.Equals(4))
            {
                return dayWithinYear - 119;
            }

            if (monthFromTime.Equals(5))
            {
                return dayWithinYear - 150;
            }

            if (monthFromTime.Equals(6))
            {
                return dayWithinYear - 180;
            }

            if (monthFromTime.Equals(7))
            {
                return dayWithinYear - 211;
            }

            if (monthFromTime.Equals(8))
            {
                return dayWithinYear - 242;
            }

            if (monthFromTime.Equals(9))
            {
                return dayWithinYear - 272;
            }

            if (monthFromTime.Equals(10))
            {
                return dayWithinYear - 303;
            }

            if (monthFromTime.Equals(11))
            {
                return dayWithinYear - 333;
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// The weekday for a particular time value.
        /// </summary>
        public static double WeekDay(double t)
        {
            return (Day(t) + 4)%7;
        }

        public static double LocalTza
        {
            get
            {
                return TimeZoneInfo.Local.BaseUtcOffset.TotalMilliseconds;
            }
        }

        public static double DaylightSavingTa(double t)
        {
            var timeInYear = t - TimeFromYear(YearFromTime(t));
            var isLeapYear = InLeapYear(t).Equals(1);
            var weekDay = WeekDay(TimeFromYear(YearFromTime(t)));
            
            var year = YearFromTime(t);
            if (year < 9999 && year > -9999)
            {
                // in DateTimeOffset range so we can use it
            }
            else
            {
                // use similar leap-ed year
                year = isLeapYear ? 2000 : 1999;
            }

            var dateTime = new DateTime((int)year, 1, 1).AddMilliseconds(timeInYear);

            return TimeZoneInfo.Local.IsDaylightSavingTime(dateTime) ? MsPerHour : 0;
        }

        public static double UtcToLocalTime(double t)
        {
            return t + LocalTza + DaylightSavingTa(t);
        }

        public static double LocalTimeToUtc(double t)
        {
            return t - LocalTza - DaylightSavingTa(t - LocalTza);
        }
    }
}
