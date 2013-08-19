using System.Linq;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
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
            instance.FastAddProperty("length", 0, true, false, true);
            Push(instance, arguments);

            return instance;
        }

        private object Push(object thisObject, object[] arguments)
        {
            var o = TypeConverter.ToObject(_engine, thisObject);
            var lenVal = o.Get("length");
            var n = TypeConverter.ToUint32(lenVal);
            foreach (var e in arguments)
            {
                o.Put(TypeConverter.ToString(n), e, true);
                n++;
            }
            o.Put("length", n, true);

            return n;
        }

        private object Pop(object thisObject, object[] arguments)
        {
            var o = TypeConverter.ToObject(_engine, thisObject);
            var lenVal = o.Get("length");
            var len = TypeConverter.ToUint32(lenVal);
            if (len == 0)
            {
                o.Put("length", 0, true);
                return Undefined.Instance;
            }
            else
            {
                var indx = TypeConverter.ToString(len - 1);
                var element = o.Get(indx);
                o.Delete(indx, true);
                o.Put("length", indx, true);
                return element;
            }
        }
    }
}
