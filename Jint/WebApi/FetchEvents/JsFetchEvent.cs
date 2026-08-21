#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.FetchEvents;

/// <summary>
/// A <c>FetchEvent</c> instance: the inbound request as script sees it, plus the two operations that answer
/// it and that keep it alive.
/// <para>
/// https://w3c.github.io/ServiceWorker/#fetchevent-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>It extends <c>Event</c> directly.</b> The Service Workers Standard interposes <c>ExtendableEvent</c>
/// (https://w3c.github.io/ServiceWorker/#extendableevent-interface), whose sole member is
/// <c>waitUntil()</c>, and puts the lifetime machinery there so that <c>install</c> and <c>activate</c> can
/// share it. Jint has neither of those events and no service worker lifecycle for them to extend, so an
/// interface object whose only instances would be <c>FetchEvent</c>s would be a name with nothing behind it:
/// <c>waitUntil</c> lives on <c>FetchEvent.prototype</c> instead and there is no <c>ExtendableEvent</c>
/// global. This is the flat shape Cloudflare Workers exposes, and the one consequence a script can observe is
/// that <c>event instanceof ExtendableEvent</c> cannot be written — <c>instanceof FetchEvent</c> and
/// <c>instanceof Event</c> both hold.
/// </para>
/// <para>
/// <b>Five attributes are absent rather than faked</b>: <c>preloadResponse</c> (navigation preload is a
/// registration feature), <c>clientId</c>, <c>resultingClientId</c> and <c>replacesClientId</c> (there are no
/// service worker clients here, and answering <c>""</c> would let a script believe it had asked), and
/// <c>handled</c> (a promise about a fetch this engine does not perform). The <b>timed out flag</b> is absent
/// too: it is set "after an optional user agent imposed delay", and the delay exists to stop a service worker
/// being kept alive forever — here the host owns the engine's lifetime and pumps it, so there is nothing for
/// the engine to impose a deadline on. <see cref="IsActive"/> is therefore the specification's definition with
/// that flag read as permanently unset.
/// </para>
/// </remarks>
internal sealed class JsFetchEvent : JsEvent
{
    private readonly Realm _realm;

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#extendableevent-pending-promises-count — "the number of pending
    /// promises in the extend lifetime promises". The promises themselves are not kept: nothing here ever
    /// enumerates them, so counting them is the whole of what the list is for.
    /// </summary>
    private int _pendingPromises;

    internal JsFetchEvent(Engine engine, Realm realm, JsString type, EventInit init, double timeStamp, JsRequest request)
        : base(engine, type, init, timeStamp)
    {
        _realm = realm;
        Request = request;
    }

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-fetchevent-request. <c>[SameObject]</c> in the IDL, which is
    /// what holding it in a field rather than rebuilding it per read gives for free.
    /// </summary>
    internal JsRequest Request { get; }

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#fetch-event-respond-with-entered-flag — set by the first
    /// <c>respondWith()</c>, which is what makes a second one an <c>InvalidStateError</c>, and what the host
    /// reads to tell "a listener answered" from "nobody did".
    /// </summary>
    internal bool RespondWithEntered { get; private set; }

    /// <summary>
    /// What <c>respondWith()</c> was given, after the <c>Promise&lt;Response&gt;</c> conversion. This is Jint's
    /// stand-in for the specification's <i>wait to respond flag</i> plus the reaction it installs on <c>r</c>:
    /// there the flag gates the Handle Fetch algorithm, here the host's
    /// <c>FetchHandlerOperation</c> is what settles from this promise, and both amount to "the response is
    /// whatever this settles to".
    /// </summary>
    internal JsPromise? ResponsePromise { get; private set; }

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#extendableevent-active: "active when its timed out flag is unset
    /// and either its pending promises count is greater than zero or its dispatch flag is set". The timed out
    /// flag does not exist here (see the class remarks), so this is the other two terms.
    /// </summary>
    internal bool IsActive => _pendingPromises > 0 || DispatchFlag;

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#fetch-event-respondwith — <c>respondWith(r)</c>, whose steps are
    /// followed here in the order they are written, because the order is observable: a second call on an event
    /// that is no longer being dispatched reports the dispatch flag rather than the entered flag.
    /// </summary>
    internal void RespondWith(JsValue r)
    {
        // Step 2.
        if (!DispatchFlag)
        {
            ThrowInvalidState("respondWith", "the event is not being dispatched. respondWith() must be called synchronously from a 'fetch' listener.");
        }

        // Step 3.
        if (RespondWithEntered)
        {
            ThrowInvalidState("respondWith", "respondWith() has already been called on this event.");
        }

        // Step 4: "Add lifetime promise r to event." The note says it plainly — respondWith() extends the
        // lifetime of the event by default as if waitUntil(r) had been called — so the response promise is a
        // lifetime promise like any other, which is what keeps the event active while it is outstanding and
        // therefore what lets a listener call waitUntil() from inside its own .then().
        //
        // Its rejection is deliberately not reported: it is the one lifetime promise whose failure the host
        // already learns about, as the operation's PromiseRejectedException.
        var promise = AddLifetimePromise(r, reportRejection: false, "respondWith");

        // Step 5. This is why a second listener does not run after the first has answered, and why the
        // bubbling pass is skipped: the first responder wins, and the rest of the dispatch is pointless.
        StopPropagationFlag = true;
        StopImmediatePropagationFlag = true;

        // Steps 6 and 7.
        RespondWithEntered = true;
        ResponsePromise = promise;
    }

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#dom-extendableevent-waituntil — "the waitUntil(f) method steps are
    /// to add lifetime promise f to this", and nothing else.
    /// </summary>
    internal void WaitUntil(JsValue f) => AddLifetimePromise(f, reportRejection: true, "waitUntil");

