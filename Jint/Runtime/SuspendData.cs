using Jint.Native.Iterator;

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
