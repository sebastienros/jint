using System.Globalization;
using AngleSharp.Dom;
using Jint.Browser.Runtime;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The ten properties <c>getComputedStyle</c> answers for even when the cascade declared nothing.
/// </summary>
/// <remarks>
/// <para>
/// <a href="https://drafts.csswg.org/cssom/#resolved-values">CSSOM</a> says a computed style declaration
/// answers a <i>resolved value</i> for every supported longhand — the initial value where nothing is
/// declared, and the used value for the box properties. AngleSharp.Css reports the cascade's declarations
/// and nothing else, so a property no rule mentions is the empty string. The standing decision in
/// <c>Jint.Tests.Browser/Views/ComputedStyleTests</c> is not to keep an initial-value table here; this is
/// the exception to it, and it is deliberately the smallest one that makes the browser drivable.
/// </para>
/// <para>
/// <b>Why exactly these ten.</b> They are what an automation client reads to decide that an element can be
/// interacted with. Playwright's actionability check ends in <c>style.visibility !== "visible"</c> and then
/// <c>rect.width &gt; 0 &amp;&amp; rect.height &gt; 0</c>; its role engine drops an element whose
/// <c>display</c> is <c>none</c> and treats <c>contents</c> as a pass-through; its pointer path walks the
/// ancestors for the first non-empty <c>pointer-events</c>. <c>opacity</c>, <c>overflow</c> and
/// <c>position</c> are the rest of the same question — is this element on the page, and is it reachable.
/// Every other property stays the declared cascade, because a value nothing declares and nothing reads is a
/// value this browser would only be guessing at.
/// </para>
/// <para>
/// <b>Declared beats resolved, always.</b> These are consulted only where
/// <c>ICssStyleDeclaration.GetPropertyValue</c> answers the empty string, so an author rule, a user-agent
/// rule and an inline style all win — including an inherited one, which is how
/// <c>visibility: hidden</c> on an ancestor still reads as <c>hidden</c> on its descendants.
/// </para>
/// <para>
/// <b>The geometry is the flat box model's, and only where the element has a box.</b> <c>width</c> and
/// <c>height</c> answer the row model's rectangle in <c>px</c> — the same numbers
/// <c>getBoundingClientRect</c> gives, so a client that compares the two is told one story. An element with
/// no box (not rendered, detached, or belonging to another document) answers <c>auto</c>, which is both the
/// initial value and what a browser answers for an element <c>display: none</c> took out of the layout. A
/// <i>declared</i> width wins over the model, which is the one place this deliberately diverges from CSSOM's
/// used value: every box here is exactly as wide as the viewport, so answering the model for an element that
/// declared <c>width: 100px</c> would replace a true answer with a false one.
/// </para>
/// </remarks>
internal static class ResolvedStyle
{
    /// <summary>The value <paramref name="property"/> resolves to, or <see langword="null"/> for the rest.</summary>
    /// <param name="property">The CSS property name, as CSSOM hands it over — lower case, but not assumed to be.</param>
    /// <param name="element">The element the style was computed for.</param>
    /// <param name="runtime">The page whose layout answers the geometry.</param>
    internal static string? ValueOf(string property, IElement element, PageRuntime runtime)
    {
        // https://drafts.csswg.org/cssom/#dom-cssstyledeclaration-getpropertyvalue step 1 lower-cases the
        // name before anything looks at it, and the generated CSS attribute accessors pass the constant.
        switch (property.ToLowerInvariant())
        {
            // https://drafts.csswg.org/css2/#propdef-visibility — initial: visible. The one the whole
            // exception exists for.
            case "visibility":
                return "visible";

            // https://drafts.csswg.org/css-display-3/#propdef-display — initial: inline. AngleSharp's
            // default sheet declares `display` for most elements and forgets a dozen HTML5 sectioning
            // elements, which is a divergence of its own recorded in Jint.Browser/AGENTS.md: those read
            // `inline` here rather than the `block` HTML's rendering section gives them.
            case "display":
                return "inline";

            // https://drafts.csswg.org/css-color-4/#propdef-opacity — initial: 1.
            case "opacity":
                return "1";

            // https://svgwg.org/svg2-draft/interact.html#PointerEventsProperty — initial: auto.
            case "pointer-events":
                return "auto";

            // https://drafts.csswg.org/css-overflow-3/#propdef-overflow and its two longhands — initial:
            // visible. Each resolves on its own, so a page that declared only `overflow-x` reads `visible`
            // for the shorthand rather than the `"scroll visible"` pair a browser serializes; the shorthand
            // is not what a client reads, and computing one would mean a serializer this has no use for.
            case "overflow":
            case "overflow-x":
            case "overflow-y":
                return "visible";

            // https://drafts.csswg.org/css-position-3/#propdef-position — initial: static.
            case "position":
                return "static";

            // https://drafts.csswg.org/css-sizing-3/#propdef-width and #propdef-height — initial: auto, and
            // the used value in px for an element the flat model gave a row to.
            case "width":
                return Extent(element, runtime, horizontal: true);

            case "height":
                return Extent(element, runtime, horizontal: false);

            default:
                return null;
        }
    }

    /// <summary>The element's box in one axis, or <c>auto</c> when it has none.</summary>
    private static string Extent(IElement element, PageRuntime runtime, bool horizontal)
    {
        if (runtime.Layout.Current().DocumentBoxOf(element) is not { } box)
        {
            return "auto";
        }

        var value = horizontal ? box.Width : box.Height;
        return value.ToString("0.####", CultureInfo.InvariantCulture) + "px";
    }
}
