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

            obj.FastAddProperty("NaN", double.NaN, false, false, false);
            obj.FastAddProperty("MAX_VALUE", double.MaxValue, false, false, false);
            obj.FastAddProperty("MIN_VALUE", double.MinValue, false, false, false);
            obj.FastAddProperty("POSITIVE_INFINITY", double.PositiveInfinity, false, false, false);
            obj.FastAddProperty("NEGATIVE_INFINITY", double.NegativeInfinity, false, false, false);

            return obj;
        }


    }
}
