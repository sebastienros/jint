using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintArrayExpression : JintExpression
{
    private readonly ExpressionCache _arguments = new();

    private JintArrayExpression(ArrayExpression expression) : base(expression)
    {
        _arguments.Initialize(expression.Elements.AsSpan()!);
    }

    public static JintExpression Build(ArrayExpression expression)
    {
        return expression.Elements.Count == 0
            ? JintEmptyArrayExpression.Instance
            : new JintArrayExpression(expression);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var expressions = ((ArrayExpression) _expression).Elements.AsSpan();
        var engine = context.Engine;
        if (!_arguments.HasSpreads)
        {
            var suspendable = engine.ExecutionContext.Suspendable;
            JsValue[] values;
            int startIndex;
            if (suspendable is { IsResuming: true }
                && suspendable.Data.TryGet(this, out ExpressionBufferSuspendData? suspendData))
            {
                // Resume: reuse the partially-filled buffer so already-evaluated
                // elements (which may have observable side effects) are not re-evaluated.
                values = suspendData!.Buffer;
                startIndex = suspendData.NextIndex;
            }
            else
            {
                values = new JsValue[expressions.Length];
                startIndex = 0;
            }

            var nextIndex = _arguments.BuildArguments(context, values, startIndex);

            // If generator suspended during element evaluation, stow the partial
            // buffer in suspend data so resume continues from the right index.
            if (context.IsSuspended())
            {
                if (suspendable is not null)
                {
                    var data = suspendable.Data.GetOrCreate<ExpressionBufferSuspendData>(this);
                    data.Buffer = values;
                    data.NextIndex = nextIndex;
                }
                return JsValue.Undefined;
            }

            suspendable?.Data.Clear(this);
            return new JsArray(engine, values);
        }

        var array = _arguments.ArgumentListEvaluationWithSpreadsResumable(context, this);

        // If generator suspended during argument evaluation, the partial target
        // is preserved in suspend data; on resume we continue from there.
        if (context.IsSuspended())
        {
            return JsValue.Undefined;
        }

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
