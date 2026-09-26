namespace Jint.HtmlParser.Css.Values.Properties;

internal enum CssDeclarationContext
{
    Style, Keyframe, FontFace, Page, Margin, CounterStyle, PropertyRegistration,
    PositionTry, ViewTransition, FontPalette, ColorProfile, FeatureMap, Viewport
}

internal enum CssPropertyGrammar
{
    Display, Visibility, Opacity, Position, PointerEvents, BoxSizing, ZIndex, OverflowAxis, Overflow,
    Sizing, FlexBasis, FlexFactor, FlexDirection, FlexWrap, Direction, Flex, FlexFlow,
    AlignItems, AlignSelf, JustifyItems, JustifySelf, PlaceItems, PlaceSelf, Color,
    WhiteSpace, WhiteSpaceCollapse, TextWrapMode, WhiteSpaceTrim
}

// Only completed entries have initial/inheritance metadata. Pending catalog rows never invent defaults.
internal sealed record CssPropertyMetadata(string Name, CssPropertyGrammar Grammar, string InitialValue,
    bool Inherited, IReadOnlyList<string> Longhands)
{
    internal IReadOnlyList<string> Aliases { get; } = Array.Empty<string>();
    internal IReadOnlyList<string> ResetOnlyLonghands { get; } = Array.Empty<string>();
}

internal static class CssPropertyRegistry
{
    private static readonly string[] OverflowLonghands = ["overflow-x", "overflow-y"];
    private static readonly System.Collections.ObjectModel.ReadOnlyDictionary<string, CssPropertyMetadata> Entries = Build();
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
        return result;
    }

    private readonly record struct NameNormalization(string Name, CssValueWork? Work);

    internal static CssPropertyMetadata? Find(string normalizedName, CssDeclarationContext context) =>
        context is CssDeclarationContext.Style or CssDeclarationContext.Keyframe &&
        Entries.TryGetValue(normalizedName, out var entry) ? entry : null;

    private static System.Collections.ObjectModel.ReadOnlyDictionary<string, CssPropertyMetadata> Build()
    {
        var entries = new Dictionary<string, CssPropertyMetadata>(StringComparer.Ordinal);
        Add("display", CssPropertyGrammar.Display, "inline");
        Add("visibility", CssPropertyGrammar.Visibility, "visible", true);
        // CSS Color 4 §3.2; Backgrounds 3 §2.2. No computed-color metadata.
        Add("color", CssPropertyGrammar.Color, "canvastext", true);
        Add("background-color", CssPropertyGrammar.Color, "transparent");
        Add("opacity", CssPropertyGrammar.Opacity, "1");
        Add("position", CssPropertyGrammar.Position, "static");
        Add("pointer-events", CssPropertyGrammar.PointerEvents, "auto", true);
        Add("box-sizing", CssPropertyGrammar.BoxSizing, "content-box");
        Add("z-index", CssPropertyGrammar.ZIndex, "auto");
        Add("overflow-x", CssPropertyGrammar.OverflowAxis, "visible");
        Add("overflow-y", CssPropertyGrammar.OverflowAxis, "visible");
        entries.Add("overflow", new("overflow", CssPropertyGrammar.Overflow, "visible", false,
            Array.AsReadOnly(OverflowLonghands)));
        // Sizing 3 §3.1, Flexbox 1 §§5/7, Alignment 3 §§6/7, Writing Modes 3 §2.1.
        Add("width", CssPropertyGrammar.Sizing, "auto");
        Add("height", CssPropertyGrammar.Sizing, "auto");
        Add("flex-basis", CssPropertyGrammar.FlexBasis, "auto");
        Add("flex-grow", CssPropertyGrammar.FlexFactor, "0");
        Add("flex-shrink", CssPropertyGrammar.FlexFactor, "1");
        Add("flex-direction", CssPropertyGrammar.FlexDirection, "row");
        Add("flex-wrap", CssPropertyGrammar.FlexWrap, "nowrap");
        Add("direction", CssPropertyGrammar.Direction, "ltr", true);
        Add("align-items", CssPropertyGrammar.AlignItems, "normal");
        Add("align-self", CssPropertyGrammar.AlignSelf, "auto");
        Add("justify-items", CssPropertyGrammar.JustifyItems, "legacy");
        Add("justify-self", CssPropertyGrammar.JustifySelf, "auto");
        Shorthand("flex", CssPropertyGrammar.Flex, "0 1 auto", ["flex-grow", "flex-shrink", "flex-basis"]);
        Shorthand("flex-flow", CssPropertyGrammar.FlexFlow, "row nowrap", ["flex-direction", "flex-wrap"]);
        Shorthand("place-items", CssPropertyGrammar.PlaceItems, "normal legacy", ["align-items", "justify-items"]);
        Shorthand("place-self", CssPropertyGrammar.PlaceSelf, "auto", ["align-self", "justify-self"]);
        // CSS Text 4 §§3–5.1. Grammar/computed values only, independent of layout.
        Add("white-space-collapse", CssPropertyGrammar.WhiteSpaceCollapse, "collapse", true);
        Add("text-wrap-mode", CssPropertyGrammar.TextWrapMode, "wrap", true);
        Add("white-space-trim", CssPropertyGrammar.WhiteSpaceTrim, "none");
        Shorthand("white-space", CssPropertyGrammar.WhiteSpace, "normal",
            ["white-space-collapse", "text-wrap-mode", "white-space-trim"]);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, CssPropertyMetadata>(entries);

        void Shorthand(string name, CssPropertyGrammar grammar, string initial, string[] longhands) =>
            entries.Add(name, new(name, grammar, initial, false, Array.AsReadOnly(longhands)));

        void Add(string name, CssPropertyGrammar grammar, string initial, bool inherited = false) =>
            entries.Add(name, new(name, grammar, initial, inherited, Array.Empty<string>()));
    }
}
