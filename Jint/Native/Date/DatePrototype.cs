using System;
using System.Globalization;
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

            obj.FastAddProperty("constructor", dateConstructor, false, false, false);

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
            FastAddProperty("setTime", new ClrFunctionInstance<DateInstance, double>(Engine, SetTime, 0), true, false, true);
            FastAddProperty("setMilliseconds", new ClrFunctionInstance<DateInstance, double>(Engine, SetMilliseconds, 0), true, false, true);
            FastAddProperty("setSeconds", new ClrFunctionInstance<DateInstance, double>(Engine, SetSeconds, 0), true, false, true);
            FastAddProperty("setUTCSeconds", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCSeconds, 0), true, false, true);
            FastAddProperty("setMinutes", new ClrFunctionInstance<DateInstance, double>(Engine, SetMinutes, 0), true, false, true);
            FastAddProperty("setUTCMinutes", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCMinutes, 0), true, false, true);
            FastAddProperty("setHours", new ClrFunctionInstance<DateInstance, double>(Engine, SetHours, 0), true, false, true);
            FastAddProperty("setUTCHours", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCHours, 0), true, false, true);
            FastAddProperty("setDate", new ClrFunctionInstance<DateInstance, double>(Engine, SetDate, 0), true, false, true);
            FastAddProperty("setUTCDate", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCDate, 0), true, false, true);
            FastAddProperty("setMonth", new ClrFunctionInstance<DateInstance, double>(Engine, SetMonth, 0), true, false, true);
            FastAddProperty("setUTCMonth", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCMonth, 0), true, false, true);
            FastAddProperty("setFullYear", new ClrFunctionInstance<DateInstance, double>(Engine, SetFullYear, 0), true, false, true);
            FastAddProperty("setUTCFullYear", new ClrFunctionInstance<DateInstance, double>(Engine, SetUTCFullYear, 0), true, false, true);
            FastAddProperty("toUTCString", new ClrFunctionInstance<DateInstance, string>(Engine, ToUTCString, 0), true, false, true);
            FastAddProperty("toISOString", new ClrFunctionInstance<DateInstance, string>(Engine, ToISOString, 0), true, false, true);
            FastAddProperty("toJSON", new ClrFunctionInstance<DateInstance, string>(Engine, ToJSON, 0), true, false, true);
        }

        private double ValueOf(DateInstance thisObj, object[] arguments)
        {
            return thisObj.PrimitiveValue;
        }

        private string ToString(DateInstance thisObj, object[] arg2)
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

            return thisObj.ToDateTime().Year;
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

            return thisObj.ToDateTime().Month - 1;
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

            return thisObj.ToDateTime().Day;
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

            return (int)thisObj.ToDateTime().DayOfWeek;
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

            return thisObj.ToDateTime().Hour;
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

            return thisObj.ToDateTime().Minute;
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

            return thisObj.ToDateTime().Second;
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

            return thisObj.ToDateTime().Millisecond;
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

            return TimeZoneInfo.Local.GetUtcOffset(thisObj.ToDateTime()).Hours;
        }

        private double SetTime(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetMilliseconds(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetSeconds(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetUTCSeconds(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetMinutes(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetUTCMinutes(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetHours(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetUTCHours(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetDate(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetUTCDate(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetMonth(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetUTCMonth(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetFullYear(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private double SetUTCFullYear(DateInstance thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private string ToUTCString(DateInstance thisObj, object[] arguments) { throw new NotImplementedException(); }
        private string ToISOString(DateInstance thisObj, object[] arguments) { throw new NotImplementedException(); }
        private string ToJSON(DateInstance thisObj, object[] arguments) { throw new NotImplementedException(); }
    }
}
