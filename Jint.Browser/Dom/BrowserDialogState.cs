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
    private bool _closing;

    private BrowserDialogState(Element element) => _element = element;

    internal static BrowserDialogState Of(DomRealm realm, Element element)
    {
        if (!EventDom.IsHtml(element, "dialog")) Throw.TypeError(realm.OwningRealm, "Illegal invocation of HTMLDialogElement member");
        return States.GetValue(element, static target => new BrowserDialogState(target));
    }

    internal string ReturnValue { get; set; } = "";
    internal bool Open => _element.HasContentAttribute("open");
    internal static bool IsModal => false;

    internal void Show(DomRealm realm)
    {
        if (Open) return;
        if (!FireToggle(realm, "beforetoggle", "closed", "open", cancelable: true) || Open) return;

        // Complete bounded candidate discovery before committing the transition. Focus itself
        // uses the existing Events focus update algorithm and does not allocate a second store.
        var control = FindFocusDelegate(realm);
        Check(realm);
        ScheduleToggle(realm, "closed", "open");
        _element.SetAttributeNS(null, "open", "");
        var focused = BrowserEventRealm.Of(realm.Engine).FocusedElement;
        _previousFocus = focused is null ? null : new WeakReference<Element>(focused);
        if (Open) FocusController.Focus(realm, control);
    }

    internal JsValue ShowModal(DomRealm realm)
        => DomFailures.Refuse(realm, "HTMLDialogElement.showModal", "NotSupportedError", "Modal dialogs are not available.");

    internal void Close(DomRealm realm, string? result = null)
    {
        if (!Open || _closing) return;
        _closing = true;
        try
        {
            FireToggle(realm, "beforetoggle", "open", "closed", cancelable: false);
            if (!Open) return;
            var focused = BrowserEventRealm.Of(realm.Engine).FocusedElement;
            var restore = focused is not null && IsWithin(realm, focused, _element);
            Check(realm);
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
        finally
        {
            _closing = false;
        }
    }

    private Element FindFocusDelegate(DomRealm realm)
    {
        if (_element.HasContentAttribute("autofocus")) return _element;
        Element? first = null;
        foreach (var candidate in NodeTraversal.DescendantElements(_element, () => realm.NativeReadCheckpoint(256), realm.CancellationToken))
        {
            if (!FocusController.IsFocusable(realm, candidate)) continue;
            first ??= candidate;
            if (candidate.HasContentAttribute("autofocus")) return candidate;
        }
        return first ?? _element;
    }

    private static bool IsWithin(DomRealm realm, Element element, Element ancestor)
    {
        var work = 0;
        for (Node? node = element; node is not null; node = node.ParentNode ?? (node as ShadowRoot)?.Host)
        {
            if ((++work & 255) == 0) Check(realm);
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

    private static void Check(DomRealm realm)
    {
        realm.CancellationToken.ThrowIfCancellationRequested();
        realm.Engine.Constraints.Check();
    }

    private sealed class PendingToggle(string oldState, string newState)
    {
        internal readonly string OldState = oldState;
        internal readonly string NewState = newState;
    }
}
