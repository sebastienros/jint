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
    PromiseCapability Capability,
    JsValue Handler
);

internal sealed record ResolvingFunctions(
    Function.Function Resolve,
    Function.Function Reject
);

public sealed record ManualPromise(
    JsValue Promise,
    Action<JsValue> Resolve,
    Action<JsValue> Reject
);
