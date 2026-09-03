using System.Globalization;

namespace Jint.Browser.Runtime;

/// <summary>
/// Answers a CSS media query from the page's media environment — its viewport, its media type and the
/// preferences a client emulated.
/// </summary>
/// <remarks>
/// <para>
/// AngleSharp.Css parses media queries and models a render device, but its own <c>matchMedia</c> answers
/// <see langword="false"/> for every query — <c>CssMediaQueryList.ComputeMatched</c> is a stub — so a page
/// asking whether it is on a narrow screen would always be told no. This evaluates the subset a page actually
/// branches on instead, which is the honest half of what the CSSOM change will complete.
/// </para>
/// <para>
/// The grammar handled is a comma-separated list of queries, each an optional <c>not</c> or <c>only</c>, an
/// optional media type, and <c>and</c>-joined feature tests. A feature this does not know is <em>unknown</em>
/// rather than false, so the query it appears in answers <see langword="false"/> however it is written —
/// which is also what a query this cannot parse answers, the specification's own rule of treating a malformed
/// query as <c>not all</c>. Media Queries 4's <c>or</c> and its range syntax (<c>(width &gt;= 600px)</c>) are
/// unknown to it and therefore false; the colon form of the same test is not.
/// </para>
/// <para>
/// <b>The dimension features are read from the viewport and the discrete ones from
/// <see cref="PageMediaEnvironment.ValueOf"/></b>, which is the single table a client's
/// <c>Emulation.setEmulatedMedia</c> writes into. A feature written without a value is in
/// <a href="https://drafts.csswg.org/mediaqueries-5/#mq-boolean-context">boolean context</a>, where it is
/// false exactly when its value is one of the specification's falsy ones — <c>none</c>,
/// <c>no-preference</c> or <c>0</c>.
/// </para>
/// </remarks>
internal static class MediaQuery
{
    /// <summary>How many CSS pixels one <c>em</c> is taken to be, there being no cascade to ask.</summary>
    /// <remarks>
    /// <see cref="PageRenderDevice.FontSize"/> reports the same number, so an <c>em</c> in a
    /// <c>matchMedia</c> query and an <c>em</c> the cascade resolves are the same length.
    /// </remarks>
    internal const double PixelsPerEm = 16;

    /// <summary>The values that make a feature false when it is written without one.</summary>
    private static readonly string[] _falsy = ["none", "no-preference", "0"];

    /// <summary>
    /// The features whose values are ordered rather than distinct, and the order, weakest first.
    /// </summary>
    /// <remarks>
    /// <a href="https://drafts.csswg.org/mediaqueries-5/#color-gamut">Media Queries 5</a> defines both as
    /// "at least": <c>(color-gamut: srgb)</c> matches a display that can do P3, and
    /// <c>(dynamic-range: standard)</c> matches a display that can do high. Comparing them for equality
    /// would answer no to a page asking whether it may use sRGB colours on a wide-gamut screen.
    /// </remarks>
    private static readonly Dictionary<string, string[]> _ordered = new(StringComparer.Ordinal)
    {
        ["color-gamut"] = ["srgb", "p3", "rec2020"],
        ["dynamic-range"] = ["standard", "high"],
        ["video-dynamic-range"] = ["standard", "high"],
    };

    /// <summary>Whether <paramref name="text"/> matches <paramref name="environment"/>.</summary>
    internal static bool Matches(string text, PageMediaEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            // An empty query is `all`, which matches.
            return true;
        }

