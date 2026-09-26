namespace Jint.HtmlParser.Css.Values.Properties;

internal enum CssDeclarationContext
{
    Style, Keyframe, FontFace, Page, Margin, CounterStyle, PropertyRegistration,
    PositionTry, ViewTransition, FontPalette, ColorProfile, FeatureMap, Viewport
}

internal enum CssPropertyGrammar { Display, Visibility, Opacity, Position, PointerEvents, BoxSizing, ZIndex, OverflowAxis, Overflow }

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
        Add("opacity", CssPropertyGrammar.Opacity, "1");
        Add("position", CssPropertyGrammar.Position, "static");
        Add("pointer-events", CssPropertyGrammar.PointerEvents, "auto", true);
        Add("box-sizing", CssPropertyGrammar.BoxSizing, "content-box");
        Add("z-index", CssPropertyGrammar.ZIndex, "auto");
        Add("overflow-x", CssPropertyGrammar.OverflowAxis, "visible");
        Add("overflow-y", CssPropertyGrammar.OverflowAxis, "visible");
        entries.Add("overflow", new("overflow", CssPropertyGrammar.Overflow, "visible", false,
            Array.AsReadOnly(OverflowLonghands)));
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, CssPropertyMetadata>(entries);

        void Add(string name, CssPropertyGrammar grammar, string initial, bool inherited = false) =>
            entries.Add(name, new(name, grammar, initial, inherited, Array.Empty<string>()));
    }
}
