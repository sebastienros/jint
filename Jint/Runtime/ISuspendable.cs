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
    /// The try statement whose finally block we're currently executing.
    /// Used to properly resume execution in finally blocks.
    /// </summary>
    /// <remarks>
    /// This is a dispatch hint only, and one slot is enough for it: the completion a finalizer
    /// interrupted is parked per try statement, in <see cref="Data"/>, because nested finalizers can
    /// each have one in flight. See <see cref="TrySuspendData.PendingCompletion"/>.
    /// </remarks>
    object? CurrentFinallyStatement { get; set; }

    /// <summary>
    /// Dictionary for tracking state of loops, destructuring, etc. during async execution.
    /// Keyed by Jint expression/statement instances (not AST nodes) to avoid collisions.
    /// </summary>
    SuspendDataDictionary Data { get; }
}
