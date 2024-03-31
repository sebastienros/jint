using Jint.Native;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintFunctionDeclarationStatement : JintStatement<FunctionDeclaration>
{
    public JintFunctionDeclarationStatement(FunctionDeclaration statement) : base(statement)
    {
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        return new Completion(CompletionType.Normal, JsEmpty.Instance, ((JintStatement) this)._statement);
    }
}
