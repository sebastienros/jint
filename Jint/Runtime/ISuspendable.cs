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
