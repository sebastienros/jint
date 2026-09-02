using System.Globalization;

namespace Jint.DevTools.Session;

/// <summary>
/// The opaque identifiers the protocol hands clients: a browser, a target, a session.
/// </summary>
/// <remarks>
/// Thirty-two uppercase hexadecimal characters, which is the shape Chrome's are and which more than one
/// client parses as exactly that before putting it in a URL. Nothing about a target is derivable from its
/// identifier, deliberately: a client that guessed one would be addressing an engine it was never told about.
/// </remarks>
internal static class Identifiers
{
    private static int _counter;

    /// <summary>Mints one identifier, unique for the life of the process.</summary>
    internal static string New()
    {
        var ordinal = Interlocked.Increment(ref _counter);
        var guid = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture).ToUpperInvariant();
        return string.Create(CultureInfo.InvariantCulture, $"{guid.AsSpan(0, 24)}{ordinal:X8}");
    }
}
