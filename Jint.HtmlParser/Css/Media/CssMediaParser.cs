using System.Text;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Media;

// Media Queries 5 §§3–3.2, CSSOM §4. Parse C1 components directly, recovering per comma.
internal static class CssMediaParser
{
    internal static CssMediaQuery[] Parse(CssComponentValueList values, string source, CssSyntaxParser parser, CssValueWork work)
    {
        var queries = new List<CssMediaQuery>();
        var part = new List<CssComponentValue>();
        var hasComma = false;
        foreach (var value in values)
        {
            work.Charge(1);
            if (Token(value, CssTokenKind.Comma))
            {
                queries.Add(Query(part.ToArray(), source, parser, work));
                part.Clear();
                hasComma = true;
            }
            else if (!Token(value, CssTokenKind.Whitespace)) part.Add(value);
        }
        if (part.Count != 0 || hasComma) queries.Add(Query(part.ToArray(), source, parser, work));
        work.CheckCancellation();
        return queries.ToArray();
    }

    private static CssMediaQuery Query(CssComponentValue[] values, string source, CssSyntaxParser parser, CssValueWork work)
    {
        try
        {
            if (values.Length == 0 || !ValidAnyValue(new CssComponentValueList(values), work)) throw new MediaSyntaxException();
            string? type = null;
            var negate = false;
            var modifier = "";
            var start = 0;
            if (Ident(values[0], work) is { } first &&
                !(first == "not" && values.Length > 1 && InParens(values[1])))
            {
                if (first is "not" or "only")
                {
                    negate = first == "not";
                    modifier = first + " ";
                    start++;
                }
                if (start == values.Length || Ident(values[start], work) is not { } mediaType ||
                    mediaType is "not" or "only" or "and" or "or" or "layer") throw new MediaSyntaxException();
                type = mediaType;
                start++;
                if (start < values.Length)
                {
                    if (Ident(values[start++], work) != "and" || start == values.Length) throw new MediaSyntaxException();
                }
            }
            var builder = new StringBuilder(modifier);
            if (type is not null) builder.Append(CssSyntaxSerializer.SerializeIdentifier(type, work));
            var program = new List<CssMediaInstruction>();
            if (start < values.Length)
            {
                if (type is not null) builder.Append(" and ");
                Condition(values[start..], type is null, source, parser, builder, program, work);
            }
            else if (type is null) throw new MediaSyntaxException();
            work.CheckCancellation();
            var text = builder.ToString();
            // A wholly unknown condition has no device-dependent term capable of eliminating it.
            var hasFeature = false;
            foreach (var instruction in program) { work.Charge(1); hasFeature |= instruction.Operation == CssMediaOperation.Feature; }
            if (type is null && program.Count != 0 && !hasFeature) return Invalid();
            return new CssMediaQuery(text, type, negate, program.ToArray());
        }
        catch (MediaSyntaxException) { return Invalid(); }
    }

    private static CssMediaQuery Invalid() => new("not all", "all", true, []);

    private sealed record Task(CssComponentValue[]? Values = null, bool Operand = false,
        bool AllowOr = true, string? Text = null, CssMediaOperation? Operation = null,
        CssComponentValue? Recovery = null, int BuilderStart = 0, int ProgramStart = 0);

