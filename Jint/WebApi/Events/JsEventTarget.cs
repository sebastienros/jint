#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.GlobalEvents;

namespace Jint.WebApi.Events;

/// <summary>
/// An <c>EventTarget</c> instance: a list of event listeners, plus the dispatch algorithm that runs them.
/// <para>
/// https://dom.spec.whatwg.org/#interface-eventtarget
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Dispatch is flat unless a host supplies a tree.</b> The specification's dispatch algorithm walks an
/// <i>event path</i> built from the target's ancestors in a node tree; every target this engine ships is
/// tree-less, and the specification itself says that is the normal case — "all author-created EventTargets do
/// not participate in a tree structure". For those the path is the single item «target», which is exactly
/// what the algorithm produces for a tree-less target, and every step that survives that reduction is
/// implemented here rather than approximated:
/// </para>
/// <list type="bullet">
/// <item><description>
/// the two passes are still both run, so <c>eventPhase</c> is <c>AT_TARGET</c> throughout and a
/// <c>capture: true</c> listener runs in the first pass and a <c>capture: false</c> one in the second —
/// which means capturing listeners on one target run <i>before</i> non-capturing ones whatever order they
/// were registered in, precisely as a browser behaves for a listener-only target;
/// </description></item>
/// <item><description>
/// <c>stopPropagation()</c> ends the dispatch after the pass that called it (step 5 of <i>invoke</i>),
/// while <c>stopImmediatePropagation()</c> additionally breaks out of the pass it was called in;
/// </description></item>
/// <item><description>
/// the listener list is cloned before each pass, so a listener added during dispatch does not run, and one
/// removed during dispatch does not either — the <c>removed</c> field is what makes the second true;
/// </description></item>
/// <item><description>
/// a <c>once</c> listener is removed <i>before</i> it is invoked, so a callback that throws still cannot run
/// twice.
/// </description></item>
/// </list>
/// <para>
/// <b>A host that has a tree gets the whole algorithm instead.</b> A wrapper that reports
/// <see cref="IsNode"/> dispatches through <see cref="EventDispatch"/>, which builds the path from
/// <see cref="GetParent"/> and runs the capture, target and bubble phases over it with retargeting,
/// <c>composedPath()</c> and activation behaviour. The seams a DOM overrides are listed on that class; every
/// one of them has a default that keeps a tree-less target on the reduction above, so nothing here changes
/// for a target that overrides none.
/// </para>
/// <para>
/// <b>What a throwing listener does depends on whether the host set a
/// <see cref="DiagnosticsSink"/>.</b> The specification says to <i>report</i> the exception and carry on to
/// the next listener (inner invoke step 2.10), which presumes a global error-reporting channel; with a sink
/// that channel exists, so the <c>JavaScriptException</c> is reported as a
/// <see cref="DiagnosticEventKind.UncaughtCallbackError"/> and the dispatch continues, exactly as specified.
/// With no sink there is nowhere to report to and swallowing the exception would lose it entirely, so it
/// propagates instead — out of <c>dispatchEvent</c> for a script-driven dispatch, out of
/// <c>controller.abort()</c> for an <c>abort</c> event, out of the event-loop pump when the abort came from
/// <c>AbortSignal.timeout()</c>. Only a <c>JavaScriptException</c> is ever reported: a constraint failure is
/// a <c>JintException</c> but not one of those, so it erupts whatever the host configured. The dispatch state
/// is unwound in every case, so the event and the target stay usable.
/// </para>
/// <para>
/// Reporting it is HTML's <i>report an exception</i>, so on an engine with
/// <see cref="WebApiFeatures.GlobalEvents"/> it also fires an <c>error</c> event at the global scope — which
/// is where the re-entrancy rule bites, because the listener that just threw may itself have been running as
/// part of a report. <c>Jint.WebApi.GlobalEvents.GlobalEventTarget</c> is what declines the second dispatch.
/// </para>
/// </remarks>
internal class JsEventTarget : ObjectInstance
{
    private static readonly JsString _handleEvent = new("handleEvent");

