#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.WebApi.Events;

/// <summary>
/// One entry of an event's path — https://dom.spec.whatwg.org/#event-path-item.
/// </summary>
/// <param name="InvocationTarget">
/// https://dom.spec.whatwg.org/#event-path-invocation-target: the target whose listener list this item runs.
/// </param>
/// <param name="InvocationTargetInShadowTree">
/// Whether <paramref name="InvocationTarget"/> is a node whose root is a shadow root. HTML reads it to decide
/// whether <c>window.event</c> is updated; nothing in this engine does yet, and it is carried because
/// <i>invoke</i> passes it to <i>inner invoke</i> — and because step 6.10 reads it back rather than asking
/// the tree for a root a second time.
/// </param>
/// <param name="ShadowAdjustedTarget">
/// https://dom.spec.whatwg.org/#event-path-shadow-adjusted-target: what <c>event.target</c> answers from this
/// item on, or <see langword="null"/> for an item the event merely passes through.
/// </param>
/// <param name="RelatedTarget">The event's related target, retargeted against the invocation target.</param>
/// <param name="RootOfClosedTree">Whether the invocation target is a shadow root whose mode is closed.</param>
/// <param name="SlotInClosedTree">Whether this item was reached through a slot inside a closed shadow tree.</param>
/// <remarks>
/// The struct's seventh member in the specification is a <i>touch target list</i>, which
/// <c>TouchEvent</c> is the only interface to populate and which no algorithm here can produce; it is
/// therefore always empty and is not modelled. Everything that reads it — the <i>clearTargets</i> test and
/// <i>invoke</i>'s "set event's touch target list" — degenerates accordingly.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EventPathItem(
    JsEventTarget InvocationTarget,
    bool InvocationTargetInShadowTree,
    JsEventTarget? ShadowAdjustedTarget,
    JsEventTarget? RelatedTarget,
    bool RootOfClosedTree,
    bool SlotInClosedTree);

