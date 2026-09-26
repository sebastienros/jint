using System.Text;
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
        var closers = new List<char>();
        var values = components;
        while (values.Count != 0)
        {
            work.Charge(1);
            var last = values[values.Count - 1];
            if (last.Kind == CssComponentKind.Token || last.IsClosed) break;
            closers.Add(last.Kind == CssComponentKind.Function ? ')' : last.OpeningDelimiter switch
            { '(' => ')', '[' => ']', '{' => '}', _ => throw new InvalidOperationException() });
            values = last.Values;
        }
        var builder = new StringBuilder(lexicalSuffix);
        for (var i = closers.Count - 1; i >= 0; i--) { work.Charge(1); builder.Append(closers[i]); }
        work.CheckCancellation();
        var result = builder.ToString();
        work.Charge(result.Length);
        work.CheckCancellation();
        return result;
    }
}
