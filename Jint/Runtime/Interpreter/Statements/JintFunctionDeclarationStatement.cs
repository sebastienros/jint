using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements
{
    internal sealed class JintFunctionDeclarationStatement : JintStatement<FunctionDeclaration>
    {
        public JintFunctionDeclarationStatement(Engine engine, FunctionDeclaration statement) : base(engine, statement)
        {
        }

        public override Completion Execute()
        {
            return new Completion(CompletionType.Normal, null, null);
        }
    }
}