/// <summary>
/// https://dom.spec.whatwg.org/#concept-event-dispatch over a tree: building the event path, retargeting,
/// the capture, target and bubble phases, <c>composedPath()</c> and activation behaviour.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a seam, not a DOM.</b> Nothing here knows what a node is; it asks the target. A host that wants
/// tree dispatch — Jint's own browser package, or any other DOM built on this engine — derives its wrappers
/// from <see cref="JsEventTarget"/> and overrides:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="JsEventTarget.IsNode"/>, which is what selects this algorithm at all;
/// </description></item>
/// <item><description>
/// <see cref="JsEventTarget.GetParent"/>, DOM's <i>get the parent</i>, which is the whole of the path;
/// </description></item>
/// <item><description>
/// <see cref="JsEventTarget.TreeParent"/>, <see cref="JsEventTarget.IsShadowRoot"/>,
/// <see cref="JsEventTarget.IsClosedShadowRoot"/>, <see cref="JsEventTarget.ShadowHost"/>,
/// <see cref="JsEventTarget.IsSlot"/> and <see cref="JsEventTarget.AssignedSlot"/>, which are what
/// retargeting and the closed-tree hiding in <c>composedPath()</c> are written in terms of;
/// </description></item>
/// <item><description>
/// optionally <see cref="JsEventTarget.GetRoot"/>, the one seam that is overridden for cost rather than
/// for meaning: a wrapper that knows its own root spares the dispatch the walk that derives it;
/// </description></item>
/// <item><description>
/// <see cref="JsEventTarget.HasActivationBehavior"/> and its three algorithms, plus
/// <see cref="JsEvent.IsActivationEvent"/> on the event interface that carries activation — a
/// <c>MouseEvent</c> whose type is <c>click</c>, which no engine-supplied event is.
/// </description></item>
/// </list>
/// <para>
/// Every one of those has a default that says "not in a tree", so an engine that supplies no DOM never
/// reaches this class: <see cref="JsEventTarget.DispatchEvent"/> reads one virtual and takes the flat path,
/// which allocates no path list and behaves exactly as it did before this file existed.
/// </para>
/// <para>
/// <b>What is deliberately not here.</b> The <i>legacy target override flag</i>, which HTML uses only to make
/// a <c>load</c> event fired at a <c>Window</c> report the document as its target — a host wanting it
/// overrides <see cref="JsEventTarget.EventTargetValue"/> or dispatches at the document. The touch target
/// list, for want of a <c>TouchEvent</c>. <i>legacyOutputDidListenersThrowFlag</i>, which only Indexed
/// Database reads. And <i>invoke</i>'s second pass over the four <c>webkit</c>-prefixed animation and
/// transition types, which needs an engine that can fire an <c>animationend</c>.
/// </para>
/// </remarks>
internal static class EventDispatch
{
    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-dispatch — dispatch <paramref name="ev"/> at
    /// <paramref name="target"/>, which is a node.
    /// </summary>
    /// <returns>False when the event was canceled, which is what <c>dispatchEvent</c> returns.</returns>
    internal static bool Dispatch(JsEventTarget target, JsEvent ev)
    {
        // Step 1.
        ev.DispatchFlag = true;

        // Steps 3 to 5. The touch target list is empty for every event this engine can build, so steps 6.2
        // and 6.9.4 have nothing to retarget.
        JsEventTarget? activationTarget = null;
        var clearTargets = false;
        var path = ev.EnsurePath();

        try
        {
            // The two roots the whole dispatch is derived from. https://dom.spec.whatwg.org/#concept-tree-root
            // is a walk up TreeParent for a host that does not answer it directly, so each is read once here
            // rather than per path item: the target's changes only where the path crosses a shadow boundary,
            // and the event's related target does not change at all while the path is being built.
            var targetRoot = target.GetRoot();
            var relatedRoot = ev.RelatedTarget is { IsNode: true } related ? related.GetRoot() : null;

            var relatedTarget = Retarget(ev.RelatedTarget, relatedRoot, targetRoot);

            // Step 6. A dispatch whose target and related target retarget onto the same node is not
            // dispatched at all — which is what keeps a mouseover between two children of one host from
            // being seen outside it. The second disjunct readmits the case where the event's own related
            // target *is* the target, because there the equality is not a retargeting artefact.
            if (!ReferenceEquals(target, relatedTarget) || ReferenceEquals(ev.RelatedTarget, target))
            {
                BuildPath(ev, target, targetRoot, relatedTarget, relatedRoot, path, ref activationTarget);

                // Step 6.10.
                clearTargets = ComputeClearTargets(path);

                // Step 6.11.
                activationTarget?.LegacyPreActivationBehavior();

                InvokeCapturePhase(ev, path);
                InvokeBubblePhase(ev, path);
            }
        }
        finally
        {
            // Steps 7 to 10, which have to run even when a listener threw: an event whose dispatch flag
            // stayed set could never be dispatched again, and one whose currentTarget stayed set would lie.
            ev.EventPhase = JsEvent.PhaseNone;
            ev.CurrentTarget = JsValue.Null;
            path.Clear();
            ev.DispatchFlag = false;
            ev.StopPropagationFlag = false;
            ev.StopImmediatePropagationFlag = false;
            ev.InPassiveListenerFlag = false;

            // Step 11: an event that ended inside a shadow tree reports nothing about where it was.
            if (clearTargets)
            {
                ev.Target = JsValue.Null;
                ev.RelatedTarget = null;
            }
        }

        // Step 12. Outside the finally on purpose: an activation behaviour is an action the page takes, and
        // running it while unwinding an exception from a listener would run it in the wrong order and hide
        // whatever it threw behind the original.
        if (activationTarget is { } activation)
        {
            if (!ev.CanceledFlag)
            {
                activation.ActivationBehavior(ev);
            }
            else
            {
                activation.LegacyCanceledActivationBehavior();
            }
        }

        // Step 13.
        return !ev.CanceledFlag;
    }

