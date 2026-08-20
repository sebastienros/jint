namespace Jint.Native.Promise;

/// <summary>
/// The specification's "upon fulfillment of <i>promise</i>, do ..." / "upon rejection of <i>promise</i>,
/// do ...": a reaction whose handlers produce no value and which carries no result promise, i.e.
/// <see href="https://tc39.es/ecma262/#sec-performpromisethen">PerformPromiseThen</see> with no
/// <c>resultCapability</c>.
/// </summary>
/// <remarks>
/// <para>
/// The handlers of an algorithm written in the specification's own prose are not observable from script —
/// no <c>Function.prototype.call</c> is looked up, no <c>name</c>/<c>length</c> descriptors are
/// materialized, and script can never get hold of the function object because there is none. An
/// <see cref="IPromiseContinuation"/> is exactly that, which is why <c>await</c> itself uses one, and why an
/// engine-internal algorithm that reaches for a <c>ClrFunction</c> handler pair is both allocating two
/// function objects per step and mis-describing what it is doing.
/// </para>
/// <para>
/// The job granularity is identical to a JavaScript handler's — one job per reaction — so substituting this
/// for a handler pair cannot move a microtask boundary.
/// </para>
/// </remarks>
internal sealed class VoidPromiseContinuation : IPromiseContinuation
{
    private readonly Action<JsValue>? _onFulfilled;
    private readonly Action<JsValue>? _onRejected;

    internal VoidPromiseContinuation(Action<JsValue>? onFulfilled, Action<JsValue>? onRejected)
    {
        _onFulfilled = onFulfilled;
        _onRejected = onRejected;
    }

    public void Invoke(Engine engine, JsValue value, ReactionType type)
    {
        if (type == ReactionType.Fulfill)
        {
            _onFulfilled?.Invoke(value);
        }
        else
        {
            _onRejected?.Invoke(value);
        }
    }
}