    internal readonly Realm _realm;

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-event-listener-list. Allocated on the first
    /// <c>addEventListener</c>, so a target nobody listens to costs one null field.
    /// </summary>
    private List<EventListenerRegistration>? _listeners;

    internal JsEventTarget(Engine engine, Realm realm) : base(engine, ObjectClass.Object)
    {
        _realm = realm;
    }

    /// <summary>
    /// What a dispatch reports as https://dom.spec.whatwg.org/#dom-event-target and
    /// https://dom.spec.whatwg.org/#dom-event-currenttarget, and therefore the <c>this</c> a listener is
    /// invoked with.
    /// </summary>
    /// <remarks>
    /// The target itself for every <c>EventTarget</c> a script can reach, which is what the specification
    /// says. The one override is the engine's synthetic global target, whose listener list is not reachable
    /// as an object at all: it answers with the global object, so a global <c>error</c> listener sees the
    /// <c>event.target</c> and the <c>this</c> a browser gives it rather than an object script has no other
    /// way to name.
    /// </remarks>
    internal virtual JsValue EventTargetValue => this;

    /// <summary>
    /// Whether this target is the engine's global scope — WebIDL's <c>WindowOrWorkerGlobalScope</c>, which in
    /// Jint is the synthetic global target and nothing else.
    /// </summary>
    /// <remarks>
    /// Read by two rules. HTML's <i>special error event handling</i>, which is what makes a global
    /// <c>onerror</c> take five arguments and cancel by returning <see langword="true"/> where every other
    /// event handler takes the event and cancels by returning <see langword="false"/> — see
    /// <see cref="InvokeCallback"/>. And dispatch's "parent is a <c>Window</c> object" branch
    /// (https://dom.spec.whatwg.org/#concept-event-dispatch step 6.9.5), which is what puts the global scope
    /// on the path of an event travelling up from a document without asking whether it is a node: this
    /// engine's global target is the only <c>Window</c> a tree can reach, and a worker's global is never a
    /// node's ancestor because a worker has no node tree.
    /// </remarks>
    internal virtual bool IsGlobalScope => false;

    /// <summary>
    /// Whether this target is a <i>node</i> — the thing https://dom.spec.whatwg.org/#concept-event-dispatch
    /// builds an event path over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// False for every target this engine ships, and that is what makes the tree-less dispatch the default:
    /// <see cref="DispatchEvent"/> reads this one virtual and, when it is false, runs the reduction of the
    /// algorithm to the single-item path «target», which allocates no path list and behaves exactly as it did
    /// before the tree existed. An <c>AbortSignal</c>, a <c>MessagePort</c>, an <c>EventSource</c>, a
    /// <c>WebSocket</c>, a <c>Worker</c>, a <c>BroadcastChannel</c> and the synthetic global target are all
    /// tree-less and stay on that path.
    /// </para>
    /// <para>
    /// <b>A host that overrides any other seam on this class must override this too.</b> The tree walk, the
    /// shadow queries and the activation hooks below are consulted only on the path dispatch, and this is the
    /// single read that chooses it — a wrapper that answered <see langword="false"/> while overriding
    /// <see cref="GetParent"/> would silently dispatch to itself alone.
    /// </para>
    /// </remarks>
    internal virtual bool IsNode => false;

    /// <summary>
    /// https://dom.spec.whatwg.org/#get-the-parent — the target the event travels to next, or
    /// <see langword="null"/> to end the path.
    /// </summary>
    /// <remarks>
    /// "Unless specified otherwise it returns null", which is the whole of the base implementation. A DOM
    /// overrides it three times: a node answers its parent or, when it is assigned, its assigned slot; a
    /// shadow root answers its host unless the event's composed flag is unset and the shadow root is the
    /// event's target's root; a document answers the global scope for every event except <c>load</c>.
    /// </remarks>
    internal virtual JsEventTarget? GetParent(JsEvent ev) => null;

