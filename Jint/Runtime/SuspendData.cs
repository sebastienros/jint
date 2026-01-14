using Jint.Native;
using Jint.Native.Iterator;
using Jint.Native.Object;
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
    /// Null for constructs that don't use iterators (e.g., regular for loops).
    /// </summary>
    public IteratorInstance? Iterator { get; set; }
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
    /// Whether the iterator has been exhausted (done=true).
    /// </summary>
    public bool Done { get; set; }
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
internal sealed class ForLoopSuspendData : SuspendData
{
    /// <summary>
    /// The saved values of loop variables (let bindings in for loop init).
    /// </summary>
    public Dictionary<Key, JsValue>? BoundValues { get; set; }
}

/// <summary>
/// Stores the state of a for-await-of loop when an async function awaits inside it.
/// </summary>
internal sealed class ForAwaitSuspendData : SuspendData
{
    /// <summary>
    /// The resolved iterator result from awaiting the next() Promise.
    /// </summary>
    public ObjectInstance? ResolvedIteratorResult { get; set; }

    /// <summary>
    /// The accumulated result value (v) from previous iterations.
    /// </summary>
    public JsValue AccumulatedValue { get; set; } = JsValue.Undefined;
}

/// <summary>
/// Stores the state of a do-while loop when an async function awaits inside it.
/// Saves block-scoped variable values so they can be restored when resuming.
/// </summary>
internal sealed class DoWhileLoopSuspendData : SuspendData
{
    /// <summary>
    /// The saved values of block-scoped variables (let/const bindings in loop body).
    /// </summary>
    public Dictionary<Key, JsValue>? BlockBoundValues { get; set; }
}

/// <summary>
/// Stores the state of a while loop when an async function awaits inside it.
/// Saves block-scoped variable values so they can be restored when resuming.
/// </summary>
internal sealed class WhileLoopSuspendData : SuspendData
{
    /// <summary>
    /// The saved values of block-scoped variables (let/const bindings in loop body).
    /// </summary>
    public Dictionary<Key, JsValue>? BlockBoundValues { get; set; }
}

internal sealed class SuspendDataDictionary
{
    /// <summary>
    /// Unified dictionary for all suspend data (for-of loops, destructuring patterns, etc.).
    /// </summary>
    private Dictionary<object, SuspendData>? _suspendData;

    /// <summary>
    /// Gets or creates suspend data of the specified type (for constructs without iterators).
    /// </summary>
    public T GetOrCreate<T>(object key, IteratorInstance? iteratorInstance = null) where T : SuspendData, new()
    {
        _suspendData ??= [];
        if (!_suspendData.TryGetValue(key, out var data))
        {
            data = new T
            {
                Iterator = iteratorInstance,
            };
            _suspendData[key] = data;
        }
        return (T) data;
    }

    /// <summary>
    /// Tries to get existing suspend data of the specified type.
    /// </summary>
    public bool TryGet<T>(object key, out T? data) where T : SuspendData
    {
        if (_suspendData?.TryGetValue(key, out var baseData) == true && baseData is T d)
        {
            data = d;
            return true;
        }
        data = null;
        return false;
    }

    /// <summary>
    /// Clears suspend data for the given key when the construct completes.
    /// </summary>
    public void Clear(object key)
    {
        _suspendData?.Remove(key);
    }
}
