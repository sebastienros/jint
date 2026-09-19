#if NET8_0_OR_GREATER
using Jint.Runtime;

namespace Jint.WebApi.Locks;

/// <summary>
/// One engine's standing in a <see cref="LockManager"/>: the <c>clientId</c> its requests and its held locks
/// are reported under, and the only route a manager has back into that engine.
/// </summary>
/// <remarks>
/// <para>
/// The Web Locks API is written against two things Jint does not have — an <i>agent</i> and an
/// <i>environment settings object</i> — and an engine is Jint's answer to both. So there is one agent per
/// engine, created the first time that engine reaches <c>navigator.locks</c>, and
/// <see cref="ClientId"/> is "environment's id": an opaque string, and the same value every
/// <c>query()</c> reports for every lock this engine holds. Two engines sharing a manager therefore report
/// two different ids, which is exactly what <c>web-locks/query.https.any.js</c> asks a window and its worker
/// for.
/// </para>
/// <para>
/// <b>The manager only ever calls <see cref="Schedule"/>.</b> A manager may be shared by engines running on
/// different threads, so the state changes happen inside the manager's own critical section while everything
/// JavaScript can see — invoking the callback, settling a promise — is an event-loop task on the engine that
/// owns it. That is the <c>BroadcastChannelBroker</c> discipline, and for the same reason: a
/// <c>JsValue</c> belongs to one engine and is touched on that engine's thread alone.
/// </para>
/// </remarks>
internal sealed class LockAgent
{
    private readonly Engine _engine;

    internal LockAgent(Engine engine)
    {
        _engine = engine;

        // The specification's own example writes them as UUIDs, and nothing but equality is ever asked of
        // one: a script compares two ids to learn whether two locks belong to one context.
        ClientId = Guid.NewGuid().ToString();
    }

    /// <summary>https://w3c.github.io/web-locks/#lock-clientid — "an opaque string".</summary>
    internal string ClientId { get; }

    /// <summary>
    /// The evaluation cycle work registered <i>now</i> belongs to, carried by every request and every held
    /// lock so that a grant arriving after a <c>RestoreGlobalSnapshot</c> is dropped at dequeue rather than
    /// run against the restored globals.
    /// </summary>
    internal int Generation => _engine.EventLoopGeneration;

    /// <summary>
    /// Queues one piece of JavaScript-visible work on the owning engine. <b>May be called from another
    /// engine's thread</b> — a release or a steal elsewhere is what grants a lock here — which is why it does
    /// nothing but enqueue.
    /// </summary>
    /// <remarks>
    /// A <see cref="EventLoopJobKind.Task"/> rather than a microtask, because the specification says so
    /// twice: the grant steps and the <c>ifAvailable</c> miss are both "enqueue the following steps on
    /// callback's relevant settings object's responsible event loop". The registration carries the cycle and
    /// deliberately no allocation budget, the way a <c>BroadcastChannel</c> or <c>MessagePort</c> delivery
    /// does: a manager a host shares between engines delivers for as long as those engines live, and each
    /// grant begins a budget of its own rather than accumulating against the request that started it.
    /// </remarks>
    internal void Schedule(int generation, Action work)
        => _engine.AddToEventLoop(work, generation, EventLoopJobKind.Task);
}
#endif
