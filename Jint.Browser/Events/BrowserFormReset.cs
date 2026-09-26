using Jint.Browser.CustomElements;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Browser.Events;

/// <summary>HTML control reset components over one native form-owner inventory.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#concept-form-reset</remarks>
internal static class BrowserFormReset
{
    internal static void Reset(DomRealm realm, Element form)
    {
        var controls = HtmlFormOwner.ControlsOf(form, realm.NativeReadCheckpoint, token: realm.CancellationToken,
            customElements: CustomElementRegistry.Of(realm.Engine)).ToArray();
        // Refuse unavailable components before changing the supported controls in this inventory.
        foreach (var control in controls)
        {
            realm.CancellationToken.ThrowIfCancellationRequested();
            if (CustomElementRegistry.Of(realm.Engine)?.TryGetRecord(control) is { FormAssociated: true })
                throw new NotSupportedException("Form-associated custom-element reset requires its reaction callback.");
            if (EventDom.IsHtml(control, "input") && HtmlInputTypes.Get(control) != HtmlInputType.File)
                control.GetHtmlState()!.GetInputValueState(realm.CancellationToken)!.GetValue(realm.CancellationToken);
        }
        foreach (var control in controls)
        {
            realm.NativeReadCheckpoint(1);
            if (control.NamespaceUri != Namespaces.Html) continue;
            switch (control.LocalName)
            {
                case "input" when HtmlInputTypes.Get(control) != HtmlInputType.File:
                    control.GetHtmlState()!.GetInputValueState(realm.CancellationToken)!.ResetValue(realm.CancellationToken);
                    if (HtmlInputTypes.Get(control) is HtmlInputType.Checkbox or HtmlInputType.Radio)
                        HtmlCheckednessAlgorithms.ResetCheckedness(control, realm.CancellationToken);
                    break;
                case "textarea":
                    control.GetHtmlState()!.TextArea!.Reset(realm.CancellationToken);
                    break;
                case "select":
                    control.GetHtmlState()!.GetSelectState(realm.CancellationToken)!.Reset(realm.CancellationToken);
                    break;
                case "output":
                    BrowserOutputValue.Reset(control, realm.NativeReadCheckpoint, realm.CancellationToken);
                    break;
            }
        }
    }
}
