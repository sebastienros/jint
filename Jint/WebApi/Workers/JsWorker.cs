#if NET8_0_OR_GREATER
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.WebApi.Workers;

/// <summary>
/// A <c>Worker</c> instance: the parent's handle on one worker, and the <see cref="JsEventTarget"/> its
/// <c>message</c> events are retargeted at.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#dedicated-workers-and-the-worker-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no <c>worker.port</c>, and that is HTML's shape rather than a simplification.</b> A dedicated
/// worker's two ports are unexposed: <c>Worker</c> includes the <c>MessageEventTarget</c> mixin, so this
/// object <i>is</i> the parent end of the channel — "all messages received by that port must immediately be
/// retargeted at the <c>Worker</c> object". A port a script could reach belongs to <c>SharedWorker</c>.
/// </para>
/// <para>
/// The object outlives the connection. A terminated, closed or failed worker leaves it alive and inert:
/// <c>postMessage</c> still serializes and then goes nowhere, <c>terminate()</c> does nothing, and no further
/// event fires — which is the state HTML's disentangled-port clause describes rather than a concession.
/// </para>
/// </remarks>
internal sealed class JsWorker : JsEventTarget
{
    /// <summary>https://html.spec.whatwg.org/multipage/indices.html#event-message.</summary>
    internal const string MessageEventType = "message";

    /// <summary>https://html.spec.whatwg.org/multipage/indices.html#event-messageerror.</summary>
    internal const string MessageErrorEventType = "messageerror";

    /// <summary>https://html.spec.whatwg.org/multipage/indices.html#event-error.</summary>
    internal const string ErrorEventType = "error";

    internal JsWorker(Engine engine, Realm realm) : base(engine, realm)
    {
        _prototype = realm.Intrinsics.Worker.PrototypeObject;
    }

    /// <summary>
    /// The connection this object is the parent end of. Assigned once, by the constructor, immediately after
    /// the object is created — it is a property rather than a constructor argument only because the link needs
    /// the object it retargets at.
    /// </summary>
    internal WorkerLink? Link { get; set; }
}
#endif