        foreach (var query in text.Split(','))
        {
            if (MatchesOne(query.Trim(), environment))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesOne(string query, PageMediaEnvironment environment)
    {
        if (query.Length == 0)
        {
            return false;
        }

        var negated = false;
        var parts = Split(query);
        var index = 0;

        if (parts.Count > 0 && parts[0].Equals("not", StringComparison.OrdinalIgnoreCase))
        {
            negated = true;
            index = 1;
        }
        else if (parts.Count > 0 && parts[0].Equals("only", StringComparison.OrdinalIgnoreCase))
        {
            index = 1;
        }

        var matched = true;
        var terms = 0;

        for (; index < parts.Count; index++)
        {
            var part = parts[index];
            if (part.Equals("and", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            terms++;

            if (!part.StartsWith('('))
            {
                matched &= MatchesType(part, environment);
                continue;
            }

            // Media Queries 4: a feature this does not know is *unknown*, not false, and an unknown term
            // makes the whole query false however it is written — `not (bogus: 1)` answers false, exactly as
            // a malformed query does. Folding it into `matched` and then negating would answer true.
            if (MatchesFeature(part, environment) is not { } answer)
            {
                return false;
            }

            matched &= answer;
        }

        // `not` and `only` are prefixes, not queries: neither means anything without a term after it.
        if (terms == 0)
        {
            return false;
        }

        return negated ? !matched : matched;
    }

    /// <summary>
    /// Whether a bare media type in a query names the one the page is being shown as.
    /// </summary>
    /// <remarks>
    /// <c>all</c> always matches; everything else is the emulated media type, which is <c>screen</c> until
    /// <c>Emulation.setEmulatedMedia</c> says <c>print</c>. A type neither of them names — <c>speech</c>,
    /// or one of the types Media Queries 4 deprecated — is false rather than unknown, which is that
    /// specification's own rule for a media type it does not recognize.
    /// </remarks>
    private static bool MatchesType(string type, PageMediaEnvironment environment)
        => type.Equals("all", StringComparison.OrdinalIgnoreCase)
        || type.Equals(environment.MediaType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the feature matches, or <see langword="null"/> when this does not know the feature — which
    /// Media Queries 4 calls unknown, and which makes the whole query false however it is written.
    /// </summary>
    private static bool? MatchesFeature(string feature, PageMediaEnvironment environment)
    {
        var body = feature.Trim('(', ')').Trim();
        if (body.Length == 0)
        {
            return null;
        }

        var viewport = environment.Viewport;
        var colon = body.IndexOf(':', StringComparison.Ordinal);
        var name = (colon < 0 ? body : body[..colon]).Trim().ToLowerInvariant();
        var value = colon < 0 ? null : body[(colon + 1)..].Trim().ToLowerInvariant();

        return name switch
        {
            "width" or "min-width" or "max-width" or "device-width" or "min-device-width" or "max-device-width"
                => Compare(name, value, viewport.Width),
            "height" or "min-height" or "max-height" or "device-height" or "min-device-height" or "max-device-height"
                => Compare(name, value, viewport.Height),
            "resolution" or "min-resolution" or "max-resolution"
                => Compare(name, value, viewport.DeviceScaleFactor * 96),
            "aspect-ratio" or "min-aspect-ratio" or "max-aspect-ratio"
                => CompareRatio(name, value, viewport.Height == 0 ? 0 : (double) viewport.Width / viewport.Height),
            "orientation" => value is null
                ? viewport.Width != 0 && viewport.Height != 0
                : value == (viewport.Width >= viewport.Height ? "landscape" : "portrait"),
            "grid" => value is "0",
            "color" => value is null or "8",
            "monochrome" => value is "0",
            _ => Discrete(name, value, environment),
        };
    }

    /// <summary>
    /// A feature whose values are words rather than lengths, answered from the media environment.
    /// </summary>
    /// <remarks>
    /// Written with a value it matches on equality — or on "at least" for the two features
    /// <see cref="_ordered"/> names — and written without one it is in boolean context, where it is false
    /// exactly when the feature's current value is one the specification calls falsy.
    /// </remarks>
    private static bool? Discrete(string name, string? value, PageMediaEnvironment environment)
    {
        if (environment.ValueOf(name) is not { } current)
        {
            return null;
        }

        if (value is null)
        {
            return Array.IndexOf(_falsy, current) < 0;
        }

        if (_ordered.TryGetValue(name, out var order))
        {
            var asked = Array.IndexOf(order, value);
            return asked >= 0 && Array.IndexOf(order, current) >= asked;
        }

        return string.Equals(value, current, StringComparison.Ordinal);
    }

    private static bool? Compare(string name, string? value, double actual)
    {
        // A range feature written without a value is in boolean context, where it is false only when the
        // feature's own value is zero — a viewport with a width is `(width)`.
        if (value is null)
        {
            return actual != 0;
        }

        return TryLength(value, out var target) ? Ordered(name, actual, target, tolerance: 0.5) : null;
    }

    /// <summary>An <c>&lt;ratio&gt;</c>, which CSS writes as <c>16/9</c> and also accepts as a bare number.</summary>
    private static bool? CompareRatio(string name, string? value, double actual)
    {
        if (value is null)
        {
            return actual != 0;
        }

        var slash = value.IndexOf('/', StringComparison.Ordinal);
        double target;

        if (slash < 0)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out target))
            {
                return null;
            }
        }
        else if (double.TryParse(value[..slash].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            && double.TryParse(value[(slash + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var height)
            && height != 0)
        {
            target = width / height;
        }
        else
        {
            return null;
        }

        return Ordered(name, actual, target, tolerance: 0.0001);
    }

    /// <summary>The <c>min-</c> / <c>max-</c> / exact comparison a range feature's prefix asks for.</summary>
    private static bool Ordered(string name, double actual, double target, double tolerance)
    {
        if (name.StartsWith("min-", StringComparison.Ordinal))
        {
            return actual >= target;
        }

        if (name.StartsWith("max-", StringComparison.Ordinal))
        {
            return actual <= target;
        }

        return Math.Abs(actual - target) < tolerance;
    }

    private static bool TryLength(string value, out double pixels)
    {
        pixels = 0;

        var span = value.AsSpan().Trim();
        var unit = "";

        foreach (var candidate in (string[]) ["dppx", "dpcm", "dpi", "rem", "em", "px", "x"])
        {
            if (span.EndsWith(candidate, StringComparison.Ordinal))
            {
                unit = candidate;
                span = span[..^candidate.Length];
                break;
            }
        }

        if (!double.TryParse(span.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        pixels = unit switch
        {
            "em" or "rem" => number * PixelsPerEm,
            "dppx" or "x" => number * 96,
            "dpcm" => number * 2.54,
            _ => number,
        };

        return true;
    }

    /// <summary>Splits a query into its words and parenthesised feature tests, keeping the parentheses.</summary>
    private static List<string> Split(string query)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < query.Length; i++)
        {
            var c = query[i];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (depth == 0 && char.IsWhiteSpace(c))
            {
                if (i > start)
                {
                    parts.Add(query[start..i]);
                }

                start = i + 1;
            }
        }

        if (start < query.Length)
        {
            parts.Add(query[start..]);
        }

        return parts;
    }
}
