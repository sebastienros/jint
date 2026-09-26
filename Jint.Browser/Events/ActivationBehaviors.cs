using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// HTML's activation behaviours, keyed by the element a click reached.
/// <para>
/// https://dom.spec.whatwg.org/#eventtarget-activation-behavior, and the per-element definitions in
/// https://html.spec.whatwg.org/multipage/
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>What "activation" means with no layout.</b> Every one of these is reached the same way a browser reaches
/// it — a <c>MouseEvent</c> named <c>click</c> is dispatched through the tree, the dispatcher picks the nearest
/// ancestor with an activation behaviour as the activation target, and the behaviour runs after the listeners
/// unless one of them canceled the event. Nothing here measures or paints; the behaviours that would need a
/// rendering (a file picker, a colour picker, a date picker) become a host seam or a no-op, and the ones that
/// are pure state (checkedness, <c>details.open</c>, selectedness) are exact.
/// </para>
/// <para>
/// <b>None of it goes through AngleSharp's own <c>DoClick</c>.</b> That method dispatches on AngleSharp's event
/// bus, which holds nothing a script registered, and it runs no activation behaviour whatsoever — a checkbox it
/// clicks does not toggle, a <c>&lt;summary&gt;</c> it clicks does not open its <c>&lt;details&gt;</c>, and an
/// <c>&lt;a href&gt;</c> it clicks does not navigate. Design doc §5 is the rule and this is where it bites.
/// </para>
/// </remarks>
internal static class ActivationBehaviors
{
    /// <summary>
    /// What <see cref="LegacyPreActivationBehavior"/> changed, so
    /// <see cref="LegacyCanceledActivationBehavior"/> can put it back.
    /// </summary>
    /// <remarks>
    /// Keyed on the wrapper in a <see cref="ConditionalWeakTable{TKey,TValue}"/> rather than held in a stack,
    /// because a listener that throws with no diagnostics sink escapes the dispatch before step 12 runs and a
    /// stack would keep the entry for ever. A weak entry left behind is overwritten by the next
    /// pre-activation of that element and collected with it. The one case it cannot model is an element whose
    /// own <c>click</c> listener clicks the same element again, which is unbounded recursion either way.
    /// </remarks>
    private static readonly ConditionalWeakTable<DomNodeObject, PreActivationSnapshot> _snapshots = new();
    private static readonly ConditionalWeakTable<Element, PendingToggle> PendingToggles = new();

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-activation-behavior — whether this node has one at all, which
    /// is what lets the dispatcher choose it as the activation target.
    /// </summary>
    internal static bool Has(Node node)
        => node is Element { NamespaceUri: Namespaces.Html } element
            && element.LocalName is "a" or "area" or "button" or "input" or "label" or "option" or "summary";

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-legacy-pre-activation-behavior, run before any listener so a
    /// listener sees the checkbox already toggled — which is exactly why a page's <c>onclick</c> can read
    /// <c>this.checked</c> and get the new value.
    /// </summary>
    internal static void LegacyPreActivationBehavior(DomNodeObject wrapper)
    {
        using var mutation = wrapper.DomRealm.MutateLayout();
        // A disabled control's activation behaviour does nothing, so its pre-activation behaviour must do
        // nothing either — otherwise the toggle would happen with no activation behaviour left to roll it
        // back. HTML reaches the same place by never letting a click at a disabled control be dispatched at
        // all; the events here are still dispatched, and this is what keeps the state right.
        if (wrapper.Node is not Element input || !EventDom.IsHtml(input, "input") || EventDom.Disabled(wrapper.DomRealm, input))
        {
            return;
        }

        // https://html.spec.whatwg.org/multipage/input.html#checkbox-state-(type=checkbox) and
        // #radio-button-state-(type=radio) — the two input types with a legacy-pre-activation behaviour.
        if (IsType(input, "checkbox"))
        {
            _snapshots.AddOrUpdate(wrapper, PreActivationSnapshot.ForCheckbox(HtmlCheckableState.Get(input)!.Checked, HtmlCheckableState.Get(input)!.Indeterminate));
            HtmlCheckednessAlgorithms.Set(input, !HtmlCheckableState.Get(input)!.Checked, HtmlCheckedChangeOrigin.UserInteraction, wrapper.DomRealm.CancellationToken);
            HtmlCheckableState.Get(input)!.SetIndeterminate(false);
            return;
        }

        if (IsType(input, "radio"))
        {
            var previously = HtmlCheckableState.FirstCheckedRadio(input, wrapper.DomRealm.CancellationToken);
            _snapshots.AddOrUpdate(wrapper, PreActivationSnapshot.ForRadio(previously));
            HtmlCheckednessAlgorithms.Set(input, true, HtmlCheckedChangeOrigin.UserInteraction, wrapper.DomRealm.CancellationToken);
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-legacy-canceled-activation-behavior — a listener called
    /// <c>preventDefault()</c>, so the checkedness the pre-activation behaviour changed goes back.
    /// </summary>
    internal static void LegacyCanceledActivationBehavior(DomNodeObject wrapper)
    {
        using var mutation = wrapper.DomRealm.MutateLayout();
        if (wrapper.Node is not Element input || !EventDom.IsHtml(input, "input") || !_snapshots.TryGetValue(wrapper, out var snapshot))
        {
            return;
        }

        _snapshots.Remove(wrapper);

        if (snapshot.IsCheckbox)
        {
            HtmlCheckednessAlgorithms.Set(input, snapshot.WasChecked, HtmlCheckedChangeOrigin.Algorithm, wrapper.DomRealm.CancellationToken);
            HtmlCheckableState.Get(input)!.SetIndeterminate(snapshot.WasIndeterminate);
            return;
        }

        // A radio group's rollback is not "uncheck this one": HTML says to restore the element that was
        // checked before, and a group with nothing checked stays with nothing checked.
        HtmlCheckednessAlgorithms.Set(input, false, HtmlCheckedChangeOrigin.Algorithm, wrapper.DomRealm.CancellationToken);

        if (snapshot.PreviouslyChecked is { } previous
            && HtmlCheckableState.SameRadioGroup(previous, input, wrapper.DomRealm.CancellationToken))
        {
            HtmlCheckednessAlgorithms.Set(previous, true, HtmlCheckedChangeOrigin.Algorithm, wrapper.DomRealm.CancellationToken);
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-activation-behavior, run after the listeners when the event was
    /// not canceled.
    /// </summary>
    internal static void Run(DomNodeObject wrapper, JsEvent ev)
    {
        using var mutation = wrapper.DomRealm.MutateLayout();
        var realm = BrowserEventRealm.Of(wrapper.DomRealm.Engine);

        if (wrapper.Node is not Element { NamespaceUri: Namespaces.Html } element)
        {
            return;
        }

        switch (element.LocalName)
        {
            case "a":
            case "area":
                FollowHyperlink(realm, element, Accessibility.ContentDom.Url(element, "href"), element.GetAttributeNS(null, "target"));
                return;
            case "button":
                RunButton(wrapper, element);
                return;
            case "input":
                RunInput(realm, wrapper, element, ev);
                return;
            case "label":
                RunLabel(wrapper, element, ev);
                return;
            case "option":
                SelectOption(wrapper.DomRealm, element);
                return;
            case "summary":
                RunSummary(wrapper, element);
                return;
        }
    }

    // -----------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/links.html#following-hyperlinks-2. An <c>&lt;a&gt;</c> or
    /// <c>&lt;area&gt;</c> with no <c>href</c> has no activation behaviour at all, which is what makes
    /// <c>&lt;a&gt;</c> a plain inline element.
    /// </summary>
    private static void FollowHyperlink(BrowserEventRealm realm, Element source, string? url, string? target)
    {
        if (!source.HasContentAttribute("href"))
        {
            return;
        }

        realm.ActivationHost.FollowHyperlink(realm, source, url ?? "", target ?? "");
    }

    /// <summary>https://html.spec.whatwg.org/multipage/form-elements.html#the-button-element.</summary>
    private static void RunButton(DomNodeObject wrapper, Element button)
    {
        if (EventDom.Disabled(wrapper.DomRealm, button))
        {
            return;
        }

        switch (EventDom.ButtonType(button))
        {
            case "submit":
                FormSubmission.Submit(wrapper.DomRealm, HtmlFormOwner.Of(button), button);
                break;
            case "reset":
                FormSubmission.Reset(wrapper.DomRealm, HtmlFormOwner.Of(button));
                break;
        }
    }

    /// <summary>https://html.spec.whatwg.org/multipage/input.html#input-activation-behavior.</summary>
    private static void RunInput(BrowserEventRealm realm, DomNodeObject wrapper, Element input, JsEvent ev)
    {
        if (EventDom.Disabled(wrapper.DomRealm, input))
        {
            return;
        }

        switch (EventDom.InputType(input))
        {
            case "submit":
                FormSubmission.Submit(wrapper.DomRealm, HtmlFormOwner.Of(input), input);
                return;

            case "image":
                // The image activation algorithm returns before selecting a coordinate if its document is
                // no longer fully active (a click listener can adopt the input into another document).
                if (HtmlFormOwner.Of(input) is not { } owner)
                {
                    return;
                }

                var page = Runtime.PageRuntime.Find(wrapper.Engine);
                if (page is not null && !ReferenceEquals(input.OwnerDocument, page.Document))
                {
                    return;
                }

                SelectCoordinate(realm, page, input, ev);
                FormSubmission.Submit(wrapper.DomRealm, owner, input);
                return;

            case "reset":
                FormSubmission.Reset(wrapper.DomRealm, HtmlFormOwner.Of(input));
                return;

            case "checkbox":
            case "radio":
                // The checkedness was already changed by the legacy pre-activation behaviour; the activation
                // behaviour is only the two events. HTML fires `input` with bubbles and composed both true and
                // `change` with bubbles true, in that order, and both are plain Events rather than InputEvents.
                //
                // Step 1 of the input activation behaviour is "if the element is not connected, then return",
                // so a detached control toggles silently: the checkedness is the element's own state and the
                // two events announce a change to a *document*. The snapshot is dropped either way — the
                // toggle stands, so there is nothing left to roll back.
                _snapshots.Remove(wrapper);

                if (IsConnected(input))
                {
                    FireInputAndChange(wrapper);
                }

                return;

            case "file":
                realm.ActivationHost.OpenFileChooser(realm, input);
                return;

            // "Show the picker, if applicable" for a control whose picker is the platform's — a colour well, a
            // calendar, a time spinner. There is no platform here and no value to pick with, so the behaviour
            // is honestly nothing rather than a guessed value.
            default:
                return;
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#image-button-state-(type=image): "if the element
    /// has an image, and the user activated the button using a pointing device, the selected coordinate is
    /// the position of the pointer relative to the image; otherwise it is (0, 0)". Every activation sets
    /// one, which is what makes a synthetic <c>click()</c> after a real one select nothing again.
    /// </summary>
    /// <remarks>
    /// <b>Three conditions, and each excludes a real case.</b> The image must be <i>completely available</i>
    /// (<c>Media.PageImages</c>): an <c>&lt;input type=image&gt;</c> with no <c>src</c>, one whose fetch
    /// failed and one whose bytes are not a container this browser reads have nothing to select within, and
    /// HTML makes every one of them behave as a plain submit button. The activation must be trusted, because
    /// <c>element.click()</c> and a dispatched <c>MouseEvent</c> are a script rather than a user — HTML asks
    /// for a pointing device. And the pointer must have been measured inside <i>this</i> button
    /// (<see cref="BrowserEventRealm.PendingImagePoint"/>), which excludes a keyboard activation, a
    /// <c>&lt;label&gt;</c>'s forwarded click and a script's click fired from inside a listener of a real
    /// release on some other element.
    /// </remarks>
    private static void SelectCoordinate(
        BrowserEventRealm realm,
        Runtime.PageRuntime? page,
        Element input,
        JsEvent ev)
    {
        if (ev.IsTrusted
            && realm.PendingImagePoint is { } point
            && ReferenceEquals(point.Image, input)
            && page?.ImagesIfLoaded?.Find(input) is { State: Media.ImageAvailability.CompletelyAvailable })
        {
            realm.SelectImageCoordinate(input, point.X, point.Y);
            return;
        }

        realm.SelectImageCoordinate(input, 0, 0);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#the-label-element — forward the click to the labeled
    /// control, unless the click was already inside interactive content.
    /// </summary>
    /// <remarks>
    /// The exclusion is what stops the forward from looping: the synthetic click at the control bubbles back
    /// up through this very label, whose activation behaviour then sees a target that <i>is</i> an interactive
    /// content descendant and does nothing. It is HTML's own loop guard, not one added here.
    /// </remarks>
    private static void RunLabel(DomNodeObject wrapper, Element label, JsEvent ev)
    {
        if (HtmlLabelAssociation.ControlFor(label, wrapper.DomRealm.NativeReadCheckpoint, wrapper.DomRealm.CancellationToken) is not { } control)
        {
            return;
        }

        if (ev.Target is DomNodeObject { Node: { } target } && IsInsideInteractiveContent(label, target))
        {
            return;
        }

        // https://html.spec.whatwg.org/multipage/interaction.html#fire-a-synthetic-pointer-event — the
        // forwarded click carries the original's trust, so a user's click on a label reaches the control as a
        // trusted click and a script's `label.click()` reaches it as an untrusted one.
        InputDispatcher.FireSyntheticClick(wrapper.DomRealm.WrapNode(control), ev.IsTrusted);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-elements.html#the-option-element — selecting an option
    /// changes the select's value and fires <c>input</c> then <c>change</c> at the <b>select</b>, which is
    /// where a page listens.
    /// </summary>
    /// <remarks>
    /// Internal rather than private because <c>Page.SelectAsync</c> reaches the same algorithm: a host
    /// choosing an option and a client clicking one have to change the same state and fire the same two
    /// events, or a page could tell the two apart.
    /// </remarks>
    internal static void SelectOption(DomRealm dom, Element option)
    {
        using var mutation = dom.MutateLayout();
        if (!EventDom.IsHtml(option, "option")
            || HtmlSelectAncestry.GetNearestSelect(option, dom.NativeReadCheckpoint, dom.CancellationToken) is not { } select)
        {
            return;
        }

        var state = select.GetHtmlState()!.GetSelectState(dom.CancellationToken)!;
        if (!state.ApplyUserSelection(option, selected: true, dom.CancellationToken))
        {
            return;
        }

        var target = dom.WrapNode(select);
        dom.Engine.Tasks.Post(() =>
        {
            using var update = dom.MutateLayout();
            state.CompleteUserSelection(dom.CancellationToken);
            FireInputAndChange(target);
        });
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/interactive-elements.html#the-summary-element — toggle the
    /// <c>open</c> attribute of the <c>&lt;details&gt;</c> this is the summary for.
    /// </summary>
    /// <remarks>
    /// The <c>toggle</c> event is not fired here but queued, which is what
    /// https://html.spec.whatwg.org/multipage/interactive-elements.html#details-notification-task-steps says:
    /// it is a task on the details toggle event task source, so a script that toggles twice in one turn sees
    /// the events after its own code returns. It is queued on the engine's own task queue, so a page loop's
    /// <c>ProcessTasks</c> delivers it.
    /// </remarks>
    private static void RunSummary(DomNodeObject wrapper, Element summary)
    {
        if (summary.ParentNode is not Element details || !EventDom.IsHtml(details, "details") || !ReferenceEquals(FirstSummaryOf(details), summary))
        {
            return;
        }

        if (details.HasContentAttribute("open"))
        {
            details.RemoveAttribute("open");
        }
        else
        {
            details.SetAttribute("open", "");
        }

        ScheduleToggle(wrapper.DomRealm, details);
    }

    // HTML details notification task steps: one task per details element until delivery.
    internal static void ScheduleToggle(DomRealm dom, Element details)
    {
        var pending = PendingToggles.GetOrCreateValue(details);
        if (pending.Scheduled) return;
        pending.Scheduled = true;
        var target = dom.WrapNode(details);
        dom.Engine.Tasks.Post(() =>
        {
            pending.Scheduled = false;
            Fire(target, "toggle", bubbles: false, composed: false);
        });
    }

    private sealed class PendingToggle
    {
        internal bool Scheduled;
    }

    /// <summary>
    /// The first <c>&lt;summary&gt;</c> child, which is the only one that is "the summary for" a details —
    /// https://html.spec.whatwg.org/multipage/interactive-elements.html#the-summary-element.
    /// </summary>
    private static Element? FirstSummaryOf(Element details)
    {
        for (var node = details.FirstChild; node is not null; node = node.NextSibling)
        {
            if (node is Element child && EventDom.IsHtml(child, "summary"))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#interactive-content — whether the click landed on
    /// interactive content inside the label, which is what suppresses the label's forward.
    /// </summary>
    private static bool IsInsideInteractiveContent(Element label, Node target)
    {
        for (var node = target; node is not null && !ReferenceEquals(node, label); node = node.ParentNode)
        {
            if (node is Element element && IsInteractiveContent(element))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInteractiveContent(Element element) => element.NamespaceUri == Namespaces.Html && element.LocalName switch
    {
        "button" or "details" or "embed" or "iframe" or "label" or "select" or "textarea" => true,
        "a" => element.HasContentAttribute("href"),
        "input" => HtmlInputTypes.Get(element) != HtmlInputType.Hidden,
        "audio" or "video" => element.HasContentAttribute("controls"),
        "img" or "object" => element.HasContentAttribute("usemap"),
        _ => false,
    };

    /// <summary>
    /// https://dom.spec.whatwg.org/#connected — whether the node's <i>shadow-including root</i> is a
    /// document, which is what "connected" means and what the checkbox and radio activation behaviours ask
    /// before they announce anything.
    /// </summary>
    /// <remarks>
    /// The walk crosses a shadow boundary through the root's host, so a control inside an open or closed
    /// shadow tree of a connected host is connected — the eight shadow cases of
    /// <c>Event-dispatch-detached-input-and-change.html</c> are what say so. AngleSharp has no member that
    /// answers this: <c>Node.OwnerDocument</c> is the node document whether or not the node is in it.
    /// </remarks>
    private static bool IsConnected(Node node)
    {
        var current = node;

        while (true)
        {
            if (current.ParentNode is { } parent)
            {
                current = parent;
                continue;
            }

            if (current is ShadowRoot { Host: { } host })
            {
                current = host;
                continue;
            }

            return current is Document;
        }
    }

    /// <summary>
    /// The input type comparison HTML asks for: the <c>type</c> content attribute's keyword, matched
    /// ASCII-case-insensitively. AngleSharp's <c>Type</c> property already answers the lower-case keyword and
    /// the missing-value default, so this is a plain ordinal compare on top of it.
    /// </summary>
    internal static bool IsType(Element input, string type)
        => EventDom.IsHtml(input, "input") && string.Equals(EventDom.InputType(input), type, StringComparison.Ordinal);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#the-input-element — "fire an event named input …
    /// then fire an event named change". Both are plain <c>Event</c>s: the <c>InputEvent</c> interface is for
    /// editing, not for a checkbox.
    /// </summary>
    internal static void FireInputAndChange(DomNodeObject target)
    {
        Fire(target, "input", bubbles: true, composed: true);
        Fire(target, "change", bubbles: true, composed: false);
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-event-fire for an event the engine created.</summary>
    internal static void Fire(JsEventTarget target, string type, bool bubbles, bool composed)
    {
        var events = target._realm.Intrinsics.Event;
        target.DispatchEvent(events.CreateTrustedEvent(
            JsString.Create(type),
            new EventInit(bubbles, Cancelable: false, composed)));
    }

    /// <summary>What a legacy pre-activation behaviour changed, so a canceled activation can undo it.</summary>
    private sealed class PreActivationSnapshot
    {
        private PreActivationSnapshot(bool isCheckbox, bool wasChecked, bool wasIndeterminate, Element? previouslyChecked)
        {
            IsCheckbox = isCheckbox;
            WasChecked = wasChecked;
            WasIndeterminate = wasIndeterminate;
            PreviouslyChecked = previouslyChecked;
        }

        internal bool IsCheckbox { get; }

        internal bool WasChecked { get; }

        internal bool WasIndeterminate { get; }

        internal Element? PreviouslyChecked { get; }

        internal static PreActivationSnapshot ForCheckbox(bool wasChecked, bool wasIndeterminate)
            => new(isCheckbox: true, wasChecked, wasIndeterminate, previouslyChecked: null);

        internal static PreActivationSnapshot ForRadio(Element? previouslyChecked)
            => new(isCheckbox: false, wasChecked: false, wasIndeterminate: false, previouslyChecked);
    }
}
