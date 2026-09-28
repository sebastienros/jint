using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.HtmlParser.Css.Model;

// https://www.w3.org/TR/css-properties-values-api-1/#at-property-rule
internal sealed class CssPropertyRule(
    string name, CssRegisteredSyntax syntax, bool inherits, CssReferenceInput? initial, CssSourceSpan span) : CssRule(span)
{
    internal override CssRuleType Type => CssRuleType.Property;
    internal string Name { get; } = name;
    internal CssRegisteredSyntax Syntax { get; } = syntax;
    internal bool Inherits { get; } = inherits;
    internal CssReferenceInput? Initial { get; } = initial;
    internal string? InitialValue(CssValueWork work) => Initial is null ? null : CssPropertyParser.ValueText(Initial, work);

    internal static CssPropertyRule? Parse(string source, CssRuleSyntax rule, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work)
    {
        if (rule.Block is not { } block) return null;
        var name = CssPrimitiveParser.ParseDashedIdentifier(rule.Prelude, work);
        if (!name.IsMatch || name.Value.Text.Length == 2) return null;
        CssRegisteredSyntax? syntax = null;
        bool? inherits = null;
        CssReferenceInput? initial = null;
        foreach (var entry in parser.ParseBlockContents(block))
        {
            work.Charge(1);
            if (entry.Kind != CssBlockItemKind.Declarations) continue;
            foreach (var declaration in entry.Declarations)
            {
                work.Charge(1);
                if (declaration.IsImportant) continue;
                switch (CssPropertyRegistry.NormalizeName(declaration.Name, work))
                {
                    case "syntax":
                        var text = CssPrimitiveParser.ParseString(declaration.Value, work);
                        if (text.IsMatch && CssRegisteredSyntax.Parse(text.Value.Text, work) is { } parsed)
                            syntax = parsed;
                        break;
                    case "inherits":
                        var ident = CssPrimitiveParser.ParseIdentifier(declaration.Value, work);
                        if (ident.IsMatch)
                        {
                            var keyword = CssPropertyRegistry.NormalizeName(ident.Value.Text, work);
                            if (keyword is "true" or "false") inherits = keyword == "true";
                        }
                        break;
                    case "initial-value":
                        var input = CssReferenceInput.FromComponents(source, declaration.Value,
                            options?.Limits.MaxNestingDepth ?? 0, declaration.ValueSourceSpan, work,
                            declaration.ValueSerializationSpan, declaration.ValueTermination);
                        var analysis = CssReferenceParser.Analyze(input, CssReferenceUse.CustomPropertyValue, work);
                        if (analysis.Kind == CssReferenceAnalysisKind.Literal &&
                            !CssPrimitiveParser.ParseWideKeyword(input.Components, work).IsMatch)
                            initial = input;
                        break;
                }
            }
        }
        if (syntax is null || inherits is null) return null;
        if (!syntax.IsUniversal)
        {
            if (initial is null || syntax.Match(initial, work) is not { } value ||
                !CssRegisteredSyntax.IsIndependent(initial, value, work)) return null;
        }
        work.CheckCancellation();
        return new(name.Value.Text, syntax, inherits.Value, initial, rule.Span);
    }
}

internal enum CssRegisteredType
{
    Literal, Length, Number, Percentage, LengthPercentage, String, Color, Integer, Angle, Time,
    Resolution, CustomIdent, Image, Url, TransformFunction, TransformList
}

internal sealed record CssRegisteredValue(CssRegisteredType Type, IReadOnlyList<CssPropertyValue> Values, string Separator);

// A registration syntax is immutable and owns no document, query, or host callback.
internal sealed class CssRegisteredSyntax
{
    private readonly record struct Component(CssRegisteredType Type, string Name, char Multiplier);
    private readonly Component[] _components;
    private CssRegisteredSyntax(string text, Component[] components) { Text = text; _components = components; }
    internal string Text { get; }
    internal bool IsUniversal => _components.Length == 0;

