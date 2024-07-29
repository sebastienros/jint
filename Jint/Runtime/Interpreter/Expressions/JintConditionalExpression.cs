namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintConditionalExpression : JintExpression
{
    private readonly JintExpression _test;
    private readonly JintExpression _consequent;
    private readonly JintExpression _alternate;

    public JintConditionalExpression(ConditionalExpression expression) : base(expression)
    {
        _test = Build(expression.Test);
        _consequent = Build(expression.Consequent);
        _alternate = Build(expression.Alternate);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        return TypeConverter.ToBoolean(_test.GetValue(context))
            ? _consequent.GetValue(context)
            : _alternate.GetValue(context);
    }
}