    private static void Condition(CssComponentValue[] values, bool allowOr, string source, CssSyntaxParser parser, StringBuilder builder,
        List<CssMediaInstruction> program, CssValueWork work)
    {
        var tasks = new Stack<Task>();
        tasks.Push(new Task(values, AllowOr: allowOr));
        while (tasks.TryPop(out var task))
        {
            work.Charge(1);
            try
            {
                if (task.Recovery is not null) continue;
                if (task.Text is { } text) { builder.Append(text); work.Charge(text.Length); continue; }
                if (task.Operation is { } operation) { program.Add(new CssMediaInstruction(operation)); continue; }
                var items = task.Values!;
                if (task.Operand)
                {
                    if (items.Length != 1 || !InParens(items[0])) throw new MediaSyntaxException();
                    var value = items[0];
                    if (value.Kind == CssComponentKind.Function)
                    {
                        // General-enclosed is syntactically valid and retains unknown truth.
                        program.Add(new CssMediaInstruction(CssMediaOperation.Unknown));
                        AppendSource(builder, source, value.Span, work);
                        builder.Append(parser.ValueTermination(new CssComponentValueList([value]), value.Span, work));
                        continue;
                    }
                    var inside = WithoutWhitespace(value.Values, work);
                    tasks.Push(new Task(Recovery: value, BuilderStart: builder.Length, ProgramStart: program.Count));
                    builder.Append('(');
                    tasks.Push(new Task(Text: ")"));
                    if (Feature(inside, source, parser, program, builder, work)) continue;
                    if (inside.Length == 0) throw new MediaSyntaxException();
                    if (Ident(inside[0], work) is { } name && name != "not")
                    {
                        program.Add(new CssMediaInstruction(CssMediaOperation.Unknown));
                        AppendSource(builder, source, Span(inside), work);
                        builder.Append(parser.ValueTermination(new CssComponentValueList(inside), Span(inside), work));
                        continue;
                    }
                    tasks.Push(new Task(inside));
                    continue;
                }
                if (items.Length == 2 && Ident(items[0], work) == "not")
                {
                    builder.Append("not ");
                    tasks.Push(new Task(Operation: CssMediaOperation.Not));
                    tasks.Push(new Task([items[1]], Operand: true));
                    continue;
                }
                if (items.Length == 0 || (items.Length & 1) == 0) throw new MediaSyntaxException();
                string? join = null;
                for (var i = 1; i < items.Length; i += 2)
                {
                    work.Charge(1);
                    var op = Ident(items[i], work);
                    if (op is not ("and" or "or") || (!task.AllowOr && op == "or") ||
                        (join is not null && op != join)) throw new MediaSyntaxException();
                    join = op;
                }
                for (var i = items.Length - 1; i >= 0; i -= 2)
                {
                    if (i > 0) tasks.Push(new Task(Operation: join == "and" ? CssMediaOperation.And : CssMediaOperation.Or));
                    tasks.Push(new Task([items[i]], Operand: true));
                    if (i > 0) tasks.Push(new Task(Text: " " + join + " "));
                }
            }
            catch (MediaSyntaxException)
            {
                Task? recovery = null;
                while (tasks.TryPop(out var pending))
                {
                    work.Charge(1);
                    if (pending.Recovery is not null) { recovery = pending; break; }
                }
                if (recovery?.Recovery is not { } value) throw;
                builder.Length = recovery.BuilderStart;
                program.RemoveRange(recovery.ProgramStart, program.Count - recovery.ProgramStart);
                program.Add(new CssMediaInstruction(CssMediaOperation.Unknown));
                AppendSource(builder, source, value.Span, work);
                builder.Append(parser.ValueTermination(new CssComponentValueList([value]), value.Span, work));
            }
        }
    }

    private static bool ValidAnyValue(CssComponentValueList values, CssValueWork work)
    {
        var pending = new Stack<CssComponentValueList>();
        pending.Push(values);
        while (pending.TryPop(out var list))
        {
            foreach (var value in list)
            {
                work.Charge(1);
                if (value.Kind is CssComponentKind.Function or CssComponentKind.SimpleBlock) pending.Push(value.Values);
                else if (value.Token.Kind is CssTokenKind.BadString or CssTokenKind.BadUrl or
                    CssTokenKind.CloseParenthesis or CssTokenKind.CloseSquareBracket or CssTokenKind.CloseCurlyBracket) return false;
            }
        }
        return true;
    }

