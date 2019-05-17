using Jint.Collections;
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
                Extensible = true,
                Prototype = engine.Object.PrototypeObject
            };

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(2)
            {
                ["name"] = new PropertyDescriptor("Map", PropertyFlag.Configurable),
                ["next"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "next", Next, 0, PropertyFlag.Configurable), true, false, true)
            };
        }

        private JsValue Next(JsValue thisObj, JsValue[] arguments)
        {
            return ((IteratorInstance) thisObj).Next();
        }
    }
}