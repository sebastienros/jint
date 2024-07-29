using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.13
/// </summary>
internal sealed class JintThrowStatement : JintStatement<ThrowStatement>
{
    private JintExpression _argument = null!;

    public JintThrowStatement(ThrowStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _argument = JintExpression.Build(_statement.Argument);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        return new Completion(CompletionType.Throw, _argument.GetValue(context), _argument._expression);
    }
}