    /// <summary>
    /// Steps 6.1 to 6.9 of https://dom.spec.whatwg.org/#concept-event-dispatch: the walk from the target up
    /// through <see cref="JsEventTarget.GetParent"/>, appending one path item per target the event reaches.
    /// </summary>
    /// <remarks>
    /// The two roots come in rather than being derived here. <c>targetRoot</c> is the dispatch target's, and
    /// the walk carries it forward: everything the path is measured against is a root, and the only thing
    /// that changes one is step 6.9.7's crossing into the tree above. <c>relatedRoot</c> is the event's own
    /// related target's, or null when it has none or it is not a node; it cannot change while the path is
    /// being built, so retargeting against each item costs no walk of its own.
    /// </remarks>
    private static void BuildPath(
        JsEvent ev,
        JsEventTarget target,
        JsEventTarget targetRoot,
        JsEventTarget? relatedTarget,
        JsEventTarget? relatedRoot,
        List<EventPathItem> path,
        ref JsEventTarget? activationTarget)
    {
        // Step 6.3. targetOverride is the target itself: the legacy target override flag is HTML's and is
        // never given here.
        Append(path, target, targetRoot, target, relatedTarget, slotInClosedTree: false);

        // Steps 6.4 and 6.5.
        var isActivationEvent = ev.IsActivationEvent;
        if (isActivationEvent && target.HasActivationBehavior)
        {
            activationTarget = target;
        }

        // Steps 6.6 to 6.8.
        var slottable = target.AssignedSlot is not null ? target : null;
        var slotInClosedTree = false;

        // The target the walk is standing on and its root, which is what the next item's root is derived
        // from: a step that stayed inside one node tree has not changed root, and that is every step of a
        // walk up a node tree.
        var child = target;
        var childRoot = targetRoot;
        var parent = target.GetParent(ev);

        // Step 6.9.
        while (parent is not null)
        {
            var parentRoot = RootOf(parent, child, childRoot);

            // Step 6.9.1: the parent of an assigned slottable is its assigned slot, so reaching one means the
            // event has entered a shadow tree — and a closed one has to be remembered, because
            // composedPath() hides it from listeners outside. The specification asserts that the parent is a
            // slot; the assertion is written out as a condition because the tree comes from a host, and a
            // wrapper that reported an assigned slot without putting that slot on the path would otherwise
            // mark an open tree closed.
            if (slottable is not null)
            {
                slottable = null;
                if (parent.IsSlot && parentRoot is { IsClosedShadowRoot: true })
                {
                    slotInClosedTree = true;
                }
            }

            // Step 6.9.2.
            if (parent.AssignedSlot is not null)
            {
                slottable = parent;
            }

            // Step 6.9.3.
            var parentRelatedTarget = Retarget(ev.RelatedTarget, relatedRoot, parentRoot);

            // A null root is a parent that is not a node, which is the other half of the condition.
            if (parent.IsGlobalScope || (parentRoot is not null && IsShadowIncludingInclusiveAncestor(targetRoot, parentRoot)))
            {
                // Step 6.9.5: still inside the tree the target's root spans, so the event only passes
                // through — the item carries no shadow-adjusted target and reports the capturing or bubbling
                // phase.
                if (isActivationEvent && ev.Bubbles && activationTarget is null && parent.HasActivationBehavior)
                {
                    activationTarget = parent;
                }

                Append(path, parent, parentRoot, shadowAdjustedTarget: null, parentRelatedTarget, slotInClosedTree);
            }
            else if (ReferenceEquals(parent, parentRelatedTarget))
            {
                // Step 6.9.6: the event has arrived at its own related target, so it stops rather than
                // telling that subtree about a movement that never left it.
                parent = null;
            }
            else
            {
                // Step 6.9.7: the walk has crossed a shadow boundary, so the host becomes a new target and
                // the item is an AT_TARGET one — which is what makes event.target answer the host to a
                // listener outside the shadow tree.
                target = parent;
                targetRoot = parentRoot ?? parent.GetRoot();
                if (isActivationEvent && activationTarget is null && target.HasActivationBehavior)
                {
                    activationTarget = target;
                }

                Append(path, parent, parentRoot, target, parentRelatedTarget, slotInClosedTree);
            }

            // Steps 6.9.8 and 6.9.9.
            if (parent is not null)
            {
                child = parent;
                childRoot = parentRoot;
                parent = parent.GetParent(ev);
            }

            slotInClosedTree = false;
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-tree-root of <paramref name="node"/>, or null when it is not a
    /// node and therefore has no tree.
    /// </summary>
    /// <remarks>
    /// It is derived from <paramref name="childRoot"/> whenever the walk merely went up inside one node
    /// tree, which is every step that has not crossed a slot or a shadow boundary — so a dispatch costs one
    /// root walk rather than one per path item. Anything else is a different tree and is asked. A host
    /// answering <see cref="JsEventTarget.GetRoot"/> directly pays nothing either way.
    /// </remarks>
    private static JsEventTarget? RootOf(JsEventTarget node, JsEventTarget child, JsEventTarget? childRoot)
    {
        if (!node.IsNode)
        {
            return null;
        }

        return childRoot is not null && ReferenceEquals(child.TreeParent, node) ? childRoot : node.GetRoot();
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-path-append — <i>append to an event path</i>.
    /// </summary>
    /// <remarks>
    /// <c>invocationTargetRoot</c> is that target's tree root, or null when it is not a node, and is the
    /// whole of what the item's <i>invocation-target-in-shadow-tree</i> flag is read from.
    /// </remarks>
    private static void Append(
        List<EventPathItem> path,
        JsEventTarget invocationTarget,
        JsEventTarget? invocationTargetRoot,
        JsEventTarget? shadowAdjustedTarget,
        JsEventTarget? relatedTarget,
        bool slotInClosedTree)
    {
        var invocationTargetInShadowTree = invocationTargetRoot is { IsShadowRoot: true };
        var rootOfClosedTree = invocationTarget.IsClosedShadowRoot;

        path.Add(new EventPathItem(
            invocationTarget,
            invocationTargetInShadowTree,
            shadowAdjustedTarget,
            relatedTarget,
            rootOfClosedTree,
            slotInClosedTree));
    }

    /// <summary>
    /// Step 6.10 of https://dom.spec.whatwg.org/#concept-event-dispatch: whether the dispatch has to forget
    /// its target afterwards, because the last thing that was a target is inside a shadow tree.
    /// </summary>
    private static bool ComputeClearTargets(List<EventPathItem> path)
    {
        for (var i = path.Count - 1; i >= 0; i--)
        {
            var item = path[i];
            if (item.ShadowAdjustedTarget is null)
            {
                continue;
            }

            // An item that has a shadow-adjusted target has *itself* as one — the first item is the
            // dispatch target and step 6.9.7's is the host the walk had just crossed to — so the flag the
            // item already carries answers the first half without asking the tree again.
            return item.InvocationTargetInShadowTree || IsInShadowTree(item.RelatedTarget);
        }

        return false;
    }

    private static bool IsInShadowTree(JsEventTarget? target) =>
        target is { IsNode: true } node && node.GetRoot().IsShadowRoot;

    /// <summary>
    /// Step 6.12 of https://dom.spec.whatwg.org/#concept-event-dispatch: the path from the far end down to
    /// the target.
    /// </summary>
    private static void InvokeCapturePhase(JsEvent ev, List<EventPathItem> path)
    {
        for (var i = path.Count - 1; i >= 0; i--)
        {
            ev.EventPhase = path[i].ShadowAdjustedTarget is not null ? JsEvent.PhaseAtTarget : JsEvent.PhaseCapturing;
            Invoke(ev, path, i, capturePass: true);
        }
    }

    /// <summary>
    /// Step 6.13 of https://dom.spec.whatwg.org/#concept-event-dispatch: the path back up, which a
    /// non-bubbling event walks without invoking anything above its targets.
    /// </summary>
    private static void InvokeBubblePhase(JsEvent ev, List<EventPathItem> path)
    {
        for (var i = 0; i < path.Count; i++)
        {
            if (path[i].ShadowAdjustedTarget is not null)
            {
                ev.EventPhase = JsEvent.PhaseAtTarget;
            }
            else
            {
                if (!ev.Bubbles)
                {
                    continue;
                }

                ev.EventPhase = JsEvent.PhaseBubbling;
            }

            Invoke(ev, path, i, capturePass: false);
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-listener-invoke for one path item.
    /// </summary>
    /// <remarks>
    /// The target and related target are set <b>before</b> the stop-propagation check, which is what the
    /// algorithm says and is observable: an event whose propagation a listener stopped still ends the
    /// dispatch reporting the related target of the last item the loops walked over.
    /// </remarks>
    private static void Invoke(JsEvent ev, List<EventPathItem> path, int index, bool capturePass)
    {
        var item = path[index];

        // Steps 1 and 2: an item the event passes through reports the nearest target at or below it.
        for (var i = index; i >= 0; i--)
        {
            if (path[i].ShadowAdjustedTarget is { } adjusted)
            {
                ev.Target = adjusted.EventTargetValue;
                break;
            }
        }

        // Step 3.
        ev.RelatedTarget = item.RelatedTarget;

        // Step 5.
        if (ev.StopPropagationFlag)
        {
            return;
        }

        // Step 6.
        ev.CurrentTarget = item.InvocationTarget.EventTargetValue;

        // Steps 7 to 9. The struct's invocation-target-in-shadow-tree flag travels with them, because inner
        // invoke reads it: a listener whose invocation target is inside a shadow tree runs without the
        // Window's `event` being set to the event it is handling.
        item.InvocationTarget.InvokeListeners(ev, capturePass, item.InvocationTargetInShadowTree);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#retarget — <i>retarget</i> <paramref name="a"/> against the target whose
    /// tree root is <paramref name="againstRoot"/>: climb out of every shadow tree that target is not
    /// inside, so that a listener never sees a node it has no business knowing about.
    /// </summary>
    /// <remarks>
    /// <c>aRoot</c> is <paramref name="a"/>'s own tree root when the caller already has it, which is the
    /// only hop that can be given: every hop after the first stands on a shadow host the walk has just
    /// reached, and null asks. <paramref name="againstRoot"/> is null for a target that is not a node, which
    /// the algorithm's "B is a node" condition makes the same answer as a target in no tree at all.
    /// </remarks>
    private static JsEventTarget? Retarget(JsEventTarget? a, JsEventTarget? aRoot, JsEventTarget? againstRoot)
    {
        while (a is not null)
        {
            if (!a.IsNode)
            {
                return a;
            }

            var root = aRoot ?? a.GetRoot();
            aRoot = null;

            if (!root.IsShadowRoot)
            {
                return a;
            }

            if (againstRoot is not null && IsShadowIncludingInclusiveAncestor(root, againstRoot))
            {
                return a;
            }

            a = root.ShadowHost;
        }

        return null;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-shadow-including-inclusive-ancestor, asked the way both callers
    /// ask it: whether the tree rooted at <paramref name="ancestorRoot"/> contains — shadow trees
    /// included — the node whose own tree root is <paramref name="nodeRoot"/>.
    /// </summary>
    /// <remarks>
    /// The specification's walk climbs every ancestor between the node and its root before it can leave that
    /// tree at all. Both callers pass an <i>ancestor</i> that a <see cref="JsEventTarget.GetRoot"/> produced,
    /// and a root has no tree parent, so none of the ancestors in between can be it: stepping tree by tree is
    /// the same predicate, and it costs one comparison per tree the node is nested in rather than one per
    /// ancestor it has. That is what keeps a dispatch linear in the depth of the tree.
    /// </remarks>
    private static bool IsShadowIncludingInclusiveAncestor(JsEventTarget ancestorRoot, JsEventTarget nodeRoot)
    {
        var current = nodeRoot;
        while (true)
        {
            if (ReferenceEquals(current, ancestorRoot))
            {
                return true;
            }

            if (!current.IsShadowRoot || current.ShadowHost is not { } host)
            {
                return false;
            }

            current = host.GetRoot();
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-composedpath over a non-empty path: the invocation targets the
    /// listener now running is allowed to see, in target-first order.
    /// </summary>
    /// <remarks>
    /// The bookkeeping is the specification's, and it is what makes a closed shadow tree invisible from
    /// outside: a listener whose own item is at hidden level <i>n</i> sees only items at level <i>n</i> or
    /// below, and crossing a closed root or a slot inside one is what moves the level.
    /// </remarks>
    internal static List<JsValue> ComposedPath(JsEvent ev, List<EventPathItem> path)
    {
        var composedPath = new List<JsValue>(path.Count);

        // Steps 4 to 6.
        var currentTarget = ev.CurrentTarget;
        composedPath.Add(currentTarget);

        // Steps 7 to 11: find the item the current target is invoking, counting the closed roots crossed on
        // the way to it.
        var currentTargetIndex = 0;
        var currentTargetHiddenSubtreeLevel = 0;
        for (var index = path.Count - 1; index >= 0; index--)
        {
            if (path[index].RootOfClosedTree)
            {
                currentTargetHiddenSubtreeLevel++;
            }

            if (ReferenceEquals(path[index].InvocationTarget.EventTargetValue, currentTarget))
            {
                currentTargetIndex = index;
                break;
            }

            if (path[index].SlotInClosedTree)
            {
                currentTargetHiddenSubtreeLevel--;
            }
        }

        // Steps 12 to 14: everything below the current target that it may see, prepended so the result stays
        // in target-first order.
        var currentHiddenLevel = currentTargetHiddenSubtreeLevel;
        var maxHiddenLevel = currentTargetHiddenSubtreeLevel;
        for (var index = currentTargetIndex - 1; index >= 0; index--)
        {
            if (path[index].RootOfClosedTree)
            {
                currentHiddenLevel++;
            }

            if (currentHiddenLevel <= maxHiddenLevel)
            {
                composedPath.Insert(0, path[index].InvocationTarget.EventTargetValue);
            }

            if (path[index].SlotInClosedTree)
            {
                currentHiddenLevel--;
                if (currentHiddenLevel < maxHiddenLevel)
                {
                    maxHiddenLevel = currentHiddenLevel;
                }
            }
        }

        // Steps 15 to 17: and everything above it.
        currentHiddenLevel = currentTargetHiddenSubtreeLevel;
        maxHiddenLevel = currentTargetHiddenSubtreeLevel;
        for (var index = currentTargetIndex + 1; index < path.Count; index++)
        {
            if (path[index].SlotInClosedTree)
            {
                currentHiddenLevel++;
            }

            if (currentHiddenLevel <= maxHiddenLevel)
            {
                composedPath.Add(path[index].InvocationTarget.EventTargetValue);
            }

            if (path[index].RootOfClosedTree)
            {
                currentHiddenLevel--;
                if (currentHiddenLevel < maxHiddenLevel)
                {
                    maxHiddenLevel = currentHiddenLevel;
                }
            }
        }

        return composedPath;
    }
}
#endif
