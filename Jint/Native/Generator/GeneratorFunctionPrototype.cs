using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Generator
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator-objects
    /// </summary>
    internal sealed class GeneratorFunctionPrototype : ObjectInstance
    {
        internal GeneratorFunctionPrototype(Engine engine) : base(engine)
        {
        }

        public static GeneratorFunctionPrototype CreatePrototypeObject(Engine engine)
        {
            var obj = new GeneratorFunctionPrototype(engine)
            {
                _prototype = engine.GeneratorPrototype
            };

            return obj;
        }

        protected override void Initialize()
        {
            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("GeneratorFunction", PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }
    }
}