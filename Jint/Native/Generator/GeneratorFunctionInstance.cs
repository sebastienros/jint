using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Generator
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-generatorfunction-objects
    /// </summary>
    internal sealed class GeneratorFunctionInstance : FunctionInstance
    {
        private readonly FunctionInstance _functionBody;

        public GeneratorFunctionInstance(Engine engine, FunctionInstance functionBody) : base(engine, JsString.Empty)
        {
            _functionBody = functionBody;
            _prototype = _engine.GeneratorFunction.PrototypeObject;
            var prototype = new ObjectInstance(_engine)
            {
                _prototype = _engine.GeneratorFunction.PrototypeObject._prototype
            };
            _prototypeDescriptor = new PropertyDescriptor(prototype, PropertyFlag.Writable);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluatebody
        /// </summary>
        public override JsValue Call(JsValue functionObject, JsValue[] arguments)
        {
            _engine.FunctionDeclarationInstantiation(_functionBody, arguments);
            var G = OrdinaryCreateFromConstructor(functionObject, _engine.GeneratorFunction.PrototypeObject.Prototype, (engine, _) => new Generator(engine));
            G.GeneratorStart(_functionBody);
            return G;
        }
    }
}