    internal static CssRegisteredSyntax? Parse(string text, CssValueWork work)
    {
        work.Charge(text.Length);
        if (text.Trim(' ', '\t', '\r', '\n', '\f') == "*") return new(text, []);
        var components = new List<Component>();
        foreach (var raw in text.Split('|'))
        {
            work.Charge(raw.Length + 1);
            var part = raw.Trim(' ', '\t', '\r', '\n', '\f');
            if (part.Length == 0) return null;
            var multiplier = part[^1] is '+' or '#' ? part[^1] : '\0';
            if (multiplier != '\0') part = part[..^1];
            if (part.Length == 0) return null;
            var type = part switch
            {
                "<length>" => CssRegisteredType.Length,
                "<number>" => CssRegisteredType.Number,
                "<percentage>" => CssRegisteredType.Percentage,
                "<length-percentage>" => CssRegisteredType.LengthPercentage,
                "<string>" => CssRegisteredType.String,
                "<color>" => CssRegisteredType.Color,
                "<integer>" => CssRegisteredType.Integer,
                "<angle>" => CssRegisteredType.Angle,
                "<time>" => CssRegisteredType.Time,
                "<resolution>" => CssRegisteredType.Resolution,
                "<custom-ident>" => CssRegisteredType.CustomIdent,
                "<image>" => CssRegisteredType.Image,
                "<url>" => CssRegisteredType.Url,
                "<transform-function>" => CssRegisteredType.TransformFunction,
                "<transform-list>" => CssRegisteredType.TransformList,
                _ => CssRegisteredType.Literal
            };
            if (type == CssRegisteredType.TransformList && multiplier != '\0') return null;
            if (type == CssRegisteredType.Literal)
            {
                var input = ParseInput(part, work);
                var ident = CssPrimitiveParser.ParseCustomIdentifier(input.Components, [], work);
                if (!ident.IsMatch || ident.Span.Length != part.Length) return null;
                part = ident.Value.Text;
            }
            components.Add(new(type, part, multiplier));
        }
        work.CheckCancellation();
        return new(text, components.ToArray());
    }

    internal CssRegisteredValue? Match(CssReferenceInput input, CssValueWork work)
    {
        var parts = CssPropertyParser.Significant(input.Components, work);
        foreach (var component in _components)
        {
            work.Charge(1);
            if (component.Type == CssRegisteredType.TransformList && parts.Count != 0)
                throw new CssIncompleteGrammarException("@property", "R6:property:" + component.Name, parts[0].Span);
            if (parts.Count == 0 || component.Multiplier == '\0' && parts.Count != 1) continue;
            var values = new List<CssPropertyValue>();
            var valid = true;
            for (var i = 0; i < parts.Count; i++)
            {
                work.Charge(1);
                if (component.Multiplier == '#' && (i & 1) != 0)
                {
                    if (parts[i].Kind != CssComponentKind.Token || parts[i].Token.Kind != CssTokenKind.Comma)
                    { valid = false; break; }
                    continue;
                }
                var value = MatchAtom(component, parts[i], input.MaxNestingDepth, work);
                if (value is null) { valid = false; break; }
                values.Add(value);
            }
            if (valid && (component.Multiplier != '#' || (parts.Count & 1) != 0))
                return new(component.Type, values.AsReadOnly(), component.Multiplier == '#' ? ", " : " ");
        }
        work.CheckCancellation();
        return null;
    }

