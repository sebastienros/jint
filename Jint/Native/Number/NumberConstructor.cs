using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Number
{
    public class NumberConstructor : FunctionInstance
    {
        private readonly Engine _engine;

        public NumberConstructor(Engine engine)
            : base(engine, new ObjectInstance(engine.Object), null, null)
        {
            _engine = engine;

            // the constructor is the function constructor of an object
            this.Prototype.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = false }, false);
            this.Prototype.DefineOwnProperty("prototype", new DataDescriptor(Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);

        }

        public override dynamic Call(object thisObject, dynamic[] arguments)
        {
            return Construct(arguments);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.2.1
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public virtual ObjectInstance Construct(dynamic[] arguments)
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
