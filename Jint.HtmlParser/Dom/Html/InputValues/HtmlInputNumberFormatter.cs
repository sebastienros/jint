using System.Globalization;
using Jint.HtmlParser.InputValues.Dtoa;

namespace Jint.HtmlParser;

/// <summary>HTML best representation: shortest binary64 digits with ECMA Number::toString layout.</summary>
internal static class HtmlInputNumberFormatter
{
    internal static string FormatFinite(double value)
    {
        Span<char> buffer = stackalloc char[32];
        var length = WriteFinite(value, buffer);
        return new string(buffer[..length]);
    }

    internal static int WriteFinite(double value, Span<char> destination)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (destination.Length < 32) throw new ArgumentException("A 32-character buffer is required.", nameof(destination));
        if (value == 0)
        {
            destination[0] = '0';
            return 1;
        }

        Span<char> digits = stackalloc char[32];
        var builder = new DtoaBuilder(digits);
        DtoaNumberFormatter.DoubleToAscii(ref builder, value, DtoaMode.Shortest, 0, out var negative, out var n);
        var significant = builder.Slice(0, builder.Length);
        var k = significant.Length;
        var written = 0;
        if (negative) destination[written++] = '-';
        if (k <= n && n <= 21)
        {
            significant.CopyTo(destination[written..]);
            written += k;
            destination.Slice(written, n - k).Fill('0');
            written += n - k;
        }
        else if (n is > 0 and <= 21)
        {
            significant[..n].CopyTo(destination[written..]);
            written += n;
            destination[written++] = '.';
            significant[n..].CopyTo(destination[written..]);
            written += k - n;
        }
        else if (n is > -6 and <= 0)
        {
            destination[written++] = '0';
            destination[written++] = '.';
            destination.Slice(written, -n).Fill('0');
            written -= n;
            significant.CopyTo(destination[written..]);
            written += k;
        }
        else
        {
            destination[written++] = significant[0];
            if (k > 1)
            {
                destination[written++] = '.';
                significant[1..].CopyTo(destination[written..]);
                written += k - 1;
            }

            destination[written++] = 'e';
            if (n >= 1) destination[written++] = '+';
            if (!(n - 1).TryFormat(destination[written..], out var exponentLength, provider: CultureInfo.InvariantCulture))
                throw new InvalidOperationException("The ECMA layout buffer was insufficient.");
            written += exponentLength;
        }

        return written;
    }
}
