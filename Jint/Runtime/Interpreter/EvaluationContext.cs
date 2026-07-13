using System.Runtime.CompilerServices;
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
    /// cancellation / memory-limit detection latency stays far below anything observable at
    /// the granularity those constraints operate on, large enough that the per-statement cost
    /// collapses to a countdown decrement and branch.
    /// </summary>
    internal const int AmortizedConstraintCheckInterval = 64;

    private readonly bool _shouldRunPerStatementChecks;
    private readonly bool _hasAmortizedConstraints;
    private int _amortizedConstraintCountdown;

    public EvaluationContext(Engine engine)
    {
        Engine = engine;
        OperatorOverloadingAllowed = engine.Options.Interop.AllowOperatorOverloading;
        _shouldRunPerStatementChecks = engine._exactConstraints.Length > 0 || engine._isDebugMode;
        _hasAmortizedConstraints = engine._amortizedConstraints.Length > 0;
        _amortizedConstraintCountdown = AmortizedConstraintCheckInterval;
    }

    // for fast evaluation checks only
    public EvaluationContext()
    {
        Engine = null!;
        OperatorOverloadingAllowed = false;
        _shouldRunPerStatementChecks = false;
        _hasAmortizedConstraints = false;
    }

    public readonly Engine Engine;
    public bool DebugMode => Engine._isDebugMode;

    /// <summary>
    /// Frozen per context (exact constraints or debug mode at creation); statement fast paths
    /// that skip <see cref="RunBeforeExecuteStatementChecks"/> must be gated on this AND keep
    /// amortized constraints live by driving <see cref="RunAmortizedConstraintChecks"/> at a
    /// bounded cadence (e.g. once per loop iteration).
    /// </summary>
    internal bool ShouldRunPerStatementChecks => _shouldRunPerStatementChecks;

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

    public readonly bool OperatorOverloadingAllowed;

    /// <summary>
    /// Whether Normal-completion values of statements are observable in the current frame.
    /// True at script/module/eval top level (the value feeds Engine.Evaluate / eval results);
    /// false inside function bodies, where the spec only surfaces Return/Throw completions -
    /// letting expression statements skip materializing their value.
    /// Maintained by <see cref="JintStatementList.Execute"/> with save/restore semantics.
    /// </summary>
    public bool CompletionValuesObservable = true;

    // completion record information
    public string? Target;
    public CompletionType Completion;

    public void RunBeforeExecuteStatementChecks(StatementOrExpression statement)
    {
        if (_shouldRunPerStatementChecks)
        {
            Engine.RunPerStatementChecks(statement);
        }

        RunAmortizedConstraintChecks();
    }

    /// <summary>
    /// The amortized slice of the before-statement checks: with only observation-only constraints
    /// registered (e.g. a timeout — the common embedder configuration) this is the whole
    /// per-statement cost, a countdown decrement and branch. The countdown is per-context state,
    /// so detection latency stays bounded at <see cref="AmortizedConstraintCheckInterval"/>
    /// statements regardless of which call sites drive it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RunAmortizedConstraintChecks()
    {
        if (_hasAmortizedConstraints && --_amortizedConstraintCountdown == 0)
        {
            _amortizedConstraintCountdown = AmortizedConstraintCheckInterval;
            Engine.CheckAmortizedConstraints();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PrepareFor(Node node)
    {
        LastSyntaxElement = node;
        Target = null;
        Completion = CompletionType.Normal;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAbrupt() => Completion != CompletionType.Normal;
}
