using System;
using System.Globalization;
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
            FastAddProperty("toString", new ClrFunctionInstance<DateInstance, string>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toDateString", new ClrFunctionInstance<DateInstance, string>(Engine, ToDateString, 0), true, false, true);
            FastAddProperty("toTimeString", new ClrFunctionInstance<DateInstance, string>(Engine, ToTimeString, 0), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance<DateInstance, string>(Engine, ToLocaleString, 0), true, false, true);
            FastAddProperty("toLocaleDateString", new ClrFunctionInstance<DateInstance, string>(Engine, ToLocaleDateString, 0), true, false, true);
            FastAddProperty("toLocaleTimeString", new ClrFunctionInstance<DateInstance, string>(Engine, ToLocaleTimeString, 0), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance<DateInstance, double>(Engine, ValueOf, 0), true, false, true);
            FastAddProperty("getTime", new ClrFunctionInstance<DateInstance, double>(Engine, GetTime, 0), true, false, true);
            FastAddProperty("getFullYear", new ClrFunctionInstance<DateInstance, double>(Engine, GetFullYear, 0), true, false, true);
            FastAddProperty("getUTCFullYear", new ClrFunctionInstance<DateInstance, double>(Engine, GetUTCFullYear, 0), true, false, true);
            FastAddProperty("getMonth", new ClrFunctionInstance<DateInstance, double>(Engine, GetMonth, 0), true, false, true);
            FastAddProperty("getUTCMonth", new ClrFunctionInstance<DateInstance, double>(Engine, GetUTCMonth, 0), true, false, true);
            FastAddProperty("getDate", new ClrFunctionInstance<DateInstance, double>(Engine, GetDate, 0), true, false, true);
            FastAddProperty("getUTCDate", new ClrFunctionInstance<DateInstance, double>(Engine, GetUTCDate, 0), true, false, true);
            FastAddProperty("getDay", new ClrFunctionInstance<DateInstance, double>(Engine, GetDay, 0), true, false, true);
            FastAddProperty("getUTCDay", new ClrFunctionInstance<DateInstance, double>(Engine, GetUTCDay, 0), true, false, true);
            FastAddProperty("getHours", new ClrFunctionInstance<DateInstance, double>(Engine, GetHours, 0), true, false, true);
            FastAddProperty("getUTCHours", new ClrFunctionInstance<DateInstance, double>(Engine, GetUTCHours, 0), true, false, true);
            FastAddProperty("getMinutes", new ClrFunctionInstance<DateInstance, double>(Engine, GetMinutes, 0), true, false, true);
            FastAddProperty("getUTCMinutes", new ClrFunctionInstance<DateInstance, double>(Engine, GetUTCMinutes, 0), true, false, true);
            FastAddProperty("getSeconds", new ClrFunctionInstance<DateInstance, double>(Engine, GetSeconds, 0), true, false, true);
            FastAddProperty("getUTCSeconds", new ClrFunctionInstance<DateInstance, double>(Engine, GetUTCSeconds, 0), true, false, true);
            FastAddProperty("getMilliseconds", new ClrFunctionInstance<DateInstance, double>(Engine, GetMilliseconds, 0), true, false, true);
            FastAddProperty("getUTCMilliseconds", new ClrFunctionInstance<DateInstance, double>(Engine, GetUTCMilliseconds, 0), true, false, true);
            FastAddProperty("getTimezoneOffset", new ClrFunctionInstance<DateInstance, double>(Engine, GetTimezoneOffset, 0), true, false, true);
            FastAddProperty("setTime", new ClrFunctionInstance<DateInstance, double>(Engine, SetTime, 1), true, false, true);
            FastAddProperty("setMilliseconds", new ClrFunctionInstance<DateInstance, double>(Engine, SetMilliseconds, 1), true, false, true);
            FastAddProperty("setUTCMilliseconds", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCMilliseconds, 1), true, false, true);
            FastAddProperty("setSeconds", new ClrFunctionInstance<DateInstance, double>(Engine, SetSeconds, 2), true, false, true);
            FastAddProperty("setUTCSeconds", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCSeconds, 2), true, false, true);
            FastAddProperty("setMinutes", new ClrFunctionInstance<DateInstance, double>(Engine, SetMinutes, 3), true, false, true);
            FastAddProperty("setUTCMinutes", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCMinutes, 3), true, false, true);
            FastAddProperty("setHours", new ClrFunctionInstance<DateInstance, double>(Engine, SetHours, 4), true, false, true);
            FastAddProperty("setUTCHours", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCHours, 4), true, false, true);
            FastAddProperty("setDate", new ClrFunctionInstance<DateInstance, double>(Engine, SetDate, 1), true, false, true);
            FastAddProperty("setUTCDate", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCDate, 1), true, false, true);
            FastAddProperty("setMonth", new ClrFunctionInstance<DateInstance, double>(Engine, SetMonth, 2), true, false, true);
            FastAddProperty("setUTCMonth", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCMonth, 2), true, false, true);
            FastAddProperty("setFullYear", new ClrFunctionInstance<DateInstance, double>(Engine, SetFullYear, 3), true, false, true);
            FastAddProperty("setUTCFullYear", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCFullYear, 3), true, false, true);
            FastAddProperty("toUTCString", new ClrFunctionInstance<DateInstance, string>(Engine, ToUTCString, 0), true, false, true);
            FastAddProperty("toISOString", new ClrFunctionInstance<DateInstance, string>(Engine, ToISOString, 0), true, false, true);
            FastAddProperty("toJSON", new ClrFunctionInstance<DateInstance, string>(Engine, ToJSON, 1), true, false, true);
        }

        private double ValueOf(DateInstance thisObj, object[] arguments)
        {
            return thisObj.PrimitiveValue;
        }

        public string ToString(DateInstance thisObj, object[] arg2)
        {
            return thisObj.ToDateTime().ToString("F", CultureInfo.InvariantCulture);
        }

        private string ToDateString(DateInstance thisObj, object[] arguments)
        {
            return thisObj.ToDateTime().ToString("D", CultureInfo.InvariantCulture);
        }

        private string ToTimeString(DateInstance thisObj, object[] arguments)
        {
            return thisObj.ToDateTime().ToString("T", CultureInfo.InvariantCulture);
        }

        private string ToLocaleString(DateInstance thisObj, object[] arguments)
        {
            return thisObj.ToDateTime().ToString("F");
        }

        private string ToLocaleDateString(DateInstance thisObj, object[] arguments)
        {
            return thisObj.ToDateTime().ToString("D");
        }

        private string ToLocaleTimeString(DateInstance thisObj, object[] arguments)
        {
            return thisObj.ToDateTime().ToString("T");
        }

        private double GetTime(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.PrimitiveValue;
        }

        private double GetFullYear(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToLocalTime().Year;
        }

        private double GetUTCFullYear(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToUniversalTime().Year;
        }

        private double GetMonth(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToLocalTime().Month - 1;
        }

        private double GetUTCMonth(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToUniversalTime().Month;
        }

        private double GetDate(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToLocalTime().Day;
        }

        private double GetUTCDate(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToUniversalTime().Day;
        }

        private double GetDay(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return (int)thisObj.ToDateTime().ToLocalTime().DayOfWeek;
        }

        private double GetUTCDay(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return (int)thisObj.ToDateTime().ToUniversalTime().DayOfWeek;
        }

        private double GetHours(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToLocalTime().Hour;
        }

        private double GetUTCHours(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToUniversalTime().Hour;
        }

        private double GetMinutes(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToLocalTime().Minute;
        }

        private double GetUTCMinutes(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToUniversalTime().Minute;
        }

        private double GetSeconds(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToLocalTime().Second;
        }

        private double GetUTCSeconds(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToUniversalTime().Second;
        }

        private double GetMilliseconds(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToLocalTime().Millisecond;
        }

        private double GetUTCMilliseconds(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return thisObj.ToDateTime().ToUniversalTime().Second;
        }

        private double GetTimezoneOffset(DateInstance thisObj, object[] arguments)
        {
            if (double.IsNaN(thisObj.PrimitiveValue))
            {
                return double.NaN;
            }

            return - TimeZoneInfo.Local.GetUtcOffset(thisObj.ToDateTime()).Hours*60;
        }

        private double SetTime(DateInstance thisObj, object[] arguments)
        {
            return thisObj.PrimitiveValue = DateConstructor.TimeClip(TypeConverter.ToNumber(arguments.At(0)));
        }

        private double SetMilliseconds(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour,dt.Minute,dt.Second,(int)TypeConverter.ToNumber(arguments.At(0)), DateTimeKind.Local);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetUTCMilliseconds(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToUniversalTime();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, (int)TypeConverter.ToNumber(arguments.At(0)), DateTimeKind.Utc);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetSeconds(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToLocalTime();
            var ms = arguments.At(1) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(1));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, (int)TypeConverter.ToNumber(arguments.At(0)), (int) ms, DateTimeKind.Local);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetUTCSeconds(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToUniversalTime();
            var ms = arguments.At(1) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(1));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, (int)TypeConverter.ToNumber(arguments.At(0)), (int) ms, DateTimeKind.Utc);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetMinutes(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToLocalTime();
            var s = arguments.At(1) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(1));
            var ms = arguments.At(2) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(2));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (int)TypeConverter.ToNumber(arguments.At(0)), (int) s, (int) ms, DateTimeKind.Local);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetUTCMinutes(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToUniversalTime();
            var s = arguments.At(1) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(1));
            var ms = arguments.At(2) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(2));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (int)TypeConverter.ToNumber(arguments.At(0)), (int)s, (int)ms, DateTimeKind.Utc);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetHours(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToLocalTime();
            var min = arguments.At(1) == Undefined.Instance ? dt.Minute : TypeConverter.ToNumber(arguments.At(1));
            var s = arguments.At(2) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(2));
            var ms = arguments.At(3) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(3));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, (int)TypeConverter.ToNumber(arguments.At(0)), (int) min, (int)s, (int)ms, DateTimeKind.Local);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetUTCHours(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToUniversalTime();
            var min = arguments.At(1) == Undefined.Instance ? dt.Minute : TypeConverter.ToNumber(arguments.At(1));
            var s = arguments.At(2) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(2));
            var ms = arguments.At(3) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(3));
            dt = new DateTime(dt.Year, dt.Month, dt.Day, (int)TypeConverter.ToNumber(arguments.At(0)), (int)min, (int)s, (int)ms, DateTimeKind.Utc);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetDate(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToLocalTime();
            dt = new DateTime(dt.Year, dt.Month, (int)TypeConverter.ToNumber(arguments.At(0)), dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetUTCDate(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToUniversalTime();
            dt = new DateTime(dt.Year, dt.Month, (int)TypeConverter.ToNumber(arguments.At(0)), dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetMonth(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToUniversalTime();
            var date = arguments.At(1) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(1));
            dt = new DateTime(dt.Year, (int)TypeConverter.ToNumber(arguments.At(0)), (int) date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetUTCMonth(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToUniversalTime();
            var date = arguments.At(1) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(1));
            dt = new DateTime(dt.Year, (int)TypeConverter.ToNumber(arguments.At(0)), (int) date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetFullYear(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToLocalTime();
            var month = arguments.At(1) == Undefined.Instance ? dt.Month : TypeConverter.ToNumber(arguments.At(1));
            var date = arguments.At(2) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(2));
            dt = new DateTime((int)TypeConverter.ToNumber(arguments.At(0)), (int)month, (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private double SetUTCFullYear(DateInstance thisObj, object[] arguments)
        {
            var dt = thisObj.ToDateTime().ToUniversalTime();
            var month = arguments.At(1) == Undefined.Instance ? dt.Month : TypeConverter.ToNumber(arguments.At(1));
            var date = arguments.At(2) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(2));
            dt = new DateTime((int)TypeConverter.ToNumber(arguments.At(0)), (int)month,(int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private string ToUTCString(DateInstance thisObj, object[] arguments)
        {
            return thisObj.ToDateTime().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        private string ToISOString(DateInstance thisObj, object[] arguments)
        {
            return thisObj.ToDateTime().ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        private string ToJSON(DateInstance thisObj, object[] arguments)
        {
            return thisObj.ToDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
    }
}