    /// <summary>
    /// This target's parent in the node tree, ignoring the event — what
    /// https://dom.spec.whatwg.org/#concept-tree-parent means, and what the ancestor walks behind retargeting
    /// and <see cref="GetRoot"/> are written in terms of.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="GetParent"/> because the two answer different questions: the event path
    /// crosses a slot and a shadow boundary, the tree does not, and retargeting has to be able to ask where a
    /// node <i>is</i> rather than where an event would go next.
    /// </remarks>
    internal virtual JsEventTarget? TreeParent => null;

    /// <summary>Whether this target is a shadow root — https://dom.spec.whatwg.org/#interface-shadowroot.</summary>
    internal virtual bool IsShadowRoot => false;

    /// <summary>
    /// Whether this target is a shadow root whose <c>mode</c> is <c>"closed"</c>, which is what
    /// <c>composedPath()</c> hides an inside from an outside listener by.
    /// </summary>
    internal virtual bool IsClosedShadowRoot => false;

    /// <summary>
    /// The host of this shadow root — https://dom.spec.whatwg.org/#concept-documentfragment-host — or
    /// <see langword="null"/> for anything that is not one.
    /// </summary>
    internal virtual JsEventTarget? ShadowHost => null;

    /// <summary>
    /// Whether this target is a <c>slot</c> element, which is the assertion dispatch makes when a slottable's
    /// path reaches its assigned slot.
    /// </summary>
    internal virtual bool IsSlot => false;

    /// <summary>
    /// This slottable's assigned slot — https://dom.spec.whatwg.org/#slotable-assigned-slot — or
    /// <see langword="null"/> when it has none, which is also the answer for anything that is not a slottable.
    /// </summary>
    internal virtual JsEventTarget? AssignedSlot => null;

