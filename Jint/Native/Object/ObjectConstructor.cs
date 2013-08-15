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
            engine.RootFunction.DefineOwnProperty("hasOwnProperty", new ClrDataDescriptor<ObjectInstance, bool>(engine, HasOwnProperty), false);
            engine.RootFunction.DefineOwnProperty("toString", new ClrDataDescriptor<ObjectInstance, string>(engine, ToString), false);
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

        private static bool HasOwnProperty(ObjectInstance thisObject, object[] arguments)
        {
            var propertyName = TypeConverter.ToString(arguments[0]);
            var desc = thisObject.GetOwnProperty(propertyName);
            return desc != PropertyDescriptor.Undefined;
        }

        private static string ToString(ObjectInstance thisObject, object[] arguments)
        {
            if (thisObject == null || thisObject == Undefined.Instance)
            {
                return "[object Undefined]";
            }

            if (thisObject == Null.Instance)
            {
                return "[object Null]";
            }

            return string.Format("[object {0}]", thisObject.Class);
        
        }

    }
}
