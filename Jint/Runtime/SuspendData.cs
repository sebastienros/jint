using Jint.Native;
using Jint.Native.Iterator;
using Jint.Runtime.Environments;

namespace Jint.Runtime;

/// <summary>
/// Base class for generator suspension state.
/// Used to track iterator state when a generator yields inside constructs like for-of loops
/// or destructuring patterns.
/// </summary>
internal abstract class SuspendData
{
    /// <summary>
    /// The iterator instance that was in progress when the generator suspended.
    /// </summary>
    public IteratorInstance Iterator { get; init; } = null!;
}

/// <summary>
/// Stores the state of an array destructuring pattern when a generator yields inside it.
/// When a generator yields during array destructuring (e.g., [x[yield]] = iterable),
/// the iterator must be preserved so it can be properly closed when the generator
/// completes or returns.
/// </summary>
internal sealed class DestructuringSuspendData : SuspendData
{
    /// <summary>
    /// The current element index in the destructuring pattern.
    /// </summary>
    public uint ElementIndex { get; set; }

    /// <summary>
    /// Whether the iterator has been exhausted (done=true).
    /// </summary>
    public bool Done { get; set; }

    /// <summary>
    /// Values already retrieved from the iterator for each element position.
    /// Used when resuming to avoid calling next() again.
    /// </summary>
    public JsValue[]? RetrievedValues { get; set; }
}

/// <summary>
/// Stores the state of a for-of/for-in loop when a generator yields inside it.
/// </summary>
internal sealed class ForOfSuspendData : SuspendData
{
    /// <summary>
    /// The current value being processed (from TryIteratorStep).
    /// Needed when yield happens during destructuring or body execution.
    /// </summary>
    public JsValue? CurrentValue { get; set; }

    /// <summary>
    /// The accumulated result value (v) from previous iterations.
    /// </summary>
    public JsValue AccumulatedValue { get; set; } = JsValue.Undefined;

    /// <summary>
    /// The iteration environment for lexical bindings (let/const in for-of).
    /// </summary>
    public DeclarativeEnvironment? IterationEnv { get; set; }
}

/// <summary>
/// Stores the state of a regular for loop when a generator yields inside it.
/// Saves loop variable values so they can be restored when resuming.
/// </summary>
internal sealed class ForLoopSuspendData
{
    /// <summary>
    /// The saved values of loop variables (let bindings in for loop init).
    /// </summary>
    public Dictionary<Key, JsValue>? BoundValues { get; set; }

    /// <summary>
    /// The accumulated result value (v) from previous iterations.
    /// </summary>
    public JsValue AccumulatedValue { get; set; } = JsValue.Undefined;
}
