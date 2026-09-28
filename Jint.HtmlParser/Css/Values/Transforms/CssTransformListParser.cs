using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values.Transforms;

// https://drafts.csswg.org/css-transforms-2/#transform-functions
internal static class CssTransformListParser
{
    internal static CssPropertyResult Parse(List<CssComponentValue> parts, int maximumDepth, CssValueWork work)
    {
        work.CheckCancellation();
        if (parts.Count == 1 && CssPropertyParser.Keyword(parts[0], CssKeywordSet.None, work) is not null)
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword("none", parts[0].Span));
        if (parts.Count == 0) return Invalid();
        var functions = new List<CssTransformFunction>();
        foreach (var part in parts)
        {
            work.Charge(1);
            if (part.Kind != CssComponentKind.Function) return Invalid();
            var descriptor = CssTransformFunctionDescriptor.Find(CssPropertyRegistry.NormalizeName(part.FunctionName, work));
            if (descriptor is not { } function) return Invalid();
            var children = CssPropertyParser.Significant(part.Values, work);
            // Every multi-argument function uses commas, never space-separated arguments.
            if (children.Count == 0 || children.Count % 2 == 0) return Invalid();
            var count = (children.Count + 1) / 2;
            if (count < function.MinimumArguments || count > function.MaximumArguments) return Invalid();
            var arguments = new CssPropertyValue[count];
            for (var i = 0; i < count; i++)
            {
                work.Charge(1);
                if (i > 0 && (children[i * 2 - 1].Kind != CssComponentKind.Token ||
                    children[i * 2 - 1].Token.Kind != CssTokenKind.Comma)) return Invalid();
                var child = children[i * 2];
                if (function.Kind == CssTransformFunctionKind.Perspective &&
                    CssPropertyParser.Keyword(child, CssKeywordSet.None, work) is not null)
                {
                    arguments[i] = CssPropertyValue.Keyword("none", child.Span);
                    continue;
                }
                var parsed = CssTransformParser.Numeric(child, function.Production(i), maximumDepth, work,
                    functionAngle: true, ancestorDepth: 1,
                    range: function.Kind == CssTransformFunctionKind.Perspective ? new CssMathRange(lower: 0) : default);
                if (parsed.Status != CssPropertyStatus.Valid) return parsed;
                if (function.Kind == CssTransformFunctionKind.Perspective)
                {
                    // Literal negatives are invalid, even when finite conversion would round to zero.
                    if (parsed.Value.Kind == CssPropertyValueKind.Numeric && parsed.Value.Numeric.Number.Sign < 0)
                        return Invalid();
                }
                arguments[i] = parsed.Value;
            }
            functions.Add(new(function, arguments, part.Span));
        }
        var first = parts[0].Span;
        var last = parts[^1].Span;
        var list = new CssTransformList(functions, new(first.Start, last.Start + last.Length - first.Start), work);
        return CssPropertyResult.Accepted(CssPropertyValue.TransformListValue(list, CssTransformListSerializer.Serialize(list, work)));
    }

    private static CssPropertyResult Invalid() => CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
}
