using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Error
{
    public sealed class ErrorConstructor : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;

        public ErrorConstructor(Engine engine, string name)
            : base(engine, new ErrorInstance(engine, null, name), null, null, false)
        {
            _engine = engine;

            // the constructor is the function constructor of an object
            Prototype.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = false }, false);
            Prototype.DefineOwnProperty("prototype", new DataDescriptor(Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);
                                  
            // Error prototype properties
            Prototype.DefineOwnProperty("message", new ClrAccessDescriptor<ErrorInstance>(_engine, x => x.Message), false);
            Prototype.DefineOwnProperty("name", new ClrAccessDescriptor<ErrorInstance>(_engine, x => x.Name), false);

            // Error prototype functions
            Prototype.DefineOwnProperty("toString", new ClrDataDescriptor<ErrorInstance, object>(engine, ToErrorString), false);

        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var instance = new ErrorInstance(_engine, Prototype, null) {Extensible = true};

            if (arguments.Length > 0 && arguments[0] != Undefined.Instance)
            {
                instance.Message = TypeConverter.ToString(arguments[0]);
            }

            return instance;
        }

        private static object ToErrorString(ErrorInstance thisObject, object[] arguments)
        {
            return thisObject.ToErrorString();
        }

    }
}
