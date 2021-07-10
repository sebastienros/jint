using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator
{
    internal sealed class IteratorPrototype : Prototype
    {
        private readonly string _name;

        internal IteratorPrototype(
            Engine engine,
            Realm realm,
            string name,
            ObjectPrototype objectPrototype) : base(engine, realm)
        {
            _prototype = objectPrototype;
            _name = name;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(2, checkExistingKeys: false)
            {
                ["name"] = new PropertyDescriptor("Map", PropertyFlag.Configurable),
                ["next"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "next", Next, 0, PropertyFlag.Configurable), true, false, true)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(_name != null ? 2 : 1)
            {
                [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "iterator", ToIterator, 1, PropertyFlag.Configurable), true, false, true),
            };

            if (_name != null)
            {
                symbols[GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor(_name, PropertyFlag.Configurable);
            }
            SetSymbols(symbols);
        }

        private JsValue ToIterator(JsValue thisObj, JsValue[] arguments)
        {
            return thisObj;
        }

        private JsValue Next(JsValue thisObj, JsValue[] arguments)
        {
            var iterator = thisObj as IteratorInstance;
            if (iterator is null)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            iterator.TryIteratorStep(out var result);
            return result;
        }
    }
}
