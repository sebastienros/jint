using Jint.Collections;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator
{
    internal sealed class IteratorPrototype : IteratorInstance
    {
        private string _name;

        private IteratorPrototype(Engine engine) : base(engine)
        {
        }

        public static IteratorPrototype CreatePrototypeObject(Engine engine, string name)
        {
            var obj = new IteratorPrototype(engine)
            {
                _prototype = engine.Object.PrototypeObject,
                _name = name
            };

            return obj;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(2, checkExistingKeys: false)
            {
                ["name"] = new PropertyDescriptor(CommonProperties.Name, PropertyFlag.Configurable),
                ["next"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "next", Next, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable)
            };
            SetProperties(properties);

            if (_name != null)
            {
                var symbols = new SymbolDictionary(1)
                {
                    [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor(_name, PropertyFlag.Configurable)
                };
                SetSymbols(symbols);
            }
        }

        private JsValue Next(JsValue thisObj, JsValue[] arguments)
        {
            if (!(thisObj is IteratorInstance iterator))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine);
            }

            iterator.TryIteratorStep(out var result);
            return result;
        }
    }
}