namespace Jint.HtmlParser.Parsing;

/// <summary>The shared decimal/exponent scanner for CSS tokens and SVG attribute microsyntax.</summary>
/// <remarks>
/// https://drafts.csswg.org/css-syntax/#consume-number and
/// https://svgwg.org/svg2-draft/types.html#syntax. SVG also permits a trailing decimal point.
/// </remarks>
internal static class NumberScanner
{
    internal static int Scan(ReadOnlySpan<char> source, bool trailingPoint, out bool integer, Action? checkpoint = null)
    {
        integer = true;
        var i = 0;
        if (i < source.Length && source[i] is '+' or '-') i++;
        var digits = i;
        while (i < source.Length && char.IsAsciiDigit(source[i])) Step(ref i, checkpoint);
        var hasDigits = i != digits;
        if (i < source.Length && source[i] == '.' &&
            (i + 1 < source.Length && char.IsAsciiDigit(source[i + 1]) || trailingPoint && hasDigits))
        {
            integer = false;
            i++;
            var fraction = i;
            while (i < source.Length && char.IsAsciiDigit(source[i])) Step(ref i, checkpoint);
            hasDigits |= i != fraction;
        }
        if (!hasDigits) return 0;
        if (i < source.Length && source[i] is 'e' or 'E')
        {
            var exponent = i + 1;
            if (exponent < source.Length && source[exponent] is '+' or '-') exponent++;
            if (exponent < source.Length && char.IsAsciiDigit(source[exponent]))
            {
                integer = false;
                i = exponent;
                while (i < source.Length && char.IsAsciiDigit(source[i])) Step(ref i, checkpoint);
            }
        }
        return i;
    }

    private static void Step(ref int position, Action? checkpoint)
    {
        if ((++position & 1023) == 0) checkpoint?.Invoke();
    }
}
