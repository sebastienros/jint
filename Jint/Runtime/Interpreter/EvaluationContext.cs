using System.Runtime.CompilerServices;
using Jint.Constraints;
using Jint.Native.Generator;

namespace Jint.Runtime.Interpreter;

/// <summary>
/// Per Engine.Evaluate() call context.
/// </summary>
internal sealed class EvaluationContext
{
    /// <summary>
    /// How many statements may execute between checks of the amortized constraints (see
    /// Engine.Constraints.cs for the partition rationale). Small enough that timeout /
    /// cancellation detection latency stays far below anything observable at the granularity
    /// those constraints operate on, large enough that the per-statement cost collapses to a
    /// countdown decrement and branch. Nothing whose budget a single statement can blow past is
    /// checked at this cadence; see <see cref="Constraint.IsAmortizable"/>. Statements that call
    /// user CLR code re-check on return instead — see
    /// <see cref="Engine.CheckAmortizedConstraintsAtHostBoundary"/>.
    /// </summary>
    internal const int AmortizedConstraintCheckInterval = 64;

    private readonly bool _shouldRunPerStatementChecks;
    private readonly bool _bypassStatementFastPaths;
    private readonly bool _hasAmortizedConstraints;
    private readonly MaxStatementsConstraint? _statementCounter;

    public EvaluationContext(Engine engine)
    {
        Engine = engine;

        // Debug mode and coverage collection both need every executed statement to reach
        // RunPerStatementChecks - the debugger's step hook and the coverage counters both ride along
        // there - so both force the generic path and disarm the statement fast paths. Engine._isDebugMode
        // and Engine._coverage are settled before this context is built and never change afterwards, so
        // snapshotting them here is exact.
        _bypassStatementFastPaths = engine._isDebugMode || engine._coverage is not null;

        // A lone statement counter does not have to disarm the statement fast paths: it is charged
        // inline instead (see ChargeStatement), once per executed statement, at the same points
        // RunPerStatementChecks would have reached.
        _statementCounter = _bypassStatementFastPaths ? null : engine._inlineStatementCounter;
        _shouldRunPerStatementChecks = (engine._exactConstraints.Length > 0 || _bypassStatementFastPaths)
                                       && _statementCounter is null;
        _hasAmortizedConstraints = engine._amortizedConstraints.Length > 0;
    }

    // for fast evaluation checks only
    public EvaluationContext()
    {
        Engine = null!;
        _shouldRunPerStatementChecks = false;
        _bypassStatementFastPaths = false;
        _hasAmortizedConstraints = false;
        _statementCounter = null;
    }

    public readonly Engine Engine;
    public bool DebugMode => Engine._isDebugMode;

    /// <summary>
    /// Frozen per context (exact constraints or debug mode at creation); statement fast paths
    /// that skip <see cref="RunBeforeExecuteStatementChecks"/> must be gated on this AND keep
    /// amortized constraints live by driving <see cref="RunAmortizedConstraintChecks"/> at a
    /// bounded cadence (e.g. once per loop iteration), AND charge <see cref="ChargeStatement"/>
    /// once per statement they execute.
    /// </summary>
    internal bool ShouldRunPerStatementChecks => _shouldRunPerStatementChecks;

    /// <summary>
    /// Whether a statement whose result the interpreter can produce without executing it must be executed
    /// anyway. Set for debug mode (the debugger has to step onto the statement) and for coverage collection
    /// (the statement has to be counted). Frozen per context from engine state that is fixed for the engine's
    /// lifetime, so the read is one field rather than a hop through <see cref="Engine"/>.
    /// <para>
    /// The one such shortcut today is <see cref="JintStatementList"/>'s pre-resolved
    /// <c>return &lt;literal&gt;;</c>.
    /// </para>
    /// </summary>
    internal bool BypassStatementFastPaths => _bypassStatementFastPaths;

    /// <summary>
    /// Returns true if the generator is suspended (yielded) or a return was requested.
    /// This is the combined check that should be used after evaluating sub-expressions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsGeneratorAborted()
    {
        var generator = Engine.ExecutionContext.Generator;
        return generator is not null &&
               (generator._generatorState == GeneratorState.SuspendedYield || generator._returnRequested);
    }

