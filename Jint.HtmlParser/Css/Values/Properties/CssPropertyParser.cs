using System.Globalization;
using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.References;
using Jint.HtmlParser.Css.Values.Transforms;

namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Display 3 §2; Color 4 §3.4; Position 3 §2; UI 4 §6.2; Overflow 3 §3.
internal static class CssPropertyParser
{
    internal static CssPropertyResult Parse(string name, string text,
        CssDeclarationContext context = CssDeclarationContext.Style, CssParseOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Parse(name, CssReferenceInput.Parse(text, options, cancellationToken), context, new CssValueWork(cancellationToken));

    internal static CssPropertyResult Parse(string name, CssReferenceInput input,
        CssDeclarationContext context, CssValueWork work)
    {
        work.CheckCancellation();
        var result = ParseCore(name, input, context, work);
        work.CheckCancellation();
        return result;
    }

    private static CssPropertyResult ParseCore(string name, CssReferenceInput input,
        CssDeclarationContext context, CssValueWork work)
    {
        if (!Enum.IsDefined(context)) throw new ArgumentOutOfRangeException(nameof(context));
        work.Charge(name.Length);
        name = CssPropertyRegistry.NormalizeName(name, work);
        var ordinary = context is CssDeclarationContext.Style or CssDeclarationContext.Keyframe;
        if (name.Length > 2 && name.StartsWith("--", StringComparison.Ordinal))
        {
            if (!ordinary) return CssPropertyResult.Rejected(CssPropertyStatus.UnsupportedProperty);
            var custom = CssReferenceParser.ParseCustomProperty(name, input, work);
            return custom.Kind switch
            {
                CssCustomPropertyKind.Value => CssPropertyResult.Accepted(CssPropertyValue.Reference(custom.Value.Program,
                    ValueText(input, work), true)),
                CssCustomPropertyKind.WideKeyword => CssPropertyResult.Accepted(CssPropertyValue.Keyword(
                    custom.WideKeyword.CanonicalSpelling(), default)),
                CssCustomPropertyKind.PendingFeature => CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, custom.PendingFunction),
                _ => CssPropertyResult.Rejected(CssPropertyStatus.Invalid)
            };
        }
        var entry = CssPropertyRegistry.Find(name, context);
        if (entry is null)
        {
            return NameFailure(name, context)!.Value;
        }
        var analysis = CssReferenceParser.Analyze(input, CssReferenceUse.PropertyValue, work);
        if (analysis.Kind == CssReferenceAnalysisKind.InvalidSyntax) return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        if (analysis.Kind == CssReferenceAnalysisKind.PendingFeature)
            return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, analysis.PendingFunction);
        if (analysis.Kind == CssReferenceAnalysisKind.Deferred)
            return CssPropertyResult.Accepted(CssPropertyValue.Reference(analysis.Program, ValueText(input, work), false), true);
        var wide = CssPrimitiveParser.ParseWideKeyword(input.Components, work);
        if (wide.IsMatch) return CssPropertyResult.Accepted(CssPropertyValue.Keyword(wide.Value.CanonicalSpelling(), wide.Span));
        if (entry.Grammar == CssPropertyGrammar.Color)
        {
            var color = CssColorParser.Parse(input.Components, input.MaxNestingDepth, work);
            return color.Status switch
            {
                CssColorParseStatus.Match => CssPropertyResult.Accepted(CssPropertyValue.ColorValue(color.Value,
                    CssColorSerializer.SerializeSpecified(color.Value, work))),
                CssColorParseStatus.RequiresLaterGrammar => CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, color.Blocker),
                _ => Invalid()
            };
        }
        var parts = Significant(input.Components, work);
        if (entry.Grammar == CssPropertyGrammar.TransformList)
            return CssTransformListParser.Parse(parts, input.MaxNestingDepth, work);
        if (entry.Grammar is CssPropertyGrammar.TextDecoration or CssPropertyGrammar.TextDecorationLine or
            CssPropertyGrammar.TextDecorationStyle or CssPropertyGrammar.TextDecorationThickness)
            return CssTextDecorationPropertyParser.Parse(entry.Grammar, parts, input.MaxNestingDepth, work);
        if (entry.Grammar is CssPropertyGrammar.Translate or CssPropertyGrammar.Rotate or CssPropertyGrammar.Scale)
            return CssTransformParser.Parse(entry.Grammar, parts, input.MaxNestingDepth, work);
        if (entry.Grammar == CssPropertyGrammar.FontWeight)
            return CssFontWeightPropertyParser.Parse(input, parts, work);
        if (entry.Grammar == CssPropertyGrammar.FontSize)
            return CssFontSizePropertyParser.Parse(parts, input.MaxNestingDepth, work);
        if (entry.Grammar is CssPropertyGrammar.TextAlign or CssPropertyGrammar.TextAlignAll or CssPropertyGrammar.TextAlignLast)
            return CssTextAlignPropertyParser.Parse(entry.Grammar, parts, work);
        if (entry.Grammar is CssPropertyGrammar.WhiteSpace or CssPropertyGrammar.WhiteSpaceCollapse or
            CssPropertyGrammar.TextWrapMode or CssPropertyGrammar.WhiteSpaceTrim)
            return CssWhiteSpacePropertyParser.Parse(entry.Grammar, parts, work);
        if (entry.Grammar is CssPropertyGrammar.Margin or CssPropertyGrammar.MarginSide or
            CssPropertyGrammar.Padding or CssPropertyGrammar.PaddingSide)
            return CssBoxPropertyParser.Parse(entry.Grammar, parts, input.MaxNestingDepth, work);
        if (entry.Grammar is CssPropertyGrammar.Sizing or CssPropertyGrammar.MinSizing or CssPropertyGrammar.MaxSizing or CssPropertyGrammar.FlexBasis)
            return CssSizingPropertyParser.Parse(entry.Grammar, parts, input.MaxNestingDepth, work);
        if (entry.Grammar is CssPropertyGrammar.FlexFactor or CssPropertyGrammar.FlexDirection or
            CssPropertyGrammar.FlexWrap or CssPropertyGrammar.Direction or CssPropertyGrammar.Flex or CssPropertyGrammar.FlexFlow)
            return CssFlexPropertyParser.Parse(entry.Grammar, parts, input.MaxNestingDepth, work);
        if (entry.Grammar is CssPropertyGrammar.AlignItems or CssPropertyGrammar.AlignSelf or
            CssPropertyGrammar.JustifyItems or CssPropertyGrammar.JustifySelf or CssPropertyGrammar.PlaceItems or CssPropertyGrammar.PlaceSelf)
            return CssAlignmentPropertyParser.Parse(entry.Grammar, parts, work);
        if (entry.Grammar == CssPropertyGrammar.Display) return Display(parts);
        if (entry.Grammar is CssPropertyGrammar.Opacity or CssPropertyGrammar.ZIndex)
            return Numeric(entry.Grammar, input, parts, work);
        var keywords = entry.Grammar switch
        {
            CssPropertyGrammar.Visibility => "visible hidden collapse",
            CssPropertyGrammar.Position => "static relative absolute sticky fixed",
            CssPropertyGrammar.PointerEvents => "auto none visiblepainted visiblefill visiblestroke visible painted fill stroke all bounding-box",
            CssPropertyGrammar.BoxSizing => "content-box border-box",
            CssPropertyGrammar.TransformBox => "content-box border-box fill-box stroke-box view-box",
            _ => "visible hidden clip scroll auto overlay"
        };
        if (parts.Count < 1 || parts.Count > (entry.Grammar == CssPropertyGrammar.Overflow ? 2 : 1)) return Invalid();
        var first = Keyword(parts[0], keywords);
        if (first is null) return Invalid();
        if (first == "overlay") first = "auto";
        if (entry.Grammar != CssPropertyGrammar.Overflow)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(first, parts[0].Span));
        var second = parts.Count == 1 ? first : Keyword(parts[1], keywords);
        if (second == "overlay") second = "auto";
        return second is null ? Invalid() : CssPropertyResult.Accepted(CssPropertyValue.Pair(first, second, parts[0].Span));
    }

    // Mutation routes without a value still require completed name/context metadata. In
    // particular, removal must not invent the longhand/reset membership of a pending shorthand.
    internal static CssPropertyResult? NameFailure(string normalizedName, CssDeclarationContext context)
    {
        var ordinary = context is CssDeclarationContext.Style or CssDeclarationContext.Keyframe;
        if (normalizedName.Length > 2 && normalizedName.StartsWith("--", StringComparison.Ordinal))
            return ordinary ? null : CssPropertyResult.Rejected(CssPropertyStatus.UnsupportedProperty);
        if (CssPropertyRegistry.Find(normalizedName, context) is not null) return null;
        if (normalizedName == "all") return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "V0:all-reset");
        if (CssPropertyCatalog.Obligations.TryGetValue(normalizedName, out var family))
        {
            if (family == "V9" && ordinary) return CssPropertyResult.Rejected(CssPropertyStatus.UnsupportedProperty);
            return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar,
                ordinary ? family + ":" + normalizedName : "context-audit:" + context + ":" + normalizedName);
        }
        return CssPropertyResult.Rejected(CssPropertyStatus.UnsupportedProperty);
    }

    private static CssPropertyResult Numeric(CssPropertyGrammar grammar, CssReferenceInput input,
        List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count != 1) return Invalid();
        if (grammar == CssPropertyGrammar.ZIndex && Keyword(parts[0], "auto") is { } keyword)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, parts[0].Span));
        var atom = CssPrimitiveParser.ParseNumericAtom(input.Components, work);
        if (atom.IsMatch)
        {
            if (atom.Value.Kind is not (CssNumericKind.Number or CssNumericKind.Percentage) ||
                grammar == CssPropertyGrammar.ZIndex && (atom.Value.Kind != CssNumericKind.Number || !atom.Value.IsIntegerToken))
                return Invalid();
            if (grammar == CssPropertyGrammar.ZIndex)
                return CssPropertyResult.Accepted(CssPropertyValue.Number(atom.Value,
                    SerializeInteger(atom.Value.Number, work)));
            // CSS Color 4 §17: declared opacity percentages serialize as equivalent numbers.
            var number = CssMathNumbers.ParseFinite(atom.Value.Number, CssUnit.None, work);
            if (atom.Value.Kind == CssNumericKind.Percentage) number /= 100;
            var spelling = number.ToString("0.######", CultureInfo.InvariantCulture);
            return CssPropertyResult.Accepted(CssPropertyValue.Number(atom.Value, spelling));
        }
        var context = grammar == CssPropertyGrammar.ZIndex
            ? new CssMathContext(CssMathProduction.Integer, CssMathPercentageMode.Forbidden, maximumNestingDepth: input.MaxNestingDepth)
            : new CssMathContext(CssMathProduction.NumberOrPercentage, CssMathPercentageMode.Raw, maximumNestingDepth: input.MaxNestingDepth);
        var math = CssMathParser.ParseMath(parts[0], context, work);
        return math.Status switch
        {
            CssMathParseStatus.Match => CssPropertyResult.Accepted(CssPropertyValue.Calculation(math.Value,
                CssMathSerializer.SerializeSpecified(math.Value, work))),
            CssMathParseStatus.RequiresLaterGrammar => CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar,
                "math:" + math.PendingFunction),
            _ => Invalid()
        };
    }

    private static string SerializeInteger(CssNumber number, CssValueWork work)
    {
        work.CheckCancellation();
        if (number.Sign == 0) return "0";
        // The caller admitted only integer tokens: their spelling is a sign and decimal digits.
        // Keep those digits exactly; floating-point formatting can round even exact Int64 values.
        var spelling = number.Spelling;
        var start = spelling[0] is '+' or '-' ? 1 : 0;
        while (spelling[start] == '0')
        {
            work.Charge(1);
            start++;
        }
        work.CheckCancellation();
        var result = number.Sign < 0
            ? string.Concat("-".AsSpan(), spelling.AsSpan(start))
            : spelling[start..];
        work.Charge(result.Length);
        work.CheckCancellation();
        return result;
    }

    // Unordered outer/inner/list-item groups; legacy spellings serialize to their shortest equivalent.
    private static CssPropertyResult Display(List<CssComponentValue> parts)
    {
        if (parts.Count is < 1 or > 3) return Invalid();
        if (parts.Count == 1 && Keyword(parts[0], "none contents table-row-group table-header-group table-footer-group table-row table-cell table-column-group table-column table-caption ruby-base ruby-text ruby-base-container ruby-text-container inline-block inline-table inline-flex inline-grid") is { } single)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(single, parts[0].Span));
        string? outer = null, inner = null;
        var list = false;
        foreach (var part in parts)
        {
            if (Keyword(part, "block inline run-in") is { } o && outer is null) outer = o;
            else if (Keyword(part, "flow flow-root table flex grid ruby") is { } i && inner is null) inner = i;
            else if (Keyword(part, "list-item") is not null && !list) list = true;
            else return Invalid();
        }
        if (list && inner is not (null or "flow" or "flow-root")) return Invalid();
        outer ??= inner == "ruby" ? "inline" : "block";
        inner ??= "flow";
        var text = list
            ? (outer == "block" ? "" : outer + " ") + (inner == "flow" ? "" : inner + " ") + "list-item"
            : (outer, inner) switch
            {
                (_, "flow") => outer,
                ("inline", "flow-root") => "inline-block",
                ("inline", "table") => "inline-table",
                ("inline", "flex") => "inline-flex",
                ("inline", "grid") => "inline-grid",
                ("inline", "ruby") => "ruby",
                ("block", "ruby") => "block ruby",
                ("block", _) => inner,
                _ => outer + " " + inner
            };
        return CssPropertyResult.Accepted(CssPropertyValue.Keyword(text, parts[0].Span));
    }

    internal static List<CssComponentValue> Significant(CssComponentValueList values, CssValueWork work)
    {
        var parts = new List<CssComponentValue>();
        for (var i = 0; i < values.Count; i++)
        {
            work.Charge(1);
            if (values[i].Kind != CssComponentKind.Token || values[i].Token.Kind != CssTokenKind.Whitespace) parts.Add(values[i]);
        }
        return parts;
    }

    internal static string? Keyword(CssComponentValue value, string choices, CssValueWork? work = null)
    {
        if (value.Kind != CssComponentKind.Token || value.Token.Kind != CssTokenKind.Ident) return null;
        var name = CssPropertyRegistry.NormalizeName(value.Token.Text, work);
        var remaining = choices.AsSpan();
        while (!remaining.IsEmpty)
        {
            var space = remaining.IndexOf(' ');
            var choice = space < 0 ? remaining : remaining[..space];
            if (name.AsSpan().SequenceEqual(choice)) return choice.ToString();
            if (space < 0) break;
            remaining = remaining[(space + 1)..];
        }
        return null;
    }

    internal static string ValueText(CssReferenceInput input, CssValueWork work)
    {
        work.CheckCancellation();
        var values = input.Components;
        var start = 0;
        var end = values.Count;
        // CSS whitespace is a token production. Source characters inside identifiers (including
        // escaped ASCII whitespace and non-ASCII name characters) must remain untouched.
        while (start < end && IsWhitespace(values[start]))
        {
            work.Charge(1);
            start++;
        }
        while (end > start && IsWhitespace(values[end - 1]))
        {
            work.Charge(1);
            end--;
        }
        if (start == end)
        {
            work.CheckCancellation();
            return "";
        }
        var first = values[start].Span;
        var last = values[end - 1].Span;
        var length = last.Start + last.Length - first.Start;
        work.CheckCancellation();
        var text = input.SourceSlice(new CssSourceSpan(first.Start, length)).ToString();
        work.Charge(length);
        work.CheckCancellation();
        return text;
    }

    private static bool IsWhitespace(CssComponentValue value) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace;

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
