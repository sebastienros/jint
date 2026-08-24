using Jint.Constraints;

namespace Jint.Native.Promise;

internal enum PromiseState
{
    Pending,
    Fulfilled,
    Rejected
}

internal enum ReactionType
{
    Fulfill,
    Reject
}

internal sealed record PromiseReaction(
    ReactionType Type,
    PromiseCapability? Capability,
    JsValue? Handler,
    IPromiseContinuation? Continuation = null,
    MemoryLimitConstraint.OperationState? MemoryState = null
);

/// <summary>
/// An engine-internal reaction target invoked directly by the promise reaction job in place of
/// a JS-callable handler. Used where the spec's handler closure can never be observed from user
/// code (await continuations and similar engine bookkeeping), so no JS function object, delegate
/// or name/length descriptors need to be materialized per reaction. Reactions carrying a
/// continuation never have a result capability.
/// </summary>
internal interface IPromiseContinuation
{
    void Invoke(Engine engine, JsValue value, ReactionType type);
}

internal sealed record ResolvingFunctions(
    Function.Function Resolve,
    Function.Function Reject
);

/// <summary>
/// The handle <see cref="Engine.AdvancedOperations.RegisterPromise"/> returns: a JavaScript promise plus the
/// two functions that settle it. Only the engine can hand one out — settling requires the resolving functions
/// the engine built for that particular promise, so a host-constructed instance could never mean anything.
/// </summary>
// Written out longhand rather than declared positionally purely so the constructor can be internal: a
// positional record takes no accessibility modifier on its primary constructor. The property, deconstruction
// and `with` surface below is exactly what a positional record would have generated, so "simplifying" this
// back to one silently re-exposes the public constructor.
public sealed record ManualPromise
{
    internal ManualPromise(JsValue promise, Action<JsValue> resolve, Action<JsValue> reject)
    {
        Promise = promise;
        Resolve = resolve;
        Reject = reject;
    }

    /// <summary>
    /// The promise handed to script.
    /// </summary>
    public JsValue Promise { get; init; }

    /// <summary>
    /// Fulfills <see cref="Promise"/> with the value passed to it.
    /// </summary>
    public Action<JsValue> Resolve { get; init; }

    /// <summary>
    /// Rejects <see cref="Promise"/> with the value passed to it.
    /// </summary>
    public Action<JsValue> Reject { get; init; }

    /// <summary>
    /// Deconstructs the handle into its three parts, as <c>var (promise, resolve, reject) = …</c>.
    /// </summary>
    public void Deconstruct(out JsValue Promise, out Action<JsValue> Resolve, out Action<JsValue> Reject)
    {
        Promise = this.Promise;
        Resolve = this.Resolve;
        Reject = this.Reject;
    }
}

/// <summary>
/// Internal version of ManualPromise that accepts CLR objects for thread-safe Task interop.
/// </summary>
internal sealed record ManualPromiseWithClrValue(
    JsValue Promise,
    Action<object?> Resolve,
    Action<object?> Reject
);

/// <summary>
/// https://tc39.es/ecma262/#sec-hostpromiserejectiontracker
/// Operation type for the HostPromiseRejectionTracker abstract operation.
/// </summary>
public enum PromiseRejectionOperation
{
    /// <summary>
    /// The promise was rejected without a handler.
    /// </summary>
    Reject,

    /// <summary>
    /// A handler was added to a previously unhandled rejected promise.
    /// </summary>
    Handle
}
