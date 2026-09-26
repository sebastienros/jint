using System.Globalization;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.HtmlParser.Css.Values.Properties;

// Sizing 3 §3.1/3.2 and Flexbox 1 §7.2.3, drafts checked 2026-09-25.
internal static class CssSizingPropertyParser
{
    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts,
        int maximumDepth, CssValueWork work)
    {
        if (parts.Count != 1) return Invalid();
        var part = parts[0];
        var keywords = grammar == CssPropertyGrammar.FlexBasis
            ? "auto content min-content max-content fit-content stretch" : "auto min-content max-content fit-content stretch";
        if (CssPropertyParser.Keyword(part, keywords, work) is { } keyword)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span));
        if (part.Kind == CssComponentKind.Function)
        {
            var name = CssPropertyRegistry.NormalizeName(part.FunctionName, work);
            if (name == "fit-content")
            {
                var children = CssPropertyParser.Significant(part.Values, work);
                if (children.Count != 1) return Invalid();
                var argument = Numeric(children[0], false, maximumDepth, work, 1);
                return argument.Status == CssPropertyStatus.Valid
                    ? CssPropertyResult.Accepted(CssPropertyValue.FitContent(argument.Value, part.Span)) : argument;
            }
            // Anchor Positioning and Sizing 4 extend the sizing grammar. They are named debt,
            // not arbitrary functions accepted as raw text or reported as invalid CSS.
            if (name is "anchor-size" or "calc-size")
                return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "sizing:" + name);
        }
        return Numeric(part, false, maximumDepth, work);
    }

    internal static CssPropertyResult Numeric(CssComponentValue part, bool numberOnly,
        int maximumDepth, CssValueWork work, int ancestorDepth = 0)
    {
        work.CheckCancellation();
        if (part.Kind == CssComponentKind.Token &&
            part.Token.Kind is CssTokenKind.Number or CssTokenKind.Dimension or CssTokenKind.Percentage)
        {
            var token = part.Token;
            var unit = token.Kind == CssTokenKind.Dimension ? CssUnits.Recognize(token.Unit, work) : CssUnit.None;
            var number = CssNumber.FromValidatedToken(token.NumberText, work);
            // Validate exact lexical sign before finite conversion (including tiny negatives).
            if (number.Sign < 0) return Invalid();
            if (numberOnly ? token.Kind != CssTokenKind.Number :
                token.Kind == CssTokenKind.Number ? number.Sign != 0 :
                token.Kind == CssTokenKind.Dimension && unit.Category() != CssUnitCategory.Length) return Invalid();
            var kind = token.Kind switch
            {
                CssTokenKind.Number => CssNumericKind.Number,
                CssTokenKind.Percentage => CssNumericKind.Percentage,
                _ => CssNumericKind.Dimension
            };
            var atom = new CssNumericAtom(kind, number, unit, token.IsInteger, part.Span);
            var finite = CssMathNumbers.ParseFinite(number, unit, work);
            var text = finite.ToString("0.######", CultureInfo.InvariantCulture);
            if (!numberOnly)
                text += kind == CssNumericKind.Percentage ? "%" :
                    kind == CssNumericKind.Number ? "px" : CssMathNumbers.CanonicalUnit(unit).ToString().ToLowerInvariant();
            work.Charge(text.Length);
            work.CheckCancellation();
            return CssPropertyResult.Accepted(CssPropertyValue.Number(atom, text));
        }
        var context = new CssMathContext(numberOnly ? CssMathProduction.Number : CssMathProduction.LengthPercentage,
            numberOnly ? CssMathPercentageMode.Forbidden : CssMathPercentageMode.Length,
            new CssMathRange(lower: 0), maximumDepth, ancestorDepth);
        var math = CssMathParser.ParseMath(part, context, work);
        return math.Status switch
        {
            CssMathParseStatus.Match => CssPropertyResult.Accepted(CssPropertyValue.Calculation(math.Value,
                CssMathSerializer.SerializeSpecified(math.Value, work))),
            CssMathParseStatus.RequiresLaterGrammar => CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar,
                "math:" + math.PendingFunction),
            _ => Invalid()
        };
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