    /// <summary>
    /// Whether this target has an https://dom.spec.whatwg.org/#eventtarget-activation-behavior, so a dispatch
    /// of an activation event may pick it as the activation target.
    /// </summary>
    internal virtual bool HasActivationBehavior => false;

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-activation-behavior, run after the listeners when the event
    /// was not canceled.
    /// </summary>
    internal virtual void ActivationBehavior(JsEvent ev)
    {
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-legacy-pre-activation-behavior, run <i>before</i> any listener
    /// so that a listener sees the checkbox already toggled.
    /// </summary>
    internal virtual void LegacyPreActivationBehavior()
    {
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-legacy-canceled-activation-behavior, which undoes what
    /// <see cref="LegacyPreActivationBehavior"/> did when the event turns out to have been canceled.
    /// </summary>
    internal virtual void LegacyCanceledActivationBehavior()
    {
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-tree-root — the topmost <see cref="TreeParent"/>, which is this
    /// target itself when it has none.
    /// </summary>
    internal JsEventTarget GetRoot()
    {
        var root = this;
        while (root.TreeParent is { } parent)
        {
            root = parent;
        }

        return root;
    }

    /// <summary>
    /// Whether one dispatch of <paramref name="type"/> could invoke anything at all. The engine asks before
    /// <i>building</i> a trusted event, so a target nobody listens to costs a walk of an empty list rather
    /// than an object nothing would have read.
    /// </summary>
    internal bool HasListenerOfType(string type)
    {
        if (_listeners is not { } listeners)
        {
            return false;
        }

        foreach (var listener in listeners)
        {
            if (!listener.Removed && string.Equals(listener.Type, type, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-listener-add. The <i>default passive value</i> is always
    /// false here: it is only ever true for touch and wheel events on a <c>Window</c> or a document node,
    /// none of which exists in this engine.
    /// </summary>
    internal void AddListener(EventListenerRegistration listener)
    {
        // Step 2: a listener whose signal is already aborted is never added at all.
        if (listener.Signal is { Aborted: true })
        {
            return;
        }

        // Step 3: a null callback is not an error, it is simply nothing to add.
        if (listener.Callback.IsNull())
        {
            return;
        }

        var listeners = _listeners ??= new List<EventListenerRegistration>();

        // Step 5: a duplicate (type, callback, capture) registration is ignored — which is what makes the
        // list unable to hold two equal listeners, and is why removeEventListener can stop at the first hit.
        // An event-handler registration (onabort) is exempt: its callback is HTML's event handler processing
        // algorithm rather than the value the script assigned, so it can never collide with one of these.
        if (!listener.IsEventHandler && FindListener(listeners, listener.Type, listener.Callback, listener.Capture) >= 0)
        {
            return;
        }

        listeners.Add(listener);

        // Step 6: the signal removes the listener when it is aborted. The algorithm is remembered on the
        // registration so that removeEventListener can take it off the signal again — the specification
        // leaves it there, but a long-lived signal would then retain every target that ever borrowed it.
        if (listener.Signal is { } signal)
        {
            var algorithm = new Action(() => RemoveListener(listener));
            listener.AbortAlgorithm = algorithm;
            signal.AddAbortAlgorithm(algorithm);
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-listener-remove.
    /// </summary>
    internal void RemoveListener(EventListenerRegistration listener)
    {
        // The removed flag is what stops a dispatch already in flight from invoking it: the pass runs over a
        // clone of the list, so removing it from the list alone would come too late.
        listener.Removed = true;
        _listeners?.Remove(listener);

        if (listener.AbortAlgorithm is { } algorithm)
        {
            listener.Signal?.RemoveAbortAlgorithm(algorithm);
            listener.AbortAlgorithm = null;
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-eventtarget-removeeventlistener steps 2 and 3.
    /// </summary>
    internal void RemoveListener(string type, JsValue callback, bool capture)
    {
        var listeners = _listeners;
        if (listeners is null)
        {
            return;
        }

        var index = FindListener(listeners, type, callback, capture);
        if (index >= 0)
        {
            RemoveListener(listeners[index]);
        }
    }

    /// <summary>
    /// The registration an event-handler IDL attribute such as <c>onabort</c> keeps, or
    /// <see langword="null"/> when the attribute has never been set to an object.
    /// </summary>
    internal EventListenerRegistration? FindEventHandler(string type)
    {
        var listeners = _listeners;
        if (listeners is null)
        {
            return null;
        }

        foreach (var listener in listeners)
        {
            if (listener.IsEventHandler && string.Equals(listener.Type, type, StringComparison.Ordinal))
            {
                return listener;
            }
        }

        return null;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-dispatch. A target that is not in a tree takes the
    /// reduction of the algorithm to the single-item path «target»; a node takes the whole of it, over the
    /// path its <see cref="GetParent"/> builds.
    /// </summary>
    /// <returns>False when the event was canceled, which is what <c>dispatchEvent</c> returns.</returns>
    internal bool DispatchEvent(JsEvent ev)
    {
        return IsNode ? EventDispatch.Dispatch(this, ev) : DispatchFlat(ev);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-dispatch, reduced to the single-item path a tree-less
    /// target produces. See the class remarks for what that reduction does and does not change.
    /// </summary>
    /// <returns>False when the event was canceled, which is what <c>dispatchEvent</c> returns.</returns>
    private bool DispatchFlat(JsEvent ev)
    {
        var targetValue = EventTargetValue;

        ev.DispatchFlag = true;
        ev.Target = targetValue;
        ev.CurrentTarget = targetValue;
        ev.EventPhase = JsEvent.PhaseAtTarget;

        try
        {
            InvokePass(ev, capturePass: true);
            InvokePass(ev, capturePass: false);
        }
        finally
        {
            // The dispatch tail, which has to run even when a listener threw: an event whose dispatch flag
            // stayed set could never be dispatched again, and one whose currentTarget stayed set would lie.
            // The target is deliberately kept — only a shadow tree clears it.
            ev.EventPhase = JsEvent.PhaseNone;
            ev.CurrentTarget = Null;
            ev.DispatchFlag = false;
            ev.StopPropagationFlag = false;
            ev.StopImmediatePropagationFlag = false;
            ev.InPassiveListenerFlag = false;
        }

        return !ev.CanceledFlag;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire — create an event, initialize it, dispatch it. The
    /// event is trusted because the engine, not a script, is what created it.
    /// </summary>
    internal void FireEvent(JsString type)
    {
        var ev = _realm.Intrinsics.Event.CreateTrustedEvent(type);
        DispatchEvent(ev);
    }

    /// <summary>
    /// One pass of https://dom.spec.whatwg.org/#concept-event-listener-invoke over the single-item path,
    /// followed by https://dom.spec.whatwg.org/#concept-event-listener-inner-invoke over the clone.
    /// </summary>
    private void InvokePass(JsEvent ev, bool capturePass)
    {
        // Invoke step 5: the pass does not start at all once propagation has been stopped, which is how
        // stopPropagation() in the capturing pass keeps the bubbling one from running.
        if (ev.StopPropagationFlag)
        {
            return;
        }

        InvokeListeners(ev, capturePass);
    }

    /// <summary>
    /// Steps 7 to 9 of https://dom.spec.whatwg.org/#concept-event-listener-invoke: clone this target's
    /// listener list and run https://dom.spec.whatwg.org/#concept-event-listener-inner-invoke over the clone.
    /// </summary>
    /// <remarks>
    /// Called with <c>currentTarget</c> already set to this target, by the flat dispatch above and by
    /// <see cref="EventDispatch"/> for one item of a path. The <i>found</i> return value is not modelled: it
    /// exists only to drive the second pass over the four <c>webkit</c>-prefixed animation and transition
    /// event types, and no algorithm in this engine can produce one.
    /// </remarks>
    internal void InvokeListeners(JsEvent ev, bool capturePass)
    {
        var listeners = _listeners;
        if (listeners is null || !HasListenerFor(listeners, ev.TypeName, capturePass))
        {
            return;
        }

        // Invoke step 7: "Let listeners be a clone of ... event listener list. This avoids event listeners
        // added after this point from being run." The scan above keeps that clone off the common paths — a
        // dispatch to a target with only non-capturing listeners would otherwise allocate an array for a
        // capturing pass with nothing in it.
        var snapshot = listeners.ToArray();

        foreach (var listener in snapshot)
        {
            if (listener.Removed || !string.Equals(listener.Type, ev.TypeName, StringComparison.Ordinal))
            {
                continue;
            }

            // Inner invoke steps 2.3 and 2.4: the capture flag decides which of the two passes a listener
            // runs in, whatever phase the item is at. At an AT_TARGET item both passes run over it, so a
            // capturing listener on the target runs before a non-capturing one however they were registered.
            if (listener.Capture != capturePass)
            {
                continue;
            }

            // Step 2.5: removed before it runs, so a throwing once-listener still cannot fire twice.
            if (listener.Once)
            {
                RemoveListener(listener);
            }

            if (listener.Passive)
            {
                ev.InPassiveListenerFlag = true;
            }

            try
            {
                InvokeCallback(listener, ev);
            }
            catch (JavaScriptException exception) when (_engine._webApi?.Diagnostics is { } diagnostics)
            {
                // Inner invoke step 2.10: "If this throws an exception exception: Report exception for
                // listener's callback's ... global object." Reporting it is what lets the dispatch carry on to
                // the next listener, which is the behaviour a page relies on and which is only honest once
                // there is somewhere for the report to go. Only a JavaScriptException: everything that bounds
                // execution is a JintException but not one of these, so a constraint still stops the dispatch
                // dead. With no sink there is no catch at all — see the class remarks.
                //
                // Reporting it is HTML's report an exception, whose step 5 fires an `error` event at the
                // global scope before step 6's console report. That is a no-op unless the GlobalEvents
                // feature is on and something is listening, and it declines to recurse when the listener that
                // just threw was itself running as part of a report.
                _engine._webApi?.FireGlobalErrorEvent(exception);
                diagnostics.Report(DiagnosticEvent.ForUncaughtCallbackError(exception, DiagnosticCallbackSource.EventListener));
            }
            finally
            {
                ev.InPassiveListenerFlag = false;
            }

            // Step 2.11.
            if (ev.StopImmediatePropagationFlag)
            {
                break;
            }
        }
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#call-a-user-objects-operation, for the <c>EventListener</c> callback
    /// interface's single operation: a callable callback is called with the current target as its
    /// <c>this</c>, and anything else has <c>handleEvent</c> looked up on it afresh — the lookup is per
    /// invocation, so a script may swap the method between two dispatches.
    /// </summary>
    /// <remarks>
    /// An <b>event-handler</b> registration is a different algorithm and takes the branch below:
    /// https://html.spec.whatwg.org/multipage/webappapis.html#the-event-handler-processing-algorithm.
    /// </remarks>
    private void InvokeCallback(EventListenerRegistration listener, JsEvent ev)
    {
        var callback = listener.Callback;

        if (listener.IsEventHandler)
        {
            InvokeEventHandler(callback, ev);
            return;
        }

        if (callback is ICallable directly)
        {
            directly.Call(ev.CurrentTarget, ev);
            return;
        }

        // addEventListener's callback is a WebIDL callback *interface* and does get the lookup.
        if (callback is not ObjectInstance handler)
        {
            return;
        }

        if (handler.Get(_handleEvent) is not ICallable operation)
        {
            Throw.TypeError(_engine.Realm, "Failed to invoke an event listener: its handleEvent property is not a function.");
            return;
        }

        operation.Call(handler, ev);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#the-event-handler-processing-algorithm — what an
    /// event handler IDL attribute (<c>onmessage</c>, <c>onerror</c>, …) does that an <c>addEventListener</c>
    /// callback does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two things. It knows nothing about <c>handleEvent</c>, so a non-callable value assigned to the attribute
    /// is simply never called (step 2). And its <b>return value is read</b> (step 5), which is the half that
    /// makes the two <c>onerror</c> shapes different:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Special error event handling</b> (step 3) — an <c>ErrorEvent</c> named <c>error</c> at a global scope
    /// — invokes the handler with «<c>message</c>, <c>filename</c>, <c>lineno</c>, <c>colno</c>, <c>error</c>»
    /// and cancels the event when it returns <see langword="true"/>. That is the legacy shape a worker's own
    /// <c>onerror</c> has, and it is what decides HTML's <i>notHandled</i> on the worker side.
    /// </description></item>
    /// <item><description>
    /// Every other handler — including <c>Worker.onerror</c>, which is <c>AbstractWorker</c>'s plain
    /// <c>EventHandler</c> — is invoked with the event and cancels when it returns <see langword="false"/>.
    /// </description></item>
    /// </list>
    /// <para>
    /// Both tests are for the boolean value itself rather than for truthiness, which is what the algorithm
    /// says: a handler returning <c>0</c> or <c>""</c> cancels nothing. Cancelling goes through
    /// <see cref="JsEvent.SetCanceledFlag"/>, so a non-cancelable event is unaffected either way.
    /// </para>
    /// </remarks>
    private void InvokeEventHandler(JsValue callback, JsEvent ev)
    {
        if (callback is not ICallable handler)
        {
            return;
        }

        // Step 3: an ErrorEvent named `error` fired at a global scope, and nothing else.
        if (ev is JsErrorEvent errorEvent
            && IsGlobalScope
            && string.Equals(ev.TypeName, GlobalEventNames.ErrorName, StringComparison.Ordinal))
        {
            var legacy = handler.Call(
                ev.CurrentTarget,
                JsString.Create(errorEvent.Message),
                JsString.Create(errorEvent.Filename),
                JsNumber.Create(errorEvent.Lineno),
                JsNumber.Create(errorEvent.Colno),
                errorEvent.Error);

            if (legacy is JsBoolean && legacy.AsBoolean())
            {
                ev.SetCanceledFlag();
            }

            return;
        }

        var result = handler.Call(ev.CurrentTarget, ev);

        if (result is JsBoolean && !result.AsBoolean())
        {
            ev.SetCanceledFlag();
        }
    }

    /// <summary>
    /// Whether one pass of the dispatch could invoke anything at all: a listener of this type whose capture
    /// flag puts it in this pass.
    /// </summary>
    private static bool HasListenerFor(List<EventListenerRegistration> listeners, string type, bool capture)
    {
        foreach (var listener in listeners)
        {
            if (listener.Capture == capture && string.Equals(listener.Type, type, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindListener(List<EventListenerRegistration> listeners, string type, JsValue callback, bool capture)
    {
        for (var i = 0; i < listeners.Count; i++)
        {
            var candidate = listeners[i];
            if (!candidate.IsEventHandler
                && candidate.Capture == capture
                && string.Equals(candidate.Type, type, StringComparison.Ordinal)
                && ReferenceEquals(candidate.Callback, callback))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// One entry of an <see cref="JsEventTarget"/>'s event listener list,
/// https://dom.spec.whatwg.org/#concept-event-listener.
/// </summary>
/// <remarks>
/// A class rather than a struct because the abort algorithm a <c>signal</c> option installs closes over the
/// very entry it has to remove, and because <see cref="Removed"/> has to be observable from the clone a
/// dispatch already in flight is walking.
/// </remarks>
internal sealed class EventListenerRegistration
{
    internal EventListenerRegistration(string type, JsValue callback)
    {
        Type = type;
        Callback = callback;
    }

    /// <summary>The event type this listener observes.</summary>
    internal string Type { get; }

    /// <summary>
    /// The value the script passed, kept verbatim: <c>removeEventListener</c> compares object identity
    /// against it, so anything derived from it — an unwrapped <c>handleEvent</c>, a bound function — would
    /// make a listener unremovable. For an event-handler attribute this is the assigned value instead, and
    /// it is replaced in place when the attribute is reassigned so that the listener keeps its position.
    /// </summary>
    internal JsValue Callback { get; set; }

    /// <summary>https://dom.spec.whatwg.org/#event-listener-capture.</summary>
    internal bool Capture { get; init; }

    /// <summary>https://dom.spec.whatwg.org/#event-listener-passive.</summary>
    internal bool Passive { get; init; }

    /// <summary>https://dom.spec.whatwg.org/#event-listener-once.</summary>
    internal bool Once { get; init; }

    /// <summary>https://dom.spec.whatwg.org/#event-listener-signal.</summary>
    internal JsAbortSignal? Signal { get; init; }

    /// <summary>
    /// Whether this entry backs an event-handler IDL attribute such as <c>onabort</c> rather than an
    /// <c>addEventListener</c> call. Such an entry is invisible to <c>addEventListener</c>'s duplicate check
    /// and to <c>removeEventListener</c>, because in the specification its callback is HTML's event handler
    /// processing algorithm and not the function the script assigned.
    /// </summary>
    internal bool IsEventHandler { get; init; }

    /// <summary>https://dom.spec.whatwg.org/#event-listener-removed.</summary>
    internal bool Removed { get; set; }

    /// <summary>The abort algorithm <see cref="Signal"/> holds for this entry, so it can be taken off again.</summary>
    internal Action? AbortAlgorithm { get; set; }
}
#endif
