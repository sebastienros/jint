using Jint.Native;
using Jint.Native.Iterator;
using Jint.Runtime.Environments;

namespace Jint.Runtime;

/// <summary>
/// Stores the state of a for-of/for-in loop when a generator yields inside it.
/// Following the NiL.JS SuspendData pattern for iterator state preservation.
/// </summary>
internal sealed class ForOfSuspendData
{
    /// <summary>
    /// The iterator instance from HeadEvaluation that was in progress.
    /// </summary>
    public IteratorInstance Iterator { get; init; } = null!;

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