    /// <summary>
    /// Returns true if execution is suspended (generator at yield or async function at await).
    /// Use this after evaluating expressions that may suspend.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSuspended() => Engine?.ExecutionContext.IsSuspended == true;

    public Node LastSyntaxElement
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Engine.GetLastSyntaxElement();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Engine._lastSyntaxElement = value;
    }

    /// <summary>
    /// Whether an operand may have a CLR operator behind it
    /// (<c>Options.Interop.AllowOperatorOverloading</c>). Read from the engine rather than
    /// snapshotted here: this context is built before <c>Options.Apply</c> runs the host's
    /// configuration callbacks, and one of those may still enable the option — the same reason
    /// <see cref="Engine._maxRecursionDepth"/> is read after Apply. The engine's field is the
    /// snapshot, fixed for its lifetime, so this stays a per-engine constant the way the call sites
    /// assume.
    /// <para>
    /// The null test is for the engine-less context above, which constant folding builds at
    /// preparation time and which has to answer <see langword="false"/> rather than throw.
    /// </para>
    /// </summary>
    public bool OperatorOverloadingAllowed => Engine is { _operatorOverloadingAllowed: true };

    /// <summary>
    /// Whether Normal-completion values of statements are observable in the current frame.
    /// True at script/module/eval top level (the value feeds Engine.Evaluate / eval results);
    /// false inside function bodies, where the spec only surfaces Return/Throw completions -
    /// letting expression statements skip materializing their value.
    /// Maintained by <see cref="JintStatementList.Execute"/> with save/restore semantics.
    /// </summary>
    public bool CompletionValuesObservable = true;

    public void RunBeforeExecuteStatementChecks(StatementOrExpression statement)
    {
        if (_shouldRunPerStatementChecks)
        {
            Engine.RunPerStatementChecks(statement);
        }
        else
        {
            // Mutually exclusive by construction: _statementCounter is only set when the exact list
            // collapsed to that one constraint, which is precisely when _shouldRunPerStatementChecks
            // is false. Charging in both branches would double-count.
            ChargeStatement();
        }

        RunAmortizedConstraintChecks();
    }

    /// <summary>
    /// Charges one executed statement against the inline statement counter, if there is one. Statement
    /// fast paths that bypass <see cref="RunBeforeExecuteStatementChecks"/> must call this exactly where
    /// the generic path would have run the check — once per non-block statement, plus once for a block
    /// whose contents run through a <see cref="JintStatementList"/> — so the statement at which
    /// <see cref="MaxStatementsConstraint"/> throws is the same with the fast path armed or disarmed.
    /// <see cref="MaxStatementsConstraint"/> is sealed, so this is a devirtualized, inlineable call, and a
    /// null field is a predictable branch when no counter is configured.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ChargeStatement()
    {
        _statementCounter?.Check();
    }

    /// <summary>
    /// The amortized slice of the before-statement checks: with only observation-only constraints
    /// registered (e.g. a timeout — the common embedder configuration) this is the whole
    /// per-statement cost, a countdown decrement and branch. The countdown is
    /// <em>per-engine</em> state (<see cref="Engine._amortizedConstraintCountdown"/>, which explains
    /// why), so detection latency stays bounded at <see cref="AmortizedConstraintCheckInterval"/>
    /// statements regardless of which call sites drive it, of how many top-level entries the host
    /// makes, and of how short each of them is.
    /// <para>
    /// The <see cref="_hasAmortizedConstraints"/> test must stay first: the parameterless
    /// constructor leaves <see cref="Engine"/> null and relies on it short-circuiting.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RunAmortizedConstraintChecks()
    {
        if (_hasAmortizedConstraints && --Engine._amortizedConstraintCountdown <= 0)
        {
            Engine._amortizedConstraintCountdown = AmortizedConstraintCheckInterval;
            Engine.CheckAmortizedConstraints();
        }
    }

    /// <summary>
    /// Establishes <paramref name="node"/> as the node an error would be reported against. This is
    /// the whole of the per-node ceremony: abrupt completions travel out of band (as a
    /// <see cref="JavaScriptException"/> or through <see cref="Engine._error"/>) and a break or
    /// continue label travels on the <see cref="Completion"/> it belongs to, so there is no
    /// completion state on the context left to reset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PrepareFor(Node node) => LastSyntaxElement = node;
}
