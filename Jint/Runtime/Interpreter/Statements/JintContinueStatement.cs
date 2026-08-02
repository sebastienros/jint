using Jint.Native;

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
        // The label travels on the completion, read back from _statement by Completion.Target.
        return new Completion(CompletionType.Continue, JsEmpty.Instance, _statement);
    }
}
