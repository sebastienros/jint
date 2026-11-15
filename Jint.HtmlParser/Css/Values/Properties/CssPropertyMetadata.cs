namespace Jint.HtmlParser.Css.Values.Properties;

internal enum CssDeclarationContext { Style, FontFace }

// Renderless CSS: names and defaults are data, not promises to implement value grammars.
internal sealed record CssPropertyMetadata(string Name, string InitialValue, bool Inherited,
    IReadOnlyList<string> Longhands);

internal static class CssPropertyRegistry
{
    private static readonly Dictionary<string, CssPropertyMetadata> Entries = Build();
    internal static readonly CssPropertyMetadata[] Shorthands = Entries.Values.Where(static entry => entry.Longhands.Count != 0).ToArray();
    internal static IReadOnlyDictionary<string, CssPropertyMetadata> Completed => Entries;

    // Shorthands in registry order, keyed by each longhand they cover.
    private static readonly Dictionary<string, CssPropertyMetadata[]> ShorthandsByLonghand = Shorthands
        .SelectMany(static shorthand => shorthand.Longhands, static (shorthand, longhand) => (shorthand, longhand))
        .GroupBy(static pair => pair.longhand, StringComparer.Ordinal)
        .ToDictionary(static group => group.Key, static group => group.Select(static pair => pair.shorthand).ToArray(),
            StringComparer.Ordinal);

    internal static ReadOnlySpan<CssPropertyMetadata> ShorthandsCovering(string longhand) =>
        ShorthandsByLonghand.TryGetValue(longhand, out var shorthands) ? shorthands : [];

    /// <summary>A property name, ASCII-lowered, with a legacy name alias resolved to the property it names.</summary>
    internal static string NormalizeName(string name, CssValueWork? work = null) => LowerAscii(name, work) switch
    {
        "grid-gap" => "gap",
        "grid-row-gap" => "row-gap",
        "grid-column-gap" => "column-gap",
        // Compatibility §3.4.1 legacy name aliases (https://compat.spec.whatwg.org/#css-properties), for mask.
        // -webkit-mask-composite is not aliased: Chromium keeps it a separate property for its legacy
        // compositing operators, which this catalog does not support.
        "-webkit-mask" => "mask",
        "-webkit-mask-image" => "mask-image",
        "-webkit-mask-position" => "mask-position",
        "-webkit-mask-size" => "mask-size",
        "-webkit-mask-repeat" => "mask-repeat",
        "-webkit-mask-origin" => "mask-origin",
        "-webkit-mask-clip" => "mask-clip",
        var result => result
    };

    /// <summary>
    /// An identifier ASCII-lowered, with no property alias applied: at-rule names and media features are not
    /// property names. A custom property name keeps its case.
    /// </summary>
    internal static string LowerAscii(string name, CssValueWork? work = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        work?.CheckCancellation();
        if (name.StartsWith("--", StringComparison.Ordinal)) return name;
        string result;
        if (name.AsSpan().ContainsAnyInRange('A', 'Z'))
        {
            // The copy is charged as it is written so cancellation can interrupt it.
            result = string.Create(name.Length, (name, work), static (target, state) =>
            {
                for (var i = 0; i < target.Length; i++)
                {
                    state.work?.Charge(1);
                    var c = state.name[i];
                    target[i] = c is >= 'A' and <= 'Z' ? (char) (c + 32) : c;
                }
            });
        }
        else
        {
            work?.Charge(name.Length);
            result = name;
        }
        work?.CheckCancellation();
        return result;
    }

    internal static CssPropertyMetadata? Find(string name, CssDeclarationContext context = CssDeclarationContext.Style) =>
        context == CssDeclarationContext.Style && Entries.TryGetValue(name, out var entry) ? entry : null;

