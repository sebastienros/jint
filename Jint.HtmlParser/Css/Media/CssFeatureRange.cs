using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Media;

// MQ5 sections 2.4.3/3; Conditional 5 section 6.1 uses the same range grammar.
// https://drafts.csswg.org/mediaqueries-5/#mq-range-context
internal sealed record CssFeatureRange(string Name, CssMediaComparison FirstComparison,
    CssComponentValue[] FirstValue, bool Reversed, CssMediaComparison? SecondComparison,
    CssComponentValue[] SecondValue)
{
    internal CssMediaComparison Comparison => Reversed ? FirstComparison switch
    {
        CssMediaComparison.Less => CssMediaComparison.Greater,
        CssMediaComparison.LessEqual => CssMediaComparison.GreaterEqual,
        CssMediaComparison.Greater => CssMediaComparison.Less,
        CssMediaComparison.GreaterEqual => CssMediaComparison.LessEqual,
        _ => FirstComparison
    } : FirstComparison;

    internal static CssFeatureRange? Parse(CssComponentValue[] items, CssValueWork work)
    {
        var operators = new List<(int Index, int Length, CssMediaComparison Comparison)>();
        for (var i = 0; i < items.Length; i++)
        {
            work.Charge(1);
            if (!Token(items[i], CssTokenKind.Delim)) continue;
            var delimiter = items[i].Token.Delimiter;
            if (delimiter is not ('<' or '>' or '=')) continue;
            if (operators.Count == 2) return null;
            var equal = i + 1 < items.Length && Delim(items[i + 1], '=') && delimiter != '=';
            var comparison = delimiter switch
            {
                '=' => CssMediaComparison.Equal,
                '<' => equal ? CssMediaComparison.LessEqual : CssMediaComparison.Less,
                _ => equal ? CssMediaComparison.GreaterEqual : CssMediaComparison.Greater
            };
            operators.Add((i, equal ? 2 : 1, comparison));
            if (equal) i++;
        }
        if (operators.Count == 0) return null;
        var first = operators[0];
        var left = items[..first.Index];
        var right = items[(first.Index + first.Length)..(operators.Count == 2 ? operators[1].Index : items.Length)];
        string name;
        CssComponentValue[] value;
        var reversed = false;
        if (left.Length == 1 && Token(left[0], CssTokenKind.Ident))
        {
            name = left[0].Token.Text;
            value = right;
        }
        else if (right.Length == 1 && Token(right[0], CssTokenKind.Ident))
        {
            name = right[0].Token.Text;
            value = left;
            reversed = true;
        }
        else return null;
        if (value.Length == 0) return null;
        CssMediaComparison? secondComparison = null;
        CssComponentValue[] last = [];
        if (operators.Count == 2)
        {
            var second = operators[1];
            if (!reversed || first.Comparison == CssMediaComparison.Equal || second.Comparison == CssMediaComparison.Equal ||
                (first.Comparison is CssMediaComparison.Less or CssMediaComparison.LessEqual) !=
                (second.Comparison is CssMediaComparison.Less or CssMediaComparison.LessEqual)) return null;
            last = items[(second.Index + second.Length)..];
            if (last.Length == 0) return null;
            secondComparison = second.Comparison;
        }
        work.Charge(items.Length);
        return new(CssPropertyRegistry.NormalizeName(name, work), first.Comparison, value, reversed, secondComparison, last);
    }

    internal static CssComponentValue[] WithoutWhitespace(CssComponentValueList values, CssValueWork work)
    {
        var result = new List<CssComponentValue>();
        for (var i = 0; i < values.Count; i++)
        {
            work.Charge(1);
            var value = values[i];
            // A whitespace token between < or > and = invalidates the comparison.
            var comparisonWhitespace = Token(value, CssTokenKind.Whitespace) && i > 0 && i + 1 < values.Count &&
                (Delim(values[i - 1], '<') || Delim(values[i - 1], '>')) && Delim(values[i + 1], '=');
            if (!Token(value, CssTokenKind.Whitespace) || comparisonWhitespace) result.Add(value);
        }
        return result.ToArray();
    }

    private static bool Token(CssComponentValue value, CssTokenKind kind) => value.Kind == CssComponentKind.Token && value.Token.Kind == kind;
    private static bool Delim(CssComponentValue value, char delimiter) => Token(value, CssTokenKind.Delim) && value.Token.Delimiter == delimiter;
}
