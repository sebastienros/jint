using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintLogicalAndExpression : JintExpression
{
    private readonly JintExpression _left;
    private readonly JintExpression _right;

    public JintLogicalAndExpression(LogicalExpression expression) : base(expression)
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

        if (left is JsBoolean b && !b._value)
        {
            suspendable?.Data.Clear(this);
            return b;
        }

        if (!TypeConverter.ToBoolean(left))
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

    public override bool GetBooleanValue(EvaluationContext context)
    {
        // In a plain synchronous frame nothing can suspend or resume mid-expression, so both
        // operands take the unboxed boolean path (comparison lanes return raw bools) instead of
        // materializing a JsValue only to feed TypeConverter.ToBoolean. C# && preserves the
        // JS short-circuit: the right operand is skipped when the left is falsy. Operator
        // overloading, generator/async suspension and the engine-less fast-eval context all fall
        // back to the materializing base path (ToBoolean(GetValue)), whose EvaluateInternal keeps
        // the left operand's side effects from re-running across a suspend/resume.
        var suspendable = context.Engine?.ExecutionContext.Suspendable;
        if (suspendable is null && context.Engine is not null && !context.OperatorOverloadingAllowed)
        {
            return _left.GetBooleanValue(context) && _right.GetBooleanValue(context);
        }

        return base.GetBooleanValue(context);
    }
}
