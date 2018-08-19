using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator
{
    internal sealed class IteratorPrototype : IteratorInstance
    {
        private IteratorPrototype(Engine engine) : base(engine)
        {
        }

        public static IteratorPrototype CreatePrototypeObject(Engine engine, IteratorConstructor iteratorConstructor)
        {
            var obj = new IteratorPrototype(engine)
            {
                Extensible = true, Prototype = engine.Object.PrototypeObject
            };

            obj.SetOwnProperty("name", new PropertyDescriptor("Map", PropertyFlag.Configurable));

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("next", new ClrFunctionInstance(Engine, "next", Next, 0), true, false, true);
        }

        private JsValue Next(JsValue thisObj, JsValue[] arguments)
        {
            return ((IteratorInstance) thisObj).Next();
        }
    }
}