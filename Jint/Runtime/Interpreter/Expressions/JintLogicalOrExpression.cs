using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintLogicalOrExpression : JintExpression
{
    private readonly JintExpression _left;
    private readonly JintExpression _right;

    public JintLogicalOrExpression(LogicalExpression expression) : base(expression)
    {
        _left = Build(expression.Left);
        _right = Build(expression.Right);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        JsValue left;
        var suspendable = context.Engine?.ExecutionContext.Suspendable;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out LeftOperandSuspendData? suspendData))
        {
            left = suspendData!.LeftValue;
        }
        else
        {
            left = _left.GetValue(context);

            // Check for generator suspension after evaluating left operand
            if (context.IsSuspended())
            {
                return left;
            }
        }

        if (left is JsBoolean b && b._value)
        {
            suspendable?.Data.Clear(this);
            return b;
        }

        if (TypeConverter.ToBoolean(left))
        {
            suspendable?.Data.Clear(this);
            return left;
        }

        var right = _right.GetValue(context);
        if (context.IsSuspended())
        {
            if (suspendable is not null)
            {
                suspendable.Data.GetOrCreate<LeftOperandSuspendData>(this).LeftValue = left;
            }

            return right;
        }

        suspendable?.Data.Clear(this);
        return right;
    }
}
