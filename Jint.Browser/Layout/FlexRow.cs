using System.Globalization;
using AngleSharp.Dom;
using Jint.Browser.Dom.Views;

namespace Jint.Browser.Layout;

/// <summary>
/// https://drafts.csswg.org/css-flexbox/#layout-algorithm - single-line horizontal placement in the
/// synthetic box model. Intrinsic text measurement is still one row, not a font or CSS layout engine.
/// </summary>
internal static class FlexRow
{
    internal static bool IsHorizontal(IElement element, CssCascade.Traversal? cascade)
    {
        if (cascade?.Of(element) is not { } visibility
            || CssCascade.ValueOf(visibility, "display") is not ("flex" or "inline-flex"))
        {
            return false;
        }

        var style = cascade.CompleteOf(element);
        return style is not null
            && CssCascade.ValueOf(style, "flex-direction") is not ("column" or "column-reverse")
            && CssCascade.ValueOf(style, "flex-wrap") is not ("wrap" or "wrap-reverse");
    }

    internal static bool IsReversed(IElement element, CssCascade.Traversal? cascade)
    {
        var style = cascade?.CompleteOf(element);
        return style is not null
            && ((CssCascade.ValueOf(style, "flex-direction") == "row-reverse")
                != (CssCascade.ValueOf(style, "direction") == "rtl"));
    }

    internal static string Alignment(IElement child, IElement parent, CssCascade.Traversal? cascade)
    {
        var style = cascade?.CompleteOf(child);
        var alignment = style is null ? null : CssCascade.ValueOf(style, "align-self");
        if (alignment is null or "" or "auto")
        {
            style = cascade?.CompleteOf(parent);
            alignment = style is null ? null : CssCascade.ValueOf(style, "align-items");
        }

        return alignment is null or "" or "normal" ? "stretch" : alignment;
    }

    internal static double[] Widths(IElement[] children, double available, CssCascade.Traversal? cascade)
    {
        var widths = new double[children.Length];
        var growth = new double[children.Length];
        var shrinkage = new double[children.Length];
        var basisTotal = 0d;
        var growthTotal = 0d;
        var shrinkFactorTotal = 0d;
        var shrinkageTotal = 0d;
        for (var i = 0; i < children.Length; i++)
        {
            var style = cascade?.CompleteOf(children[i]);
            var basis = style is null ? null : CssCascade.ValueOf(style, "flex-basis");
            var width = style is null ? null : CssCascade.ValueOf(style, "width");
            widths[i] = Length(basis, available) ?? Length(width, available) ?? FlatLayout.RowHeight;
            growth[i] = Number(style is null ? null : CssCascade.ValueOf(style, "flex-grow")) ?? 0;
            var shrink = Number(style is null ? null : CssCascade.ValueOf(style, "flex-shrink")) ?? 1;
            shrinkage[i] = shrink * widths[i];
            basisTotal += widths[i];
            growthTotal += growth[i];
            shrinkFactorTotal += shrink;
            shrinkageTotal += shrinkage[i];
        }

        var free = available - basisTotal;
        for (var i = 0; i < widths.Length; i++)
        {
            if (free > 0 && growthTotal > 0)
            {
                widths[i] += free * growth[i] / Math.Max(1, growthTotal);
            }
            else if (free < 0 && shrinkageTotal > 0)
            {
                widths[i] = Math.Max(0, widths[i] + free * Math.Min(1, shrinkFactorTotal) * shrinkage[i] / shrinkageTotal);
            }
        }

        return widths;
    }

    private static double? Length(string? value, double available)
    {
        if (value is null)
        {
            return null;
        }

        if (value.EndsWith("px", StringComparison.Ordinal))
        {
            return Number(value[..^2]);
        }

        if (value.EndsWith('%'))
        {
            return Number(value[..^1]) * available / 100;
        }

        return value == "0" ? 0 : null;
    }

    private static double? Number(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number) && number >= 0 ? number : null;
}
