using System.Globalization;

namespace Jint.Browser.Tool;

/// <summary>What a command line means by a duration, a size, a header and a cookie.</summary>
/// <remarks>
/// Every one of these refuses rather than guesses. A tool that read <c>--timeout 30</c> as thirty
/// milliseconds, or <c>--memory-limit 256</c> as 256 bytes, would fail a page for a reason its user never
/// wrote down — so a bare number has exactly one documented meaning per option and everything else needs a
/// unit.
/// </remarks>
internal static class ValueSyntax
{
    /// <summary>Reads <c>500ms</c>, <c>30s</c>, <c>5m</c>, <c>1h</c>, or a bare number of seconds.</summary>
    internal static TimeSpan Duration(string option, string text)
    {
        var trimmed = text.Trim();
        var (digits, factor) = Split(trimmed, [("ms", 0.001), ("s", 1), ("m", 60), ("h", 3600)], defaultFactor: 1);

        if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0 || double.IsInfinity(value))
        {
            throw new ToolUsageException($"'--{option} {text}' is not a duration; write it as 30s, 500ms, 5m or a number of seconds");
        }

        return TimeSpan.FromSeconds(value * factor);
    }

    /// <summary>Reads <c>256mb</c>, <c>512kb</c>, <c>1gb</c>, or a bare number of bytes.</summary>
    /// <remarks>The multipliers are binary — <c>1kb</c> is 1024 bytes — which is what every byte budget in
    /// <c>BrowserOptions</c> is expressed in.</remarks>
    internal static long Size(string option, string text)
    {
        var trimmed = text.Trim();
        var (digits, factor) = Split(trimmed, [("gb", 1024d * 1024 * 1024), ("mb", 1024d * 1024), ("kb", 1024d), ("b", 1)], defaultFactor: 1);

        if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0 || double.IsInfinity(value))
        {
            throw new ToolUsageException($"'--{option} {text}' is not a size; write it as 256mb, 512kb, 1gb or a number of bytes");
        }

        var bytes = value * factor;
        if (bytes > long.MaxValue)
        {
            throw new ToolUsageException($"'--{option} {text}' is larger than this process can address");
        }

        return (long) bytes;
    }

    /// <summary>Reads a whole number, refusing one outside <paramref name="minimum"/>.</summary>
    internal static int Integer(string option, string text, int minimum)
    {
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum)
        {
            throw new ToolUsageException($"'--{option} {text}' is not a whole number of at least {minimum.ToString(CultureInfo.InvariantCulture)}");
        }

        return value;
    }

    /// <summary>Reads <c>Name: value</c>, which is how a header is written on a command line and on the wire.</summary>
    internal static (string Name, string Value) Header(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            throw new ToolUsageException($"'--header {text}' is not a header; write it as 'Name: value'");
        }

        return (text[..colon].Trim(), text[(colon + 1)..].Trim());
    }

    /// <summary>Reads <c>name=value</c>, the cookie pair a <c>Set-Cookie</c> header starts with.</summary>
    internal static string Cookie(string text)
    {
        var equals = text.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0)
        {
            throw new ToolUsageException($"'--cookie {text}' is not a cookie; write it as 'name=value'");
        }

        return text.Trim();
    }

    /// <summary>Reads one of a fixed set of words, naming them all when the word is not among them.</summary>
    internal static T Word<T>(string option, string text, params (string Name, T Value)[] words) where T : struct
    {
        foreach (var (name, value) in words)
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        var names = string.Join(", ", words.Select(word => word.Name));
        throw new ToolUsageException($"'--{option} {text}' is not one of {names}");
    }

    /// <summary>Splits a value from its unit suffix, longest suffix first.</summary>
    private static (string Digits, double Factor) Split(string text, (string Suffix, double Factor)[] units, double defaultFactor)
    {
        foreach (var (suffix, factor) in units)
        {
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && text.Length > suffix.Length)
            {
                return (text[..^suffix.Length].Trim(), factor);
            }
        }

        return (text, defaultFactor);
    }
}

/// <summary>
/// The command line was wrong, which is exit code 1 and a message on standard error.
/// </summary>
/// <remarks>
/// It is deliberately separate from every failure a page can produce: a user who wrote the command wrongly
/// gets one exit code, a page that would not load gets another, and a page that ran out of its budget gets a
/// third — so a script driving this tool can tell "I asked for the wrong thing" from "the site is down".
/// </remarks>
internal sealed class ToolUsageException : Exception
{
    internal ToolUsageException(string message) : base(message)
    {
    }
}
