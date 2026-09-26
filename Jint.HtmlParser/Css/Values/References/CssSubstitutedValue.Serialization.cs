using System.Text;

namespace Jint.HtmlParser.Css.Values.References;

internal sealed partial class CssSubstitutedValue
{
    // CSS Variables 1 §4.1: retain authored spelling and comments, replacing only selected invocations.
    // CSS Syntax 3 §9: token identities at substitution joins must survive reserialization.
    internal string SerializeCustomProperty(CssValueWork work)
    {
        work.CheckCancellation();
        var text = new StringBuilder(Root.LexicalLength);
        var tasks = new Stack<(CssSegment Segment, bool Close)>();
        tasks.Push((Root, false));
        var boundary = false;
        var boundaryPosition = -1;
        var previous = CssTokenKind.None;
        var previousDelimiter = '\0';
        var previousDashDash = false;
        var previousTerminalHexEscape = false;
        while (tasks.TryPop(out var task))
        {
            work.Charge(1);
            var segment = task.Segment;
            if (task.Close)
            {
                var close = segment.Original.Kind == CssComponentKind.Function ? ')' : segment.Original.OpeningDelimiter switch
                { '(' => ')', '[' => ']', '{' => '}', _ => throw new InvalidOperationException() };
                Write(close.ToString(), close switch
                { ')' => CssTokenKind.CloseParenthesis, ']' => CssTokenKind.CloseSquareBracket, _ => CssTokenKind.CloseCurlyBracket });
                continue;
            }
            if (segment.Kind == CssSegmentKind.Trivia)
            {
                if (segment.IsSubstitutionBoundary)
                {
                    boundary = true;
                    boundaryPosition = text.Length;
                }
                else if (segment.SyntheticLexical is { } recovery)
                {
                    work.Charge(recovery.Length);
                    text.Append(recovery);
                    previousTerminalHexEscape = false;
                }
                else if (segment.LexicalSpan.Length != 0)
                {
                    var gap = segment.Source!.SourceSlice(segment.LexicalSpan);
                    work.Charge(gap.Length);
                    text.Append(gap);
                    previous = CssTokenKind.None; // A captured source gap already separates tokens.
                    boundary = false;
                    boundaryPosition = -1;
                    previousTerminalHexEscape = false;
                }
                continue;
            }
            if (segment.Kind == CssSegmentKind.Token)
            {
                var token = segment.Original.Token;
                Write(segment.Source!.SourceSlice(segment.LexicalSpan), token.Kind, token.Delimiter);
                continue;
            }
            if (segment.Kind == CssSegmentKind.Container)
            {
                var original = segment.Original;
                var first = original.Kind == CssComponentKind.Function ? CssTokenKind.Function : original.OpeningDelimiter switch
                { '(' => CssTokenKind.OpenParenthesis, '[' => CssTokenKind.OpenSquareBracket, _ => CssTokenKind.OpenCurlyBracket };
                Write(segment.Source!.SourceSlice(segment.OpenerSpan), first);
                previous = original.Kind == CssComponentKind.Function ? CssTokenKind.OpenParenthesis : first;
                tasks.Push((segment, true));
            }
            for (var i = segment.Children.Length - 1; i >= 0; i--)
            {
                work.Charge(1);
                tasks.Push((segment.Children[i], false));
            }
        }
        work.CheckCancellation();
        // A valid empty custom property is distinguishable from the guaranteed-invalid value.
        if (text.Length == 0) return " ";
        work.Charge(text.Length);
        var result = text.ToString();
        work.CheckCancellation();
        return result;

        void Write(ReadOnlySpan<char> spelling, CssTokenKind kind, char delimiter = '\0')
        {
            if (spelling.Length == 0) return;
            if (boundary && (NeedsSeparator(previous, previousDelimiter, kind, delimiter) ||
                previousTerminalHexEscape && IsCssWhitespace(spelling[0])) ||
                // CDO/CDC involve three or four characters rather than one adjacent token pair.
                boundaryPosition >= 0 && text.Length - boundaryPosition <= 2 &&
                text.Length >= 2 && text[^2] == '<' && text[^1] == '!' && spelling.StartsWith("--", StringComparison.Ordinal) ||
                boundary && kind == CssTokenKind.Delim && delimiter == '>' && previousDashDash)
            {
                work.Charge(4);
                text.Append("/**/");
                boundaryPosition = -1;
            }
            work.Charge(spelling.Length);
            text.Append(spelling);
            previous = kind;
            previousDelimiter = delimiter;
            previousDashDash = kind == CssTokenKind.Ident && spelling.SequenceEqual("--");
            previousTerminalHexEscape = EndsInHexEscape(spelling, work);
            boundary = false;
        }
    }

    // C1 has already tokenized the fragment. Inspect only its lexical tail: even a six-digit escape
    // consumes one following whitespace code point (CRLF is one after preprocessing). A join comment
    // closes the escape without sacrificing the authored whitespace token on the other side.
    private static bool EndsInHexEscape(ReadOnlySpan<char> spelling, CssValueWork work)
    {
        var index = spelling.Length - 1;
        var digits = 0;
        while (index >= 0 && digits < 6 && spelling[index] is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F')
        {
            work.Charge(1);
            digits++;
            index--;
        }
        if (digits == 0) return false;
        var slashes = 0;
        while (index >= 0 && spelling[index] == '\\')
        {
            work.Charge(1);
            slashes++;
            index--;
        }
        return (slashes & 1) != 0;
    }

    private static bool IsCssWhitespace(char value) => value is ' ' or '\t' or '\n' or '\r' or '\f';

    private static bool NeedsSeparator(CssTokenKind left, char leftDelimiter, CssTokenKind right, char rightDelimiter)
    {
        var name = right is CssTokenKind.Ident or CssTokenKind.Function or CssTokenKind.Url or CssTokenKind.BadUrl;
        var number = right is CssTokenKind.Number or CssTokenKind.Percentage or CssTokenKind.Dimension;
        var hyphen = right == CssTokenKind.Delim && rightDelimiter == '-';
        var cdc = right == CssTokenKind.Cdc;
        if (left == CssTokenKind.Ident)
            return name || number || hyphen || cdc || right == CssTokenKind.OpenParenthesis;
        if (left is CssTokenKind.AtKeyword or CssTokenKind.Hash or CssTokenKind.Dimension)
            return name || number || hyphen || cdc;
        if (left == CssTokenKind.Number)
            return name || number || cdc || right == CssTokenKind.Delim && rightDelimiter == '%';
        if (left != CssTokenKind.Delim) return false;
        return leftDelimiter switch
        {
            '#' or '-' => name || number || hyphen || cdc,
            '@' => name || hyphen || cdc,
            '.' or '+' => number,
            '/' => right == CssTokenKind.Delim && rightDelimiter == '*',
            _ => false
        };
    }
}
