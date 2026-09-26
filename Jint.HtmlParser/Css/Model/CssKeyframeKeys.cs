using System.Globalization;
using System.Text;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// Preserve ordered multiplicity. Matching compares exact decimal values, never doubles.
internal sealed class CssKeyframeKeys(CssNumber[] values, string text)
{
    private static readonly CssNumber Zero = CssNumber.FromValidatedToken("0", new CssValueWork(default));
    private static readonly CssNumber Hundred = CssNumber.FromValidatedToken("100", new CssValueWork(default));
    internal string Text { get; } = text;

    internal static CssKeyframeKeys? Parse(string source, CssParseOptions? options, CssValueWork work)
    {
        var parser = new CssSyntaxParser(source, options, work.Token, work.CheckCancellation);
        return FromComponents(parser.ParseComponentValues(), work, out _);
    }

    internal static CssKeyframeKeys? FromComponents(CssComponentValueList components, CssValueWork work,
        out bool timeline)
    {
        timeline = false;
        var numbers = new List<CssNumber>();
        var builder = new StringBuilder();
        var needValue = true;
        var rangeName = false;
        foreach (var component in components)
        {
            work.Charge(1);
            if (component.Kind != CssComponentKind.Token) return null;
            var token = component.Token;
            if (token.Kind == CssTokenKind.Whitespace) continue;
            // Recognize the extension's shape so demanded timeline grammar stays named pending.
            if (rangeName && token.Kind == CssTokenKind.Percentage) { timeline = true; return null; }
            if (!needValue)
            {
                if (token.Kind != CssTokenKind.Comma) return null;
                needValue = true;
                continue;
            }
            CssNumber number;
            if (token.Kind == CssTokenKind.Ident)
            {
                if (CssAscii.EqualsIgnoreCase(token.Text, "from")) number = Zero;
                else if (CssAscii.EqualsIgnoreCase(token.Text, "to")) number = Hundred;
                else
                {
                    if (rangeName || !IsTimelineRange(token.Text)) return null;
                    rangeName = true;
                    continue;
                }
            }
            else if (token.Kind == CssTokenKind.Percentage)
            {
                number = CssNumber.FromValidatedToken(token.NumberText, work);
                if (number.Sign < 0 || number.CompareTo(Hundred, work) > 0) return null;
            }
            else return null;
            if (rangeName) return null;
            if (numbers.Count != 0) builder.Append(", ");
            builder.Append(SerializeNumber(number, work)).Append('%');
            numbers.Add(number);
            needValue = false;
        }
        if (rangeName || needValue) return null;
        work.Charge(numbers.Count);
        var result = new CssKeyframeKeys(numbers.ToArray(), builder.ToString());
        work.Charge(result.Text.Length);
        work.CheckCancellation();
        return result;
    }

    private static bool IsTimelineRange(string name) =>
        CssAscii.EqualsIgnoreCase(name, "cover") || CssAscii.EqualsIgnoreCase(name, "contain") ||
        CssAscii.EqualsIgnoreCase(name, "entry") || CssAscii.EqualsIgnoreCase(name, "exit") ||
        CssAscii.EqualsIgnoreCase(name, "entry-crossing") || CssAscii.EqualsIgnoreCase(name, "exit-crossing");

    internal bool Matches(CssKeyframeKeys other, CssValueWork work)
    {
        work.Charge(1);
        if (values.Length != other.Values.Length) return false;
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(1);
            if (values[i].CompareTo(other.Values[i], work) != 0) return false;
        }
        return true;
    }

    private CssNumber[] Values => values;

    private static string SerializeNumber(CssNumber number, CssValueWork work)
    {
        if (number.Sign == 0) return "0";
        // A bounded exact decimal spelling. Extremely small exponents stay scientific to
        // avoid an output allocation proportional to their numeric magnitude.
        var spelling = number.Spelling;
        var digits = new StringBuilder();
        var fraction = 0;
        var afterPoint = false;
        var exponentStart = spelling.Length;
        for (var i = spelling[0] == '+' ? 1 : 0; i < spelling.Length; i++)
        {
            work.Charge(1);
            var ch = spelling[i];
            if (ch is 'e' or 'E') { exponentStart = i + 1; break; }
            if (ch == '.') { afterPoint = true; continue; }
            digits.Append(ch);
            if (afterPoint) fraction++;
        }
        var all = digits.ToString();
        var first = 0;
        while (first < all.Length && all[first] == '0') { work.Charge(1); first++; }
        var end = all.Length;
        while (end > first && all[end - 1] == '0') { work.Charge(1); end--; }
        work.Charge(all.Length);
        var significant = all.Substring(first, end - first);
        long magnitude = 0;
        var exponentSign = 1;
        if (exponentStart < spelling.Length && spelling[exponentStart] is '+' or '-')
        {
            work.Charge(1);
            if (spelling[exponentStart] == '-') exponentSign = -1;
            exponentStart++;
        }
        // Saturation preserves bounded arithmetic, but every exponent digit is still charged.
        // Leading zeros can be arbitrarily long even when the final magnitude fits Int32.
        for (var i = exponentStart; i < spelling.Length; i++)
        {
            work.Charge(1);
            magnitude = System.Math.Min((long) int.MaxValue + 2, magnitude * 10 + spelling[i] - '0');
        }
        var exponent = exponentSign * magnitude;
        if (exponent > int.MaxValue || exponent < int.MinValue)
        {
            work.Charge(spelling.Length);
            return spelling; // exact, finite output; comparisons still use CssNumber
        }
        var power = (long) exponent - fraction + all.Length - end;
        var position = significant.Length + power;
        string result;
        if (position is > 0 and <= 3)
            result = position >= significant.Length ? significant + new string('0', (int) position - significant.Length)
                : significant.Insert((int) position, ".");
        else if (position is <= 0 and >= -6)
            result = "0." + new string('0', (int) -position) + significant;
        else
            result = significant + "e" + power.ToString(CultureInfo.InvariantCulture);
        work.Charge(result.Length);
        return result;
    }
}
