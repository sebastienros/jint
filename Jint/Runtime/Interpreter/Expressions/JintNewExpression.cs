using Jint.Native;
using Jint.Native.Function;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintNewExpression : JintExpression
{
    private readonly ExpressionCache _arguments = new();
    private readonly JintExpression _calleeExpression;
    private readonly bool _zeroArgs;

    // Monomorphic call-site cache: the constructor object seen by the last evaluation.
    // Identity pins the realm and the immutable per-instance IsConstructor /
    // IsZeroArgLeafConstructor verdicts, so a hit skips the constructor type-test — and for
    // zero-argument leaf built-ins (default-time-system Date) the whole Engine.Construct
    // call-stack ceremony, which such constructors can neither observe nor throw through.
    // A miss (callee reassigned, different engine sharing this prepared node) falls through
    // to the generic path and re-caches — never a permanent decline.
    private Function? _cachedConstructor;
    private bool _cachedLeafZeroArg;

    public JintNewExpression(NewExpression expression) : base(expression)
    {
        _arguments.Initialize(expression.Arguments.AsSpan());
        _calleeExpression = Build(expression.Callee);
        _zeroArgs = expression.Arguments.Count == 0;
    }

    /// <summary>
    /// Builds the handler for <paramref name="expression"/>: <see cref="JintSuspendableNewExpression"/> when one of
    /// its arguments may suspend the frame, this one otherwise, so the choice costs an ordinary <c>new</c> nothing
    /// at run time.
    /// </summary>
    internal static JintExpression Build(NewExpression expression)
    {
        return JintCallExpression.ArgumentsMaySuspend(expression.Arguments.AsSpan())
            ? new JintSuspendableNewExpression(expression)
            : new JintNewExpression(expression);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;

        // todo: optimize by defining a common abstract class or interface
        var jsValue = _calleeExpression.GetValue(context);

        var isCachedConstructor = ReferenceEquals(jsValue, _cachedConstructor);
        if (isCachedConstructor
            && _cachedLeafZeroArg
            && !engine._isDebugMode
            && engine.ExecutionContext.Suspendable is null)
        {
            // Error objects cannot be produced, but a custom .NET-level failure would still
            // resolve its script location from the last syntax element.
            context.LastSyntaxElement = _expression;
            return ((IConstructor) jsValue).Construct(Arguments.Empty, jsValue);
        }

        if (context.IsSuspended())
        {
            return jsValue;
        }

        var arguments = _arguments.ArgumentListEvaluation(context, this, out var rented);

        // Reset the location to the "new" keyword so that if an Error object is
        // constructed below, the stack trace will capture the correct location.
        context.LastSyntaxElement = _expression;

        if (context.IsSuspended())
        {
            // Argument list suspended mid-evaluation. ExpressionCache keeps the buffer
            // alive in suspend data and returns rented=false. An argument list that can
            // suspend is built as JintSuspendableNewExpression, so this is only a backstop.
            return jsValue;
        }

        if (!isCachedConstructor && !jsValue.IsConstructor)
        {
            Throw.TypeError(engine.Realm, $"{_calleeExpression.SourceText} is not a constructor");
        }

        // construct the new instance using the Function's constructor method
        var instance = engine.Construct(jsValue, arguments, jsValue, _calleeExpression);

        if (rented)
        {
            engine._jsValueArrayPool.ReturnArray(arguments);
        }

        if (!isCachedConstructor && jsValue is Function function)
        {
            _cachedConstructor = function;
            _cachedLeafZeroArg = _zeroArgs && function.IsZeroArgLeafConstructor && function is IConstructor;
        }

        return instance;
    }
}

/// <summary>
/// A <c>new</c> one of whose arguments may suspend the frame. EvaluateNew evaluates the constructor once, before
/// ArgumentListEvaluation (https://tc39.es/ecma262/#sec-evaluatenew), so when the arguments suspend the
/// constructor is parked on the suspendable keyed by this handler (<see cref="CallSuspendData"/>), and the resume
/// that re-enters the handler takes it back instead of evaluating the callee, and every call and getter in it,
/// again (sebastienros/jint#4314).
/// </summary>
/// <remarks>
/// <see cref="JintNewExpression.Build"/> picks this handler at build time, so <see cref="JintNewExpression"/>
/// carries none of it. There is no constructor cache: the leaf lane that cache serves never runs in a frame that
/// can suspend. The argument buffer is keyed by the argument list rather than by this handler, since one key
/// holds one entry.
/// </remarks>
internal sealed class JintSuspendableNewExpression : JintExpression
{
    private readonly ExpressionCache _arguments = new();
    private readonly JintExpression _calleeExpression;

    public JintSuspendableNewExpression(NewExpression expression) : base(expression)
    {
        _arguments.Initialize(expression.Arguments.AsSpan());
        _calleeExpression = Build(expression.Callee);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;

        JsValue constructor;
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out CallSuspendData? parked))
        {
            // Taken back rather than read; see CallSuspendData.
            suspendable.Data.Clear(this);
            constructor = parked!.Function;
        }
        else
        {
            constructor = _calleeExpression.GetValue(context);
            if (suspendable is not null && suspendable.IsSuspended)
            {
                // The callee itself suspended, and resumes through its own handler.
                return constructor;
            }
        }

        var arguments = _arguments.ArgumentListEvaluation(context, _arguments, out var rented);

        // Reset the location to the "new" keyword so that if an Error object is
        // constructed below, the stack trace will capture the correct location.
        context.LastSyntaxElement = _expression;

        if (suspendable is not null && suspendable.IsSuspended)
        {
            // The argument list keeps its own buffer and returned rented=false.
            suspendable.Data.GetOrCreate<CallSuspendData>(this).Function = constructor;
            return constructor;
        }

        if (!constructor.IsConstructor)
        {
            Throw.TypeError(engine.Realm, $"{_calleeExpression.SourceText} is not a constructor");
        }

        var instance = engine.Construct(constructor, arguments, constructor, _calleeExpression);

        if (rented)
        {
            engine._jsValueArrayPool.ReturnArray(arguments);
        }

        return instance;
    }
}
