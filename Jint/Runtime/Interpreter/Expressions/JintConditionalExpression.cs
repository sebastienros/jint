using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintConditionalExpression : JintExpression
{
    private readonly JintExpression _test;
    private readonly JintExpression _consequent;
    private readonly JintExpression _alternate;

    public JintConditionalExpression(ConditionalExpression expression) : base(expression)
    {
        _test = Build(expression.Test);
        _consequent = Build(expression.Consequent);
        _alternate = Build(expression.Alternate);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        JsValue testValue;
        var suspendable = context.Engine?.ExecutionContext.Suspendable;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out LeftOperandSuspendData? suspendData))
        {
            testValue = suspendData!.LeftValue;
        }
        else
        {
            testValue = _test.GetValue(context);

            // Check for generator suspension after evaluating test
            if (context.IsSuspended())
            {
                return testValue;
            }
        }

        var result = TypeConverter.ToBoolean(testValue)
            ? _consequent.GetValue(context)
            : _alternate.GetValue(context);

        if (context.IsSuspended())
        {
            if (suspendable is not null)
            {
                suspendable.Data.GetOrCreate<LeftOperandSuspendData>(this).LeftValue = testValue;
            }

            return result;
        }

        suspendable?.Data.Clear(this);
        return result;
    }
}
