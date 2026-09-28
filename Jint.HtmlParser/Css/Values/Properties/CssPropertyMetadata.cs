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

    internal static string NormalizeName(string name, CssValueWork? work = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        work?.CheckCancellation();
        if (name.StartsWith("--", StringComparison.Ordinal)) return name;
        var result = string.Create(name.Length, (name, work), static (target, state) =>
        {
            for (var i = 0; i < target.Length; i++)
            {
                state.work?.Charge(1);
                var c = state.name[i];
                target[i] = c is >= 'A' and <= 'Z' ? (char) (c + 32) : c;
            }
        });
        work?.CheckCancellation();
        return result switch
        {
            "grid-gap" => "gap",
            "grid-row-gap" => "row-gap",
            "grid-column-gap" => "column-gap",
            _ => result
        };
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
        Add("overflow-x", "visible");
        Add("overflow-y", "visible");
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            Add(side, "auto");
            Add("margin-" + side, "0px");
            Add("padding-" + side, "0px");
            Add("border-" + side + "-width", "0px");
            Add("border-" + side + "-style", "none");
        }
        Shorthand("overflow", "visible", ["overflow-x", "overflow-y"]);
        Shorthand("flex", "0 1 auto", ["flex-grow", "flex-shrink", "flex-basis"]);
        Shorthand("flex-flow", "row nowrap", ["flex-direction", "flex-wrap"]);
        Shorthand("gap", "normal", ["row-gap", "column-gap"]);
        Shorthand("inset", "auto", ["top", "right", "bottom", "left"]);
        Shorthand("margin", "0px", ["margin-top", "margin-right", "margin-bottom", "margin-left"]);
        Shorthand("padding", "0px", ["padding-top", "padding-right", "padding-bottom", "padding-left"]);
        Shorthand("border-width", "0px", ["border-top-width", "border-right-width", "border-bottom-width", "border-left-width"]);
        Shorthand("border-style", "none", ["border-top-style", "border-right-style", "border-bottom-style", "border-left-style"]);
        return entries;

        void Add(string name, string initial, bool inherited = false) =>
            entries[name] = new(name, initial, inherited, Array.Empty<string>());
        void Shorthand(string name, string initial, string[] longhands) =>
            entries[name] = new(name, initial, false, Array.AsReadOnly(longhands));
    }
}
