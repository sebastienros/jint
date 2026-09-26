using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// A conservative lexical hint only: the real sheet parser decides validity and placement.
internal static class CssImportPrelude
{
    internal static bool MayContainImport(string source, CssParseOptions? options, CssValueWork work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        work.CheckCancellation();
        cancellationToken.ThrowIfCancellationRequested();
        var limits = options?.Limits ?? ParseLimits.Unbounded;
        if (limits.MaxInputCharacters > 0 && source.Length > limits.MaxInputCharacters)
            throw new ParseLimitException(ParseLimitKind.InputCharacters, limits.MaxInputCharacters, source.Length);
        var tokenizer = new CssTokenizer(source, limits.MaxTokenCharacters, options?.Diagnostics,
            cancellationToken, checkpoint: work.CheckCancellation);
        var closers = new Stack<CssTokenKind>();
        var charged = 0;
        while (true)
        {
            var token = tokenizer.Next();
            var end = token.Kind == CssTokenKind.None ? source.Length : token.Span.Start + token.Span.Length;
            work.Charge(end - charged);
            work.Charge(1);
            charged = end;
            if (token.Kind == CssTokenKind.None) { work.CheckCancellation(); return false; }
            if (closers.Count == 0 && token.Kind == CssTokenKind.AtKeyword && CssAscii.EqualsIgnoreCase(token.Text, "import"))
            { work.CheckCancellation(); return true; }
            var closer = token.Kind switch
            {
                CssTokenKind.Function or CssTokenKind.OpenParenthesis => CssTokenKind.CloseParenthesis,
                CssTokenKind.OpenSquareBracket => CssTokenKind.CloseSquareBracket,
                CssTokenKind.OpenCurlyBracket => CssTokenKind.CloseCurlyBracket,
                _ => CssTokenKind.None
            };
            if (closer != CssTokenKind.None)
            {
                if (limits.MaxNestingDepth > 0 && closers.Count >= limits.MaxNestingDepth)
                    throw new ParseLimitException(ParseLimitKind.NestingDepth, limits.MaxNestingDepth, (long) closers.Count + 1);
                closers.Push(closer);
            }
            else if (closers.TryPeek(out var expected) && token.Kind == expected) closers.Pop();
        }
    }
}
