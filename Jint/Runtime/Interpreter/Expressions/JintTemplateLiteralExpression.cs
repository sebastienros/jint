using Esprima.Ast;
using Jint.Native;
using Jint.Pooling;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintTemplateLiteralExpression : JintExpression
{
    internal readonly TemplateLiteral _templateLiteralExpression;
    internal JintExpression[] _expressions = Array.Empty<JintExpression>();

    public JintTemplateLiteralExpression(TemplateLiteral expression) : base(expression)
    {
        _templateLiteralExpression = expression;
        _initialized = false;
    }

    protected override void Initialize(EvaluationContext context)
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

    private JsString BuildString(EvaluationContext context)
    {
        using var sb = StringBuilderPool.Rent();
        ref readonly var elements = ref _templateLiteralExpression.Quasis;
        for (var i = 0; i < elements.Count; i++)
        {
            var quasi = elements[i];
            sb.Builder.Append(quasi.Value.Cooked);
            if (i < _expressions.Length)
            {
                var value = _expressions[i].GetValue(context);
                sb.Builder.Append(TypeConverter.ToString(value));
            }
        }

        return JsString.Create(sb.ToString());
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        return BuildString(context);
    }
}
