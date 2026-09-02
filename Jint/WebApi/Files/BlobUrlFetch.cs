#if NET8_0_OR_GREATER
using System.Globalization;
using System.Runtime.InteropServices;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.Files;

/// <summary>
/// The response a fetch of a <c>blob:</c> URL produces: a status, a header list and the bytes, already sliced
/// if a <c>Range</c> asked for part of them.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct BlobUrlResponse(int Status, string StatusText, List<HeaderEntry> Headers, ReadOnlyMemory<byte> Body);

/// <summary>
/// The <c>blob</c> arm of <i>scheme fetch</i>: how a <c>blob:</c> URL becomes a response without a socket.
/// <para>
/// https://fetch.spec.whatwg.org/#scheme-fetch
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>It is answered before every network check, and that is the algorithm's own order.</b> Scheme fetch
/// dispatches on the URL's scheme and reaches HTTP fetch only for <c>http</c> and <c>https</c>, so
/// <c>Options.WebApi.Fetch.AllowedSchemes</c>, the host's <c>UrlFilter</c>, the concurrency cap and the
/// <c>HttpClient</c> are all beside the point here: a blob URL names bytes this engine's own script created,
/// and answering it opens nothing and reaches nowhere. Both <c>fetch</c> and <c>XMLHttpRequest</c> take this
/// path, which is what makes <c>xhr.open('GET', URL.createObjectURL(blob))</c> work on an engine with no
/// network grant at all.
/// </para>
/// <para>
/// <b>Only <c>GET</c>.</b> Step 2 of the arm returns a network error for any other method, which is what
/// <c>FileAPI/url/resources/fetch-tests.js</c> checks for six of them.
/// </para>
/// </remarks>
internal static class BlobUrlFetch
{
    /// <summary>
    /// Builds the response, or returns <see langword="false"/> for the two failures the arm names: a method
    /// other than <c>GET</c>, and a <c>Range</c> the grammar or the blob's own length refuses.
    /// </summary>
    /// <param name="blob">The blob the URL resolved to.</param>
    /// <param name="method">The request's method, already normalized.</param>
    /// <param name="rangeHeader">The request's <c>Range</c> header, or <see langword="null"/> when it has none.</param>
    /// <param name="response">The response to answer with.</param>
    internal static bool TryBuild(JsBlob blob, string method, string? rangeHeader, out BlobUrlResponse response)
    {
        response = default;

        if (!string.Equals(method, "GET", StringComparison.Ordinal))
        {
            return false;
        }

        var full = blob.Data;
        var type = blob.MediaType;

        if (rangeHeader is null)
        {
            // Steps 9.1 to 9.4: the whole blob, with the two headers the arm names and no others.
            response = new BlobUrlResponse(
                200,
                "OK",
                [
                    new HeaderEntry("content-length", full.Length.ToString(CultureInfo.InvariantCulture)),
                    new HeaderEntry("content-type", type),
                ],
                full);

            return true;
        }

        if (!TryParseSingleRange(rangeHeader, out var start, out var end))
        {
            return false;
        }

        long rangeStart;
        long rangeEnd;

        if (start is null)
        {
            // Step 10.6: a suffix range — the last `end` bytes.
            rangeStart = full.Length - end!.Value;
            rangeEnd = rangeStart + end.Value - 1;

            // A suffix longer than the blob is not one of the arm's failures; it names the whole blob.
            if (rangeStart < 0)
            {
                rangeStart = 0;
                rangeEnd = full.Length - 1;
            }
        }
        else
        {
            // Step 10.7.
            rangeStart = start.Value;
            if (rangeStart >= full.Length)
            {
                return false;
            }

            rangeEnd = end is null || end.Value >= full.Length ? full.Length - 1 : end.Value;
        }

        // Step 10.8: "invoke slice blob given rangeStart, rangeEnd + 1, and type".
        var sliced = full.Slice((int) rangeStart, (int) (rangeEnd - rangeStart + 1));

        // Steps 10.12 to 10.15.
        var contentRange = string.Create(
            CultureInfo.InvariantCulture,
            $"bytes {rangeStart}-{rangeEnd}/{full.Length}");

        response = new BlobUrlResponse(
            206,
            "Partial Content",
            [
                new HeaderEntry("content-length", sliced.Length.ToString(CultureInfo.InvariantCulture)),
                new HeaderEntry("content-type", type),
                new HeaderEntry("content-range", contentRange),
            ],
            sliced);

        return true;
    }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#simple-range-header-value — "parse a single range header value" with
    /// <i>allowWhitespace</i> true, which is what the blob arm passes.
    /// </summary>
    /// <remarks>
    /// Whitespace is allowed in five places and nowhere else, and the failure cases are as load-bearing as
    /// the successes: a trailing comma, a second range, a start that is not a digit, a missing hyphen and a
    /// start greater than the end are each a network error rather than a whole-blob response.
    /// <c>xhr/blob-range.any.js</c> is seventeen rows of exactly those.
    /// </remarks>
    private static bool TryParseSingleRange(string value, out long? rangeStart, out long? rangeEnd)
    {
        rangeStart = null;
        rangeEnd = null;

        if (!value.StartsWith("bytes", StringComparison.Ordinal))
        {
            return false;
        }

        var position = 5;

        SkipWhitespace(value, ref position);

        if (position >= value.Length || value[position] != '=')
        {
            return false;
        }

        position++;
        SkipWhitespace(value, ref position);

        var start = CollectDigits(value, ref position);
        SkipWhitespace(value, ref position);

        if (position >= value.Length || value[position] != '-')
        {
            return false;
        }

        position++;
        SkipWhitespace(value, ref position);

        var end = CollectDigits(value, ref position);

        // "If position is not past the end of data, then return failure" — which is what refuses a comma and
        // therefore a second range.
        if (position != value.Length)
        {
            return false;
        }

        if (start is null && end is null)
        {
            return false;
        }

        if (start is not null && end is not null && start > end)
        {
            return false;
        }

        rangeStart = start;
        rangeEnd = end;
        return true;
    }

    /// <summary>HTTP tab or space, https://fetch.spec.whatwg.org/#http-tab-or-space.</summary>
    private static void SkipWhitespace(string value, ref int position)
    {
        while (position < value.Length && value[position] is '\t' or ' ')
        {
            position++;
        }
    }

    /// <summary>
    /// A run of ASCII digits "interpreted as decimal number", or <see langword="null"/> for none. A value too
    /// large for a <see cref="long"/> saturates rather than failing: the only thing it is ever compared
    /// against is a blob's length, so <c>bytes=4-100000000000</c> has to mean "to the end" and not "refused".
    /// </summary>
    private static long? CollectDigits(string value, ref int position)
    {
        var start = position;
        long collected = 0;
        var saturated = false;

        while (position < value.Length && value[position] is >= '0' and <= '9')
        {
            if (!saturated)
            {
                if (collected > (long.MaxValue - (value[position] - '0')) / 10)
                {
                    saturated = true;
                    collected = long.MaxValue;
                }
                else
                {
                    collected = (collected * 10) + (value[position] - '0');
                }
            }

            position++;
        }

        return position == start ? null : collected;
    }
}
#endif
