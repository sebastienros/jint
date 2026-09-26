using System.Globalization;

namespace Jint.HtmlParser;

internal enum HtmlInputNumericParseResult { Success, SyntaxError, NonFinite, Inapplicable }

/// <summary>HTML §2.3.4.3 floating-point grammar and the separately permissive parsing algorithm.</summary>
internal static class HtmlInputNumberSyntax
{
    internal static string Sanitize(string source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return TryParseValue(source, out _, cancellationToken: cancellationToken) ? source : string.Empty;
    }

    internal static bool TryParseValue(ReadOnlySpan<char> source, out double value,
        Action<long>? checkpoint = null, CancellationToken cancellationToken = default)
        => TryGetNumber(source, strict: true, out value, checkpoint, cancellationToken) == HtmlInputNumericParseResult.Success;

    internal static bool TryParsePrefix(ReadOnlySpan<char> source, out double value,
        Action<long>? checkpoint = null, CancellationToken cancellationToken = default)
        => TryGetNumber(source, strict: false, out value, checkpoint, cancellationToken) == HtmlInputNumericParseResult.Success;

    internal static HtmlInputNumericParseResult TryGetNumber(ReadOnlySpan<char> source, bool strict, out double value,
        Action<long>? checkpoint = null, CancellationToken cancellationToken = default)
    {
        var work = new HtmlInputValueWork(checkpoint, cancellationToken);
        return TryGetNumber(source, strict, out value, ref work);
    }
    internal static HtmlInputNumericParseResult TryGetNumber(ReadOnlySpan<char> source, bool strict, out double value,
        ref HtmlInputValueWork work)
    {
        work.Check();
        var start = 0;
        if (!strict)
            while (start < source.Length && HtmlTextSanitizer.IsAsciiWhitespace(source[start])) { work.Step(); start++; }
        source = source[start..];
        var end = Scan(source, strict, ref work);
        value = 0;
        work.Check();
        if (end <= 0) return HtmlInputNumericParseResult.SyntaxError;
        return Convert(source, end, ref work, out value) ? HtmlInputNumericParseResult.Success : HtmlInputNumericParseResult.NonFinite;
    }

    internal static bool Convert(ReadOnlySpan<char> source, int end, ref HtmlInputValueWork work, out double value)
    {
        value = 0;
        work.Check();
        if (end <= 0) return false;
        source = source[..end];
        // Every binary64 midpoint terminates with fewer than 1200 significant decimal digits
        // (its denominator divides 2^1075). Retain a conservative prefix and a sticky tail,
        // so nearest/ties-even conversion sees the correct side even after millions of zeros.
        Span<char> compact = stackalloc char[1240];
        var negative = source[0] == '-';
        var written = negative ? 1 : 0;
        if (negative) compact[0] = '-';
        var coefficientStart = written;
        var i = 0;
        if (source[0] is '-' or '+') { work.Step(); i++; }
        var afterPoint = false;
        long fractionDigits = 0;
        long significantDigits = 0;
        var sticky = false;
        for (; i < source.Length && source[i] is not ('e' or 'E'); i++)
        {
            work.Step();
            var c = source[i];
            if (c == '.') { afterPoint = true; continue; }
            if (afterPoint) fractionDigits++;
            if (significantDigits == 0 && c == '0') continue;
            significantDigits++;
            if (significantDigits <= 1200) compact[written++] = c;
            else sticky |= c != '0';
        }
        long exponent = 0;
        if (i < source.Length)
        {
            work.Step(); i++;
            var exponentNegative = false;
            if (i < source.Length && source[i] is '+' or '-')
            { exponentNegative = source[i] == '-'; work.Step(); i++; }
            var saturation = (long) source.Length + 4096;
            for (; i < source.Length; i++)
            {
                work.Step();
                exponent = Math.Min(saturation, exponent * 10 + source[i] - '0');
            }
            if (exponentNegative) exponent = -exponent;
        }
        work.Check();
        if (significantDigits == 0) return true;
        exponent += significantDigits - (written - coefficientStart) - fractionDigits;
        if (sticky) { compact[written++] = '1'; exponent--; }
        var order = written - coefficientStart + exponent;
        if (order > 309) return false;
        if (order < -323) return true;
        // Normalize the point before BCL conversion, keeping its exponent in the binary64
        // magnitude range even when 1200 retained digits cancel a large negative exponent.
        exponent = order - 1;
        while (written > coefficientStart + 1 && compact[written - 1] == '0') written--;
        if (written > coefficientStart + 1)
        {
            compact.Slice(coefficientStart + 1, written - coefficientStart - 1).CopyTo(compact[(coefficientStart + 2)..]);
            compact[coefficientStart + 1] = '.';
            written++;
        }
        compact[written++] = 'e';
        if (!exponent.TryFormat(compact[written..], out var exponentLength, provider: CultureInfo.InvariantCulture))
            throw new InvalidOperationException("The bounded HTML number buffer was insufficient.");
        written += exponentLength;
        var success = double.TryParse(compact[..written], NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
        work.Check();
        if (value == 0) value = 0; // HTML's result set excludes negative zero, including underflow.
        return success;
    }

    private static int Scan(ReadOnlySpan<char> source, bool strict, ref HtmlInputValueWork work)
    {
        var i = 0;
        if (i < source.Length && (source[i] == '-' || (!strict && source[i] == '+'))) { work.Step(); i++; }
        var integerStart = i;
        while (i < source.Length && IsDigit(source[i])) { work.Step(); i++; }
        var integerDigits = i - integerStart;
        if (i < source.Length && source[i] == '.')
        {
            work.Step(); i++;
            var fractionStart = i;
            while (i < source.Length && IsDigit(source[i])) { work.Step(); i++; }
            if (i == fractionStart && (strict || integerDigits == 0)) return -1;
        }
        else if (integerDigits == 0) return -1;

        if (i < source.Length && source[i] is 'e' or 'E')
        {
            var beforeExponent = i;
            work.Step(); i++;
            if (i < source.Length && source[i] is '+' or '-') { work.Step(); i++; }
            var exponentStart = i;
            while (i < source.Length && IsDigit(source[i])) { work.Step(); i++; }
            if (i == exponentStart)
            {
                if (strict) return -1;
                i = beforeExponent;
            }
        }

        work.Check();
        return strict && i != source.Length ? -1 : i;
    }

    internal static bool IsDigit(char c) => c is >= '0' and <= '9';
}