    private static bool Feature(CssComponentValue[] items, string source, CssSyntaxParser parser, List<CssMediaInstruction> program,
        StringBuilder builder, CssValueWork work)
    {
        if (items.Length == 0) return false;
        // Boolean and colon syntax. Range syntax is handled separately below.
        if (Ident(items[0], work) is { } name && (items.Length == 1 || Token(items[1], CssTokenKind.Colon)))
        {
            var comparison = CssMediaComparison.Boolean;
            var baseName = name;
            if (name.StartsWith("min-", StringComparison.Ordinal)) { baseName = name[4..]; comparison = CssMediaComparison.GreaterEqual; }
            if (name.StartsWith("max-", StringComparison.Ordinal)) { baseName = name[4..]; comparison = CssMediaComparison.LessEqual; }
            var discrete = Discrete(baseName);
            if (comparison != CssMediaComparison.Boolean && (items.Length == 1 || discrete is not null || baseName == "grid"))
                return Unknown(program, builder, source, parser, items, work);
            if (items.Length > 1 && comparison == CssMediaComparison.Boolean) comparison = CssMediaComparison.Equal;
            var value = items.Length == 1 ? [] : items[2..];
            var feature = Validate(baseName, comparison, value, work);
            if (feature is null) return Unknown(program, builder, source, parser, items, work);
            program.Add(new CssMediaInstruction(CssMediaOperation.Feature, feature));
            builder.Append(CssSyntaxSerializer.SerializeIdentifier(name, work));
            if (value.Length != 0) builder.Append(": ").Append(ValueText(feature));
            return true;
        }
        var operators = new List<(int Index, int Length, CssMediaComparison Comparison)>();
        for (var i = 0; i < items.Length; i++)
        {
            work.Charge(1);
            if (items[i].Kind != CssComponentKind.Token || items[i].Token.Kind != CssTokenKind.Delim) continue;
            var delimiter = items[i].Token.Delimiter;
            if (delimiter is not ('<' or '>' or '=')) continue;
            var equal = i + 1 < items.Length && Delim(items[i + 1], '=') && delimiter != '=';
            var comparison = delimiter == '=' ? CssMediaComparison.Equal : delimiter == '<'
                ? equal ? CssMediaComparison.LessEqual : CssMediaComparison.Less
                : equal ? CssMediaComparison.GreaterEqual : CssMediaComparison.Greater;
            operators.Add((i, equal ? 2 : 1, comparison));
            if (equal) i++;
        }
        if (operators.Count == 0) return false;
        if (operators.Count > 2) throw new MediaSyntaxException();
        var first = operators[0];
        var left = items[..first.Index];
        var rightStart = first.Index + first.Length;
        var right = items[rightStart..(operators.Count == 2 ? operators[1].Index : items.Length)];
        string? rangeName;
        CssComponentValue[] rangeValue;
        var reverse = false;
        if (left.Length == 1 && Ident(left[0], work) is { } leftName) { rangeName = leftName; rangeValue = right; }
        else if (right.Length == 1 && Ident(right[0], work) is { } rightName) { rangeName = rightName; rangeValue = left; reverse = true; }
        else throw new MediaSyntaxException();
        if (operators.Count == 2 && (!reverse || first.Comparison == CssMediaComparison.Equal)) throw new MediaSyntaxException();
        if (rangeName == "grid" || Discrete(rangeName) is not null || rangeName.StartsWith("min-", StringComparison.Ordinal) ||
            rangeName.StartsWith("max-", StringComparison.Ordinal)) return Unknown(program, builder, source, parser, items, work);
        var feature1 = Validate(rangeName, reverse ? Reverse(first.Comparison) : first.Comparison, rangeValue, work);
        if (feature1 is null) return Unknown(program, builder, source, parser, items, work);
        CssMediaFeature? feature2 = null;
        CssComponentValue[] last = [];
        if (operators.Count == 2)
        {
            var second = operators[1];
            if (second.Comparison == CssMediaComparison.Equal ||
                (first.Comparison is CssMediaComparison.Less or CssMediaComparison.LessEqual) !=
                (second.Comparison is CssMediaComparison.Less or CssMediaComparison.LessEqual)) throw new MediaSyntaxException();
            last = items[(second.Index + second.Length)..];
            feature2 = Validate(rangeName, second.Comparison, last, work);
            if (feature2 is null) return Unknown(program, builder, source, parser, items, work);
        }
        program.Add(new CssMediaInstruction(CssMediaOperation.Feature, feature1));
        if (reverse) builder.Append(ValueText(feature1)).Append(Operator(first.Comparison)).Append(rangeName);
        else builder.Append(rangeName).Append(Operator(first.Comparison)).Append(ValueText(feature1));
        if (feature2 is not null)
        {
            program.Add(new CssMediaInstruction(CssMediaOperation.Feature, feature2));
            program.Add(new CssMediaInstruction(CssMediaOperation.And));
            builder.Append(Operator(operators[1].Comparison)).Append(ValueText(feature2));
        }
        return true;
    }

