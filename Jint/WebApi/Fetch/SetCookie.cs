#if NET8_0_OR_GREATER
using System.Globalization;

namespace Jint.WebApi.Fetch;

/// <summary>
/// One parsed <c>Set-Cookie</c> header, https://httpwg.org/http-extensions/draft-ietf-httpbis-rfc6265bis.html#name-the-set-cookie-header-field.
/// </summary>
/// <remarks>
/// Deliberately engine-free, for the same reason <see cref="HeaderList"/> is: a response's headers are
/// classified on a thread pool thread while the engine goes on running script.
/// </remarks>
internal sealed class SetCookie
{
    internal required string Name { get; init; }

    internal required string Value { get; init; }

    /// <summary>
    /// The <c>Domain</c> attribute, lowercased and with the leading dot removed, or <see langword="null"/>
    /// for a host-only cookie.
    /// </summary>
    internal string? Domain { get; init; }

    /// <summary>The <c>Path</c> attribute, or <see langword="null"/> to take the URL's default-path.</summary>
    internal string? Path { get; init; }

    /// <summary>When the cookie expires, or <see langword="null"/> for a session cookie.</summary>
    internal DateTimeOffset? Expires { get; init; }

    internal bool Secure { get; init; }

    internal bool HttpOnly { get; init; }
}

/// <summary>
/// The <c>Set-Cookie</c> parser of
/// https://httpwg.org/http-extensions/draft-ietf-httpbis-rfc6265bis.html#name-parsing-set-cookie-header-f,
/// plus the two name prefixes the storage model refuses.
/// </summary>
/// <remarks>
/// Written here rather than left to <see cref="System.Net.CookieContainer"/>'s own
/// <c>SetCookies(Uri, string)</c>, which takes a single comma-joined string and therefore has to guess where
/// one header ends and the next begins — the exact ambiguity a <c>Set-Cookie</c> carrying
/// <c>Expires=Wed, 09 Jun 2021 10:18:14 GMT</c> creates, and the reason the Fetch Standard keeps the values
/// apart in the first place.
/// </remarks>
internal static class SetCookieParser
{
    /// <summary>
    /// The date the specification calls "the earliest representable date and time", used for a
    /// non-positive <c>Max-Age</c>.
    /// </summary>
    private static readonly DateTimeOffset _earliest = new(1601, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] _monthNames = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>
    /// Parses one <c>Set-Cookie</c> value, answering <see langword="false"/> for every value the
    /// specification says to ignore.
    /// </summary>
    internal static bool TryParse(string header, out SetCookie? cookie)
    {
        cookie = null;
        if (string.IsNullOrEmpty(header))
        {
            return false;
        }

        // Step 1: a value carrying a control character is ignored entirely.
        foreach (var c in header)
        {
            if (c <= 0x08 || (c >= 0x0A && c <= 0x1F) || c == 0x7F)
            {
                return false;
            }
        }

        // Step 2: the name-value pair is everything before the first ';'.
        var semicolon = header.IndexOf(';');
        var pair = semicolon < 0 ? header : header.Substring(0, semicolon);
        var attributes = semicolon < 0 ? string.Empty : header.Substring(semicolon + 1);

        // Step 3: "If the name-value-pair string lacks a %x3D ("=") character, then the name string is empty,
        // and the value string is the value of name-value-pair."
        var equals = pair.IndexOf('=');
        string name;
        string value;
        if (equals < 0)
        {
            name = string.Empty;
            value = pair.Trim();
        }
        else
        {
            name = pair.Substring(0, equals).Trim();
            value = pair.Substring(equals + 1).Trim();
        }

        // Step 5: "If the name string is empty and the value string is empty, ignore the set-cookie-string."
        if (name.Length == 0 && value.Length == 0)
        {
            return false;
        }

        string? domain = null;
        string? path = null;
        DateTimeOffset? expires = null;
        long? maxAge = null;
        var secure = false;
        var httpOnly = false;

        foreach (var attribute in SplitAttributes(attributes))
        {
            var (attributeName, attributeValue) = attribute;

            if (attributeName.Equals("expires", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseCookieDate(attributeValue, out var parsed))
                {
                    expires = parsed;
                }
            }
            else if (attributeName.Equals("max-age", StringComparison.OrdinalIgnoreCase))
            {
                // "If the first character of the attribute-value is neither a DIGIT nor a '-', ignore."
                if (attributeValue.Length > 0
                    && (char.IsAsciiDigit(attributeValue[0]) || attributeValue[0] == '-')
                    && long.TryParse(attributeValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seconds))
                {
                    maxAge = seconds;
                }
            }
            else if (attributeName.Equals("domain", StringComparison.OrdinalIgnoreCase))
            {
                if (attributeValue.Length != 0)
                {
                    var trimmed = attributeValue[0] == '.' ? attributeValue.Substring(1) : attributeValue;
                    if (trimmed.Length != 0)
                    {
                        domain = trimmed.ToLowerInvariant();
                    }
                }
            }
            else if (attributeName.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                // "If attribute-value is empty or if its first character is not '/', ignore the attribute."
                if (attributeValue.Length != 0 && attributeValue[0] == '/')
                {
                    path = attributeValue;
                }
            }
            else if (attributeName.Equals("secure", StringComparison.OrdinalIgnoreCase))
            {
                secure = true;
            }
            else if (attributeName.Equals("httponly", StringComparison.OrdinalIgnoreCase))
            {
                httpOnly = true;
            }
        }

        // "If a cookie has both the Max-Age and the Expires attribute, the Max-Age attribute has precedence."
        if (maxAge is { } delta)
        {
            expires = delta <= 0 ? _earliest : SafeAdd(DateTimeOffset.UtcNow, delta);
        }

        // https://httpwg.org/http-extensions/draft-ietf-httpbis-rfc6265bis.html#name-cookie-name-prefixes — the
        // two prefixes a name may carry are promises about how the cookie was set, and a cookie that breaks
        // its own promise is ignored rather than downgraded.
        if (name.StartsWith("__Secure-", StringComparison.Ordinal) && !secure)
        {
            return false;
        }

        if (name.StartsWith("__Host-", StringComparison.Ordinal)
            && (!secure || domain is not null || !string.Equals(path, "/", StringComparison.Ordinal)))
        {
            return false;
        }

        cookie = new SetCookie
        {
            Name = name,
            Value = value,
            Domain = domain,
            Path = path,
            Expires = expires,
            Secure = secure,
            HttpOnly = httpOnly,
        };

        return true;
    }

