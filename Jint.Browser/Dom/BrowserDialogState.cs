using System.Runtime.CompilerServices;
using Jint.Browser.Events;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>Non-modal dialog transitions; raw parsing and attribute reflection create no state.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/interactive-elements.html#the-dialog-element</remarks>
internal sealed class BrowserDialogState
{
    private static readonly ConditionalWeakTable<Element, BrowserDialogState> States = new();
    private readonly Element _element;
    private WeakReference<Element>? _previousFocus;
    private PendingToggle? _toggle;

    private BrowserDialogState(Element element) => _element = element;

    internal static BrowserDialogState Of(DomRealm realm, Element element)
    {
        if (!EventDom.IsHtml(element, "dialog")) Throw.TypeError(realm.OwningRealm, "Illegal invocation of HTMLDialogElement member");
        return States.GetValue(element, static target => new BrowserDialogState(target));
    }

    internal string ReturnValue { get; set; } = "";
    internal static bool IsModal => false;

    internal bool IsOpen(DomRealm realm, DomReadWork? work = null)
    {
        work ??= new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var open = IsOpen(work);
        work.Check();
        return open;
    }

    private bool IsOpen(DomReadWork work) => work.Attribute(_element, "open") is not null;

    internal void Show(DomRealm realm, DomReadWork? work = null)
    {
        work ??= new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var open = IsOpen(work);
        work.Check();
        if (open) return;
        if (!FireToggle(realm, "beforetoggle", "closed", "open", cancelable: true)) return;
        open = IsOpen(work);
        work.Check();
        if (open) return;

        // Complete bounded candidate discovery before committing the transition. Focus itself
        // uses the existing Events focus update algorithm and does not allocate a second store.
        var control = FindFocusDelegate(realm, work);
        work.Check();
        ScheduleToggle(realm, "closed", "open");
        _element.SetAttributeNS(null, "open", "");
        var focused = BrowserEventRealm.Of(realm.Engine).FocusedElement;
        _previousFocus = focused is null ? null : new WeakReference<Element>(focused);
        open = IsOpen(work);
        work.Check();
        if (open) FocusController.Focus(realm, control);
    }

#pragma warning disable CA1822 // Keep the unsupported operation on the concrete dialog receiver API.
    internal JsValue ShowModal(DomRealm realm)
        => DomFailures.Refuse(realm, "HTMLDialogElement.showModal", "NotSupportedError", "Modal dialogs are not available.");
#pragma warning restore CA1822

    internal void Close(DomRealm realm, string? result = null, DomReadWork? work = null)
    {
        work ??= new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var open = IsOpen(work);
        work.Check();
        if (!open) return;
        FireToggle(realm, "beforetoggle", "open", "closed", cancelable: false);
        open = IsOpen(work);
        work.Check();
        if (!open) return;
        var focused = BrowserEventRealm.Of(realm.Engine).FocusedElement;
        var restore = focused is not null && IsWithin(work, focused, _element);
        work.Check();
        ScheduleToggle(realm, "open", "closed");
        _element.RemoveAttributeNS(null, "open");
        if (result is not null) ReturnValue = result;
        var previous = _previousFocus;
        _previousFocus = null;
        if (restore && previous is not null && previous.TryGetTarget(out var target)
            && ReferenceEquals(target.OwnerDocument, _element.OwnerDocument))
        {
            FocusController.Focus(realm, target);
        }
        var wrapper = realm.WrapNode(_element);
        realm.Engine.Tasks.Post(() => ActivationBehaviors.Fire(wrapper, "close", bubbles: false, composed: false));
    }

    private Element FindFocusDelegate(DomRealm realm, DomReadWork work)
    {
        if (work.Attribute(_element, "autofocus") is not null) return _element;
        Element? first = null;
        foreach (var candidate in Descendants(_element, work))
        {
            if (!FocusController.IsFocusable(realm, candidate, work)) continue;
            first ??= candidate;
            if (work.Attribute(candidate, "autofocus") is not null) return candidate;
        }
        return first ?? _element;
    }

    private static bool IsWithin(DomReadWork work, Element element, Element ancestor)
    {
        for (Node? node = element; node is not null; node = node.ParentNode ?? (node as ShadowRoot)?.Host)
        {
            work.Step();
            if (ReferenceEquals(node, ancestor)) return true;
        }
        return false;
    }

    private bool FireToggle(DomRealm realm, string type, string oldState, string newState, bool cancelable)
        => realm.WrapNode(_element).DispatchEvent(BrowserDialogToggleEvent.Create(realm, type, oldState, newState, cancelable));

    private void ScheduleToggle(DomRealm realm, string oldState, string newState)
    {
        // Preserve the earliest old state while updating the eventual new state.
        if (_toggle is { } previous) oldState = previous.OldState;
        var pending = new PendingToggle(oldState, newState);
        _toggle = pending;
        var wrapper = realm.WrapNode(_element);
        realm.Engine.Tasks.Post(() =>
        {
            // Supersede the previous queued task so the final toggle takes its actual
            // position after tasks queued between this element's transitions.
            if (!ReferenceEquals(_toggle, pending)) return;
            _toggle = null;
            wrapper.DispatchEvent(BrowserDialogToggleEvent.Create(wrapper.DomRealm, "toggle", pending.OldState, pending.NewState, cancelable: false));
        });
    }

    private static IEnumerable<Element> Descendants(Element root, DomReadWork work)
    {
        Node? node = root.FirstChild;
        work.Step();
        while (node is not null)
        {
            if (node is Element element) yield return element;
            var child = node.FirstChild;
            work.Step();
            if (child is not null) { node = child; continue; }
            while (true)
            {
                var sibling = node.NextSibling;
                work.Step();
                if (sibling is not null) { node = sibling; break; }
                node = node.ParentNode!;
                work.Step();
                if (ReferenceEquals(node, root)) { node = null; break; }
            }
        }
    }

    private sealed class PendingToggle(string oldState, string newState)
    {
        internal readonly string OldState = oldState;
        internal readonly string NewState = newState;
    }
}
