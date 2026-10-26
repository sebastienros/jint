namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// Probes the native stack, then runs the <c>if</c> statement it stands in for. Built only into an
/// <c>else if</c> chain longer than <see cref="SyntaxNesting.EvaluationProbeInterval"/> arms, one per that many
/// arms, so a chain of ordinary length never contains one and runs exactly as it did.
/// </summary>
/// <remarks>
/// <para>
/// The statement counterpart of <see cref="Expressions.JintStackProbeExpression"/>, for the other shape a proxy
/// auto-config file is made of: running an <c>else if</c> chain recurses once per arm, and a function built at the
/// top of the stack can run from the bottom of a deep recursion, where the stack its call probed for is less than
/// a long chain needs.
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
        context.Engine._stackGuard.EnsureNativeStackHeadroom();
        return _wrapped.ExecuteWithoutStatementChecks(context);
    }

    internal override void ExecuteDiscarded(EvaluationContext context)
    {
        context.Engine._stackGuard.EnsureNativeStackHeadroom();
        _wrapped.ExecuteDiscarded(context);
    }
}
