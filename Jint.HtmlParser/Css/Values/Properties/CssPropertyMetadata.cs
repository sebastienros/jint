namespace Jint.HtmlParser.Css.Values.Properties;

internal enum CssDeclarationContext
{
    Style, Keyframe, FontFace, Page, Margin, CounterStyle, PropertyRegistration,
    PositionTry, ViewTransition, FontPalette, ColorProfile, FeatureMap, Viewport
}

internal enum CssPropertyGrammar
{
    Display, Visibility, Opacity, Position, PointerEvents, BoxSizing, ZIndex, OverflowAxis, Overflow,
    Sizing, MinSizing, MaxSizing, Margin, MarginSide, Padding, PaddingSide,
    FlexBasis, FlexFactor, FlexDirection, FlexWrap, Direction, Flex, FlexFlow,
    AlignItems, AlignSelf, JustifyItems, JustifySelf, PlaceItems, PlaceSelf,
    AlignContent, JustifyContent, PlaceContent, GapSide, Gap, Color, Paint, Clip, ClipPath, Image,
    WhiteSpace, WhiteSpaceCollapse, TextWrapMode, WhiteSpaceTrim, FontWeight, FontSize,
    TextAlign, TextAlignAll, TextAlignLast, Translate, Rotate, Scale, TransformList, TransformBox,
    TextDecoration, TextDecorationLine, TextDecorationStyle, TextDecorationThickness, BackgroundClip, Cursor, InsetSide,
    ContainerName, ContainerType, Container, WritingMode, All,
    BorderWidth, BorderStyle, BorderWidths, BorderStyles, BorderColors, Border,
    CornerRadius, BorderRadius, Outline, OutlineStyle, OutlineColor, OutlineOffset
}

// Only completed entries have initial/inheritance metadata. Pending catalog rows never invent defaults.
internal sealed record CssPropertyMetadata(string Name, CssPropertyGrammar Grammar, string InitialValue,
    bool Inherited, IReadOnlyList<string> Longhands)
{
    internal IReadOnlyList<string> Aliases { get; } = Array.Empty<string>();
    internal IReadOnlyList<string> ResetOnlyLonghands { get; init; } = Array.Empty<string>();
}

internal static class CssPropertyRegistry
{
    private static readonly string[] OverflowLonghands = ["overflow-x", "overflow-y"];
    private static readonly System.Collections.ObjectModel.ReadOnlyDictionary<string, CssPropertyMetadata> Entries = Build();
    private static readonly CssPropertyMetadata?[] IndexedEntries = CssPropertyCatalog.Index(Entries);
    internal static IReadOnlyDictionary<string, CssPropertyMetadata> Completed => Entries;

    internal static string NormalizeName(string name, CssValueWork? work = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        work?.CheckCancellation();
        if (name.StartsWith("--", StringComparison.Ordinal)) return name;
        var result = string.Create(name.Length, new NameNormalization(name, work), static (target, state) =>
        {
            var source = state.Name;
            for (var i = 0; i < source.Length; i++)
            {
                state.Work?.Charge(1);
                target[i] = source[i] is >= 'A' and <= 'Z' ? (char) (source[i] + 32) : source[i];
            }
        });
        work?.CheckCancellation();
        return CssPropertyEffects.Canonical(result);
    }

    private readonly record struct NameNormalization(string Name, CssValueWork? Work);

    internal static CssPropertyMetadata? Find(string normalizedName, CssDeclarationContext context)
    {
        if (context is not (CssDeclarationContext.Style or CssDeclarationContext.Keyframe)) return null;
        ArgumentNullException.ThrowIfNull(normalizedName);
        var index = CssPropertyNameLookup.Match(normalizedName);
        return index >= 0 ? IndexedEntries[index] : null;
    }

