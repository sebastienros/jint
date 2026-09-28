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
        var keywords = grammar switch
        {
            CssPropertyGrammar.FlexBasis => CssKeywordSet.AutoContentMinContentMaxContentEtc,
            CssPropertyGrammar.MaxSizing => CssKeywordSet.NoneMinContentMaxContentFitContentEtc,
            _ => CssKeywordSet.AutoMinContentMaxContentFitContentEtc
        };
        if (CssPropertyParser.Keyword(part, keywords, work) is { } keyword)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span));
        // Sizing 4 §3.2 extends <box-size>; implementation remains a named obligation.
        if (CssPropertyParser.Keyword(part, CssKeywordSet.Contain, work) is not null)
            return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "sizing:contain");
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
            if (CssAnchorSizeCalcSizeNames.Match(name))
                return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "sizing:" + name);
        }
        return Numeric(part, false, maximumDepth, work);
    }

    internal static CssPropertyResult Numeric(CssComponentValue part, bool numberOnly,
        int maximumDepth, CssValueWork work, int ancestorDepth = 0, bool nonnegative = true, bool allowPercentage = true)
    {
        work.CheckCancellation();
        if (part.Kind == CssComponentKind.Token &&
            part.Token.Kind is CssTokenKind.Number or CssTokenKind.Dimension or CssTokenKind.Percentage)
        {
            var token = part.Token;
            if (!allowPercentage && token.Kind == CssTokenKind.Percentage) return Invalid();
            var unit = token.Kind == CssTokenKind.Dimension ? CssUnits.Recognize(token.Unit, work) : CssUnit.None;
            var number = CssNumber.FromValidatedToken(token.NumberText, work);
            // Validate exact lexical sign before finite conversion (including tiny negatives).
            if (nonnegative && number.Sign < 0) return Invalid();
            if (numberOnly ? token.Kind != CssTokenKind.Number :
                token.Kind == CssTokenKind.Number ? number.Sign != 0 :
                token.Kind == CssTokenKind.Dimension && unit.Category() != CssUnitCategory.Length) return Invalid();
            var kind = token.Kind switch
            {
                CssTokenKind.Number => CssNumericKind.Number,
                CssTokenKind.Percentage => CssNumericKind.Percentage,
                _ => CssNumericKind.Dimension
            };
            // Values 4 §6.1: unitless zero in a length production is a length, also in the typed
            // atom consumed by computation. Its canonical specified spelling is already 0px.
            if (!numberOnly && kind == CssNumericKind.Number)
            {
                kind = CssNumericKind.Dimension;
                unit = CssUnit.Px;
            }
            var atom = new CssNumericAtom(kind, number, unit, token.IsInteger, part.Span);
            var finite = CssMathNumbers.ParseFinite(number, unit, work);
            var text = SerializeNumber(finite, work);
            if (!numberOnly)
                text += kind == CssNumericKind.Percentage ? "%" :
                    kind == CssNumericKind.Number ? "px" : CssMathNumbers.CanonicalUnit(unit).ToString().ToLowerInvariant();
            work.Charge(text.Length);
            work.CheckCancellation();
            return CssPropertyResult.Accepted(CssPropertyValue.Number(atom, text));
        }
        var context = new CssMathContext(numberOnly ? CssMathProduction.Number :
                allowPercentage ? CssMathProduction.LengthPercentage : CssMathProduction.Length,
            numberOnly || !allowPercentage ? CssMathPercentageMode.Forbidden : CssMathPercentageMode.Length,
            nonnegative ? new CssMathRange(lower: 0) : default, maximumDepth, ancestorDepth);
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

    // Use the same fixed-point precision as CssMathSerializer. Custom numeric formats
    // round large integral coordinates to 15 significant digits and lose represented digits.
    private static string SerializeNumber(double value, CssValueWork work)
    {
        if (value == 0) value = 0;
        Span<char> scratch = stackalloc char[384];
        work.CheckCancellation();
        if (!value.TryFormat(scratch, out var length, "F6", CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Finite CSS number exceeded the formatter bound.");
        work.CheckCancellation();
        while (length > 0 && scratch[length - 1] == '0' && scratch[..length].IndexOf('.') >= 0) length--;
        if (length > 0 && scratch[length - 1] == '.') length--;
        var text = scratch[..length].ToString();
        work.Charge(length);
        work.CheckCancellation();
        return text;
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
