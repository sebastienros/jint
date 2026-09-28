using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.HtmlParser.Css.Values.Properties;

// https://drafts.csswg.org/css-backgrounds-3/#borders and #corners
// https://drafts.csswg.org/css-logical-1/#border-properties
// https://drafts.csswg.org/css-ui-4/#outline
internal static class CssBorderPropertyParser
{
    private static readonly string[] PhysicalSides = ["top", "right", "bottom", "left"];
    private static readonly string[] LogicalSides = ["block-start", "block-end", "inline-start", "inline-end"];
    private static readonly string[] PhysicalCorners = ["top-left", "top-right", "bottom-right", "bottom-left"];
    private static readonly string[] LogicalCorners = ["start-start", "start-end", "end-end", "end-start"];
    private static readonly IReadOnlyList<string> LogicalWidths = LogicalNames("width");
    private static readonly IReadOnlyList<string> LogicalStyles = LogicalNames("style");
    private static readonly IReadOnlyList<string> LogicalColors = LogicalNames("color");
    private static readonly IReadOnlyList<string> LogicalRadii = LogicalNames("radius");
    internal static readonly IReadOnlyList<string> ImageReset = Array.AsReadOnly(new[]
    {
        "border-image-source", "border-image-slice", "border-image-width", "border-image-outset", "border-image-repeat"
    });

    internal static void Register(Dictionary<string, CssPropertyMetadata> entries)
    {
        var border = new List<string>();
        foreach (var kind in new[] { "width", "style", "color" })
        {
            var grammar = kind switch
            {
                "width" => CssPropertyGrammar.BorderWidth,
                "style" => CssPropertyGrammar.BorderStyle,
                _ => CssPropertyGrammar.Color
            };
            var group = kind switch
            {
                "width" => CssPropertyGrammar.BorderWidths,
                "style" => CssPropertyGrammar.BorderStyles,
                _ => CssPropertyGrammar.BorderColors
            };
            var initial = kind switch { "width" => "medium", "style" => "none", _ => "currentcolor" };
            var physical = PhysicalSides.Select(side => "border-" + side + "-" + kind).ToArray();
            border.AddRange(physical);
            foreach (var name in physical.Concat(LogicalSides.Select(side => "border-" + side + "-" + kind)))
                Add(name, grammar, initial);
            Add("border-" + kind, group, initial, physical);
            foreach (var axis in new[] { "block", "inline" })
                Add("border-" + axis + "-" + kind, group, initial,
                    ["border-" + axis + "-start-" + kind, "border-" + axis + "-end-" + kind]);
        }
        // Reset-only values are CSS-wide initial, not a claim that border-image grammar is complete.
        border.AddRange(ImageReset);
        entries.Add("border", new("border", CssPropertyGrammar.Border, "medium none currentcolor", false,
            border.AsReadOnly())
        { ResetOnlyLonghands = ImageReset });
        foreach (var side in PhysicalSides.Concat(LogicalSides))
            Add("border-" + side, CssPropertyGrammar.Border, "medium none currentcolor",
                ["border-" + side + "-width", "border-" + side + "-style", "border-" + side + "-color"]);
        foreach (var axis in new[] { "block", "inline" })
            Add("border-" + axis, CssPropertyGrammar.Border, "medium none currentcolor",
                ["border-" + axis + "-start-width", "border-" + axis + "-end-width",
                 "border-" + axis + "-start-style", "border-" + axis + "-end-style",
                 "border-" + axis + "-start-color", "border-" + axis + "-end-color"]);
        foreach (var corner in PhysicalCorners.Concat(LogicalCorners))
            Add("border-" + corner + "-radius", CssPropertyGrammar.CornerRadius, "0px");
        Add("border-radius", CssPropertyGrammar.BorderRadius, "0px",
            PhysicalCorners.Select(corner => "border-" + corner + "-radius").ToArray());
        Add("outline-width", CssPropertyGrammar.BorderWidth, "medium");
        Add("outline-style", CssPropertyGrammar.OutlineStyle, "none");
        Add("outline-color", CssPropertyGrammar.OutlineColor, "auto");
        Add("outline-offset", CssPropertyGrammar.OutlineOffset, "0px");
        Add("outline", CssPropertyGrammar.Outline, "medium none auto", ["outline-width", "outline-style", "outline-color"]);
        return;

        void Add(string name, CssPropertyGrammar grammar, string initial, string[]? longhands = null) =>
            entries.Add(name, new(name, grammar, initial, false, Array.AsReadOnly(longhands ?? [])));
    }

