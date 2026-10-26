using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// Probes the native stack, then evaluates the operator node it stands in for. Built only into a binary or
/// logical chain deeper than <see cref="SyntaxNesting.EvaluationProbeInterval"/>, one per that many levels,
/// so a chain of ordinary length never contains one and evaluates exactly as it did.
/// </summary>
/// <remarks>
/// <para>
/// Evaluating <c>a || b || …</c> recurses once per operand, and a node is built where it first runs, which
/// need not be where it runs later: a function built at the top of the stack can be called again from the
/// bottom of a deep recursion. The build-time probe cannot see that, and the call that reaches the function
/// probes only once, for the runtime's reserve, which a chain a few hundred operands deep outgrows. So a deep
/// chain carries its own probes, and running out of stack inside one is the catchable
/// <c>RangeError</c> the rest of the interpreter throws rather than a native stack overflow.
/// </para>
/// <para>
/// It is transparent: it evaluates under the node it wraps and forwards every evaluation entry. The only
/// difference a parent can see is the node's type, and the parents that build one — binary and logical
/// operators — test an operand's type only to pick a fast lane for an identifier, a constant or a member read,
/// which the operator node it wraps never was.
/// </para>
/// </remarks>
internal sealed class JintStackProbeExpression : JintExpression
{
    private readonly JintExpression _operator;

    private JintStackProbeExpression(JintExpression wrapped) : base(wrapped._expression)
    {
        _operator = wrapped;
    }

    /// <summary>
    /// Builds <paramref name="operand"/> of a binary or logical operator, behind a probe when it is itself an
    /// operator node at a depth the probe interval divides.
    /// </summary>
    // Inlined so that building a chain costs no extra frame per level, and AtProbeDepth kept out of line so
    // the constructor's own frame does not grow: those frames are what a deep chain runs out of stack on
    // while it is built.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JintExpression BuildOperand(Expression operand) => AtProbeDepth(Build(operand));

    /// <summary>
    /// Puts <paramref name="operand"/>, an operand just built for the operator being built, behind a probe when
    /// it is itself an operator node and the operator being built sits at a depth the probe interval divides.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static JintExpression AtProbeDepth(JintExpression operand)
    {
        if (SyntaxNesting.IsEvaluationProbeDepth
            && operand is JintBinaryExpression or JintLogicalOrExpression or JintLogicalAndExpression or NullishCoalescingExpression)
        {
            return new JintStackProbeExpression(operand);
        }

        return operand;
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        Probe(context);
        return _operator.GetValue(context);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        Probe(context);
        return _operator.EvaluateWithoutNodeTracking(context);
    }

    public override bool GetBooleanValue(EvaluationContext context)
    {
        Probe(context);
        return _operator.GetBooleanValue(context);
    }

    internal override bool HasDiscardFastPath => _operator.HasDiscardFastPath;

    internal override void EvaluateAndDiscard(EvaluationContext context)
    {
        Probe(context);
        _operator.EvaluateAndDiscard(context);
    }

    internal override void EnableShortCircuitPropagation() => _operator.EnableShortCircuitPropagation();

    private static void Probe(EvaluationContext context)
    {
        // An engine-less context only ever folds a pair of literals, which never builds a probe.
        context.Engine?._stackGuard.EnsureNativeStackHeadroom();
    }
}
