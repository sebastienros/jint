using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Number
{
    public sealed class NumberConstructor : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;

        public NumberConstructor(Engine engine)
            : base(engine, new ObjectInstance(engine.Object), null, null, false)
        {
            _engine = engine;

            // the constructor is the function constructor of an object
            this.Prototype.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = false }, false);
            this.Prototype.DefineOwnProperty("prototype", new DataDescriptor(Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);

            // Number prototype properties
            this.Prototype.DefineOwnProperty("NaN", new DataDescriptor(double.NaN), false);
            this.Prototype.DefineOwnProperty("MAX_VALUE", new DataDescriptor(double.MaxValue), false);
            this.Prototype.DefineOwnProperty("MIN_VALUE", new DataDescriptor(double.MinValue), false);
            this.Prototype.DefineOwnProperty("POSITIVE_INFINITY", new DataDescriptor(double.PositiveInfinity), false);
            this.Prototype.DefineOwnProperty("NEGATIVE_INFINITY", new DataDescriptor(double.NegativeInfinity), false);

        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.2.1
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(object[] arguments)
        {
            return Construct(arguments.Length > 0 ? TypeConverter.ToNumber(arguments[0]) : 0);
        }

        public NumberInstance Construct(double value)
        {
            var instance = new NumberInstance(Prototype);
            instance.PrimitiveValue = value;
            return instance;
        }


    }
}
