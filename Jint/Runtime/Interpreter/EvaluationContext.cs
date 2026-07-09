using System.Runtime.CompilerServices;
using Jint.Native.Generator;

namespace Jint.Runtime.Interpreter;

/// <summary>
/// Per Engine.Evaluate() call context.
/// </summary>
internal sealed class EvaluationContext
{
    private readonly bool _shouldRunBeforeExecuteStatementChecks;

    public EvaluationContext(Engine engine)
    {
        Engine = engine;
        OperatorOverloadingAllowed = engine.Options.Interop.AllowOperatorOverloading;
        _shouldRunBeforeExecuteStatementChecks = engine._constraints.Length > 0 || engine._isDebugMode;
    }

    // for fast evaluation checks only
    public EvaluationContext()
    {
        Engine = null!;
        OperatorOverloadingAllowed = false;
        _shouldRunBeforeExecuteStatementChecks = false;
    }

    public readonly Engine Engine;
    public bool DebugMode => Engine._isDebugMode;

    /// <summary>
    /// Frozen per context (constraints or debug mode at creation); statement fast paths that
    /// skip <see cref="RunBeforeExecuteStatementChecks"/> entirely must be gated on this.
    /// </summary>
    internal bool ShouldRunBeforeExecuteStatementChecks => _shouldRunBeforeExecuteStatementChecks;

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
        if (_shouldRunBeforeExecuteStatementChecks)
        {
            Engine.RunBeforeExecuteStatementChecks(statement);
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
