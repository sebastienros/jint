using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
/// </summary>
internal sealed class JintThrowStatement : JintStatement<ThrowStatement>
{
    private readonly JintExpression _argument;

    public JintThrowStatement(ThrowStatement statement) : base(statement)
    {
        _argument = JintExpression.Build(statement.Argument);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var value = _argument.GetValue(context);

        if (context.DebugMode)
        {
            context.Engine.Debugger.OnExceptionThrown(value, _argument._expression.Location);
        }

        return new Completion(CompletionType.Throw, value, _argument._expression);
    }
}