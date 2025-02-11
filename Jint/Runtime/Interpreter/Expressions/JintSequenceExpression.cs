using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintSequenceExpression : JintExpression
{
    private JintExpression[] _expressions = [];
    private bool _initialized;

    public JintSequenceExpression(SequenceExpression expression) : base(expression)
    {
    }

    private void Initialize()
    {
        var expression = (SequenceExpression) _expression;
        ref readonly var expressions = ref expression.Expressions;
        var temp = new JintExpression[expressions.Count];
        for (var i = 0; i < (uint) temp.Length; i++)
        {
            temp[i] = Build(expressions[i]);
        }

        _expressions = temp;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }
            
        var result = JsValue.Undefined;
        foreach (var expression in _expressions)
        {
            result = expression.GetValue(context);
        }

        return result;
    }
}