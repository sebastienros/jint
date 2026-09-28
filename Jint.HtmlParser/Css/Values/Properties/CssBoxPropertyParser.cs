using Jint.HtmlParser.Css.Values.References;

namespace Jint.HtmlParser.Css.Values.Properties;

// https://drafts.csswg.org/css-box-4/#margins and #paddings: physical sides, in TRBL order.
internal static class CssBoxPropertyParser
{
    internal static CssPropertyResult Parse(CssPropertyGrammar grammar, List<CssComponentValue> parts,
        int maximumDepth, CssValueWork work)
    {
        var shorthand = grammar is CssPropertyGrammar.Margin or CssPropertyGrammar.Padding;
        var margin = grammar is CssPropertyGrammar.Margin or CssPropertyGrammar.MarginSide;
        if (parts.Count < 1 || parts.Count > (shorthand ? 4 : 1))
            return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        var values = new CssPropertyValue[parts.Count];
        for (var i = 0; i < parts.Count; i++)
        {
            work.Charge(1);
            var part = parts[i];
            if (margin && CssPropertyParser.Keyword(part, CssKeywordSet.Auto, work) is { } keyword)
                values[i] = CssPropertyValue.Keyword(keyword, part.Span);
            else
            {
                // Anchor Positioning extends margin's grammar; never accept unimplemented functions
                // as raw text. Padding continues to use only the <length-percentage> production.
                if (margin && part.Kind == CssComponentKind.Function &&
                    CssPropertyRegistry.NormalizeName(part.FunctionName, work) == "anchor-size")
                    return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "box:anchor-size");
                var parsed = CssSizingPropertyParser.Numeric(part, false, maximumDepth, work, nonnegative: !margin);
                if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                values[i] = parsed.Value;
            }
        }
        if (!shorthand) return CssPropertyResult.Accepted(values[0]);
        var top = values[0];
        var right = values.Length > 1 ? values[1] : top;
        var bottom = values.Length > 2 ? values[2] : top;
        var left = values.Length > 3 ? values[3] : right;
        var text = Serialize(top.Serialize(), right.Serialize(), bottom.Serialize(), left.Serialize(), work);
        work.CheckCancellation();
        return CssPropertyResult.Accepted(CssPropertyValue.Shorthand(text, parts[0].Span, top, right, bottom, left));
    }

    internal static string Serialize(string top, string right, string bottom, string left, CssValueWork work)
    {
        work.CheckCancellation();
        var count = 4;
        if (CssSubstitutionArguments.Equals(left, right, work))
        {
            count = 3;
            if (CssSubstitutionArguments.Equals(bottom, top, work))
            {
                count = 2;
                if (CssSubstitutionArguments.Equals(right, top, work)) count = 1;
            }
        }
        var text = count switch
        {
            1 => top,
            2 => top + " " + right,
            3 => top + " " + right + " " + bottom,
            _ => top + " " + right + " " + bottom + " " + left
        };
        work.Charge(text.Length);
        work.CheckCancellation();
        return text;
    }
}
