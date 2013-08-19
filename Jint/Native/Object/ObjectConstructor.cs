using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Object
{
    public sealed class ObjectConstructor : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;

        public ObjectConstructor(Engine engine) : base(engine, engine.RootFunction, null, null, false)
        {
            _engine = engine;
            engine.RootFunction.DefineOwnProperty("hasOwnProperty", new ClrDataDescriptor<object, bool>(engine, HasOwnProperty), false);
            engine.RootFunction.DefineOwnProperty("toString", new ClrDataDescriptor<object, string>(engine, ToString), false);
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Undefined.Instance;
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var instance = new ObjectInstance(_engine, this.Prototype);

            // the constructor is the function constructor of an object
            instance.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = false }, false);
            instance.DefineOwnProperty("prototype", new DataDescriptor(this.Prototype) { Writable = false, Enumerable = false, Configurable = false }, false);
            return instance;
        }

        private bool HasOwnProperty(object thisObject, object[] arguments)
        {
            var p = TypeConverter.ToString(arguments[0]);
            var o = TypeConverter.ToObject(_engine, thisObject);
            var desc = o.GetOwnProperty(p);
            return desc != PropertyDescriptor.Undefined;
        }

        private static string ToString(object thisObject, object[] arguments)
        {
            return TypeConverter.ToString(thisObject);

        }

    }
}
