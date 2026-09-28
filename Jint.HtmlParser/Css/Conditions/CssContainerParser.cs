using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Conditions;

// Conditional 5 §5.4. Grammar and explicit dependencies survive projection unchanged.
internal static class CssContainerParser
{
    internal static CssContainerRule? Parse(string source, CssComponentValueList prelude, CssSourceSpan span, CssSyntaxParser parser, CssValueWork work)
    {
        var pending = new Stack<CssComponentValueList>();
        pending.Push(prelude);
        while (pending.TryPop(out var values))
            foreach (var value in values)
            {
                work.Charge(1);
                if (value.Kind is CssComponentKind.Function or CssComponentKind.SimpleBlock) pending.Push(value.Values);
                else if (value.Token.Kind is CssTokenKind.BadString or CssTokenKind.BadUrl or
                    CssTokenKind.CloseParenthesis or CssTokenKind.CloseSquareBracket or CssTokenKind.CloseCurlyBracket) return null;
            }
        var parts = CssPropertyParser.Significant(prelude, work);
        if (parts.Count == 0) return null;
        foreach (var part in parts)
        {
            work.Charge(1);
            if (Token(part, CssTokenKind.Comma)) return ConditionList(source, prelude, span, parser, work);
        }
        var name = "";
        if (CssContainerPropertyParser.IsName(parts[0], work))
        {
            name = parts[0].Token.Text;
            parts.RemoveAt(0);
        }
        var instructions = new List<CssContainerInstruction>();
        if (parts.Count != 0 && !TryParseCondition(parts, instructions, work)) return null;
        if (parts.Count == 0 && name.Length == 0) return null;
        var query = parts.Count == 0 ? "" : CssStyleSheet.SelectorText(source, new CssComponentValueList(parts.ToArray()), parser, work);
        var text = name.Length == 0 ? query : CssSyntaxSerializer.SerializeIdentifier(name, work) + (query.Length == 0 ? "" : " " + query);
        work.CheckCancellation();
        return new(name, query, text, new(instructions.ToArray()), span);
    }

    // Conditional 5 §5.4's condition list is preserved, never silently treated as an invalid rule.
    // Each branch remains typed; evaluation is outside this finite single-container slice.
    private static CssContainerRule? ConditionList(string source, CssComponentValueList prelude,
        CssSourceSpan span, CssSyntaxParser parser, CssValueWork work)
    {
        var branches = new List<CssContainerQuery>();
        var texts = new List<string>();
        var values = new List<CssComponentValue>();
        foreach (var value in prelude)
        {
            work.Charge(1);
            if (!Token(value, CssTokenKind.Comma)) { values.Add(value); continue; }
            if (!Branch()) return null;
        }
        if (!Branch()) return null;
        var text = string.Join(", ", texts);
        work.Charge(text.Length);
        work.CheckCancellation();
        return new("", "", text, new([], "C6:container-condition-list"), span, branches.ToArray());

        bool Branch()
        {
            work.Charge(values.Count);
            var branch = Parse(source, new CssComponentValueList(values.ToArray()), span, parser, work);
            values.Clear();
            if (branch is null) return false;
            branches.Add(new(branch.ContainerName, branch.ContainerQuery, branch.Condition));
            texts.Add(branch.ConditionText);
            return true;
        }
    }

    private sealed record Task(List<CssComponentValue>? Parts = null, CssComponentValue? Operand = null,
        CssMediaOperation? Operation = null);

    internal static bool TryParseCondition(List<CssComponentValue> parts, List<CssContainerInstruction> program, CssValueWork work)
    {
        var pending = new Stack<Task>();
        pending.Push(new(Parts: parts));
        while (pending.TryPop(out var task))
        {
            work.Charge(1);
            if (task.Operation is { } operation) { program.Add(new(operation)); continue; }
            if (task.Operand is { } operand)
            {
                if (operand.Kind == CssComponentKind.SimpleBlock && operand.OpeningDelimiter == '(')
                {
                    var values = CssPropertyParser.Significant(operand.Values, work);
                    if (values.Count == 0) return false;
                    if (values[0].Kind is CssComponentKind.SimpleBlock or CssComponentKind.Function || Ident(values[0], "not"))
                    { pending.Push(new(Parts: values)); continue; }
                }
                if (!Operand(operand, program, work)) return false;
                continue;
            }
            var items = task.Parts!;
            if (items.Count == 2 && Ident(items[0], "not"))
            {
                pending.Push(new(Operation: CssMediaOperation.Not));
                pending.Push(new(Operand: items[1]));
                continue;
            }
            if (items.Count == 0 || (items.Count & 1) == 0) return false;
            CssMediaOperation? join = null;
            for (var i = 1; i < items.Count; i += 2)
            {
                work.Charge(1);
                var next = Ident(items[i], "and") ? CssMediaOperation.And
                    : Ident(items[i], "or") ? CssMediaOperation.Or : (CssMediaOperation?) null;
                if (next is null || join is not null && join != next) return false;
                join = next;
            }
            for (var i = items.Count - 1; i >= 0; i -= 2)
            {
                work.Charge(1);
                if (i != 0) pending.Push(new(Operation: join!.Value));
                pending.Push(new(Operand: items[i]));
            }
        }
        return true;
    }

