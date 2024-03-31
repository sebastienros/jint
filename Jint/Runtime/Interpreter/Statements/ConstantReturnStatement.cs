using Jint.Native;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class ConstantStatement : JintStatement
{
    private readonly JsValue _value;
    private CompletionType _completionType;

    public ConstantStatement(Statement statement, CompletionType completionType, JsValue value) : base(statement)
    {
        _completionType = completionType;
        _value = value;
    }

    protected override Completion ExecuteInternal(EvaluationContext context) => new(_completionType, _value, _statement);
}
