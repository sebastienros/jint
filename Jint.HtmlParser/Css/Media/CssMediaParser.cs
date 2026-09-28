using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Values;
using System.Globalization;
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
                if (CssNotOnlyNames.Match(first))
                {
                    negate = first == "not";
                    modifier = first + " ";
                    start++;
                }
                if (start == values.Length || Ident(values[start], work) is not { } mediaType ||
                    CssNotOnlyAndNames.Match(mediaType)) throw new MediaSyntaxException();
                type = mediaType;
                start++;
                if (start < values.Length)
                {
                    if (Ident(values[start++], work) != "and" || start == values.Length) throw new MediaSyntaxException();
                }
            }
            var builder = new ValueStringBuilder(stackalloc char[128]);
            string text;
            var program = new List<CssMediaInstruction>();
            try
            {
                builder.Append(modifier);
                if (type is not null) builder.Append(CssSyntaxSerializer.SerializeIdentifier(type, work));
                if (start < values.Length)
                {
                    if (type is not null) builder.Append(" and ");
                    Condition(values[start..], type is null, source, parser, ref builder, program, work);
                }
                else if (type is null) throw new MediaSyntaxException();
                work.CheckCancellation();
                text = builder.ToString();
            }
            finally
            {
                builder.Dispose();
            }
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

    private static void Condition(CssComponentValue[] values, bool allowOr, string source, CssSyntaxParser parser, ref ValueStringBuilder builder,
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
                        AppendSource(ref builder, source, value.Span, work);
                        builder.Append(parser.ValueTermination(new CssComponentValueList([value]), value.Span, work));
                        continue;
                    }
                    var inside = CssFeatureRange.WithoutWhitespace(value.Values, work);
                    tasks.Push(new Task(Recovery: value, BuilderStart: builder.Length, ProgramStart: program.Count));
                    builder.Append('(');
                    tasks.Push(new Task(Text: ")"));
                    if (Feature(inside, source, parser, program, ref builder, work)) continue;
                    if (inside.Length == 0) throw new MediaSyntaxException();
                    if (Ident(inside[0], work) is { } name && name != "not")
                    {
                        program.Add(new CssMediaInstruction(CssMediaOperation.Unknown));
                        AppendSource(ref builder, source, Span(inside), work);
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
                    if (!CssAndOrNames.Match(op) || (!task.AllowOr && op == "or") ||
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
                AppendSource(ref builder, source, value.Span, work);
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
        ref ValueStringBuilder builder, CssValueWork work)
    {
        if (items.Length == 0) return false;
        // Boolean and colon syntax. Range syntax is handled separately below.
        if (Ident(items[0], work) is { } name && (items.Length == 1 || Token(items[1], CssTokenKind.Colon)))
        {
            var comparison = CssMediaComparison.Boolean;
            var baseName = name;
            if (name.StartsWith("min-", StringComparison.Ordinal)) { baseName = name[4..]; comparison = CssMediaComparison.GreaterEqual; }
            if (name.StartsWith("max-", StringComparison.Ordinal)) { baseName = name[4..]; comparison = CssMediaComparison.LessEqual; }
            var discrete = CssMediaFeatureKeywordLookup.Match(baseName);
            if (comparison != CssMediaComparison.Boolean && (items.Length == 1 || discrete != CssKeywordSet.Empty || baseName == "grid"))
                return Unknown(program, ref builder, source, parser, items, work);
            if (items.Length > 1 && comparison == CssMediaComparison.Boolean) comparison = CssMediaComparison.Equal;
            var value = items.Length == 1 ? [] : items[2..];
            var feature = Validate(baseName, comparison, value, work);
            if (feature is null) return Unknown(program, ref builder, source, parser, items, work);
            program.Add(new CssMediaInstruction(CssMediaOperation.Feature, feature));
            builder.Append(CssSyntaxSerializer.SerializeIdentifier(name, work));
            if (value.Length != 0)
            {
                builder.Append(": ");
                builder.Append(ValueText(feature));
            }
            return true;
        }
        if (CssFeatureRange.Parse(items, work) is not { } range) return false;
        var rangeName = range.Name;
        if (rangeName == "grid" || CssMediaFeatureKeywordLookup.Match(rangeName) != CssKeywordSet.Empty || rangeName.StartsWith("min-", StringComparison.Ordinal) ||
            rangeName.StartsWith("max-", StringComparison.Ordinal)) return Unknown(program, ref builder, source, parser, items, work);
        var feature1 = Validate(rangeName, range.Comparison, range.FirstValue, work);
        if (feature1 is null) return Unknown(program, ref builder, source, parser, items, work);
        CssMediaFeature? feature2 = null;
        if (range.SecondComparison is { } secondComparison)
        {
            feature2 = Validate(rangeName, secondComparison, range.SecondValue, work);
            if (feature2 is null) return Unknown(program, ref builder, source, parser, items, work);
        }
        program.Add(new CssMediaInstruction(CssMediaOperation.Feature, feature1));
        builder.Append(range.Reversed ? ValueText(feature1) : rangeName);
        builder.Append(Operator(range.FirstComparison));
        builder.Append(range.Reversed ? rangeName : ValueText(feature1));
        if (feature2 is not null)
        {
            program.Add(new CssMediaInstruction(CssMediaOperation.Feature, feature2));
            program.Add(new CssMediaInstruction(CssMediaOperation.And));
            builder.Append(Operator(range.SecondComparison!.Value));
            builder.Append(ValueText(feature2));
        }
        return true;
    }

    private static CssMediaFeature? Validate(string name, CssMediaComparison comparison,
        CssComponentValue[] value, CssValueWork work)
    {
        var keywords = CssMediaFeatureKeywordLookup.Match(name);
        if (keywords != CssKeywordSet.Empty)
        {
            if (comparison == CssMediaComparison.Boolean) return new(name, comparison, 0, CssUnit.None, "");
            if (value.Length != 1 || Ident(value[0], work) is not { } keyword || CssKeywordLookup.Match(keyword, keywords) is null) return null;
            return new(name, comparison, 0, CssUnit.None, keyword);
        }
        if (!CssWidthHeightAspectRatioNames.Match(name))
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
            var numerator = ParseNumber(a, work);
            var denominator = ParseNumber(b, work);
            var spelling = SerializeNumber(numerator, work) + " / " + SerializeNumber(denominator, work);
            return new(name, comparison, numerator / denominator, CssUnit.None, null, spelling);
        }
        if (value.Length != 1 || value[0].Kind != CssComponentKind.Token) return null;
        var token = value[0].Token;
        if (token.Kind is not (CssTokenKind.Number or CssTokenKind.Dimension)) return null;
        var number = CssNumber.FromValidatedToken(token.NumberText, work);
        if (CssWidthHeightNames.Match(name))
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
        var projected = ParseNumber(number, work);
        var spelling = SerializeNumber(projected, work) +
            (unit == CssUnit.None ? "" : unit.ToString().ToLowerInvariant());
        return new(name, comparison, projected, unit, null, spelling);
    }

    private static double ParseNumber(CssNumber number, CssValueWork work)
    {
        work.Charge(number.Spelling.Length);
        var value = double.Parse(number.Spelling, NumberStyles.Float, CultureInfo.InvariantCulture);
        work.CheckCancellation();
        return System.Math.Clamp(value, -double.MaxValue, double.MaxValue);
    }

    private static string SerializeNumber(double value, CssValueWork work)
    {
        var text = value.ToString("F6", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        work.Charge(text.Length);
        return text == "-0" ? "0" : text;
    }

    private static bool KnownPending(string name) => name.StartsWith("--", StringComparison.Ordinal) || CssDeviceWidthDeviceHeightDeviceAspectRatioNames.Match(name);

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
    private static bool Unknown(List<CssMediaInstruction> program, ref ValueStringBuilder builder,
        string source, CssSyntaxParser parser, CssComponentValue[] items, CssValueWork work)
    {
        var span = Span(items);
        program.Add(new CssMediaInstruction(CssMediaOperation.Unknown));
        AppendSource(ref builder, source, span, work);
        builder.Append(parser.ValueTermination(new CssComponentValueList(items), span, work));
        return true;
    }
    private static CssSourceSpan Span(CssComponentValue[] values) => new(values[0].Span.Start,
        values[^1].Span.Start + values[^1].Span.Length - values[0].Span.Start);
    private static void AppendSource(ref ValueStringBuilder builder, string source, CssSourceSpan span, CssValueWork work)
    {
        work.Charge(span.Length);
        work.CheckCancellation();
        builder.Append(source.AsSpan(span.Start, span.Length));
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
    private sealed class MediaSyntaxException : Exception;
}
