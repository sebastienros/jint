using System;
using Jint.Native.Errors;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Array
{
    public sealed class ArrayConstructor : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;

        public ArrayConstructor(Engine engine) :  base(engine, new ObjectInstance(engine.RootFunction), null, null)
        {
            _engine = engine;

            // the constructor is the function constructor of an object
            this.Prototype.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = false }, false);
            this.Prototype.DefineOwnProperty("prototype", new DataDescriptor(this.Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);

            // Array prototype properties
            this.Prototype.DefineOwnProperty("length", new MethodProperty<ArrayInstance>(_engine, x => x.Length), false);

            this.Prototype.DefineOwnProperty("push", new DataDescriptor(new ClrFunctionInstance(engine, (Action<ArrayInstance, object>)Push)), false);
            this.Prototype.DefineOwnProperty("pop", new DataDescriptor(new ClrFunctionInstance(engine, (Func<ArrayInstance, object>)Pop)), false);
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var instance = new ArrayInstance(Prototype);

            foreach (var arg in arguments)
            {
                instance.Push(arg);
            }

            return instance;
        }

        private static void Push(ArrayInstance thisObject, object o)
        {
            thisObject.Push(o);
        }

        private static object Pop(ArrayInstance thisObject)
        {
            return thisObject.Pop();
        }
    }
}