    private static CssMediaFeature? Validate(string name, CssMediaComparison comparison,
        CssComponentValue[] value, CssValueWork work)
    {
        if (Discrete(name) is { } keywords)
        {
            if (comparison == CssMediaComparison.Boolean) return new(name, comparison, 0, CssUnit.None, "");
            if (value.Length != 1 || Ident(value[0], work) is not { } keyword || !keywords.Contains(keyword, StringComparer.Ordinal)) return null;
            return new(name, comparison, 0, CssUnit.None, keyword);
        }
        if (name is not ("width" or "height" or "aspect-ratio" or "resolution" or "color" or "color-index" or "monochrome" or "grid"))
        {
            if (KnownPending(name)) throw new CssIncompleteRuleGrammarException("media", "R2:media-feature:" + name,
                value.Length == 0 ? default : value[0].Span);
            return null;
        }
        if (comparison == CssMediaComparison.Boolean) return new(name, comparison, 0, CssUnit.None, null);
        if (value.Length == 0) return null;
        foreach (var component in value)
        {
            work.Charge(1);
            if (component.Kind == CssComponentKind.Function)
                throw new CssIncompleteRuleGrammarException("media", "R2:media-value-expression", component.Span);
        }
        if (name == "aspect-ratio")
        {
            if (value.Length is not (1 or 3) || !Number(value[0], work, out var a) || a.Sign < 0) return null;
            var b = CssNumber.FromValidatedToken("1", work);
            if (value.Length == 3 && (!Delim(value[1], '/') || !Number(value[2], work, out b) || b.Sign < 0)) return null;
            var numerator = CssMathNumbers.ParseFinite(a, CssUnit.None, work);
            var denominator = CssMathNumbers.ParseFinite(b, CssUnit.None, work);
            var spelling = CssMathSerializer.SerializeFiniteNumber(numerator, work) + " / " +
                CssMathSerializer.SerializeFiniteNumber(denominator, work);
            return new(name, comparison, numerator / denominator, CssUnit.None, null, spelling);
        }
        if (value.Length != 1 || value[0].Kind != CssComponentKind.Token) return null;
        var token = value[0].Token;
        if (token.Kind is not (CssTokenKind.Number or CssTokenKind.Dimension)) return null;
        var number = CssNumber.FromValidatedToken(token.NumberText, work);
        if (name is "width" or "height")
        {
            if (token.Kind == CssTokenKind.Number)
                return number.Sign == 0 ? NumericFeature(name, comparison, number, CssUnit.None, work) : null;
            var unit = CssUnits.Recognize(token.Unit, work);
            if (unit is >= CssUnit.Px and <= CssUnit.Rem) return NumericFeature(name, comparison, number, unit, work);
            if (unit.Category() == CssUnitCategory.Length)
                throw new CssIncompleteRuleGrammarException("media", "R2:media-length-unit:" + token.Unit, token.Span);
            return null;
        }
        if (name == "resolution")
        {
            if (token.Kind != CssTokenKind.Dimension) return null;
            var unit = CssUnits.Recognize(token.Unit, work);
            return unit.Category() == CssUnitCategory.Resolution ? NumericFeature(name, comparison, number, unit, work) : null;
        }
        if (token.Kind != CssTokenKind.Number || !token.IsInteger) return null;
        if (name == "grid" && number.Sign != 0 && number.CompareTo(CssNumber.FromValidatedToken("1", work), work) != 0) return null;
        return NumericFeature(name, comparison, number, CssUnit.None, work);
    }

    private static CssMediaFeature NumericFeature(string name, CssMediaComparison comparison,
        CssNumber number, CssUnit unit, CssValueWork work)
    {
        var projected = CssMathNumbers.ParseFinite(number, CssUnit.None, work);
        var spelling = CssMathSerializer.SerializeFiniteNumber(projected, work) +
            (unit == CssUnit.None ? "" : unit.ToString().ToLowerInvariant());
        return new(name, comparison, projected, unit, null, spelling);
    }

    private static string[]? Discrete(string name) => name switch
    {
        "orientation" => ["portrait", "landscape"],
        "pointer" => ["none", "coarse", "fine"],
        "hover" => ["none", "hover"],
        "prefers-color-scheme" => ["light", "dark"],
        "prefers-reduced-motion" or "prefers-reduced-transparency" or "prefers-reduced-data" => ["no-preference", "reduce"],
        "prefers-contrast" => ["no-preference", "more", "less", "custom"],
        "forced-colors" => ["none", "active"],
        "scripting" => ["none", "initial-only", "enabled"],
        _ => null
    };

