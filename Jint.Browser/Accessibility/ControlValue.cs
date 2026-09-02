using System.Globalization;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Accessibility;

/// <summary>
/// The value a widget carries: what accname step 2C substitutes for an embedded control's content, and what
/// the tree publishes as <see cref="AxNode.Value"/>.
/// </summary>
internal static class ControlValue
{
    /// <summary>Returns the element's value for its role, or the empty string when the role has none.</summary>
    internal static string For(IElement element, string role)
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
            case IHtmlInputElement input when role is "textbox" or "searchbox" or "combobox" or "spinbutton" or "slider":
                return input.Value ?? string.Empty;

            case IHtmlTextAreaElement textArea:
                return textArea.Value ?? string.Empty;

            case IHtmlSelectElement select:
                return SelectedText(select);

            case IHtmlProgressElement progress:
                return progress.Value.ToString("0.############", CultureInfo.InvariantCulture);

            case IHtmlMeterElement meter:
                return meter.Value.ToString("0.############", CultureInfo.InvariantCulture);

            case IHtmlOutputElement output:
                return output.Value ?? string.Empty;
        }

        if (role is "textbox" && element is IHtmlElement { IsContentEditable: true })
        {
            return AccessibleName.Flatten(element.TextContent);
        }

        return string.Empty;
    }

    /// <summary>Returns the range a widget spans, when its role has one.</summary>
    internal static (double? Minimum, double? Maximum) Range(IElement element, string role)
    {
        if (!IsRange(role))
        {
            return (null, null);
        }

        var minimum = Parse(element.GetAttribute("aria-valuemin"));
        var maximum = Parse(element.GetAttribute("aria-valuemax"));

        switch (element)
        {
            case IHtmlInputElement input when role is "slider" or "spinbutton":
                minimum ??= Parse(input.GetAttribute("min"));
                maximum ??= Parse(input.GetAttribute("max"));
                break;

            case IHtmlProgressElement progress:
                minimum ??= 0;
                maximum ??= progress.Maximum;
                break;

            case IHtmlMeterElement meter:
                minimum ??= meter.Minimum;
                maximum ??= meter.Maximum;
                break;
        }

        return (minimum, maximum);
    }

    private static bool IsRange(string role) =>
        role is "slider" or "spinbutton" or "progressbar" or "meter" or "scrollbar";

    private static string SelectedText(IHtmlSelectElement select)
    {
        foreach (var option in select.Options)
        {
            if (option.IsSelected)
            {
                return AccessibleName.Flatten(option.Text ?? string.Empty);
            }
        }

        return string.Empty;
    }

    private static double? Parse(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
