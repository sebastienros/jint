using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintLogicalAndExpression : JintExpression
{
    private JintExpression _left = null!;
    private JintExpression _right = null!;
    private bool _initialized;

    public JintLogicalAndExpression(LogicalExpression expression) : base(expression)
    {
    }

    private void Initialize()
    {
        var expression = (LogicalExpression) _expression;
        _left = Build(expression.Left);
        _right = Build(expression.Right);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }

        var left = _left.GetValue(context);

        if (left is JsBoolean b && !b._value)
        {
            return b;
        }

        if (!TypeConverter.ToBoolean(left))
        {
            return left;
        }

        return _right.GetValue(context);
    }
}
