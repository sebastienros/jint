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
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.TimeClip(TypeConverter.ToNumber(arguments.At(0)).AsNumber());
        }

        private JsValue SetMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCMilliseconds(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            var ms = arguments.At(1) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)ms, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCSeconds(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var ms = arguments.At(1) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)ms, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            var s = arguments.At(1) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            var ms = arguments.At(2) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(2)).AsNumber();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)s, (int)ms, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCMinutes(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var s = arguments.At(1) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            var ms = arguments.At(2) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(2)).AsNumber();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)s, (int)ms, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetHours(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            var min = arguments.At(1) == Undefined.Instance ? dt.Minute : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            var s = arguments.At(2) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(2)).AsNumber();
            var ms = arguments.At(3) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(3)).AsNumber();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)min, (int)s, (int)ms, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCHours(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var min = arguments.At(1) == Undefined.Instance ? dt.Minute : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            var s = arguments.At(2) == Undefined.Instance ? dt.Second : TypeConverter.ToNumber(arguments.At(2)).AsNumber();
            var ms = arguments.At(3) == Undefined.Instance ? dt.Millisecond : TypeConverter.ToNumber(arguments.At(3)).AsNumber();
            dt = new DateTime(dt.Year, dt.Month, dt.Day, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)min, (int)s, (int)ms, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetDate(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            dt = new DateTime(dt.Year, dt.Month, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCDate(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            dt = new DateTime(dt.Year, dt.Month, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetMonth(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var date = arguments.At(1) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            dt = new DateTime(dt.Year, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCMonth(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var date = arguments.At(1) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            dt = new DateTime(dt.Year, (int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime();
            var month = arguments.At(1) == Undefined.Instance ? dt.Month : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            var date = arguments.At(2) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(2)).AsNumber();
            dt = new DateTime((int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)month, (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Local);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue SetUTCFullYear(JsValue thisObj, JsValue[] arguments)
        {
            var dt = thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime();
            var month = arguments.At(1) == Undefined.Instance ? dt.Month : TypeConverter.ToNumber(arguments.At(1)).AsNumber();
            var date = arguments.At(2) == Undefined.Instance ? dt.Day : TypeConverter.ToNumber(arguments.At(2)).AsNumber();
            dt = new DateTime((int)TypeConverter.ToNumber(arguments.At(0)).AsNumber(), (int)month, (int)date, dt.Hour, dt.Minute, dt.Second, dt.Second, DateTimeKind.Utc);
            return thisObj.TryCast<DateInstance>().PrimitiveValue = DateConstructor.FromDateTime(dt);
        }

        private JsValue ToUTCString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        private JsValue ToISOString(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        private JsValue ToJSON(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj.TryCast<DateInstance>().ToDateTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
    }
}
