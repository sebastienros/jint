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
    /// Returns true if the generator is suspended (yielded).
    /// Use this when you only need to check for yield suspension without return request.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsGeneratorSuspended() => Engine.ExecutionContext.Suspended;

    public Node LastSyntaxElement
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Engine.GetLastSyntaxElement();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Engine._lastSyntaxElement = value;
    }

    public readonly bool OperatorOverloadingAllowed;

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
