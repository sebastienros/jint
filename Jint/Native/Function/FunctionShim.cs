using Jint.Native.Object;
using Jint.Parser.Ast;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public sealed class FunctionShim : FunctionInstance
    {
        private readonly Engine _engine;

        public FunctionShim(Engine engine, ObjectInstance prototype, Identifier[] parameters, LexicalEnvironment scope) : base(engine, prototype, parameters, scope)
        {
            _engine = engine;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Undefined.Instance;
        }
    }
}
