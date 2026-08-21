#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Events;

/// <summary>
/// An <c>EventTarget</c> instance: a list of event listeners, plus the dispatch algorithm that runs them.
/// <para>
/// https://dom.spec.whatwg.org/#interface-eventtarget
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Dispatch is flat.</b> The specification's dispatch algorithm walks an <i>event path</i> built from the
/// target's ancestors in a node tree; Jint has no node tree and the specification itself says so — "all
/// author-created EventTargets do not participate in a tree structure". The path is therefore always the
/// single item «target», which is exactly what the algorithm produces for a tree-less target, and every step
/// that survives that reduction is implemented here rather than approximated:
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
    /// https://dom.spec.whatwg.org/#concept-event-dispatch, reduced to the single-item path a tree-less
    /// target produces. See the class remarks for what that reduction does and does not change.
    /// </summary>
    /// <returns>False when the event was canceled, which is what <c>dispatchEvent</c> returns.</returns>
    internal bool DispatchEvent(JsEvent ev)
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

            // Inner invoke steps 2.3 and 2.4. On a single-item path both passes reach AT_TARGET, so each
            // listener runs in exactly one of them, chosen by its capture flag.
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
    private void InvokeCallback(EventListenerRegistration listener, JsEvent ev)
    {
        var callback = listener.Callback;

        if (callback is ICallable directly)
        {
            directly.Call(ev.CurrentTarget, ev);
            return;
        }

        // HTML's event handler processing algorithm invokes the assigned value as a function and knows
        // nothing about handleEvent, so a non-callable onabort is simply never called. addEventListener's
        // callback is a WebIDL callback *interface* and does get the lookup.
        if (listener.IsEventHandler || callback is not ObjectInstance handler)
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
