using Jint.Native.Object;
using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public class FunctionShim : FunctionInstance
    {
        public FunctionShim(ObjectInstance prototype, Identifier[] parameters, LexicalEnvironment scope) : base(prototype, parameters, scope)
        {
        }

        public override dynamic Call(Engine interpreter, object thisObject, dynamic[] arguments)
        {
            return Undefined.Instance;
        }
    }
}
