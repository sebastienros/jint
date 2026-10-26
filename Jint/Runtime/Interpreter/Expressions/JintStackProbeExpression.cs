using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// Probes the native stack, then evaluates the operator node it stands in for. Built only into an operator chain
/// deeper than <see cref="SyntaxNesting.EvaluationProbeInterval"/>, one per that many levels, so an expression of
/// ordinary depth never contains one and evaluates exactly as it did.
/// </summary>
/// <remarks>
/// <para>
/// Evaluating <c>a || b || …</c> recurses once per operand, and the hoisting walk does not count those levels
/// against its fixed limit (see <see cref="SyntaxNesting"/>), so the chain has to bound itself. A node is built
/// where it first runs, which need not be where it runs later: a function built at the top of the stack can run
/// again from the bottom of a deep recursion, with less stack left than a long chain needs.
/// </para>
/// <para>
/// It probes the way a call expression does. With <c>MaxExecutionStackCount</c> set, a stack that runs low moves
/// the evaluation to a fresh one; otherwise the <c>StackOverflowGuard</c> backstop throws the catchable
/// <c>RangeError</c> the rest of the interpreter throws.
/// </para>
/// <para>
/// It forwards every evaluation entry, under the node it wraps. A parent can tell it apart only by type, and gets
/// one only for an operand that itself has an operator operand, so the lanes that type-test an operand for an
/// identifier, a constant, a member read or <c>x % c</c> (<c>ModuloEqualityLane</c>, <c>BoxedNumericOperandLane</c>,
/// the comparison lanes) still match. <c>SumOfProductsLane</c> follows a sum's left operands and declines a sum
/// with a probe in it, which takes a sum nested exactly at a multiple of the interval, and costs only that lane.
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
    /// Builds <paramref name="operand"/> of an operator, behind a probe where <see cref="AtProbeDepth"/> puts one.
    /// </summary>
    /// <remarks>
    /// An operand that is itself an operator is built here rather than through <c>JintExpression.Build</c>, whose
    /// frame is sized for every kind of node: building a chain recurses once per level, and those frames are what a
    /// deep chain runs out of stack on while it is built. The result is the node <c>Build</c> would have made.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static JintExpression BuildOperand(Expression operand)
    {
        if (operand is not BinaryExpression binary || operand.UserData is JintExpression)
        {
            return AtProbeDepth(operand, Build(operand));
        }

        JintExpression built;
        using (SyntaxNesting.Enter())
        {
            built = binary is NonLogicalBinaryExpression nonLogical
                ? JintBinaryExpression.Build(nonLogical)
                : BuildLogical((LogicalExpression) binary);
        }

        return AtProbeDepth(operand, built);
    }

    private static JintExpression BuildLogical(LogicalExpression expression) => expression.Operator switch
    {
        Operator.LogicalAnd => new JintLogicalAndExpression(expression),
        Operator.LogicalOr => new JintLogicalOrExpression(expression),
        Operator.NullishCoalescing => new NullishCoalescingExpression(expression),
        _ => Build(expression),
    };

    /// <summary>
    /// Puts <paramref name="built"/>, the operand just built for the operator being built, behind a probe when the
    /// operator sits at a depth the probe interval divides and the operand continues the chain below it: it is an
    /// operator with an operator operand of its own.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static JintExpression AtProbeDepth(Expression operand, JintExpression built)
    {
        if (SyntaxNesting.IsEvaluationProbeDepth
            && operand is BinaryExpression { Left: BinaryExpression } or BinaryExpression { Right: BinaryExpression }
            && built is JintBinaryExpression or JintLogicalOrExpression or JintLogicalAndExpression or NullishCoalescingExpression)
        {
            return new JintStackProbeExpression(built);
        }

        return built;
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        if (MustMoveToFreshStack(context))
        {
            return context.Engine._stackGuard.RunOnEmptyStack(_operator.GetValue, context);
        }

        return _operator.GetValue(context);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (MustMoveToFreshStack(context))
        {
            return context.Engine._stackGuard.RunOnEmptyStack(_operator.EvaluateWithoutNodeTracking, context);
        }

        return _operator.EvaluateWithoutNodeTracking(context);
    }

    public override bool GetBooleanValue(EvaluationContext context)
    {
        if (MustMoveToFreshStack(context))
        {
            return context.Engine._stackGuard.RunOnEmptyStack(_operator.GetBooleanValue, context);
        }

        return _operator.GetBooleanValue(context);
    }

    internal override bool HasDiscardFastPath => _operator.HasDiscardFastPath;

    internal override void EvaluateAndDiscard(EvaluationContext context)
    {
        if (MustMoveToFreshStack(context))
        {
            // The value is discarded either way; GetValue is EvaluateAndDiscard's own default.
            context.Engine._stackGuard.RunOnEmptyStack(_operator.GetValue, context);
            return;
        }

        _operator.EvaluateAndDiscard(context);
    }

    internal override void EnableShortCircuitPropagation() => _operator.EnableShortCircuitPropagation();

    /// <summary>
    /// <see cref="JintCallExpression"/>'s probe: whether the stack-hopping lane wants a fresh stack, after which the
    /// backstop throws if the stack is still too low.
    /// </summary>
    internal static bool MustMoveToFreshStack(EvaluationContext context)
    {
        var stackGuard = context.Engine._stackGuard;
        if (!stackGuard.TryEnterOnCurrentStack())
        {
            return true;
        }

        stackGuard.EnsureNativeStackHeadroom();
        return false;
    }
}
