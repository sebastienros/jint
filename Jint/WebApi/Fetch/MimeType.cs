#if NET8_0_OR_GREATER
using System.Text;

namespace Jint.WebApi.Fetch;

/// <summary>
/// A MIME type record, https://mimesniff.spec.whatwg.org/#mime-type — an essence plus the parameters the
/// <c>Content-Type</c> header carried.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately engine-free, like <see cref="HeaderList"/> beside it, and deliberately minimal: the only
/// consumer is <c>formData()</c>, which needs the essence to choose a parser and the <c>boundary</c>
/// parameter to run it. Nothing here serializes a MIME type — the one place that would want to,
/// <c>blob()</c>, keeps the File API's own media-type normalization it already had.
/// </para>
/// <para>
/// The parameters are a list rather than a dictionary because there are one or two of them and the
/// specification's "does not exist" check is the only lookup: the first occurrence of a name wins and every
/// later one is dropped.
/// </para>
/// </remarks>
internal sealed class MimeType
{
    private readonly List<KeyValuePair<string, string>> _parameters = new();

    private MimeType(string type, string subtype)
    {
        Essence = type + "/" + subtype;
    }

    /// <summary>
    /// https://mimesniff.spec.whatwg.org/#mime-type-essence — the type, U+002F (/) and the subtype, both
    /// already ASCII-lowercased.
    /// </summary>
    internal string Essence { get; }

