using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Syntax;

// CSS Syntax §4.3 and §5.4: EOF implicitly terminates lexical constructs and open containers.
// Retained lexical values need those explicit terminators before embedding in declaration text.
internal static class CssValueTermination
{
    internal static string Create(CssComponentValueList components, CssSourceSpan retainedSpan,
        int originalSourceLength, string lexicalSuffix, CssValueWork work)
    {
        work.CheckCancellation();
        if ((long) retainedSpan.Start + retainedSpan.Length != originalSourceLength) return string.Empty;
        var closers = new ValueStringBuilder(stackalloc char[16]);
        try
        {
            var values = components;
            while (values.Count != 0)
            {
                work.Charge(1);
                var last = values[values.Count - 1];
                if (last.Kind == CssComponentKind.Token || last.IsClosed) break;
                closers.Append(last.Kind == CssComponentKind.Function ? ')' : last.OpeningDelimiter switch
                { '(' => ')', '[' => ']', '{' => '}', _ => throw new InvalidOperationException() });
                values = last.Values;
            }
            // Innermost containers close first.
            var closing = closers.RawChars[..closers.Length];
            closing.Reverse();
            work.Charge(closing.Length);
            work.CheckCancellation();
            var result = closing.IsEmpty ? lexicalSuffix : string.Concat(lexicalSuffix, closing);
            work.Charge(result.Length);
            work.CheckCancellation();
            return result;
        }
        finally
        {
            closers.Dispose();
        }
    }
}
