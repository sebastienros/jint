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


            FastAddProperty("NaN", double.NaN, false, false, false);
            FastAddProperty("MAX_VALUE", double.MaxValue, false, false, false);
            FastAddProperty("MIN_VALUE", double.MinValue, false, false, false);
            FastAddProperty("POSITIVE_INFINITY", double.PositiveInfinity, false, false, false);
            FastAddProperty("NEGATIVE_INFINITY", double.NegativeInfinity, false, false, false);

            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToNumberString), false, false, false);
        }

        private static object ToNumberString(object thisObject, object[] arguments)
        {
            return TypeConverter.ToString(thisObject);
        }

    }
}
