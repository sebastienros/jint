using System.Globalization;
using Jint.HtmlParser;

namespace Jint.Browser.Accessibility;

/// <summary>
/// The value a widget carries: what accname step 2C substitutes for an embedded control's content, and what
/// the tree publishes as <see cref="AxNode.Value"/>.
/// </summary>
internal static class ControlValue
{
    /// <summary>Returns the element's value for its role, or the empty string when the role has none.</summary>
    internal static string For(Element element, string role)
    {
        var ariaValueText = element.GetAttribute("aria-valuetext");
        if (!string.IsNullOrEmpty(ariaValueText) && IsRange(role))
        {
            return ariaValueText;
        }

        var ariaValueNow = element.GetAttribute("aria-valuenow");
        if (!string.IsNullOrEmpty(ariaValueNow) && IsRange(role))
        {
            return ariaValueNow;
        }

        switch (element)
        {
            case { NamespaceUri: Namespaces.Html, LocalName: "input" } when role is "textbox" or "searchbox" or "combobox" or "spinbutton" or "slider":
                return element.GetHtmlState()!.GetInputValueState(CancellationToken.None)!.GetValue(CancellationToken.None);

            case { NamespaceUri: Namespaces.Html, LocalName: "textarea" }:
                return element.GetHtmlState()!.TextArea!.GetValue(CancellationToken.None);

            case { NamespaceUri: Namespaces.Html, LocalName: "select" }:
                return SelectedText(element);

            case { NamespaceUri: Namespaces.Html, LocalName: "progress" }:
                return HtmlControlView.ProgressValue(element).ToString("0.############", CultureInfo.InvariantCulture);

            case { NamespaceUri: Namespaces.Html, LocalName: "meter" }:
                return HtmlControlView.Meter(element).Value.ToString("0.############", CultureInfo.InvariantCulture);

            case { NamespaceUri: Namespaces.Html, LocalName: "output" }:
                return ContentDom.TextContent(element);
        }

        if (role is "textbox" && Events.ContentEditing.HostOf(element) is not null)
        {
            return AccessibleName.Flatten(ContentDom.TextContent(element));
        }

        return string.Empty;
    }

    /// <summary>Returns the range a widget spans, when its role has one.</summary>
    internal static (double? Minimum, double? Maximum) Range(Element element, string role)
    {
        if (!IsRange(role))
        {
            return (null, null);
        }

        var minimum = Parse(element.GetAttribute("aria-valuemin"));
        var maximum = Parse(element.GetAttribute("aria-valuemax"));

        switch (element)
        {
            case { NamespaceUri: Namespaces.Html, LocalName: "input" } when role is "slider" or "spinbutton":
                minimum ??= Parse(element.GetAttribute("min"));
                maximum ??= Parse(element.GetAttribute("max"));
                break;

            case { NamespaceUri: Namespaces.Html, LocalName: "progress" }:
                minimum ??= 0;
                maximum ??= HtmlControlView.ProgressMaximum(element);
                break;

            case { NamespaceUri: Namespaces.Html, LocalName: "meter" }:
                minimum ??= HtmlControlView.Meter(element).Minimum;
                maximum ??= HtmlControlView.Meter(element).Maximum;
                break;
        }

        return (minimum, maximum);
    }

    private static bool IsRange(string role) =>
        role is "slider" or "spinbutton" or "progressbar" or "meter" or "scrollbar";

    private static string SelectedText(Element select)
    {
        var state = select.GetHtmlState()!.GetSelectState(CancellationToken.None)!;
        var index = state.GetSelectedIndex(CancellationToken.None);
        var option = index < 0 ? null : state.Options.Item((uint) index, CancellationToken.None);
        return option is null ? string.Empty
            : AccessibleName.Flatten(option.GetHtmlState()!.GetOptionState(CancellationToken.None)!.GetText(CancellationToken.None));
    }

    private static double? Parse(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
