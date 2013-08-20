using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Function
{
    public sealed class ThrowTypeError : FunctionInstance
    {
        private readonly Engine _engine;

        public ThrowTypeError(Engine engine): base(engine, engine.Function, new string[0], engine.GlobalEnvironment, false)
        {
            _engine = engine;
            DefineOwnProperty("length", new DataDescriptor(0) { Writable = false, Enumerable = false, Configurable = false }, false);
            Extensible = false;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            throw new JavaScriptException(_engine.TypeError);
        }
    }
}