    private static bool KnownPending(string name) => name.StartsWith("--", StringComparison.Ordinal) || name is
        "device-width" or "device-height" or "device-aspect-ratio" or "any-pointer" or "any-hover" or
        "overflow-block" or "overflow-inline" or "horizontal-viewport-segments" or "vertical-viewport-segments" or
        "display-mode" or "scan" or "update" or "environment-blending" or "color-gamut" or "dynamic-range" or
        "inverted-colors" or "nav-controls" or "video-color-gamut" or "video-dynamic-range" or "ua-color-scheme";

    private static string ValueText(CssMediaFeature feature) => feature.Keyword ?? feature.SpecifiedValue;
    private static string Operator(CssMediaComparison comparison) => comparison switch
    {
        CssMediaComparison.Equal => " = ",
        CssMediaComparison.Less => " < ",
        CssMediaComparison.LessEqual => " <= ",
        CssMediaComparison.Greater => " > ",
        CssMediaComparison.GreaterEqual => " >= ",
        _ => throw new InvalidOperationException()
    };
    private static CssMediaComparison Reverse(CssMediaComparison comparison) => comparison switch
    {
        CssMediaComparison.Less => CssMediaComparison.Greater,
        CssMediaComparison.LessEqual => CssMediaComparison.GreaterEqual,
        CssMediaComparison.Greater => CssMediaComparison.Less,
        CssMediaComparison.GreaterEqual => CssMediaComparison.LessEqual,
        _ => comparison
    };
    private static bool Unknown(List<CssMediaInstruction> program, StringBuilder builder,
        string source, CssSyntaxParser parser, CssComponentValue[] items, CssValueWork work)
    {
        var span = Span(items);
        program.Add(new CssMediaInstruction(CssMediaOperation.Unknown));
        AppendSource(builder, source, span, work);
        builder.Append(parser.ValueTermination(new CssComponentValueList(items), span, work));
        return true;
    }
    private static CssSourceSpan Span(CssComponentValue[] values) => new(values[0].Span.Start,
        values[^1].Span.Start + values[^1].Span.Length - values[0].Span.Start);
    private static void AppendSource(StringBuilder builder, string source, CssSourceSpan span, CssValueWork work)
    {
        work.Charge(span.Length);
        work.CheckCancellation();
        builder.Append(source, span.Start, span.Length);
        work.CheckCancellation();
    }
    private static bool Number(CssComponentValue value, CssValueWork work, out CssNumber number)
    {
        number = default;
        if (!Token(value, CssTokenKind.Number)) return false;
        number = CssNumber.FromValidatedToken(value.Token.NumberText, work);
        return true;
    }
    private static bool InParens(CssComponentValue value) => value.Kind == CssComponentKind.Function ||
        (value.Kind == CssComponentKind.SimpleBlock && value.OpeningDelimiter == '(');
    private static string? Ident(CssComponentValue value, CssValueWork work) => Token(value, CssTokenKind.Ident)
        ? CssPropertyRegistry.NormalizeName(value.Token.Text, work) : null;
    private static bool Token(CssComponentValue value, CssTokenKind kind) => value.Kind == CssComponentKind.Token && value.Token.Kind == kind;
    private static bool Delim(CssComponentValue value, char delimiter) => Token(value, CssTokenKind.Delim) && value.Token.Delimiter == delimiter;
    private static CssComponentValue[] WithoutWhitespace(CssComponentValueList values, CssValueWork work)
    {
        var result = new List<CssComponentValue>();
        for (var i = 0; i < values.Count; i++)
        {
            work.Charge(1);
            var value = values[i];
            // MQ5 §3 forbids a whitespace token between < or > and a following =.
            var comparisonWhitespace = Token(value, CssTokenKind.Whitespace) && i > 0 && i + 1 < values.Count &&
                (Delim(values[i - 1], '<') || Delim(values[i - 1], '>')) && Delim(values[i + 1], '=');
            if (!Token(value, CssTokenKind.Whitespace) || comparisonWhitespace) result.Add(value);
        }
        return result.ToArray();
    }
    private sealed class MediaSyntaxException : Exception;
}
