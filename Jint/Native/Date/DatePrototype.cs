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

            obj.FastAddProperty("constructor", dateConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toDateString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toTimeString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toLocaleDateString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toLocaleTimeString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance<DateInstance, double>(Engine, ValueOf, 0), true, false, true);
            FastAddProperty("getTime", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getFullYear", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getUTCFullYear", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getMonth", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getUTCMonth", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getDate", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getUTCDate", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getDay", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getUTCDay", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getHours", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getUTCHours", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getMinutes", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getUTCMinutes", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getSeconds", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getUTCSeconds", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getMilliseconds", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getUTCMilliseconds", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("getTimezoneOffset", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setTime", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setMilliseconds", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setSeconds", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setUTCSeconds", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setMinutes", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setUTCMinutes", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setHours", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setUTCHours", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setDate", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setUTCDate", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setMonth", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setUTCMonth", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setFullYear", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("setUTCFullYear", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toUTCString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toISOString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toJSON", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
        }

        private double ValueOf(DateInstance thisObj, object[] arguments)
        {
            return thisObj.PrimitiveValue;
        }

        private object ToString(object arg1, object[] arg2)
        {
            throw new System.NotImplementedException();
        }
    }
}
