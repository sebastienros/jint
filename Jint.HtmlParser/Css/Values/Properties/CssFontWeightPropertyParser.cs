using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Fonts 4 §2.2: ordinary/keyframe values, not font-face weight ranges.
internal static class CssFontWeightPropertyParser
{
    private static readonly CssNumericRange Range = new(
        CssNumber.FromValidatedToken("1", new CssValueWork(default)), true,
        CssNumber.FromValidatedToken("1000", new CssValueWork(default)), true);

    internal static CssPropertyResult Parse(CssReferenceInput input, List<CssComponentValue> parts, CssValueWork work)
    {
        if (parts.Count != 1) return Invalid();
        return ParseComponent(parts[0], input.MaxNestingDepth, work);
    }

    internal static CssPropertyResult ParseComponent(CssComponentValue part, int maximumDepth, CssValueWork work)
    {
        if (CssPropertyParser.Keyword(part, CssKeywordSet.NormalBoldBolderLighter, work) is { } keyword)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span));
        var atom = CssPrimitiveParser.ParseNumericAtom(new CssComponentValueList([part]), work);
        if (atom.IsMatch)
        {
            if (atom.Value.Kind != CssNumericKind.Number || !Range.Contains(atom.Value.Number, work)) return Invalid();
            var number = CssMathNumbers.ParseFinite(atom.Value.Number, CssUnit.None, work);
            return CssPropertyResult.Accepted(CssPropertyValue.Number(atom.Value,
                CssMathSerializer.SerializeFiniteNumber(number, work)));
        }
        var context = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Forbidden,
            new CssMathRange(1, 1000), maximumDepth);
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
