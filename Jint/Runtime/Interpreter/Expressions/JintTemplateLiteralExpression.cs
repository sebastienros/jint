using System.Text;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintTemplateLiteralExpression : JintExpression
{
    internal readonly TemplateLiteral _templateLiteralExpression;
    internal JintExpression[] _expressions = [];
    private bool _initialized;

    public JintTemplateLiteralExpression(TemplateLiteral expression) : base(expression)
    {
        _templateLiteralExpression = expression;
    }

    private void Initialize()
    {
        DoInitialize();
    }

    internal void DoInitialize()
    {
        ref readonly var expressions = ref _templateLiteralExpression.Expressions;
        _expressions = new JintExpression[expressions.Count];
        for (var i = 0; i < expressions.Count; i++)
        {
            _expressions[i] = Build(expressions[i]);
        }

        _initialized = true;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }

        using var sb = new ValueStringBuilder();
        ref readonly var elements = ref _templateLiteralExpression.Quasis;
        for (var i = 0; i < elements.Count; i++)
        {
            var quasi = elements[i];
            sb.Append(quasi.Value.Cooked);
            if (i < _expressions.Length)
            {
                var value = _expressions[i].GetValue(context);
                sb.Append(TypeConverter.ToString(value));
            }
        }

        return JsString.Create(sb.ToString());
    }
}
