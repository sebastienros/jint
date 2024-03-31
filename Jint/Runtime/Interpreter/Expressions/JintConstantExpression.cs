using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// Constant JsValue returning expression.
/// </summary>
internal sealed class JintConstantExpression : JintExpression
{
    private readonly JsValue _value;

    public JintConstantExpression(Expression expression, JsValue value) : base(expression)
    {
        _value = value;
    }

    public JsValue Value => _value;

    public override JsValue GetValue(EvaluationContext context) => _value;

    protected override object EvaluateInternal(EvaluationContext context) => _value;
}
