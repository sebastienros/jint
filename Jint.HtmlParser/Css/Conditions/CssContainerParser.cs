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
    internal static CssContainerRule? Parse(CssComponentValueList prelude, CssSourceSpan span, CssValueWork work)
    {
        var parts = CssPropertyParser.Significant(prelude, work);
        if (parts.Count == 0) return null;
        var name = "";
        if (CssContainerPropertyParser.IsName(parts[0], work))
        {
            name = parts[0].Token.Text;
            parts.RemoveAt(0);
        }
        var instructions = new List<CssContainerInstruction>();
        if (parts.Count != 0 && !Condition(parts, instructions, work, 0)) return null;
        if (parts.Count == 0 && name.Length == 0) return null;
        var query = CssSyntaxSerializer.SerializeComponents(new CssComponentValueList(parts.ToArray()), work);
        var text = name.Length == 0 ? query : CssSyntaxSerializer.SerializeIdentifier(name, work) + " " + query;
        work.CheckCancellation();
        return new(name, query, text, new(instructions.ToArray()), span);
    }

    private static bool Condition(List<CssComponentValue> parts, List<CssContainerInstruction> program, CssValueWork work, int depth)
    {
        if (depth > 64) throw new ParseLimitException(ParseLimitKind.NestingDepth, 64, depth);
        work.Charge(1);
        if (parts.Count == 2 && Ident(parts[0], "not"))
        {
            if (!Operand(parts[1], program, work, depth)) return false;
            program.Add(new(CssMediaOperation.Not));
            return true;
        }
        if (parts.Count == 0 || (parts.Count & 1) == 0) return false;
        CssMediaOperation? join = null;
        for (var i = 0; i < parts.Count; i += 2)
        {
            if (i != 0)
            {
                var next = Ident(parts[i - 1], "and") ? CssMediaOperation.And
                    : Ident(parts[i - 1], "or") ? CssMediaOperation.Or : (CssMediaOperation?) null;
                if (next is null || join is not null && join != next) return false;
                join = next;
            }
            if (!Operand(parts[i], program, work, depth)) return false;
            if (i != 0) program.Add(new(join!.Value));
        }
        return true;
    }

    private static bool Operand(CssComponentValue operand, List<CssContainerInstruction> program, CssValueWork work, int depth)
    {
        work.Charge(1);
        if (operand.Kind == CssComponentKind.Function)
        {
            var functionAxis = CssAscii.EqualsIgnoreCase(operand.FunctionName, "style") ? CssContainerAxis.Style
                : CssAscii.EqualsIgnoreCase(operand.FunctionName, "scroll-state") ? CssContainerAxis.ScrollState : CssContainerAxis.Unknown;
            program.Add(functionAxis == CssContainerAxis.Unknown ? new(CssMediaOperation.Unknown)
                : new(CssMediaOperation.Feature, new(functionAxis, CssMediaComparison.Boolean, Dependency: "C6:container-" + operand.FunctionName)));
            return true;
        }
        if (operand.Kind != CssComponentKind.SimpleBlock || operand.OpeningDelimiter != '(') return false;
        var parts = CssPropertyParser.Significant(operand.Values, work);
        if (parts.Count == 0) return false;
        if (parts[0].Kind is CssComponentKind.SimpleBlock or CssComponentKind.Function || Ident(parts[0], "not"))
            return Condition(parts, program, work, depth + 1);
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
        var axis = name switch
        {
            "width" => CssContainerAxis.Width, "inline-size" => CssContainerAxis.InlineSize,
            "height" => CssContainerAxis.Height, "block-size" => CssContainerAxis.BlockSize,
            "aspect-ratio" or "orientation" => CssContainerAxis.Both, _ => CssContainerAxis.Unknown
        };
        if (axis == CssContainerAxis.Unknown) { program.Add(new(CssMediaOperation.Unknown)); return true; }
        var pixels = 0d;
        string? dependency = null;
        if (parts.Count == 1) comparison = CssMediaComparison.Boolean;
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
