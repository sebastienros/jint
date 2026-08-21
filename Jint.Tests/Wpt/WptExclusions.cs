#if NET8_0_OR_GREATER
#nullable enable

namespace Jint.Tests.Wpt;

/// <summary>
/// Why a vendored web-platform-test does not pass. Every exclusion carries one, so the table reads as an
/// inventory of what is missing rather than as a list of things that are merely red.
/// </summary>
internal enum WptDivergence
{
    /// <summary>
    /// The test needs one of the Encoding Standard's legacy single-byte or multi-byte decoders
    /// (<c>windows-1252</c>, <c>shift_jis</c>, <c>gbk</c>, <c>replacement</c>, …). Jint implements
    /// <c>utf-8</c>, <c>utf-16le</c> and <c>utf-16be</c> and reports every other label as a failure, which
    /// is what <c>EncodingLabels</c> documents. Tracked by
    /// https://github.com/sebastienros/jint/issues/3106; the change that lands those decoders removes these
    /// entries.
    /// </summary>
    /// <summary>
    /// The seven legacy multi-byte encodings (Big5, EUC-JP, EUC-KR, GBK, gb18030, ISO-2022-JP, Shift_JIS)
    /// are named by the label table and refused as unsupported; their suites stay red until someone demands
    /// the tables. The single-byte families these entries used to cover are implemented and green.
    /// </summary>
    NeedsLegacyMultiByteEncodings,

    /// <summary>
    /// The test obtains its <c>SharedArrayBuffer</c> constructor through <c>WebAssembly.Memory</c>, which is
    /// what <c>common/sab.js</c> does — deliberately, so that a browser gated by cross-origin isolation
    /// still gets one. Jint has <c>SharedArrayBuffer</c> but no <c>WebAssembly</c>, so the helper hands back
    /// <see langword="null"/> and every SAB-backed case of the file fails in the helper rather than in the
    /// code under test. WebAssembly is out of scope for an interpreter, so this is the corpus meeting an
    /// environment it was not written for rather than a gap to close.
    /// </summary>
    NeedsWebAssembly,

    /// <summary>
    /// The test reaches for <c>Request</c> or <c>Response</c>. Those are the fetch object model, which lands
    /// with the fetch feature; <c>WebApiFeatures.Default</c> deliberately never includes it, and this driver
    /// enables nothing else. The suites keep these cases beside the <c>URLSearchParams</c> ones because a
    /// browser parses <c>application/x-www-form-urlencoded</c> in both places with the same algorithm.
    /// </summary>
    NeedsFetchObjectModel,

    /// <summary>
    /// The test detaches a buffer by posting it through a <c>MessageChannel</c>. Message ports are a worker
    /// primitive and Jint has no worker story, so this is the corpus meeting an environment it was not
    /// written for rather than a gap to close.
    /// </summary>
    NeedsMessageChannel,

    /// <summary>
    /// A genuine failure that is not attributable to a feature Jint has decided not to have. Every entry
    /// here is a bug or a specification detail to chase, and the phase of the harness work that stood the
    /// suites up deliberately recorded them rather than fixing them: the point was to find out what they
    /// say, and mixing engine fixes into the change that first ran them would have hidden which of the two
    /// moved. The four it recorded — WebIDL constant order, <c>TextDecoder.decode()</c> reading its input
    /// before the options dictionary was converted, and the shared UTF-16 decoder's end-of-queue step for
    /// both endiannesses — were fixed by https://github.com/sebastienros/jint/issues/3121, so nothing is
    /// filed here today.
    /// </summary>
    NeedsTriage,
}

/// <summary>
/// One excluded test: a file, the test's name or a glob over it, and why.
/// </summary>
/// <param name="File">The suite file, as a path in the vendored tree (<c>url/historical.any.js</c>).</param>
/// <param name="TestName">
/// The exact name the suite gives the test, or — when the name embeds data and a whole family diverges for
/// one reason — a glob in which <c>*</c> matches any run of characters. A glob keeps a table of two hundred
/// mechanically generated names readable, and it is safe because the driver holds every entry to the same
/// rule: it must match at least one failing test and no passing one, so a glob can never widen into a
/// blanket over cases that work.
/// </param>
/// <param name="Divergence">Which category of not-passing this is.</param>
internal sealed record WptExclusion(string File, string TestName, WptDivergence Divergence)
{
    internal bool Matches(string testName) => MatchesPattern(TestName, testName);

    /// <summary>
    /// Whether <paramref name="value"/> is what <paramref name="pattern"/> names: an ordinal match, unless
    /// the pattern carries a <c>*</c>, which stands for any run of characters. Also what the not-vendored
    /// table is checked with, since that is the same question asked about a path.
    /// </summary>
    /// <remarks>
    /// Iterative rather than recursive, so a pattern with several stars cannot blow the stack on a long
    /// name — the URL corpus builds test names out of its inputs and some of those are long.
    /// </remarks>
    internal static bool MatchesPattern(string pattern, string value)
    {
        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(pattern, value, StringComparison.Ordinal);
        }

        int p = 0, v = 0, starPattern = -1, starValue = 0;

        while (v < value.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                starPattern = p++;
                starValue = v;
            }
            else if (p < pattern.Length && pattern[p] == value[v])
            {
                p++;
                v++;
            }
            else if (starPattern >= 0)
            {
                p = starPattern + 1;
                v = ++starValue;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
#endif