    /// <summary>
    /// Adds <paramref name="seconds"/> without letting a server's absurd <c>Max-Age</c> overflow the
    /// arithmetic; the ceiling is a date no session outlives either way.
    /// </summary>
    private static DateTimeOffset SafeAdd(DateTimeOffset from, long seconds)
    {
        var remaining = (DateTimeOffset.MaxValue - from).TotalSeconds;
        return seconds >= remaining ? DateTimeOffset.MaxValue : from.AddSeconds(seconds);
    }

    /// <summary>
    /// The attribute loop of step 6: each ';'-separated chunk split at its first '=', both halves trimmed.
    /// </summary>
    private static List<(string Name, string Value)> SplitAttributes(string attributes)
    {
        var result = new List<(string, string)>();
        var start = 0;
        while (start <= attributes.Length)
        {
            var end = attributes.IndexOf(';', start);
            if (end < 0)
            {
                end = attributes.Length;
            }

            var chunk = attributes.Substring(start, end - start);
            var equals = chunk.IndexOf('=');
            if (equals < 0)
            {
                var bare = chunk.Trim();
                if (bare.Length != 0)
                {
                    result.Add((bare, string.Empty));
                }
            }
            else
            {
                var name = chunk.Substring(0, equals).Trim();
                if (name.Length != 0)
                {
                    result.Add((name, chunk.Substring(equals + 1).Trim()));
                }
            }

            start = end + 1;
        }

        return result;
    }

