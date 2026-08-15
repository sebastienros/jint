using System.Runtime.CompilerServices;
using System.Text;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintTemplateLiteralExpression : JintExpression
{
    internal readonly TemplateLiteral _templateLiteralExpression;
    internal readonly JintExpression[] _expressions;

    public JintTemplateLiteralExpression(TemplateLiteral expression) : base(expression)
    {
        _templateLiteralExpression = expression;
        ref readonly var expressions = ref expression.Expressions;
        _expressions = new JintExpression[expressions.Count];
        for (var i = 0; i < expressions.Count; i++)
        {
            _expressions[i] = Build(expressions[i]);
        }
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var suspendable = context.Engine.ExecutionContext.Suspendable;

        using var sb = new ValueStringBuilder();
        ref readonly var elements = ref _templateLiteralExpression.Quasis;

        int startIndex = 0;
        bool resumeMidIteration = false;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out TemplateLiteralSuspendData? suspendData))
        {
            // The saved accumulator already includes the quasi at startIndex (we
            // appended it before evaluating the interpolation that suspended).
            sb.Append(suspendData!.Accumulator.ToString());
            startIndex = suspendData.NextExpressionIndex;
            resumeMidIteration = true;
        }

        for (var i = startIndex; i < elements.Count; i++)
        {
            // Skip the quasi append for the resume iteration — it's already in sb.
            if (!(i == startIndex && resumeMidIteration))
            {
                var cooked = elements[i].Value.Cooked;
                CheckLength(context, (long) sb.Length + (cooked?.Length ?? 0));
                sb.Append(cooked);
            }

            if (i < _expressions.Length)
            {
                var value = _expressions[i].GetValue(context);

                // Without this break, side effects in interpolations after a suspended
                // one would continue to run during the suspended pass.
                if (context.IsSuspended())
                {
                    if (suspendable is not null)
                    {
                        var data = suspendable.Data.GetOrCreate<TemplateLiteralSuspendData>(this);
                        data.Accumulator = new StringBuilder(sb.ToString());
                        data.NextExpressionIndex = i;
                    }
                    return JsString.Empty;
                }

                var text = TypeConverter.ToString(value);
                CheckLength(context, (long) sb.Length + text.Length);
                sb.Append(text);
            }
        }

        suspendable?.Data.Clear(this);
        return JsString.Create(sb.ToString());
    }

    /// <summary>
    /// Refuses a template literal whose result would exceed <see cref="JsString.MaxLength"/>, checked
    /// before the append that would take it there. The realm is resolved only inside the cold branch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckLength(EvaluationContext context, long length)
    {
        if (length > JsString.MaxLength)
        {
            Throw.RangeError(context.Engine.Realm, "Invalid string length");
        }
    }
}
