using Jint.Native;

namespace Jint.Runtime;

/// <summary>
/// Interface for entities that can suspend execution (generators, async functions).
/// Aligns with TC39 execution context suspension semantics.
/// </summary>
internal interface ISuspendable
{
    /// <summary>
    /// Whether this suspendable is currently in a suspended state.
    /// For generators: SuspendedStart or SuspendedYield
    /// For async functions: SuspendedAwait
    /// </summary>
    bool IsSuspended { get; }

    /// <summary>
    /// Whether we are resuming from a suspended state.
    /// </summary>
    bool IsResuming { get; set; }

    /// <summary>
    /// The value yielded/awaited when suspended.
    /// For generators: the yielded value
    /// For async functions: the resume value from awaited promise
    /// </summary>
    JsValue? SuspendedValue { get; }

    /// <summary>
    /// The AST node where execution last suspended (yield or await expression).
    /// Unified property for tracking suspension location across all suspendable types.
    /// </summary>
    object? LastSuspensionNode { get; }

    /// <summary>
    /// Signals that an early return was requested (e.g., generator.return() was called).
    /// For generators: true when generator.return() was called
    /// For async functions/generators: typically false
    /// </summary>
    bool ReturnRequested { get; }

    /// <summary>
    /// The completion type used when resuming (Normal, Return, Throw).
    /// Used by yield/await expressions to determine how to handle resumption.
    /// </summary>
    CompletionType ResumeCompletionType { get; }

    /// <summary>
    /// Tracks the pending completion type when suspended in a finally block.
    /// </summary>
    CompletionType PendingCompletionType { get; set; }

    /// <summary>
    /// Tracks the pending completion value when suspended in a finally block.
    /// </summary>
    JsValue? PendingCompletionValue { get; set; }

    /// <summary>
    /// The try statement whose finally block we're currently executing.
    /// Used to properly resume execution in finally blocks.
    /// </summary>
    object? CurrentFinallyStatement { get; set; }

    /// <summary>
    /// Gets or creates suspend data of the specified type (for constructs like for loops).
    /// Keys should be Jint expression/statement instances to avoid collisions across engines.
    /// </summary>
    T GetOrCreateSuspendData<T>(object key) where T : SuspendData, new();

    /// <summary>
    /// Tries to get existing suspend data of the specified type.
    /// Returns true if suspend data exists for the given key.
    /// </summary>
    bool TryGetSuspendData<T>(object key, out T? data) where T : SuspendData;

    /// <summary>
    /// Clears suspend data for the given key when the construct completes.
    /// </summary>
    void ClearSuspendData(object key);
}
