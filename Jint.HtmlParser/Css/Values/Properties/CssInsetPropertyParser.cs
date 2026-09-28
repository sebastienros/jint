namespace Jint.HtmlParser.Css.Values.Properties;

// Positioned Layout 3 §3.1; Anchor Positioning 1 §3.2.
internal static class CssInsetPropertyParser
{
    internal static CssPropertyResult Parse(List<CssComponentValue> parts, int maximumDepth, CssValueWork work)
    {
        if (parts.Count != 1) return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
        var part = parts[0];
        work.Charge(4);
        if (CssPropertyParser.Keyword(part, CssKeywordSet.Auto, work) is { } keyword)
        {
            work.CheckCancellation();
            return CssPropertyResult.Accepted(CssPropertyValue.Keyword(keyword, part.Span));
        }
        if (part.Kind == CssComponentKind.Token)
            return CssSizingPropertyParser.Numeric(part, false, maximumDepth, work, nonnegative: false);
        // Anchor functions extend the numeric production, including within calc(). Classify
        // them iteratively before numeric parsing, rather than silently reporting invalid CSS.
        var stack = new Stack<CssComponentValue>();
        stack.Push(part);
        while (stack.TryPop(out var component))
        {
            work.Charge(1);
            switch (component.Kind)
            {
                case not (CssComponentKind.Function or CssComponentKind.SimpleBlock):
                    continue;
                case CssComponentKind.Function:
                    {
                        var name = CssPropertyRegistry.NormalizeName(component.FunctionName, work);
                        work.Charge("anchor anchor-size".Length);
                        if (CssAnchorAnchorSizeNames.Match(name))
                        {
                            work.CheckCancellation();
                            return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, "inset:" + name);
                        }
                    }
                    break;
            }
            var children = component.Values;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                work.Charge(1);
                stack.Push(children[i]);
            }
        }
        return CssSizingPropertyParser.Numeric(part, false, maximumDepth, work, nonnegative: false);
    }
}
