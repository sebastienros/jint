using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Array
{
    public sealed class ArrayConstructor : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;

        public ArrayConstructor(Engine engine) :  base(engine, new ObjectInstance(engine, engine.RootFunction), null, null, false)
        {
            _engine = engine;

            // the constructor is the function constructor of an object
            Prototype.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = false }, false);
            Prototype.DefineOwnProperty("prototype", new DataDescriptor(Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);
                                  
            // Array prototype properties
            Prototype.DefineOwnProperty("length", new ClrAccessDescriptor<ArrayInstance>(_engine, x => x.Length), false);

            // Array prototype functions
            Prototype.DefineOwnProperty("push", new ClrDataDescriptor<ArrayInstance, object>(engine, Push), false);
            Prototype.DefineOwnProperty("pop", new ClrDataDescriptor<ArrayInstance, object>(engine, Pop), false);
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var instance = new ArrayInstance(_engine, Prototype);

            foreach (var arg in arguments)
            {
                instance.Push(arg);
            }

            return instance;
        }

        private static object Push(ArrayInstance thisObject, object[] arguments)
        {
            thisObject.Push(arguments[0]);
            return arguments[0];
        }

        private static object Pop(ArrayInstance thisObject, object[] arguments)
        {
            return thisObject.Pop();
        }
    }
}
