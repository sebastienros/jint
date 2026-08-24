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
/// <remarks>
/// <see cref="Resolve"/> and <see cref="Reject"/> take a <b>CLR</b> value, and the conversion to a
/// <see cref="JsValue"/> runs on the engine's thread as part of the settlement job rather than on the thread
/// that called them. That is what the parameter type is for: a host settling from a
/// <see cref="System.Threading.Tasks.Task"/> continuation holds a CLR value and has no safe place to convert
/// it, because a <see cref="JsValue"/> belongs to the engine that built it and that engine may be busy.
/// Passing a <see cref="JsValue"/> is still correct and costs nothing extra — conversion returns it
/// unchanged — and <see langword="null"/> settles with <see cref="JsValue.Null"/>.
/// </remarks>
// Written out longhand rather than declared positionally purely so the constructor can be internal: a
// positional record takes no accessibility modifier on its primary constructor. The property, deconstruction
// and `with` surface below is exactly what a positional record would have generated, so "simplifying" this
// back to one silently re-exposes the public constructor.
public sealed record ManualPromise
{
    internal ManualPromise(JsValue promise, Action<object?> resolve, Action<object?> reject)
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
    /// Fulfills <see cref="Promise"/> with the value passed to it, converted on the engine's thread.
    /// May be called from any thread.
    /// </summary>
    public Action<object?> Resolve { get; init; }

    /// <summary>
    /// Rejects <see cref="Promise"/> with the value passed to it, converted on the engine's thread.
    /// May be called from any thread.
    /// </summary>
    public Action<object?> Reject { get; init; }

    /// <summary>
    /// Deconstructs the handle into its three parts, as <c>var (promise, resolve, reject) = …</c>.
    /// </summary>
    public void Deconstruct(out JsValue Promise, out Action<object?> Resolve, out Action<object?> Reject)
    {
        Promise = this.Promise;
        Resolve = this.Resolve;
        Reject = this.Reject;
    }
}

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
