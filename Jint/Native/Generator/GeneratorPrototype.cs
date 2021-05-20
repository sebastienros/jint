using Esprima;
using Jint.Collections;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Generator
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-generator-objects
    /// </summary>
    internal sealed class GeneratorPrototype : ObjectInstance
    {
        internal GeneratorPrototype(Engine engine) : base(engine)
        {
        }

        public static GeneratorPrototype CreatePrototypeObject(Engine engine)
        {
            var obj = new GeneratorPrototype(engine)
            {
                _prototype = IteratorPrototype.CreatePrototypeObject(engine, name: "")
            };

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            var properties = new PropertyDictionary(7, false)
            {
                ["constructor"] = new(Engine.GeneratorFunction, PropertyFlag.NonEnumerable),
                ["next"] = new(new ClrFunctionInstance(Engine, "next", Next, 1, lengthFlags), propertyFlags),
                ["return"] = new(new ClrFunctionInstance(Engine, "return", Return, 1, lengthFlags), propertyFlags),
                ["throw"] = new(new ClrFunctionInstance(Engine, "throw", Throw, 1, lengthFlags), propertyFlags)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.ToStringTag] = new("Generator", PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-generator.prototype.next
        /// </summary>
        private static JsValue Next(JsValue thisObject, JsValue[] arguments)
        {
            var g = (Generator) thisObject;
            var value = arguments.At(0);
            return g.GeneratorResume(value, null);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-generator.prototype.return
        /// </summary>
        private static JsValue Return(JsValue thisObject, JsValue[] arguments)
        {
            var g = (Generator) thisObject;
            var value = arguments.At(0);
            var C = new Completion(CompletionType.Return, value, null, new Location());
            return g.GeneratorResumeAbrupt(C, null);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-generator.prototype.throw
        /// </summary>
        private static JsValue Throw(JsValue thisObject, JsValue[] arguments)
        {
            var g = (Generator) thisObject;
            var exception = arguments.At(0);
            var C = new Completion(CompletionType.Throw, exception, null, new Location());
            return g.GeneratorResumeAbrupt(C, null);
        }
    }
}