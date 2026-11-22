using System.Globalization;
using Jint.HtmlParser;
using Jint.Browser.Dom.Views;

namespace Jint.Browser.Layout;

/// <summary>
/// https://drafts.csswg.org/css-flexbox/#layout-algorithm - single-line horizontal placement in the
/// synthetic box model. Intrinsic text measurement is still one row, not a font or CSS layout engine.
/// </summary>
internal static class FlexRow
{
    internal static bool IsHorizontal(Element element, CssCascade.Traversal? cascade)
    {
        if (cascade?.Of(element) is not { } visibility
            || CssCascade.ValueOf(visibility, "display") is not ("flex" or "inline-flex"))
        {
            return false;
        }

        var style = cascade.LayoutOf(element);
        return style is not null
            && CssCascade.ValueOf(style, "flex-direction") is not ("column" or "column-reverse")
            && (CssCascade.ValueOf(style, "flex-wrap") is not ("wrap" or "wrap-reverse") || FitsOnOneLine(element, style, cascade));
    }

    /// <summary>
    /// Whether a wrapping container's items fit on one line, which makes it the single-line row it is laid out as
    /// (https://drafts.csswg.org/css-flexbox/#algo-line-break): their percentage bases share the container, their
    /// other bases and the gaps the viewport, and an automatic basis is one row. A Bootstrap row of a 25% and a 75%
    /// column, or a breadcrumb, stays on one line; three full-width columns wrap, one under the other.
    /// </summary>
    private static bool FitsOnOneLine(Element element, Styling.NativeCssComputedStyle style, CssCascade.Traversal cascade)
    {
        var width = cascade.ReadContext?.ViewportWidth ?? Viewport.Default.Width;
        var share = 0d;
        var pixels = 0d;
        var items = 0;
        for (var node = element.FirstChild; node is not null; node = node.NextSibling)
        {
            if (node is not Element child || cascade.Of(child) is not { } item || CssCascade.ValueOf(item, "display") is "none" || FlatLayout.IsMetadata(child)
                || CssCascade.ValueOf(item, "position") is "absolute" or "fixed")
            {
                continue;
            }

            items++;
            var basis = CssCascade.ValueOf(item, "flex-basis");
            if (basis is null or "" or "auto" or "content")
            {
                basis = CssCascade.ValueOf(item, "width");
            }

            if (basis is not null && basis.EndsWith('%') && Number(basis[..^1]) is { } percent)
            {
                share += percent / 100;
            }
            else
            {
                pixels += Length(basis, width) ?? FlatLayout.RowHeight;
            }
        }

        pixels += (Length(CssCascade.ValueOf(style, "column-gap"), width) ?? 0) * Math.Max(0, items - 1);
        return share <= 1.0001 && pixels <= (1 - Math.Min(1, share)) * width;
    }

    /// <summary>Whether <paramref name="element"/> is a flex container laying its items out in a column.</summary>
    internal static bool IsColumn(Element element, CssCascade.Traversal? cascade)
        => cascade?.Of(element) is { } style
            && CssCascade.ValueOf(style, "display") is "flex" or "inline-flex"
            && CssCascade.ValueOf(style, "flex-direction") is "column" or "column-reverse";

    /// <summary>An item's flex-grow factor, zero when it does not grow.</summary>
    internal static double Grow(Element element, CssCascade.Traversal? cascade)
        => Number(cascade?.LayoutOf(element) is { } style ? CssCascade.ValueOf(style, "flex-grow") : null) ?? 0;

    internal static bool IsReversed(Element element, CssCascade.Traversal? cascade)
    {
        var style = cascade?.LayoutOf(element);
        return style is not null
            && ((CssCascade.ValueOf(style, "flex-direction") == "row-reverse")
                != (CssCascade.ValueOf(style, "direction") == "rtl"));
    }

    internal static string Alignment(Element child, Element parent, CssCascade.Traversal? cascade)
    {
        var style = cascade?.LayoutOf(child);
        var alignment = style is null ? null : CssCascade.ValueOf(style, "align-self");
        if (alignment is null or "" or "auto")
        {
            style = cascade?.LayoutOf(parent);
            alignment = style is null ? null : CssCascade.ValueOf(style, "align-items");
        }

        return alignment is null or "" or "normal" ? "stretch" : alignment;
    }

    internal static double[] Widths(IReadOnlyList<Element> children, double available, CssCascade.Traversal? cascade, Action? step = null, CancellationToken cancellationToken = default)
        => Widths(children, available, cascade, autoBasis: null, step, cancellationToken);

    /// <summary>
    /// The main size an item with no definite basis or width starts from: its explicit flex-basis or width in
    /// pixels, or <paramref name="autoBasis"/>'s content estimate, or one row.
    /// </summary>
    internal static double BaseSize(Element child, double available, CssCascade.Traversal? cascade, Func<Element, double>? autoBasis)
    {
        var style = cascade?.LayoutOf(child);
        return Resolved(style, "flex-basis", available, cascade) ?? Resolved(style, "width", available, cascade)
            ?? autoBasis?.Invoke(child) ?? FlatLayout.RowHeight;
    }