    private static System.Collections.ObjectModel.ReadOnlyDictionary<string, CssPropertyMetadata> Build()
    {
        var entries = new Dictionary<string, CssPropertyMetadata>(StringComparer.Ordinal);
        Add("display", CssPropertyGrammar.Display, "inline");
        Add("visibility", CssPropertyGrammar.Visibility, "visible", true);
        // CSS Color 4 §3.2; Backgrounds 3 §2.2. No computed-color metadata.
        Add("color", CssPropertyGrammar.Color, "canvastext", true);
        Add("background-color", CssPropertyGrammar.Color, "transparent");
        // https://drafts.csswg.org/css-backgrounds-3/#borders
        // https://drafts.csswg.org/css-logical-1/#border-properties
        CssBorderPropertyParser.Register(entries);
        // https://svgwg.org/svg2-draft/painting.html#SpecifyingPaint
        Add("fill", CssPropertyGrammar.Paint, "black", true);
        Add("stroke", CssPropertyGrammar.Paint, "none", true);
        // https://www.w3.org/TR/CSS22/visufx.html#clipping
        Add("clip", CssPropertyGrammar.Clip, "auto");
        Add("clip-path", CssPropertyGrammar.ClipPath, "none");
        Add("background-image", CssPropertyGrammar.Image, "none");
        // Backgrounds 4 §2.8. Computed layer lists retain their authored count and order.
        Add("background-clip", CssPropertyGrammar.BackgroundClip, "border-box");
        Add("opacity", CssPropertyGrammar.Opacity, "1");
        Add("position", CssPropertyGrammar.Position, "static");
        // Positioned Layout 3 §3.1. Physical longhands only; signed length-percentage values.
        foreach (var side in new[] { "top", "right", "bottom", "left" })
            Add(side, CssPropertyGrammar.InsetSide, "auto");
        Add("pointer-events", CssPropertyGrammar.PointerEvents, "auto", true);
        // CSS UI 4 §5.1.1. Image cursors retain an explicit pending boundary.
        Add("cursor", CssPropertyGrammar.Cursor, "auto", true);
        Add("box-sizing", CssPropertyGrammar.BoxSizing, "content-box");
        Add("z-index", CssPropertyGrammar.ZIndex, "auto");
        Add("overflow-x", CssPropertyGrammar.OverflowAxis, "visible");
        Add("overflow-y", CssPropertyGrammar.OverflowAxis, "visible");
        entries.Add("overflow", new("overflow", CssPropertyGrammar.Overflow, "visible", false,
            Array.AsReadOnly(OverflowLonghands)));
        // Sizing 3 §3.1, Flexbox 1 §§5/7, Alignment 3 §§6/7, Writing Modes 3 §2.1.
        Add("width", CssPropertyGrammar.Sizing, "auto");
        Add("height", CssPropertyGrammar.Sizing, "auto");
        // Box 4 §§3/4; Sizing 3 §§3.1.2/3.1.3. Physical properties only.
        Add("min-width", CssPropertyGrammar.MinSizing, "auto");
        Add("min-height", CssPropertyGrammar.MinSizing, "auto");
        Add("max-width", CssPropertyGrammar.MaxSizing, "none");
        Add("max-height", CssPropertyGrammar.MaxSizing, "none");
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            Add("margin-" + side, CssPropertyGrammar.MarginSide, "0px");
            Add("padding-" + side, CssPropertyGrammar.PaddingSide, "0px");
        }
        Shorthand("margin", CssPropertyGrammar.Margin, "0px", ["margin-top", "margin-right", "margin-bottom", "margin-left"]);
        Shorthand("padding", CssPropertyGrammar.Padding, "0px", ["padding-top", "padding-right", "padding-bottom", "padding-left"]);
        Add("flex-basis", CssPropertyGrammar.FlexBasis, "auto");
        Add("flex-grow", CssPropertyGrammar.FlexFactor, "0");
        Add("flex-shrink", CssPropertyGrammar.FlexFactor, "1");
        Add("flex-direction", CssPropertyGrammar.FlexDirection, "row");
        Add("flex-wrap", CssPropertyGrammar.FlexWrap, "nowrap");
        Add("direction", CssPropertyGrammar.Direction, "ltr", true);
        // Conditional 5 §§5.1–5.3; Writing Modes 4 §3.
        Add("writing-mode", CssPropertyGrammar.WritingMode, "horizontal-tb", true);
        Add("container-name", CssPropertyGrammar.ContainerName, "none");
        Add("container-type", CssPropertyGrammar.ContainerType, "normal");
        Shorthand("container", CssPropertyGrammar.Container, "none", ["container-name", "container-type"]);
        // CSS Fonts 4 §2.2. Descriptors remain a separate context obligation.
        Add("font-weight", CssPropertyGrammar.FontWeight, "normal", true);
        // CSS Fonts 4 §2.5; the host initial font size supplies medium at computation.
        Add("font-size", CssPropertyGrammar.FontSize, "medium", true);
        // CSS Text 4 §§7.1/7.3/7.4: text-align resets both inherited longhands.
        Add("text-align-all", CssPropertyGrammar.TextAlignAll, "start", true);
        Add("text-align-last", CssPropertyGrammar.TextAlignLast, "auto", true);
        Shorthand("text-align", CssPropertyGrammar.TextAlign, "start", ["text-align-all", "text-align-last"]);
        // Text Decoration 4 §§2.1–2.6. Decoration propagation is independent of inheritance.
        Add("text-decoration-line", CssPropertyGrammar.TextDecorationLine, "none");
        Add("text-decoration-thickness", CssPropertyGrammar.TextDecorationThickness, "auto");
        Add("text-decoration-style", CssPropertyGrammar.TextDecorationStyle, "solid");
        Add("text-decoration-color", CssPropertyGrammar.Color, "currentcolor");
        Shorthand("text-decoration", CssPropertyGrammar.TextDecoration, "none auto solid currentcolor",
            ["text-decoration-line", "text-decoration-thickness", "text-decoration-style", "text-decoration-color"]);
        // CSS Transforms 2 §5 and §12; Transforms 1 §6.
        Add("translate", CssPropertyGrammar.Translate, "none");
        Add("rotate", CssPropertyGrammar.Rotate, "none");
        Add("scale", CssPropertyGrammar.Scale, "none");
        Add("transform", CssPropertyGrammar.TransformList, "none");
        Add("transform-box", CssPropertyGrammar.TransformBox, "view-box");
        Add("align-items", CssPropertyGrammar.AlignItems, "normal");
        Add("align-self", CssPropertyGrammar.AlignSelf, "auto");
        Add("justify-items", CssPropertyGrammar.JustifyItems, "legacy");
        Add("justify-self", CssPropertyGrammar.JustifySelf, "auto");
        Shorthand("flex", CssPropertyGrammar.Flex, "0 1 auto", ["flex-grow", "flex-shrink", "flex-basis"]);
        Shorthand("flex-flow", CssPropertyGrammar.FlexFlow, "row nowrap", ["flex-direction", "flex-wrap"]);
        Shorthand("place-items", CssPropertyGrammar.PlaceItems, "normal legacy", ["align-items", "justify-items"]);
        Shorthand("place-self", CssPropertyGrammar.PlaceSelf, "auto", ["align-self", "justify-self"]);
        // https://drafts.csswg.org/css-align-3/#content-distribution
        Add("align-content", CssPropertyGrammar.AlignContent, "normal");
        Add("justify-content", CssPropertyGrammar.JustifyContent, "normal");
        Shorthand("place-content", CssPropertyGrammar.PlaceContent, "normal", ["align-content", "justify-content"]);
        // https://drafts.csswg.org/css-gaps-1/#gaps; legacy grid-* names canonicalize above.
        Add("row-gap", CssPropertyGrammar.GapSide, "normal");
        Add("column-gap", CssPropertyGrammar.GapSide, "normal");
        Shorthand("gap", CssPropertyGrammar.Gap, "normal", ["row-gap", "column-gap"]);
        // CSS Text 4 §§3–5.1. Grammar/computed values only, independent of layout.
        Add("white-space-collapse", CssPropertyGrammar.WhiteSpaceCollapse, "collapse", true);
        Add("text-wrap-mode", CssPropertyGrammar.TextWrapMode, "wrap", true);
        Add("white-space-trim", CssPropertyGrammar.WhiteSpaceTrim, "none");
        Shorthand("white-space", CssPropertyGrammar.WhiteSpace, "normal",
            ["white-space-collapse", "text-wrap-mode", "white-space-trim"]);
        Shorthand("all", CssPropertyGrammar.All, "initial", CssAllReset.Longhands(entries));
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, CssPropertyMetadata>(entries);

        void Shorthand(string name, CssPropertyGrammar grammar, string initial, string[] longhands) =>
            entries.Add(name, new(name, grammar, initial, false, Array.AsReadOnly(longhands)));

        void Add(string name, CssPropertyGrammar grammar, string initial, bool inherited = false) =>
            entries.Add(name, new(name, grammar, initial, inherited, Array.Empty<string>()));
    }
}