    /// <summary>
    /// The value of the parameter named <paramref name="name"/>, which must already be lowercased, or
    /// <see langword="null"/> when the record carries none.
    /// </summary>
    internal string? GetParameter(string name)
    {
        foreach (var parameter in _parameters)
        {
            if (string.Equals(parameter.Key, name, StringComparison.Ordinal))
            {
                return parameter.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets the parameter named <paramref name="name"/>, which must already be lowercased, replacing any
    /// value it already had.
    /// </summary>
    /// <remarks>
    /// The one mutation the record allows, and it exists for one algorithm: <c>XMLHttpRequest</c>'s
    /// <c>send()</c> rewrites an author <c>Content-Type</c> whose charset is not UTF-8 —
    /// https://xhr.spec.whatwg.org/#dom-xmlhttprequest-send step 5.
    /// </remarks>
    internal void SetParameter(string name, string value)
    {
        for (var i = 0; i < _parameters.Count; i++)
        {
            if (string.Equals(_parameters[i].Key, name, StringComparison.Ordinal))
            {
                _parameters[i] = new KeyValuePair<string, string>(name, value);
                return;
            }
        }

        _parameters.Add(new KeyValuePair<string, string>(name, value));
    }

    /// <summary>
    /// Serialize a MIME type, https://mimesniff.spec.whatwg.org/#serializing-a-mime-type — the essence
    /// followed by each parameter, quoting a value that is not an HTTP token.
    /// </summary>
    internal string Serialize()
    {
        if (_parameters.Count == 0)
        {
            return Essence;
        }

        var builder = new System.Text.StringBuilder(Essence);
        foreach (var parameter in _parameters)
        {
            builder.Append(';').Append(parameter.Key).Append('=');

            var value = parameter.Value;
            if (IsToken(value))
            {
                builder.Append(value);
                continue;
            }

            builder.Append('"');
            foreach (var c in value)
            {
                if (c is '"' or '\\')
                {
                    builder.Append('\\');
                }

                builder.Append(c);
            }

            builder.Append('"');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether every code point is an RFC 9110 <c>tchar</c> and the value is non-empty, which is what
    /// decides between the bare and the quoted serialization of a parameter value.
    /// </summary>
    private static bool IsToken(string value) => HeaderList.IsName(value);

    /// <summary>
    /// Parse a MIME type, https://mimesniff.spec.whatwg.org/#parsing-a-mime-type — <see langword="null"/> is
    /// the specification's failure.
    /// </summary>
    internal static MimeType? Parse(string input)
    {
        // Step 1: remove any leading and trailing HTTP whitespace.
        input = HeaderList.Normalize(input);

        var position = 0;

        // "If type is the empty string or does not solely contain HTTP token code points, return failure" —
        // which is exactly the header-name production, so the same predicate answers both.
        var type = CollectUntil(input, ref position, '/');
        if (!HeaderList.IsName(type) || position >= input.Length)
        {
            return null;
        }

        // Skip past the U+002F (/).
        position++;

        var subtype = TrimEndHttpWhitespace(CollectUntil(input, ref position, ';'));
        if (!HeaderList.IsName(subtype))
        {
            return null;
        }

        var mimeType = new MimeType(type.ToLowerInvariant(), subtype.ToLowerInvariant());

        while (position < input.Length)
        {
            // Skip past the U+003B (;), then any HTTP whitespace.
            position++;
            while (position < input.Length && HeaderList.IsHttpWhitespace(input[position]))
            {
                position++;
            }

            var parameterName = CollectUntilAny(input, ref position, ';', '=').ToLowerInvariant();

            if (position < input.Length)
            {
                if (input[position] == ';')
                {
                    // A parameter with no value at all; the next iteration skips past that same U+003B (;).
                    continue;
                }

                // Skip past the U+003D (=).
                position++;
            }

            if (position >= input.Length)
            {
                break;
            }

            string parameterValue;
            if (input[position] == '"')
            {
                parameterValue = CollectQuotedString(input, ref position);

                // `text/html;charset="shift_jis"iso-2022-jp` ends up as `text/html;charset=shift_jis`.
                CollectUntil(input, ref position, ';');
            }
            else
            {
                parameterValue = TrimEndHttpWhitespace(CollectUntil(input, ref position, ';'));
                if (parameterValue.Length == 0)
                {
                    continue;
                }
            }

            if (HeaderList.IsName(parameterName)
                && IsQuotedStringToken(parameterValue)
                && mimeType.GetParameter(parameterName) is null)
            {
                mimeType._parameters.Add(new KeyValuePair<string, string>(parameterName, parameterValue));
            }
        }

        return mimeType;
    }

    private static string CollectUntil(string input, ref int position, char stop)
    {
        var start = position;
        while (position < input.Length && input[position] != stop)
        {
            position++;
        }

        return input.Substring(start, position - start);
    }

    private static string CollectUntilAny(string input, ref int position, char first, char second)
    {
        var start = position;
        while (position < input.Length && input[position] != first && input[position] != second)
        {
            position++;
        }

        return input.Substring(start, position - start);
    }

    /// <summary>
    /// Collect an HTTP quoted string, https://fetch.spec.whatwg.org/#collect-an-http-quoted-string, with
    /// <i>extractValue</i> true — so the value comes back unquoted and with its backslash escapes resolved.
    /// </summary>
    private static string CollectQuotedString(string input, ref int position)
    {
        // The caller has established that the code point at position is U+0022 (").
        position++;

        var value = new StringBuilder();
        while (true)
        {
            while (position < input.Length && input[position] != '"' && input[position] != '\\')
            {
                value.Append(input[position]);
                position++;
            }

            if (position >= input.Length)
            {
                break;
            }

            var quoteOrBackslash = input[position];
            position++;

            if (quoteOrBackslash != '\\')
            {
                break;
            }

            if (position >= input.Length)
            {
                value.Append('\\');
                break;
            }

            value.Append(input[position]);
            position++;
        }

        return value.ToString();
    }

    /// <summary>https://fetch.spec.whatwg.org/#http-whitespace, from the end only.</summary>
    private static string TrimEndHttpWhitespace(string value) => value.TrimEnd('\t', '\n', '\r', ' ');

    /// <summary>
    /// https://fetch.spec.whatwg.org/#http-quoted-string-token-code-point — tab, and everything from U+0020
    /// to U+007E and from U+0080 to U+00FF.
    /// </summary>
    private static bool IsQuotedStringToken(string value)
    {
        foreach (var c in value)
        {
            if (c == '\t')
            {
                continue;
            }

            if (c is < ' ' or '\u007F' or > '\u00FF')
            {
                return false;
            }
        }

        return true;
    }
}
#endif
