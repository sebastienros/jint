using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// Probes the native stack, then runs the <c>if</c> statement it stands in for. Built only into an
/// <c>else if</c> chain longer than <see cref="SyntaxNesting.EvaluationProbeInterval"/> arms, one per that many
/// arms, so a chain of ordinary length never contains one and runs exactly as it did.
/// </summary>
/// <remarks>
/// <para>
/// The statement counterpart of <see cref="JintStackProbeExpression"/>, for the other shape a proxy auto-config file
/// is made of. Running an <c>else if</c> chain recurses once per arm, and the hoisting walk does not count those arms
/// against its fixed limit, so the chain has to bound itself. It probes the way a call expression does: with
/// <c>MaxExecutionStackCount</c> set, a low stack moves the statement to a fresh one; otherwise the backstop throws
/// a catchable <c>RangeError</c>.
/// </para>
/// <para>
/// It is transparent. <see cref="JintStatement.Execute"/> runs the per-statement checks once, for the node both
/// handlers share, and the wrapped statement then runs without them; the completion is the wrapped statement's.
/// </para>
/// </remarks>
internal sealed class JintStackProbeStatement : JintStatement
{
    private readonly JintStatement _wrapped;

    internal JintStackProbeStatement(JintStatement wrapped) : base(wrapped._statement)
    {
        _wrapped = wrapped;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        if (JintStackProbeExpression.MustMoveToFreshStack(context))
        {
            return context.Engine._stackGuard.RunOnEmptyStack(_wrapped.ExecuteWithoutStatementChecks, context);
        }

        return _wrapped.ExecuteWithoutStatementChecks(context);
    }

    internal override void ExecuteDiscarded(EvaluationContext context)
    {
        if (JintStackProbeExpression.MustMoveToFreshStack(context))
        {
            context.Engine._stackGuard.RunOnEmptyStack(ExecuteDiscardedOnFreshStack, context);
            return;
        }

        _wrapped.ExecuteDiscarded(context);
    }

    private bool ExecuteDiscardedOnFreshStack(EvaluationContext context)
    {
        _wrapped.ExecuteDiscarded(context);
        return true;
    }
}
