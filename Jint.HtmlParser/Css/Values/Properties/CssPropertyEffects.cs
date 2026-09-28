namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Cascade 5 §3/§6: declaration effects exist independently of completed value grammars.
// This table supplies correlation only; pending shorthands gain no validator or initial value.
internal static class CssPropertyEffects
{
    private static readonly System.Collections.ObjectModel.ReadOnlyDictionary<string, string[]> Shorthands = Create();
    private static readonly string[]?[] IndexedShorthands = CssPropertyCatalog.Index(Shorthands);
    internal static IReadOnlyDictionary<string, string[]> ShorthandEffects => Shorthands;

    internal static string Canonical(string name) => CssPropertyAliasLookup.Match(name) ?? name;

    internal static IReadOnlyList<string> Longhands(string name)
    {
        if (CssPropertyRegistry.Find(name, CssDeclarationContext.Style) is { Longhands.Count: > 0 } completed)
            return completed.Longhands;
        var index = CssPropertyNameLookup.Match(name);
        return index >= 0 && IndexedShorthands[index] is { } result ? result : Array.Empty<string>();
    }

    internal static bool AffectsAll(string name) => name == "all";
    internal static bool ResetByAll(string name) => name is not ("direction" or "unicode-bidi") && !name.StartsWith("--", StringComparison.Ordinal);

    private static System.Collections.ObjectModel.ReadOnlyDictionary<string, string[]> Create()
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Add("background", "background-color background-image background-position-x background-position-y background-size background-repeat-x background-repeat-y background-origin background-clip background-attachment");
        Add("background-position", "background-position-x background-position-y");
        Add("background-repeat", "background-repeat-x background-repeat-y");
        Add("text-wrap", "text-wrap-mode text-wrap-style");
        Add("text-decoration", "text-decoration-line text-decoration-style text-decoration-color text-decoration-thickness");
        Add("font", "font-style font-weight font-stretch font-size line-height font-family font-size-adjust font-kerning font-feature-settings font-language-override font-optical-sizing font-variation-settings font-variant-ligatures font-variant-caps font-variant-numeric font-variant-east-asian font-variant-alternates font-variant-position font-variant-emoji");
        Add("font-variant", "font-variant-ligatures font-variant-caps font-variant-numeric font-variant-east-asian font-variant-alternates font-variant-position font-variant-emoji");
        Add("font-synthesis", "font-synthesis-weight font-synthesis-style font-synthesis-small-caps font-synthesis-position");
        Add("border", "border-top-width border-right-width border-bottom-width border-left-width border-top-style border-right-style border-bottom-style border-left-style border-top-color border-right-color border-bottom-color border-left-color border-image-source border-image-slice border-image-width border-image-outset border-image-repeat");
        foreach (var axis in new[] { "block", "inline" })
        {
            Add("border-" + axis, $"border-{axis}-start-width border-{axis}-end-width border-{axis}-start-style border-{axis}-end-style border-{axis}-start-color border-{axis}-end-color");
            foreach (var kind in new[] { "width", "style", "color" })
                Add($"border-{axis}-{kind}", $"border-{axis}-start-{kind} border-{axis}-end-{kind}");
            foreach (var edge in new[] { "start", "end" })
                Add($"border-{axis}-{edge}", $"border-{axis}-{edge}-width border-{axis}-{edge}-style border-{axis}-{edge}-color");
        }
        foreach (var edge in new[] { "top", "right", "bottom", "left" })
            Add("border-" + edge, $"border-{edge}-width border-{edge}-style border-{edge}-color");
        foreach (var kind in new[] { "width", "style", "color" })
            Add("border-" + kind, $"border-top-{kind} border-right-{kind} border-bottom-{kind} border-left-{kind}");
        Add("border-image", "border-image-source border-image-slice border-image-width border-image-outset border-image-repeat");
        Add("border-radius", "border-top-left-radius border-top-right-radius border-bottom-right-radius border-bottom-left-radius");
        Add("outline", "outline-color outline-style outline-width");
        foreach (var family in new[] { "margin", "padding", "scroll-margin", "scroll-padding", "inset" })
        {
            var prefix = family == "inset" ? "" : family + "-";
            Add(family, $"{prefix}top {prefix}right {prefix}bottom {prefix}left");
            foreach (var axis in new[] { "block", "inline" })
                Add(family + "-" + axis, $"{family}-{axis}-start {family}-{axis}-end");
        }
        Add("columns", "column-width column-count");
        Add("column-rule", "column-rule-width column-rule-style column-rule-color");
        Add("container", "container-name container-type");
        Add("contain-intrinsic-size", "contain-intrinsic-width contain-intrinsic-height");
        Add("gap", "row-gap column-gap");
        Add("place-content", "align-content justify-content");
        Add("grid", "grid-template-rows grid-template-columns grid-template-areas grid-auto-rows grid-auto-columns grid-auto-flow");
        Add("grid-template", "grid-template-rows grid-template-columns grid-template-areas");
        Add("grid-area", "grid-row-start grid-column-start grid-row-end grid-column-end");
        Add("grid-row", "grid-row-start grid-row-end");
        Add("grid-column", "grid-column-start grid-column-end");
        Add("animation", "animation-name animation-duration animation-timing-function animation-delay animation-iteration-count animation-direction animation-fill-mode animation-play-state animation-timeline animation-range-start animation-range-end");
        Add("animation-range", "animation-range-start animation-range-end");
        Add("transition", "transition-property transition-duration transition-timing-function transition-delay transition-behavior");
        Add("overscroll-behavior", "overscroll-behavior-x overscroll-behavior-y");
        Add("mask", "mask-image mask-position mask-size mask-repeat mask-origin mask-clip mask-composite mask-mode mask-border-source mask-border-slice mask-border-width mask-border-outset mask-border-repeat mask-border-mode");
        Add("mask-border", "mask-border-source mask-border-slice mask-border-width mask-border-outset mask-border-repeat mask-border-mode");
        Add("list-style", "list-style-type list-style-position list-style-image");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, string[]>(map);

        void Add(string name, string children) => map.Add(name, children.Split(' '));
    }
}
