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

    /// <remarks>
    /// A substitution that is already a string at least <see cref="JsString.MinDeferredConcatenationLength"/>
    /// long is not copied into the buffer: the text before it is flushed into one string and the two are joined
    /// through <see cref="JsString.Concat"/>, which defers the copy as <c>+</c> does. Copying it made a template
    /// that accumulates (<c>s = `${s}x`</c>) copy its accumulator on every evaluation, so n evaluations cost
    /// O(n^2) where the same loop written with <c>+</c> is linear (issue #4460). Shorter substitutions, quasis
    /// and non-string values are appended as text, so a template whose result stays short is still one string
    /// built in one buffer, and a short flat string costs the field load it did before.
    /// </remarks>
    protected override object EvaluateInternal(EvaluationContext context)
    {
        var suspendable = context.Engine.ExecutionContext.Suspendable;

        using var sb = new ValueStringBuilder();
        ref readonly var elements = ref _templateLiteralExpression.Quasis;

        // The result is deferred followed by sb's text from pending on; sb's text before pending is already part
        // of deferred. offset is deferred's length minus pending, so the result's length is offset + sb.Length.
        JsString? deferred = null;
        var pending = 0;
        long offset = 0;

        int startIndex = 0;
        bool resumeMidIteration = false;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out TemplateLiteralSuspendData? suspendData))
        {
            // The saved accumulator already includes the quasi at startIndex (we
            // appended it before evaluating the interpolation that suspended).
            deferred = suspendData!.Deferred;
            offset = deferred?.Length ?? 0;
            sb.Append(suspendData.Accumulator.ToString());
            startIndex = suspendData.NextExpressionIndex;
            resumeMidIteration = true;
        }

        for (var i = startIndex; i < elements.Count; i++)
        {
            // Skip the quasi append for the resume iteration — it's already in sb.
            if (!(i == startIndex && resumeMidIteration))
            {
                var cooked = elements[i].Value.Cooked;
                CheckLength(context, offset + sb.Length + (cooked?.Length ?? 0));
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
                        data.Deferred = deferred;
                        data.Accumulator = new StringBuilder(sb.AsSpan(pending).ToString());
                        data.NextExpressionIndex = i;
                    }
                    return JsString.Empty;
                }

                string text;
                if (value.IsString())
                {
                    var str = value.AsStringNoTypeCheck();
                    text = str._value;
                    if (text is null || text.Length >= JsString.MinDeferredConcatenationLength)
                    {
                        if (str.Length >= JsString.MinDeferredConcatenationLength)
                        {
                            CheckLength(context, offset + sb.Length + str.Length);
                            deferred = Join(context, deferred, sb.AsSpan(pending), str);
                            pending = sb.Length;
                            offset = deferred.Length - (long) pending;
                            continue;
                        }

                        text = str.ToString();
                    }
                }
                else
                {
                    text = TypeConverter.ToStringNonString(value);
                }

                CheckLength(context, offset + sb.Length + text.Length);
                sb.Append(text);
            }
        }

        suspendable?.Data.Clear(this);

        if (deferred is null)
        {
            return JsString.Create(sb.ToString());
        }

        return Join(context, deferred, sb.AsSpan(pending), right: null);
    }

    /// <summary>
    /// Appends <paramref name="text"/>, then <paramref name="right"/> when there is one, to what the template
    /// has joined so far, without copying either string operand.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JsString Join(EvaluationContext context, JsString? deferred, ReadOnlySpan<char> text, JsString? right)
    {
        if (text.Length > 0)
        {
            var head = JsString.Create(text.ToString());
            deferred = deferred is null ? head : JsString.Concat(deferred, head, context);
        }

        if (right is not null)
        {
            deferred = deferred is null ? right : JsString.Concat(deferred, right, context);
        }

        return deferred!;
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