    internal static CssPropertyResult Parse(CssPropertyMetadata entry, List<CssComponentValue> parts,
        int maximumDepth, CssValueWork work)
    {
        if (parts.Count == 0) return Invalid();
        switch (entry.Grammar)
        {
            case CssPropertyGrammar.Border or CssPropertyGrammar.Outline:
                return Border(entry, parts, maximumDepth, work);
            case CssPropertyGrammar.CornerRadius or CssPropertyGrammar.BorderRadius:
                return Radius(entry.Grammar, parts, maximumDepth, work);
            case CssPropertyGrammar.BorderWidths or CssPropertyGrammar.BorderStyles or CssPropertyGrammar.BorderColors:
                if (parts.Count > entry.Longhands.Count) return Invalid();
                var values = new CssPropertyValue[entry.Longhands.Count];
                for (var i = 0; i < values.Length; i++)
                {
                    work.Charge(1);
                    if (i >= parts.Count) { values[i] = values[i == 3 ? 1 : 0]; continue; }
                    var parsed = entry.Grammar switch
                    {
                        CssPropertyGrammar.BorderWidths => Width(parts[i], maximumDepth, work),
                        CssPropertyGrammar.BorderStyles => Style(parts[i], false, work),
                        _ => Color(parts[i], false, maximumDepth, work)
                    };
                    if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                    values[i] = parsed.Value;
                }
                return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(
                    Serialize(entry, values.Select(value => value.Text).ToArray(), work), parts[0].Span, values));
            default:
                if (parts.Count != 1) return Invalid();
                return entry.Grammar switch
                {
                    CssPropertyGrammar.BorderWidth => Width(parts[0], maximumDepth, work),
                    CssPropertyGrammar.BorderStyle => Style(parts[0], false, work),
                    CssPropertyGrammar.OutlineStyle => Style(parts[0], true, work),
                    CssPropertyGrammar.OutlineColor => Color(parts[0], true, maximumDepth, work),
                    _ => CssSizingPropertyParser.Numeric(parts[0], false, maximumDepth, work,
                        nonnegative: false, allowPercentage: false)
                };
        }
    }

    private static CssPropertyResult Width(CssComponentValue part, int depth, CssValueWork work) =>
        WidthKeyword(part, work) is { } keyword
            ? CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span))
            : CssSizingPropertyParser.Numeric(part, false, depth, work, allowPercentage: false);

    private static string? WidthKeyword(CssComponentValue part, CssValueWork work) =>
        CssPropertyParser.Keyword(part, CssKeywordSet.NormalThinMediumThick, work) is { } keyword && keyword != "normal"
            ? keyword : null;

    private static CssPropertyResult Style(CssComponentValue part, bool outline, CssValueWork work)
    {
        if (part.Kind != CssComponentKind.Token || part.Token.Kind != CssTokenKind.Ident) return Invalid();
        work.Charge(part.Token.Text.Length + 1);
        var keyword = CssBorderStyleKeywordLookup.Match(part.Token.Text);
        if (outline && keyword == "hidden") return Invalid();
        if (outline && keyword is null) keyword = CssPropertyParser.Keyword(part, CssKeywordSet.Auto, work);
        return keyword is null ? Invalid() : CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span));
    }

    private static CssPropertyResult Color(CssComponentValue part, bool outline, int depth, CssValueWork work)
    {
        if (outline && CssPropertyParser.Keyword(part, CssKeywordSet.Auto, work) is { } auto)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(auto, part.Span));
        var parsed = CssColorParser.Parse(new CssComponentValueList([part]), depth, work);
        return parsed.Status switch
        {
            CssColorParseStatus.Match => CssPropertyResult.Accepted(CssPropertyValue.ColorValue(parsed.Value,
                CssColorSerializer.SerializeSpecified(parsed.Value, work))),
            CssColorParseStatus.RequiresLaterGrammar => CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, parsed.Blocker),
            _ => Invalid()
        };
    }

    private static CssPropertyResult Border(CssPropertyMetadata entry, List<CssComponentValue> parts, int depth, CssValueWork work)
    {
        if (parts.Count > 3) return Invalid();
        var outline = entry.Grammar == CssPropertyGrammar.Outline;
        CssPropertyValue? width = null, style = null, color = null;
        var autos = 0;
        foreach (var part in parts)
        {
            work.Charge(1);
            if (outline && CssPropertyParser.Keyword(part, CssKeywordSet.Auto, work) is not null) { autos++; continue; }
            var parsedStyle = Style(part, false, work);
            if (parsedStyle.Status == CssPropertyStatus.Valid)
            {
                if (style is not null || outline && parsedStyle.Value.Text == "hidden") return Invalid();
                style = parsedStyle.Value;
            }
            else if (WidthKeyword(part, work) is not null ||
                part.Kind == CssComponentKind.Token && part.Token.Kind is CssTokenKind.Number or CssTokenKind.Dimension or CssTokenKind.Percentage ||
                part.Kind == CssComponentKind.Function && CssMathParser.Recognize(part.FunctionName) != CssMathFunction.None)
            {
                if (width is not null) return Invalid();
                var parsed = Width(part, depth, work);
                if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                width = parsed.Value;
            }
            else
            {
                if (color is not null) return Invalid();
                var parsed = Color(part, false, depth, work);
                if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                color = parsed.Value;
            }
        }
        var span = parts[0].Span;
        if (autos != 0)
        {
            if (autos > (style is null ? 1 : 0) + (color is null ? 1 : 0)) return Invalid();
            style ??= CssPropertyValue.Keyword("auto", span);
            color ??= CssPropertyValue.Keyword("auto", span);
        }
        width ??= CssPropertyValue.Keyword("medium", span);
        style ??= CssPropertyValue.Keyword("none", span);
        color ??= outline ? CssPropertyValue.Keyword("auto", span) :
            CssPropertyValue.ColorValue(CssColorValue.Identity(CssColorKind.CurrentColor, "currentcolor", span), "currentcolor");
        var values = new CssPropertyValue[entry.Longhands.Count];
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(1);
            var name = entry.Longhands[i];
            values[i] = entry.ResetOnlyLonghands.Contains(name) ? CssPropertyValue.Keyword("initial", span) :
                name.EndsWith("-width", StringComparison.Ordinal) ? width :
                name.EndsWith("-style", StringComparison.Ordinal) ? style : color;
        }
        return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(
            Serialize(entry, values.Select(value => value.Text).ToArray(), work), span, values));
    }

    private static CssPropertyResult Radius(CssPropertyGrammar grammar, List<CssComponentValue> parts,
        int depth, CssValueWork work)
    {
        if (parts.Count > (grammar == CssPropertyGrammar.CornerRadius ? 2 : 9)) return Invalid();
        var horizontal = new List<CssPropertyValue>();
        var vertical = new List<CssPropertyValue>();
        var target = horizontal;
        foreach (var part in parts)
        {
            work.Charge(1);
            if (part.Kind == CssComponentKind.Token && part.Token.Kind == CssTokenKind.Delim && part.Token.Delimiter == '/')
            {
                if (grammar == CssPropertyGrammar.CornerRadius || target == vertical || horizontal.Count == 0) return Invalid();
                target = vertical;
                continue;
            }
            var parsed = CssSizingPropertyParser.Numeric(part, false, depth, work);
            if (parsed.Status != CssPropertyStatus.Valid) return parsed;
            target.Add(parsed.Value);
            if (target.Count > 4) return Invalid();
        }
        var span = parts[0].Span;
        if (grammar == CssPropertyGrammar.CornerRadius)
            return CssPropertyResult.Accepted(CssPropertyValue.Radius(horizontal[0],
                horizontal.Count == 2 ? horizontal[1] : horizontal[0], span, work));
        if (target == vertical && vertical.Count == 0) return Invalid();
        if (target == horizontal) vertical = horizontal;
        var corners = new CssPropertyValue[4];
        for (var i = 0; i < 4; i++) corners[i] = CssPropertyValue.Radius(At(horizontal, i), At(vertical, i), span, work);
        return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(SerializeRadii(corners, work), span, corners));

        static CssPropertyValue At(List<CssPropertyValue> values, int i) =>
            values[i < values.Count ? i : i == 3 && values.Count > 1 ? 1 : 0];
    }

    internal static string SerializePair(string first, string second, CssValueWork work)
    {
        var text = CssSubstitutionArguments.Equals(first, second, work) ? first : first + " " + second;
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }

    internal static string SerializeRadii(IReadOnlyList<CssPropertyValue> values, CssValueWork work)
    {
        var x = CssBoxPropertyParser.Serialize(values[0].Components[0].Text, values[1].Components[0].Text,
            values[2].Components[0].Text, values[3].Components[0].Text, work);
        var y = CssBoxPropertyParser.Serialize(values[0].Components[1].Text, values[1].Components[1].Text,
            values[2].Components[1].Text, values[3].Components[1].Text, work);
        var text = CssSubstitutionArguments.Equals(x, y, work) ? x : x + " / " + y;
        work.Charge(text.Length);
        return text;
    }

    internal static string Serialize(CssPropertyMetadata entry, string[] values, CssValueWork work)
    {
        if (entry.Grammar is CssPropertyGrammar.BorderWidths or CssPropertyGrammar.BorderStyles or CssPropertyGrammar.BorderColors)
            return values.Length == 2 ? SerializePair(values[0], values[1], work) :
                CssBoxPropertyParser.Serialize(values[0], values[1], values[2], values[3], work);
        string? width = null, style = null, color = null;
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(1);
            var name = entry.Longhands[i];
            if (entry.ResetOnlyLonghands.Contains(name))
            {
                if (values[i] != "initial") return "";
                continue;
            }
            ref var component = ref name.EndsWith("-width", StringComparison.Ordinal) ? ref width :
                ref name.EndsWith("-style", StringComparison.Ordinal) ? ref style : ref color;
            if (component is not null && !CssSubstitutionArguments.Equals(component, values[i], work)) return "";
            component = values[i];
        }
        var text = width + " " + style + " " + color;
        work.Charge(text.Length);
        return text;
    }

    internal static bool IsLogical(string name) =>
        name.StartsWith("border-block-", StringComparison.Ordinal) || name.StartsWith("border-inline-", StringComparison.Ordinal) ||
        name is "border-start-start-radius" or "border-start-end-radius" or "border-end-start-radius" or "border-end-end-radius";

    internal static bool OppositeMappings(string left, string right) =>
        IsLogical(left) != IsLogical(right) &&
        (IsLogical(left) ? LogicalGroup(right).Count != 0 : LogicalGroup(left).Count != 0) &&
        left.AsSpan(left.LastIndexOf('-')).SequenceEqual(right.AsSpan(right.LastIndexOf('-')));

    internal static IReadOnlyList<string> LogicalGroup(string physical)
    {
        if (!physical.StartsWith("border-", StringComparison.Ordinal) || IsLogical(physical)) return [];
        return physical switch
        {
            "border-top-width" or "border-right-width" or "border-bottom-width" or "border-left-width" => LogicalWidths,
            "border-top-style" or "border-right-style" or "border-bottom-style" or "border-left-style" => LogicalStyles,
            "border-top-color" or "border-right-color" or "border-bottom-color" or "border-left-color" => LogicalColors,
            "border-top-left-radius" or "border-top-right-radius" or "border-bottom-right-radius" or "border-bottom-left-radius" => LogicalRadii,
            _ => []
        };
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<string> LogicalNames(string suffix) =>
        Array.AsReadOnly((suffix == "radius" ? LogicalCorners : LogicalSides).Select(part => "border-" + part + "-" + suffix).ToArray());

    internal static string PhysicalName(string logical, string writingMode, string direction)
    {
        var vertical = writingMode != "horizontal-tb";
        var blockStart = writingMode switch { "vertical-rl" or "sideways-rl" => "right", "vertical-lr" or "sideways-lr" => "left", _ => "top" };
        var inlineStart = vertical ? (direction == "rtl") != (writingMode == "sideways-lr") ? "bottom" : "top" :
            direction == "rtl" ? "right" : "left";
        if (logical.EndsWith("-radius", StringComparison.Ordinal))
        {
            var block = logical.StartsWith("border-start-", StringComparison.Ordinal) ? blockStart : Opposite(blockStart);
            var inline = logical.EndsWith("-start-radius", StringComparison.Ordinal) ? inlineStart : Opposite(inlineStart);
            return "border-" + (vertical ? inline + "-" + block : block + "-" + inline) + "-radius";
        }
        var side = logical.StartsWith("border-block-", StringComparison.Ordinal) ? blockStart : inlineStart;
        if (logical.Contains("-end-", StringComparison.Ordinal)) side = Opposite(side);
        return "border-" + side + logical[logical.LastIndexOf('-')..];

        static string Opposite(string side) => side switch { "top" => "bottom", "bottom" => "top", "left" => "right", _ => "left" };
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
