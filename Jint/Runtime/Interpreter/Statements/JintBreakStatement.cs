using Jint.Native;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.8
/// </summary>
internal sealed class JintBreakStatement : JintStatement<BreakStatement>
{
    public JintBreakStatement(BreakStatement statement) : base(statement)
    {
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        context.Target = _statement.Label?.Name;
        return new Completion(CompletionType.Break, JsEmpty.Instance, _statement);
    }
}
