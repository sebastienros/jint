using Jint.Native;
using Jint.Native.Iterator;

namespace Jint.Runtime;

/// <summary>
/// Stores the state of an array destructuring pattern when a generator yields inside it.
/// When a generator yields during array destructuring (e.g., [x[yield]] = iterable),
/// the iterator must be preserved so it can be properly closed when the generator
/// completes or returns.
/// </summary>
internal sealed class DestructuringSuspendData
{
    /// <summary>
    /// The iterator instance from the destructuring that was in progress.
    /// </summary>
    public IteratorInstance Iterator { get; init; } = null!;

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
