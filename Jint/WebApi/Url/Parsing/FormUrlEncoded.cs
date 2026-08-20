#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using System.Text;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// One name/value tuple of an application/x-www-form-urlencoded list,
/// https://url.spec.whatwg.org/#concept-urlsearchparams-list.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct FormUrlEncodedEntry(string Name, string Value);

/// <summary>
/// The application/x-www-form-urlencoded parser and serializer,
/// https://url.spec.whatwg.org/#application/x-www-form-urlencoded.
/// </summary>
/// <remarks>
/// Only UTF-8 is supported, which is the only conforming encoding: the spec's own note says the legacy
/// server-side <c>_charset</c> logic "is not described here as only UTF-8 is conforming".
/// </remarks>
internal static class FormUrlEncoded
{
    /// <summary>
    /// The application/x-www-form-urlencoded string parser,
    /// https://url.spec.whatwg.org/#application/x-www-form-urlencoded/string-parser — UTF-8 encodes the input
    /// and runs the byte-sequence parser over it.
    /// </summary>
    internal static List<FormUrlEncodedEntry> Parse(string input)
        => Parse(SystemEncoding.UTF8.GetBytes(input));

    /// <summary>
    /// The application/x-www-form-urlencoded parser,
    /// https://url.spec.whatwg.org/#concept-urlencoded-parser — the byte-sequence parser the string parser
    /// above is defined in terms of, and what a request body reaches directly.
    /// </summary>
    internal static List<FormUrlEncodedEntry> Parse(byte[] bytes)
    {
        var output = new List<FormUrlEncodedEntry>();
        if (bytes.Length == 0)
        {
            return output;
        }

        var offset = 0;
        while (offset <= bytes.Length)
        {
            var end = Array.IndexOf(bytes, (byte) '&', offset);
            if (end < 0)
            {
                end = bytes.Length;
            }

            var sequence = bytes.AsSpan(offset, end - offset);
            offset = end + 1;

            if (sequence.Length == 0)
            {
                continue;
            }

            var equals = sequence.IndexOf((byte) '=');
            var name = equals < 0 ? sequence : sequence.Slice(0, equals);
            var value = equals < 0 ? default : sequence.Slice(equals + 1);

            output.Add(new FormUrlEncodedEntry(Decode(name), Decode(value)));
        }

        return output;
    }

    /// <summary>
    /// The application/x-www-form-urlencoded serializer,
    /// https://url.spec.whatwg.org/#concept-urlencoded-serializer.
    /// </summary>
    internal static string Serialize(List<FormUrlEncodedEntry> entries)
    {
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('&');
                }

                var entry = entries[i];
                PercentEncoding.Append(ref builder, entry.Name.AsSpan(), PercentEncodeSet.FormUrlEncoded, spaceAsPlus: true);
                builder.Append('=');
                PercentEncoding.Append(ref builder, entry.Value.AsSpan(), PercentEncodeSet.FormUrlEncoded, spaceAsPlus: true);
            }

            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// One component of a tuple: 0x2B (+) becomes a space, then the bytes are percent-decoded and read back as
    /// UTF-8.
    /// </summary>
    private static string Decode(ReadOnlySpan<byte> component)
    {
        if (component.Length == 0)
        {
            return string.Empty;
        }

        var bytes = component.ToArray();
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] == (byte) '+')
            {
                bytes[i] = (byte) ' ';
            }
        }

        return SystemEncoding.UTF8.GetString(PercentEncoding.Decode(bytes));
    }
}
#endif
