using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintFunctionDeclarationStatement : JintStatement<FunctionDeclaration>
    {
        public JintFunctionDeclarationStatement(FunctionDeclaration statement) : base(statement)
        {
        }

        protected override Completion ExecuteInternal(EvaluationContext context)
        {
            return NormalCompletion(JsValue.Undefined);
        }
    }
}