using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.7
/// </summary>
internal sealed class JintContinueStatement : JintStatement<ContinueStatement>
{
    public JintContinueStatement(ContinueStatement statement) : base(statement)
    {
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        context.Target = _statement.Label?.Name;
        return new Completion(CompletionType.Continue, null!, _statement);
    }
}
