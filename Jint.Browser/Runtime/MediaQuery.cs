using System.Globalization;

namespace Jint.Browser.Runtime;

/// <summary>
/// Answers a CSS media query from the viewport and from the fixed preferences a headless page reports.
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
/// </remarks>
internal static class MediaQuery
{
    /// <summary>How many CSS pixels one <c>em</c> is taken to be, there being no cascade to ask.</summary>
    private const double PixelsPerEm = 16;

    /// <summary>Whether <paramref name="text"/> matches <paramref name="viewport"/>.</summary>
    internal static bool Matches(string text, Viewport viewport)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            // An empty query is `all`, which matches.
            return true;
        }

        foreach (var query in text.Split(','))
        {
            if (MatchesOne(query.Trim(), viewport))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesOne(string query, Viewport viewport)
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
                matched &= MatchesType(part);
                continue;
            }

            // Media Queries 4: a feature this does not know is *unknown*, not false, and an unknown term
            // makes the whole query false however it is written — `not (bogus: 1)` answers false, exactly as
            // a malformed query does. Folding it into `matched` and then negating would answer true.
            if (MatchesFeature(part, viewport) is not { } answer)
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

    private static bool MatchesType(string type) => type.ToLowerInvariant() switch
    {
        "all" or "screen" => true,
        _ => false,
    };

    /// <summary>
    /// Whether the feature matches, or <see langword="null"/> when this does not know the feature — which
    /// Media Queries 4 calls unknown, and which makes the whole query false however it is written.
    /// </summary>
    private static bool? MatchesFeature(string feature, Viewport viewport)
    {
        var body = feature.Trim('(', ')').Trim();
        if (body.Length == 0)
        {
            return null;
        }

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
            "prefers-color-scheme" => Is(value, "light"),
            "prefers-reduced-motion" => Is(value, "no-preference"),
            "prefers-reduced-transparency" => Is(value, "no-preference"),
            "prefers-contrast" => Is(value, "no-preference"),
            "forced-colors" => Is(value, "none"),
            "display-mode" => Is(value, "browser"),
            "scripting" => Is(value, "enabled"),
            "hover" or "any-hover" => Is(value, "hover"),
            "pointer" or "any-pointer" => Is(value, "fine"),
            "update" => Is(value, "fast"),
            "grid" => value is "0",
            "color" => value is null or "8",
            "monochrome" => value is "0",
            _ => null,
        };
    }

    /// <summary>
    /// A feature written with a value matches on equality; written without one it is in boolean context,
    /// where it is false exactly when the feature's value is the one the specification calls falsy —
    /// <c>none</c>, or <c>no-preference</c> for the <c>prefers-*</c> family.
    /// </summary>
    private static bool Is(string? value, string expected)
        => value is null
            ? expected is not ("none" or "no-preference")
            : string.Equals(value, expected, StringComparison.Ordinal);

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
