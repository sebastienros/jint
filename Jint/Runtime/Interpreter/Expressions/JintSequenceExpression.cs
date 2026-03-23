using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintSequenceExpression : JintExpression
{
    private readonly JintExpression[] _expressions;

    public JintSequenceExpression(SequenceExpression expression) : base(expression)
    {
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
        var expressions = _expressions;
        var startIndex = 0;

        // When resuming a generator, skip sub-expressions that were already evaluated
        // before the yield point to avoid duplicate side effects
        var suspendable = context.Engine.ExecutionContext.Suspendable;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out SequenceSuspendData? suspendData))
        {
            startIndex = suspendData!.ExpressionIndex;
        }

        var result = JsValue.Undefined;
        for (var i = startIndex; i < expressions.Length; i++)
        {
            result = expressions[i].GetValue(context);

            // Check for generator suspension after each expression
            if (context.IsSuspended() || context.IsGeneratorAborted())
            {
                // Record which sub-expression we were at for proper resume
                if (suspendable is not null && context.IsSuspended())
                {
                    var data = suspendable.Data.GetOrCreate<SequenceSuspendData>(this);
                    data.ExpressionIndex = i;
                }

                return result;
            }
        }

        // Clear suspend data when sequence completes normally
        suspendable?.Data.Clear(this);

        return result;
    }
}