    private static Dictionary<string, CssPropertyMetadata> Build()
    {
        var entries = new Dictionary<string, CssPropertyMetadata>(StringComparer.Ordinal);
        foreach (var name in CssPropertyCatalog.Names)
            entries.Add(name, new(name, "", false, Array.Empty<string>()));
        Add("display", "inline");
        Add("visibility", "visible", true);
        Add("color", "rgb(0, 0, 0)", true);
        Add("background-color", "transparent");
        Add("opacity", "1");
        Add("content", "normal");
        Add("pointer-events", "auto", true);
        Add("position", "static");
        Add("box-sizing", "content-box");
        Add("z-index", "auto");
        Add("direction", "ltr", true);
        Add("writing-mode", "horizontal-tb", true);
        Add("white-space", "normal", true);
        Add("white-space-collapse", "", true);
        Add("font-size", "16px", true);
        Add("font-weight", "normal", true);
        Add("font-family", "serif", true);
        Add("font-style", "normal", true);
        Add("line-height", "normal", true);
        Add("text-align", "start", true);
        Add("text-align-last", "auto", true);
        Add("text-wrap-mode", "wrap", true);
        Add("cursor", "auto", true);
        Add("fill", "black", true);
        Add("stroke", "none", true);
        foreach (var name in new[] { "transform", "translate", "rotate", "scale", "background-image", "clip-path" })
            Add(name, "none");
        foreach (var name in new[] { "width", "height", "min-width", "min-height" }) Add(name, "auto");
        Add("max-width", "none");
        Add("max-height", "none");
        Add("flex-grow", "0");
        Add("flex-shrink", "1");
        Add("flex-basis", "auto");
        Add("flex-direction", "row");
        Add("flex-wrap", "nowrap");
        Add("align-items", "normal");
        Add("align-self", "auto");
        Add("justify-items", "legacy");
        Add("justify-self", "auto");
        Add("align-content", "normal");
        Add("justify-content", "normal");
        Add("row-gap", "normal");
        Add("column-gap", "normal");
        foreach (var name in new[] { "grid-template-columns", "grid-template-rows", "grid-template-areas" })
            Add(name, "none");
        for (var i = 0; i < CssBoxShorthands.MaskLonghands.Length; i++)
            Add(CssBoxShorthands.MaskLonghands[i], CssBoxShorthands.MaskInitials[i]);
        Add("object-fit", "fill");
        foreach (var side in new[] { "top", "right", "bottom", "left", "block-start", "block-end", "inline-start", "inline-end" })
            Add("scroll-padding-" + side, "auto");
        Add("overflow-x", "visible");
        Add("overflow-y", "visible");
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            Add(side, "auto");
            Add("margin-" + side, "0px");
            Add("padding-" + side, "0px");
            Add("border-" + side + "-width", "0px");
            Add("border-" + side + "-style", "none");
            Add("border-" + side + "-color", "currentcolor");
        }
        Add("background-position-x", "0%");
        Add("background-position-y", "0%");
        Add("background-size", "auto");
        Add("background-repeat", "repeat");
        Add("background-attachment", "scroll");
        Add("background-origin", "padding-box");
        Add("background-clip", "border-box");
        // CSS Animations 1 §3 and 2 §3, CSS Transitions 1 §2 and 2 §2.1; durations take Chromium's 0s, not Level 2's auto.
        for (var i = 0; i < CssMotionShorthands.AnimationLonghands.Length; i++)
            Add(CssMotionShorthands.AnimationLonghands[i], CssMotionShorthands.AnimationInitials[i]);
        Add("animation-composition", "replace");
        Add("animation-timeline", "auto");
        Add("animation-range-start", "normal");
        Add("animation-range-end", "normal");
        for (var i = 0; i < CssMotionShorthands.TransitionLonghands.Length; i++)
            Add(CssMotionShorthands.TransitionLonghands[i], CssMotionShorthands.TransitionInitials[i]);
        Shorthand("overflow", "visible", ["overflow-x", "overflow-y"]);
        Shorthand("flex", "0 1 auto", ["flex-grow", "flex-shrink", "flex-basis"]);
        Shorthand("flex-flow", "row nowrap", ["flex-direction", "flex-wrap"]);
        Shorthand("gap", "normal", ["row-gap", "column-gap"]);
        Shorthand("inset", "auto", ["top", "right", "bottom", "left"]);
        Shorthand("margin", "0px", ["margin-top", "margin-right", "margin-bottom", "margin-left"]);
        Shorthand("padding", "0px", ["padding-top", "padding-right", "padding-bottom", "padding-left"]);
        Shorthand("border-width", "0px", ["border-top-width", "border-right-width", "border-bottom-width", "border-left-width"]);
        Shorthand("border-style", "none", ["border-top-style", "border-right-style", "border-bottom-style", "border-left-style"]);
        Shorthand("border-color", "currentcolor", ["border-top-color", "border-right-color", "border-bottom-color", "border-left-color"]);
        foreach (var side in CssBoxShorthands.Sides)
            Shorthand("border-" + side, "", ["border-" + side + "-width", "border-" + side + "-style", "border-" + side + "-color"]);
        Shorthand("border", "", [
            "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
            "border-top-style", "border-right-style", "border-bottom-style", "border-left-style",
            "border-top-color", "border-right-color", "border-bottom-color", "border-left-color"]);
        Shorthand("background-position", "0% 0%", ["background-position-x", "background-position-y"]);
        Shorthand("background", "", CssBoxShorthands.BackgroundLonghands);
        Shorthand("mask", "", CssBoxShorthands.MaskLonghands);
        Shorthand("animation", "", CssMotionShorthands.AnimationLonghands);
        Shorthand("transition", "", CssMotionShorthands.TransitionLonghands);
        return entries;

        void Add(string name, string initial, bool inherited = false) =>
            entries[name] = new(name, initial, inherited, Array.Empty<string>());
        void Shorthand(string name, string initial, string[] longhands) =>
            entries[name] = new(name, initial, false, Array.AsReadOnly(longhands));
    }
}