    /// <summary>
    /// A length property of a flex item in pixels, resolved as <c>getComputedStyle</c> resolves it — a pagination
    /// button's <c>min-width: 2.5rem</c>, a filter's <c>flex: 0 0 8.5rem</c> — with a percentage of
    /// <paramref name="available"/>; <see langword="null"/> for <c>auto</c>, <c>none</c> or a length that needs
    /// layout.
    /// </summary>
    private static double? Resolved(Styling.NativeCssComputedStyle? style, string name, double available, CssCascade.Traversal? cascade)
    {
        if (style is null)
        {
            return null;
        }

        var viewport = Viewport.Default with { Width = (int) (cascade?.ReadContext?.ViewportWidth ?? Viewport.Default.Width) };
        return Length(ResolvedLengths.Resolve(name, style.GetNormalizedProperty(name), style, viewport), available);
    }

    internal static double[] Widths(IReadOnlyList<Element> children, double available, CssCascade.Traversal? cascade, Func<Element, double>? autoBasis, Action? step = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var widths = new double[children.Count];
        var minimums = new double[children.Count];
        var explicitMinimums = new double?[children.Count];
        var maximums = new double?[children.Count];
        var growth = new double[children.Count];
        var shrinkage = new double[children.Count];
        var basisTotal = 0d;
        var growthTotal = 0d;
        var shrinkFactorTotal = 0d;
        var shrinkageTotal = 0d;
        for (var i = 0; i < children.Count; i++)
        {
            step?.Invoke();
            if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            var style = cascade?.LayoutOf(children[i]);
            widths[i] = BaseSize(children[i], available, cascade, autoBasis);

            // https://drafts.csswg.org/css-flexbox/#min-size-auto: an item whose overflow is visible does not
            // shrink below its content's minimum width. There is no text measurement here, so the content is
            // taken to be at least one row wide — enough for a port, a toggle or an icon to keep a box a pointer
            // can hit — unless the author gave a minimum of their own.
            //
            // The content-based minimum is capped by a definite width, never by the flex basis: a `flex: 1 0 0%`
            // column holding a checkbox keeps its row, it does not collapse to nothing under its neighbour. An item
            // with no content has none to keep.
            var overflow = style is null ? null : CssCascade.ValueOf(style, "overflow-x");
            explicitMinimums[i] = Resolved(style, "min-width", available, cascade);
            maximums[i] = Resolved(style, "max-width", available, cascade);
            var specified = Resolved(style, "width", available, cascade);
            minimums[i] = explicitMinimums[i]
                ?? (overflow is null or "" or "visible"
                    ? Math.Min(specified ?? FlatLayout.RowHeight, HasContent(children[i]) ? FlatLayout.RowHeight : 0)
                    : 0);

            // §9.2 step 3.E: the hypothetical main size is the base size clamped by the min and max main sizes,
            // and it is what the free space is computed from.
            if (maximums[i] is { } hypotheticalMaximum) widths[i] = Math.Min(widths[i], hypotheticalMaximum);
            if (explicitMinimums[i] is { } hypotheticalMinimum) widths[i] = Math.Max(widths[i], hypotheticalMinimum);
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
            step?.Invoke();
            if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (free > 0 && growthTotal > 0)
            {
                widths[i] += free * growth[i] / Math.Max(1, growthTotal);
            }
            else if (free < 0 && shrinkageTotal > 0)
            {
                widths[i] = Math.Max(minimums[i], widths[i] + free * Math.Min(1, shrinkFactorTotal) * shrinkage[i] / shrinkageTotal);
            }
        }

        // https://drafts.csswg.org/css-flexbox/#resolve-flexible-lengths step 4.c: every item's used size is
        // clamped by its min and max main sizes, the minimum winning a conflict (CSS 2 §10.4). One pass, without
        // the redistribution the algorithm loops for: what the flat model needs is that an item's own
        // min-width or max-width holds, as a sidebar's `min-width: 240px` does in a browser.
        for (var i = 0; i < widths.Length; i++)
        {
            if (maximums[i] is { } maximum) widths[i] = Math.Min(widths[i], maximum);
            widths[i] = Math.Max(widths[i], minimums[i]);
        }

        return widths;
    }

    // Whether an item has anything for its content-based minimum to keep: an element, or text that is not blank.
    private static bool HasContent(Element item)
    {
        for (var node = item.FirstChild; node is not null; node = node.NextSibling)
        {
            if (node is Element || node is Text text && !string.IsNullOrWhiteSpace(text.Data))
            {
                return true;
            }
        }

        return false;
    }

    internal static double? Length(string? value, double available)
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