    /// <summary>
    /// https://w3c.github.io/ServiceWorker/#extendableevent-add-lifetime-promise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Step 1 — <b>an untrusted event cannot be extended</b> — is implemented rather than dropped, and it is
    /// what a script constructing its own <c>FetchEvent</c> and dispatching it runs into: only the event the
    /// engine created for an inbound request can be responded to. That is the same conclusion a browser
    /// reaches, and here it is also the property that keeps the host's operation honest, since the operation
    /// settles from an event nothing else could have produced.
    /// </para>
    /// <para>
    /// Step 5's decrement runs in "a microtask queued by the reaction to the promise", which is exactly what a
    /// promise reaction job is. What Jint does <b>not</b> inherit is what the specification does when the count
    /// reaches zero: that ends a service worker's extended lifetime, and here there is no worker to end — the
    /// host owns the engine and stops pumping it when it likes.
    /// </para>
    /// </remarks>
    /// <param name="value">The value to extend the lifetime with, before the <c>Promise</c> conversion.</param>
    /// <param name="reportRejection">
    /// Whether a rejection of this promise may be reported. True for <c>waitUntil()</c>, whose work nobody is
    /// awaiting: the reaction installed here <i>handles</i> the rejection, so without this the failure would
    /// vanish instead of reaching <c>unhandledrejection</c> and the host's <see cref="DiagnosticsSink"/> — the
    /// promise would simply never have looked unhandled to the rejection tracker. False for
    /// <c>respondWith()</c>, whose rejection is the operation's to report and would be noise a second time.
    /// <para>
    /// Even when true it is reported only for a promise this reaction is the <b>first</b> handler of and which
    /// is still <b>pending</b>, because those are exactly the promises the tracker will now never see: one that
    /// has already rejected has been through the tracker as this very statement ran
    /// (<c>waitUntil(Promise.reject(e))</c>), and one that already has a handler belongs to whoever attached
    /// it. Announcing either again would double-report a failure rather than rescue a lost one.
    /// </para>
    /// </param>
    /// <param name="operationName">
    /// Which member is asking, so that the two <c>InvalidStateError</c>s name the call the script actually
    /// made rather than the internal algorithm both of them run.
    /// </param>
    private JsPromise AddLifetimePromise(JsValue value, bool reportRejection, string operationName)
    {
        // Step 1.
        if (!IsTrusted)
        {
            ThrowInvalidState(operationName, "the event was not created by the engine. Only the 'fetch' event dispatched for an inbound request can be responded to or extended.");
        }

        // Step 2. The note is the behaviour worth knowing: once nothing is outstanding and the dispatch is
        // over, a later call throws rather than silently extending an event nobody is waiting for.
        if (!IsActive)
        {
            ThrowInvalidState(operationName, "the event is no longer active: its dispatch is over and none of its lifetime promises is still pending.");
        }

        // The `Promise<T>` conversion, https://webidl.spec.whatwg.org/#es-promise: any value becomes a
        // promise, so a listener may hand over a Response directly. PromiseResolve answers with the argument
        // itself only when it is already a promise whose constructor is %Promise%, and otherwise with a fresh
        // one — either way a JsPromise, which is what the reaction below needs.
        var promise = (JsPromise) _realm.Intrinsics.Promise.PromiseResolve(value);

        // Decided before PerformPromiseThen, which is what changes both of these facts. See the parameter's
        // own documentation for why each term is there.
        var announce = reportRejection && !promise.PromiseIsHandled && promise.State == PromiseState.Pending;

        // Step 4, before the reaction is installed: "The pending promises count is incremented even if the
        // given promise has already been settled."
        _pendingPromises++;

        var onFulfilled = new ClrFunction(_engine, "", (_, _) =>
        {
            _pendingPromises--;
            return Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(_engine, "", (_, _) =>
        {
            _pendingPromises--;

            if (announce)
            {
                _engine._webApi?.ReportPromiseRejection(promise, PromiseRejectionOperation.Reject);
            }

            return Undefined;
        }, 1, PropertyFlag.Configurable);

        // Step 5. Reactions rather than polling, for the reason FetchHandlerOperation gives: attaching them is
        // also what marks the promise handled, so a lifetime promise cannot be counted twice — once by the
        // rejection tracker and once here.
        PromiseOperations.PerformPromiseThen(_engine, promise, onFulfilled, onRejected, resultCapability: null);

        return promise;
    }

    /// <summary>
    /// The <c>InvalidStateError</c> both operations raise, as a JavaScript exception the listener can catch —
    /// the same shape <c>AbortSignal</c> and <c>dispatchEvent</c> raise theirs in.
    /// </summary>
    private void ThrowInvalidState(string operationName, string detail)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(
            DomExceptionNames.InvalidState,
            $"Failed to execute '{operationName}' on 'FetchEvent': {detail}");

        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }
}

/// <summary>
/// The one event type this feature fires, interned once — the sibling of
/// <c>Jint.WebApi.GlobalEvents.GlobalEventNames</c>.
/// </summary>
internal static class FetchEventNames
{
    internal const string FetchName = "fetch";

    internal static readonly JsString Fetch = new(FetchName);
}
#endif