    /// <summary>
    /// https://httpwg.org/http-extensions/draft-ietf-httpbis-rfc6265bis.html#name-dates — the deliberately
    /// lenient algorithm, which accepts every shape a real server sends and does not insist on any of them.
    /// </summary>
    internal static bool TryParseCookieDate(string text, out DateTimeOffset value)
    {
        value = default;

        int? hour = null, minute = null, second = null, dayOfMonth = null, month = null, year = null;

        foreach (var token in Tokenize(text))
        {
            if (hour is null && TryParseTime(token, out var h, out var m, out var s))
            {
                hour = h;
                minute = m;
                second = s;
                continue;
            }

            if (dayOfMonth is null && TryParseLeadingNumber(token, 1, 2, out var day) && day is >= 1 and <= 31)
            {
                dayOfMonth = day;
                continue;
            }

            if (month is null && token.Length >= 3)
            {
                var found = -1;
                for (var i = 0; i < _monthNames.Length; i++)
                {
                    if (token.AsSpan(0, 3).Equals(_monthNames[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        found = i + 1;
                        break;
                    }
                }

                if (found > 0)
                {
                    month = found;
                    continue;
                }
            }

            if (year is null && TryParseLeadingNumber(token, 2, 4, out var y))
            {
                year = y;
            }
        }

        if (dayOfMonth is null || month is null || year is null || hour is null)
        {
            return false;
        }

        // "If the year-value is greater than or equal to 70 and less than or equal to 99, increment by 1900.
        //  If the year-value is greater than or equal to 0 and less than or equal to 69, increment by 2000."
        var resolvedYear = year.Value;
        if (resolvedYear is >= 70 and <= 99)
        {
            resolvedYear += 1900;
        }
        else if (resolvedYear <= 69)
        {
            resolvedYear += 2000;
        }

        if (resolvedYear < 1601 || hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        if (dayOfMonth > DateTime.DaysInMonth(resolvedYear, month.Value))
        {
            return false;
        }

        try
        {
            value = new DateTimeOffset(resolvedYear, month.Value, dayOfMonth.Value, hour.Value, minute!.Value, second!.Value, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// The date string split on the specification's delimiter set — <c>%x09</c>, <c>%x20-2F</c>,
    /// <c>%x3B-40</c>, <c>%x5B-60</c> and <c>%x7B-7E</c>, which notably leaves <c>:</c> inside a token and
    /// takes <c>.</c> out of one.
    /// </summary>
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isPart = i < text.Length && !IsDelimiter(text[i]);
            if (isPart)
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                tokens.Add(text.Substring(start, i - start));
                start = -1;
            }
        }

        return tokens;
    }

    private static bool IsDelimiter(char c)
        => c == 0x09
        || (c >= 0x20 && c <= 0x2F)
        || (c >= 0x3B && c <= 0x40)
        || (c >= 0x5B && c <= 0x60)
        || (c >= 0x7B && c <= 0x7E);

    private static bool TryParseTime(string token, out int hour, out int minute, out int second)
    {
        hour = minute = second = 0;

        var first = token.IndexOf(':');
        if (first <= 0)
        {
            return false;
        }

        var rest = token.Substring(first + 1);
        var next = rest.IndexOf(':');
        if (next <= 0)
        {
            return false;
        }

        return TryParseLeadingNumber(token.Substring(0, first), 1, 2, out hour)
            && TryParseLeadingNumber(rest.Substring(0, next), 1, 2, out minute)
            && TryParseLeadingNumber(rest.Substring(next + 1), 1, 2, out second);
    }

    /// <summary>
    /// The leading digits of a token, as the specification's "non-digit" trailing rule allows — <c>2021)</c>
    /// is a year and <c>10x</c> is not a day of month.
    /// </summary>
    private static bool TryParseLeadingNumber(string token, int minimumDigits, int maximumDigits, out int value)
    {
        value = 0;

        var digits = 0;
        while (digits < token.Length && char.IsAsciiDigit(token[digits]))
        {
            digits++;
        }

        if (digits < minimumDigits || digits > maximumDigits)
        {
            return false;
        }

        return int.TryParse(token.AsSpan(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
#endif