    private static bool Operand(CssComponentValue operand, List<CssContainerInstruction> program, CssValueWork work)
    {
        work.Charge(1);
        switch (operand.Kind)
        {
            case CssComponentKind.Function:
                {
                    var functionAxis = CssContainerFunctionLookup.Match(operand.FunctionName);
                    program.Add(functionAxis == CssContainerAxis.Unknown ? new(CssMediaOperation.Unknown)
                        : new(CssMediaOperation.Feature, new(functionAxis, CssMediaComparison.Boolean, Dependency: "C6:container-" + operand.FunctionName)));
                    return true;
                }
            case CssComponentKind.SimpleBlock when operand.OpeningDelimiter == '(':
                break;
            default:
                return false;
        }
        var parts = CssPropertyParser.Significant(operand.Values, work);
        if (parts.Count == 0) return false;
        if (parts[0].Kind != CssComponentKind.Token || parts[0].Token.Kind != CssTokenKind.Ident)
        {
            program.Add(new(CssMediaOperation.Feature, new(CssContainerAxis.Unknown, CssMediaComparison.Boolean,
                Dependency: "C6:container-range-syntax")));
            return true;
        }
        var name = CssPropertyRegistry.NormalizeName(parts[0].Token.Text, work);
        var comparison = CssMediaComparison.Equal;
        if (name.StartsWith("min-", StringComparison.Ordinal)) { name = name[4..]; comparison = CssMediaComparison.GreaterEqual; }
        else if (name.StartsWith("max-", StringComparison.Ordinal)) { name = name[4..]; comparison = CssMediaComparison.LessEqual; }
        var axis = CssContainerAxisLookup.Match(name);
        if (axis == CssContainerAxis.Unknown) { program.Add(new(CssMediaOperation.Unknown)); return true; }
        var pixels = 0d;
        string? dependency = null;
        if (parts.Count == 1)
        {
            // MQ5 §2.4.4: a min-/max- prefixed feature cannot be used in boolean context.
            // Preserve it as general-enclosed unknown, including under not and or.
            if (comparison != CssMediaComparison.Equal) { program.Add(new(CssMediaOperation.Unknown)); return true; }
            comparison = CssMediaComparison.Boolean;
        }
        else if (parts.Count == 3 && Token(parts[1], CssTokenKind.Colon) &&
            parts[2].Kind == CssComponentKind.Token && parts[2].Token.Kind is CssTokenKind.Dimension or CssTokenKind.Number)
        {
            var token = parts[2].Token;
            var number = CssNumber.FromValidatedToken(token.NumberText, work);
            if (token.Kind == CssTokenKind.Number && number.Sign != 0) return false;
            var unit = token.Kind == CssTokenKind.Number ? CssUnit.Px : CssUnits.Recognize(token.Unit, work);
            if (unit is >= CssUnit.Px and <= CssUnit.Pc) pixels = CssMathNumbers.ParseFinite(number, unit, work);
            else dependency = "C6:container-length-unit:" + token.Unit;
        }
        else dependency = "C6:container-feature-syntax:" + name;
        if (axis is CssContainerAxis.Height or CssContainerAxis.BlockSize or CssContainerAxis.Both)
            dependency ??= "C6:container-metric:" + name;
        program.Add(new(CssMediaOperation.Feature, new(axis, comparison, pixels, dependency)));
        return true;
    }

    private static bool Token(CssComponentValue part, CssTokenKind kind) => part.Kind == CssComponentKind.Token && part.Token.Kind == kind;
    private static bool Ident(CssComponentValue part, string name) => Token(part, CssTokenKind.Ident) && CssAscii.EqualsIgnoreCase(part.Token.Text, name);
}
