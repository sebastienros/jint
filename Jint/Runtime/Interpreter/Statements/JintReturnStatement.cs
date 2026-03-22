using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.9
/// </summary>
internal sealed class JintReturnStatement : JintStatement<ReturnStatement>
{
    private readonly JintExpression? _argument;

    public JintReturnStatement(ReturnStatement statement) : base(statement)
    {
        _argument = statement.Argument != null
            ? JintExpression.Build(statement.Argument)
            : null;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var value = _argument is not null ? _argument.GetValue(context).Clone() : JsValue.Undefined;
        return new Completion(CompletionType.Return, value, _statement);
    }
}
