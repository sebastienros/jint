using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintArrayExpression : JintExpression
{
    private readonly ExpressionCache _arguments = new();
    private bool _initialized;

    private JintArrayExpression(ArrayExpression expression) : base(expression)
    {
    }

    public static JintExpression Build(ArrayExpression expression)
    {
        return expression.Elements.Count == 0
            ? JintEmptyArrayExpression.Instance
            : new JintArrayExpression(expression);
    }

    private void Initialize(EvaluationContext context)
    {
        _arguments.Initialize(context, ((ArrayExpression) _expression).Elements.AsSpan()!);

        // we get called from nested spread expansion in call
        _initialized = true;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize(context);
            _initialized = true;
        }

        var expressions = ((ArrayExpression) _expression).Elements.AsSpan();
        var engine = context.Engine;
        if (!_arguments.HasSpreads)
        {
            var values = new JsValue[expressions.Length];
            _arguments.BuildArguments(context, values);
            return new JsArray(engine, values);
        }

        var array = new List<JsValue>();
        _arguments.BuildArgumentsWithSpreads(context, array);
        return new JsArray(engine, array.ToArray());
    }

    internal sealed class JintEmptyArrayExpression : JintExpression
    {
        public static JintEmptyArrayExpression Instance =
            new(new ArrayExpression(NodeList.From(Array.Empty<Expression?>())));

        private JintEmptyArrayExpression(Expression expression) : base(expression)
        {
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            return new JsArray(context.Engine, []);
        }

        public override JsValue GetValue(EvaluationContext context)
        {
            return new JsArray(context.Engine, []);
        }
    }
}
