using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
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

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-activation-behavior — whether this node has one at all, which
    /// is what lets the dispatcher choose it as the activation target.
    /// </summary>
    internal static bool Has(INode node) => node switch
    {
        IHtmlAnchorElement or IHtmlAreaElement => true,
        IHtmlButtonElement or IHtmlInputElement => true,
        IHtmlLabelElement => true,
        IHtmlOptionElement => true,
        IHtmlElement element => element.LocalName is "summary",
        _ => false,
    };

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-legacy-pre-activation-behavior, run before any listener so a
    /// listener sees the checkbox already toggled — which is exactly why a page's <c>onclick</c> can read
    /// <c>this.checked</c> and get the new value.
    /// </summary>
    internal static void LegacyPreActivationBehavior(DomNodeObject wrapper)
    {
        // A disabled control's activation behaviour does nothing, so its pre-activation behaviour must do
        // nothing either — otherwise the toggle would happen with no activation behaviour left to roll it
        // back. HTML reaches the same place by never letting a click at a disabled control be dispatched at
        // all; the events here are still dispatched, and this is what keeps the state right.
        if (wrapper.Node is not IHtmlInputElement { IsDisabled: false } input)
        {
            return;
        }

        // https://html.spec.whatwg.org/multipage/input.html#checkbox-state-(type=checkbox) and
        // #radio-button-state-(type=radio) — the two input types with a legacy-pre-activation behaviour.
        if (IsType(input, "checkbox"))
        {
            _snapshots.AddOrUpdate(wrapper, PreActivationSnapshot.ForCheckbox(input.IsChecked, input.IsIndeterminate));
            input.IsIndeterminate = false;
            input.IsChecked = !input.IsChecked;
            return;
        }

        if (IsType(input, "radio"))
        {
            var previously = CheckedInGroup(input);
            _snapshots.AddOrUpdate(wrapper, PreActivationSnapshot.ForRadio(previously));
            SelectRadio(input);
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-legacy-canceled-activation-behavior — a listener called
    /// <c>preventDefault()</c>, so the checkedness the pre-activation behaviour changed goes back.
    /// </summary>
    internal static void LegacyCanceledActivationBehavior(DomNodeObject wrapper)
    {
        if (wrapper.Node is not IHtmlInputElement input || !_snapshots.TryGetValue(wrapper, out var snapshot))
        {
            return;
        }

        _snapshots.Remove(wrapper);

        if (snapshot.IsCheckbox)
        {
            input.IsChecked = snapshot.WasChecked;
            input.IsIndeterminate = snapshot.WasIndeterminate;
            return;
        }

        // A radio group's rollback is not "uncheck this one": HTML says to restore the element that was
        // checked before, and a group with nothing checked stays with nothing checked.
        input.IsChecked = false;

        if (snapshot.PreviouslyChecked is { } previous)
        {
            previous.IsChecked = true;
        }
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#eventtarget-activation-behavior, run after the listeners when the event was
    /// not canceled.
    /// </summary>
    internal static void Run(DomNodeObject wrapper, JsEvent ev)
    {
        var realm = BrowserEventRealm.Of(wrapper.DomRealm.Engine);

        switch (wrapper.Node)
        {
            case IHtmlAnchorElement anchor:
                FollowHyperlink(realm, anchor, anchor.Href, anchor.Target);
                return;

            case IHtmlAreaElement area:
                FollowHyperlink(realm, area, area.Href, area.Target);
                return;

            case IHtmlButtonElement button:
                RunButton(wrapper, button);
                return;

            case IHtmlInputElement input:
                RunInput(realm, wrapper, input);
                return;

            case IHtmlLabelElement label:
                RunLabel(wrapper, label, ev);
                return;

            case IHtmlOptionElement option:
                RunOption(wrapper, option);
                return;

            case IHtmlElement element when element.LocalName is "summary":
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
    private static void FollowHyperlink(BrowserEventRealm realm, IHtmlElement source, string? url, string? target)
    {
        if (!source.HasAttribute("href"))
        {
            return;
        }

        realm.ActivationHost.FollowHyperlink(realm, source, url ?? "", target ?? "");
    }

    /// <summary>https://html.spec.whatwg.org/multipage/form-elements.html#the-button-element.</summary>
    private static void RunButton(DomNodeObject wrapper, IHtmlButtonElement button)
    {
        if (button.IsDisabled)
        {
            return;
        }

        switch (button.Type)
        {
            case "submit":
                FormSubmission.Submit(wrapper.DomRealm, button.Form, button);
                break;
            case "reset":
                FormSubmission.Reset(wrapper.DomRealm, button.Form);
                break;
        }
    }

    /// <summary>https://html.spec.whatwg.org/multipage/input.html#input-activation-behavior.</summary>
    private static void RunInput(BrowserEventRealm realm, DomNodeObject wrapper, IHtmlInputElement input)
    {
        if (input.IsDisabled)
        {
            return;
        }

        switch (input.Type)
        {
            case "submit":
            case "image":
                FormSubmission.Submit(wrapper.DomRealm, input.Form, input);
                return;

            case "reset":
                FormSubmission.Reset(wrapper.DomRealm, input.Form);
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
    /// https://html.spec.whatwg.org/multipage/forms.html#the-label-element — forward the click to the labeled
    /// control, unless the click was already inside interactive content.
    /// </summary>
    /// <remarks>
    /// The exclusion is what stops the forward from looping: the synthetic click at the control bubbles back
    /// up through this very label, whose activation behaviour then sees a target that <i>is</i> an interactive
    /// content descendant and does nothing. It is HTML's own loop guard, not one added here.
    /// </remarks>
    private static void RunLabel(DomNodeObject wrapper, IHtmlLabelElement label, JsEvent ev)
    {
        if (LabeledControl(label) is not { } control)
        {
            return;
        }

        if (ev.Target is DomNodeObject target && IsInsideInteractiveContent(label, target.Node))
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
    private static void RunOption(DomNodeObject wrapper, IHtmlOptionElement option)
    {
        if (option.IsDisabled || Ancestor<IHtmlSelectElement>(option) is not { } select || select.IsDisabled)
        {
            return;
        }

        if (option.IsSelected)
        {
            return;
        }

        if (!select.IsMultiple)
        {
            foreach (var other in select.Options)
            {
                other.IsSelected = false;
            }
        }

        option.IsSelected = true;
        FireInputAndChange(wrapper.DomRealm.WrapNode(select));
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
    private static void RunSummary(DomNodeObject wrapper, IHtmlElement summary)
    {
        if (summary.ParentElement is not IHtmlDetailsElement details || !ReferenceEquals(FirstSummaryOf(details), summary))
        {
            return;
        }

        details.IsOpen = !details.IsOpen;

        var target = wrapper.DomRealm.WrapNode(details);
        var engine = wrapper.DomRealm.Engine;
        engine.Tasks.Post(() => Fire(target, "toggle", bubbles: false, composed: false));
    }

    /// <summary>
    /// The first <c>&lt;summary&gt;</c> child, which is the only one that is "the summary for" a details —
    /// https://html.spec.whatwg.org/multipage/interactive-elements.html#the-summary-element.
    /// </summary>
    private static IElement? FirstSummaryOf(IHtmlDetailsElement details)
    {
        foreach (var child in details.Children)
        {
            if (string.Equals(child.LocalName, "summary", StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#radio-button-state-(type=radio) — the radio button
    /// group: same form owner, same <c>name</c>, same tree.
    /// </summary>
    private static IHtmlInputElement? CheckedInGroup(IHtmlInputElement radio)
    {
        foreach (var member in Group(radio))
        {
            if (member.IsChecked)
            {
                return member;
            }
        }

        return null;
    }

    private static void SelectRadio(IHtmlInputElement radio)
    {
        foreach (var member in Group(radio))
        {
            member.IsChecked = ReferenceEquals(member, radio);
        }

        radio.IsChecked = true;
    }

    private static IEnumerable<IHtmlInputElement> Group(IHtmlInputElement radio)
    {
        var name = radio.Name;
        var owner = radio.Form;
        var root = (INode?) owner ?? radio.Owner;

        if (root is null || string.IsNullOrEmpty(name))
        {
            yield return radio;
            yield break;
        }

        foreach (var candidate in Descendants(root))
        {
            if (candidate is IHtmlInputElement input
                && IsType(input, "radio")
                && string.Equals(input.Name, name, StringComparison.Ordinal)
                && ReferenceEquals(input.Form, owner))
            {
                yield return input;
            }
        }
    }

    private static IEnumerable<INode> Descendants(INode root)
    {
        foreach (var child in root.ChildNodes)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#interactive-content — whether the click landed on
    /// interactive content inside the label, which is what suppresses the label's forward.
    /// </summary>
    private static bool IsInsideInteractiveContent(IHtmlLabelElement label, INode target)
    {
        for (var node = target; node is not null && !ReferenceEquals(node, label); node = node.Parent)
        {
            if (node is IElement element && IsInteractiveContent(element))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInteractiveContent(IElement element) => element.LocalName switch
    {
        "a" or "button" or "details" or "embed" or "iframe" or "label" or "select" or "textarea" => true,
        "input" => !string.Equals(element.GetAttribute("type"), "hidden", StringComparison.OrdinalIgnoreCase),
        "audio" or "video" => element.HasAttribute("controls"),
        "img" or "object" => element.HasAttribute("usemap"),
        _ => false,
    };

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#labeled-control — the control a <c>&lt;label&gt;</c>
    /// labels: the element its <c>for</c> attribute names, and otherwise the first labelable descendant.
    /// </summary>
    /// <remarks>
    /// Computed rather than read off AngleSharp, whose <c>IHtmlLabelElement.Control</c> answers
    /// <see langword="null"/> for a control the label <i>contains</i> — which is the commoner of the two
    /// spellings and the one <c>&lt;label&gt;&lt;input type=checkbox&gt;&lt;span&gt;text&lt;/span&gt;&lt;/label&gt;</c>
    /// uses. It is recorded as an AngleSharp divergence in <c>Jint.Browser/Dom/AGENTS.md</c> beside
    /// <c>input.labels</c>, which is the same gap seen from the other end.
    /// </remarks>
    private static IHtmlElement? LabeledControl(IHtmlLabelElement label)
    {
        if (label.GetAttribute("for") is { Length: > 0 } id)
        {
            return label.Owner?.GetElementById(id) is IHtmlElement named && IsLabelable(named) ? named : null;
        }

        foreach (var descendant in label.QuerySelectorAll("button, input, meter, output, progress, select, textarea"))
        {
            if (descendant is IHtmlElement candidate && IsLabelable(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#category-label — the seven labelable element kinds.
    /// A hidden input is the one exception the list carries with it.
    /// </summary>
    private static bool IsLabelable(IHtmlElement element) => element switch
    {
        IHtmlInputElement input => !IsType(input, "hidden"),
        IHtmlButtonElement or IHtmlSelectElement or IHtmlTextAreaElement => true,
        _ => element.LocalName is "meter" or "output" or "progress",
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
    /// answers this: <c>INode.Owner</c> is the node document whether or not the node is in it.
    /// </remarks>
    private static bool IsConnected(INode node)
    {
        var current = node;

        while (true)
        {
            if (current.Parent is { } parent)
            {
                current = parent;
                continue;
            }

            if (current is IShadowRoot { Host: { } host })
            {
                current = host;
                continue;
            }

            return current is IDocument;
        }
    }

    private static T? Ancestor<T>(INode node) where T : class
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// The input type comparison HTML asks for: the <c>type</c> content attribute's keyword, matched
    /// ASCII-case-insensitively. AngleSharp's <c>Type</c> property already answers the lower-case keyword and
    /// the missing-value default, so this is a plain ordinal compare on top of it.
    /// </summary>
    internal static bool IsType(IHtmlInputElement input, string type)
        => string.Equals(input.Type, type, StringComparison.Ordinal);

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
        var events = target.Engine._mainRealm.Intrinsics.Event;
        target.DispatchEvent(events.CreateTrustedEvent(
            JsString.Create(type),
            new EventInit(bubbles, Cancelable: false, composed)));
    }

    /// <summary>What a legacy pre-activation behaviour changed, so a canceled activation can undo it.</summary>
    private sealed class PreActivationSnapshot
    {
        private PreActivationSnapshot(bool isCheckbox, bool wasChecked, bool wasIndeterminate, IHtmlInputElement? previouslyChecked)
        {
            IsCheckbox = isCheckbox;
            WasChecked = wasChecked;
            WasIndeterminate = wasIndeterminate;
            PreviouslyChecked = previouslyChecked;
        }

        internal bool IsCheckbox { get; }

        internal bool WasChecked { get; }

        internal bool WasIndeterminate { get; }

        internal IHtmlInputElement? PreviouslyChecked { get; }

        internal static PreActivationSnapshot ForCheckbox(bool wasChecked, bool wasIndeterminate)
            => new(isCheckbox: true, wasChecked, wasIndeterminate, previouslyChecked: null);

        internal static PreActivationSnapshot ForRadio(IHtmlInputElement? previouslyChecked)
            => new(isCheckbox: false, wasChecked: false, wasIndeterminate: false, previouslyChecked);
    }
}
