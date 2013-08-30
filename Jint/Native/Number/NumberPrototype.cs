using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Number
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.4
    /// </summary>
    public sealed class NumberPrototype : NumberInstance
    {
        private NumberPrototype(Engine engine)
            : base(engine)
        {
        }

        public static NumberPrototype CreatePrototypeObject(Engine engine, NumberConstructor numberConstructor)
        {
            var obj = new NumberPrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.PrimitiveValue = 0;

            obj.FastAddProperty("constructor", numberConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToNumberString), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance<object, object>(Engine, ToLocaleString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance<object, object>(Engine, ValueOf), true, false, true);
            FastAddProperty("toFixed", new ClrFunctionInstance<object, object>(Engine, ToFixed), true, false, true);
            FastAddProperty("toExponential", new ClrFunctionInstance<object, object>(Engine, ToExponential), true, false, true);
            FastAddProperty("toPrecision", new ClrFunctionInstance<object, object>(Engine, ToPrecision), true, false, true);
        }

        private object ToLocaleString(object thisObj, object[] arguments)
        {
            throw new System.NotImplementedException();
        }

        private object ValueOf(object thisObj, object[] arguments)
        {
            var number = thisObj as NumberInstance;
            if (number == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return number.PrimitiveValue;
        }

        private object ToFixed(object thisObj, object[] arguments)
        {
            throw new System.NotImplementedException();
        }

        private object ToExponential(object thisObj, object[] arguments)
        {
            throw new System.NotImplementedException();
        }

        private object ToPrecision(object thisObj, object[] arguments)
        {
            throw new System.NotImplementedException();
        }

        private static object ToNumberString(object thisObject, object[] arguments)
        {
            return TypeConverter.ToString(thisObject);
        }

    }
}
