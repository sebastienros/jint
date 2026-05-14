using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class NullishCoalescingExpression : JintExpression
{
    private readonly JintExpression _left;
    private readonly JintExpression? _right;
    private readonly JsValue? _constant;

    public NullishCoalescingExpression(LogicalExpression expression) : base(expression)
    {
        _left = Build(expression.Left);

        // we can create a fast path for common literal case like variable ?? 0
        if (expression.Right is Literal l)
        {
            _constant = JintLiteralExpression.ConvertToJsValue(l);
        }
        else
        {
            _right = Build(expression.Right);
        }
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;
        return EvaluateConstantOrExpression(context);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        return EvaluateConstantOrExpression(context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JsValue EvaluateConstantOrExpression(EvaluationContext context)
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

        if (!left.IsNullOrUndefined())
        {
            suspendable?.Data.Clear(this);
            return left;
        }

        if (_constant is not null)
        {
            suspendable?.Data.Clear(this);
            return _constant;
        }

        var right = _right!.GetValue(context);
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