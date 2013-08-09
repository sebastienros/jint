using Jint.Native.Object;
using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public class FunctionShim : FunctionInstance
    {
        private readonly Engine _engine;

        public FunctionShim(Engine engine, ObjectInstance prototype, Identifier[] parameters, LexicalEnvironment scope) : base(engine, prototype, parameters, scope)
        {
            _engine = engine;
        }

        public override dynamic Call(object thisObject, dynamic[] arguments)
        {
            return Undefined.Instance;
        }
    }
}