    private static CssPropertyValue? MatchAtom(Component component, CssComponentValue part, int depth, CssValueWork work)
    {
        var single = new CssComponentValueList([part]);
        switch (component.Type)
        {
            case CssRegisteredType.Literal:
            case CssRegisteredType.CustomIdent:
                var ident = CssPrimitiveParser.ParseCustomIdentifier(single, [], work);
                return ident.IsMatch && (component.Type == CssRegisteredType.CustomIdent ||
                    CssSubstitutionArguments.Equals(ident.Value.Text, component.Name, work))
                    ? CssPropertyValue.Keyword(CssSyntaxSerializer.SerializeIdentifier(ident.Value.Text, work), part.Span) : null;
            case CssRegisteredType.String:
                var text = CssPrimitiveParser.ParseString(single, work);
                return text.IsMatch ? CssPropertyValue.Keyword(CssSyntaxSerializer.SerializeString(text.Value.Text, work), part.Span) : null;
            case CssRegisteredType.Color:
                var color = CssColorParser.Parse(single, depth, work);
                if (color.Status == CssColorParseStatus.RequiresLaterGrammar)
                    throw new CssIncompleteGrammarException("@property", color.Blocker!, part.Span);
                return color.Status == CssColorParseStatus.Match
                    ? CssPropertyValue.ColorValue(color.Value, CssColorSerializer.SerializeSpecified(color.Value, work)) : null;
            case CssRegisteredType.Image or CssRegisteredType.Url or CssRegisteredType.TransformFunction or CssRegisteredType.TransformList:
                throw new CssIncompleteGrammarException("@property", "R6:property:" + component.Name, part.Span);
        }
        var production = component.Type switch
        {
            CssRegisteredType.Length => CssMathProduction.Length,
            CssRegisteredType.Number => CssMathProduction.Number,
            CssRegisteredType.Percentage => CssMathProduction.Percentage,
            CssRegisteredType.LengthPercentage => CssMathProduction.LengthPercentage,
            CssRegisteredType.Integer => CssMathProduction.Integer,
            CssRegisteredType.Angle => CssMathProduction.Angle,
            CssRegisteredType.Time => CssMathProduction.Time,
            CssRegisteredType.Resolution => CssMathProduction.Resolution,
            _ => throw new InvalidOperationException("Not a numeric registration.")
        };
        var percentages = production switch
        {
            CssMathProduction.LengthPercentage => CssMathPercentageMode.Length,
            CssMathProduction.Percentage => CssMathPercentageMode.Raw,
            _ => CssMathPercentageMode.Forbidden
        };
        var atom = CssPrimitiveParser.ParseNumericAtom(single, work);
        if (atom.IsMatch)
        {
            var number = atom.Value;
            var matches = production switch
            {
                CssMathProduction.Number => number.Kind == CssNumericKind.Number,
                CssMathProduction.Integer => number.Kind == CssNumericKind.Number && number.IsIntegerToken,
                CssMathProduction.Percentage => number.Kind == CssNumericKind.Percentage,
                CssMathProduction.LengthPercentage when number.Kind == CssNumericKind.Percentage => true,
                _ => number.Kind == CssNumericKind.Dimension && number.Unit.Category() == (production switch
                {
                    CssMathProduction.Length or CssMathProduction.LengthPercentage => CssUnitCategory.Length,
                    CssMathProduction.Angle => CssUnitCategory.Angle,
                    CssMathProduction.Time => CssUnitCategory.Time,
                    CssMathProduction.Resolution => CssUnitCategory.Resolution,
                    _ => CssUnitCategory.None
                })
            };
            if (!matches && production is CssMathProduction.Length or CssMathProduction.LengthPercentage &&
                number.Kind == CssNumericKind.Number && number.Number.Sign == 0)
            {
                number = new(CssNumericKind.Dimension, number.Number, CssUnit.Px, false, part.Span);
                matches = true;
            }
            if (!matches) return null;
            var finite = CssMathNumbers.ParseFinite(number.Number, number.Unit, work);
            if (production == CssMathProduction.Resolution && finite < 0) return null;
            var spelling = CssMathSerializer.SerializeFiniteNumber(finite, work);
            if (number.Kind == CssNumericKind.Percentage) spelling += "%";
            else if (number.Kind == CssNumericKind.Dimension) spelling += CssMathNumbers.CanonicalUnit(number.Unit).ToString().ToLowerInvariant();
            return CssPropertyValue.Number(number, spelling);
        }
        var math = CssMathParser.ParseMath(part, new(production, percentages, maximumNestingDepth: depth), work);
        if (math.Status == CssMathParseStatus.RequiresLaterGrammar)
            throw new CssIncompleteGrammarException("@property", "math:" + math.PendingFunction, part.Span);
        return math.Status == CssMathParseStatus.Match
            ? CssPropertyValue.Calculation(math.Value, CssMathSerializer.SerializeSpecified(math.Value, work)) : null;
    }

    internal static bool IsIndependent(CssReferenceInput input, CssRegisteredValue parsed, CssValueWork work)
    {
        if (parsed.Type == CssRegisteredType.Color)
        {
            foreach (var color in parsed.Values)
            {
                work.Charge(1);
                if (color.Color.Kind == CssColorKind.CurrentColor) return false;
            }
            return true;
        }
        if (parsed.Type is CssRegisteredType.Literal or CssRegisteredType.CustomIdent or CssRegisteredType.String) return true;
        var pending = new Stack<CssComponentValueList>();
        pending.Push(input.Components);
        while (pending.TryPop(out var values))
            foreach (var value in values)
            {
                work.Charge(1);
                if (value.Kind != CssComponentKind.Token) { pending.Push(value.Values); continue; }
                var token = value.Token;
                if (token.Kind != CssTokenKind.Dimension) continue;
                var unit = CssUnits.Recognize(token.Unit, work);
                if (unit is >= CssUnit.Em and <= CssUnit.Rlh or >= CssUnit.Cqw and <= CssUnit.Cqmax or
                    CssUnit.Vi or CssUnit.Vb or CssUnit.Svi or CssUnit.Svb or CssUnit.Lvi or CssUnit.Lvb or CssUnit.Dvi or CssUnit.Dvb)
                    return false;
            }
        return true;
    }

    internal static CssReferenceInput ParseInput(string text, CssValueWork work)
    {
        var parser = new CssSyntaxParser(text, null, work.Token, work.CheckCancellation);
        var values = parser.ParseComponentValues();
        var span = new CssSourceSpan(0, text.Length);
        return CssReferenceInput.FromComponents(text, values, 0, span, work,
            valueTermination: parser.ValueTermination(values, span, work));
    }
}
