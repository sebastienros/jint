#if NET8_0_OR_GREATER
#nullable enable

using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// Runs the vendored web-platform-tests suites for the URL and Encoding standards, one theory case per
/// <c>.any.js</c> file, under the harness shim in <c>Prelude/testharness-shim.js</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The exclusion table is the point of the driver.</b> A test that does not pass has to be named in
/// <see cref="_exclusions"/> with the category it belongs to, and every entry there must match at least one
/// failing test and no passing one — so an entry that has started passing fails the run, and so does one
/// that matches nothing at all, which is what a renamed or deleted case looks like. That is the test262
/// harness's discipline: a fix cannot leave a permanent exemption behind, and neither can a corpus bump.
/// </para>
/// <para>
/// This is phase 1 of https://github.com/sebastienros/jint/issues/3104: the shim and the first two suites.
/// Failures were expected and found; the ones that are not a missing feature are parked under
/// <see cref="WptDivergence.NeedsTriage"/> rather than fixed here, so that the change which first ran these
/// suites is not also the change that moved the engine.
/// </para>
/// </remarks>
public class WptTestRunner
{
    /// <summary>
    /// Upstream files that are deliberately not vendored, as globs over their path in the wpt tree. Checked
    /// against what <i>is</i> vendored, so a re-vendor that pulls one back in without revisiting the reason
    /// fails rather than silently adding a red suite. <c>Vendor/README.md</c> carries the same table in
    /// prose, with the reason for each.
    /// </summary>
    private static readonly (string Pattern, string Reason)[] _notVendored =
    [
        // idlharness needs /resources/idlharness.js and /resources/WebIDLParser.js — a WebIDL conformance
        // framework of its own, an order of magnitude larger than the shim, and testing a layer Jint's
        // source-generated built-ins do not have.
        ("*/idlharness.any.js", "needs the WebIDL conformance harness"),

        // The legacy multi-byte and single-byte decoders, which a sibling change implements:
        // https://github.com/sebastienros/jint/issues/3106. Two of them also need XMLHttpRequest and
        // data: URLs, through encoding/resources/decoding-helpers.js.
        ("encoding/legacy-mb-*", "legacy multi-byte decoders, issue #3106"),
        ("encoding/iso-2022-jp-decoder.any.js", "legacy multi-byte decoder, issue #3106"),
        ("encoding/single-byte-decoder.any.js", "legacy single-byte decoders, issue #3106"),
        ("encoding/textdecoder-fatal-single-byte.any.js", "legacy single-byte decoders, issue #3106"),
        ("encoding/replacement-encodings.any.js", "the replacement encoding, issue #3106; also needs XMLHttpRequest"),
        ("encoding/unsupported-encodings.any.js", "needs XMLHttpRequest and data: URLs"),

        // IdnaTestV2 is a 314 kB conformance corpus for UTS-46 alone. Jint's Idna builds on IdnMapping and
        // documents where that diverges (VerifyDnsLength, CheckHyphens, ICU version skew); running the
        // corpus is an IDNA triage in its own right rather than part of standing the harness up.
        ("url/IdnaTestV2*.any.js", "UTS-46 conformance corpus, its own triage"),
    ];

    /// <summary>
    /// The lowest number of tests each file must produce. A suite whose corpus stopped being embedded, or
    /// whose registration loop threw after the first case, would otherwise be a green run of nothing. The
    /// figures are the counts observed at the pinned commit, rounded down, so a corpus bump that adds cases
    /// needs no edit and one that halves a file does.
    /// </summary>
    private static readonly Dictionary<string, int> _minimumTests = new(StringComparer.Ordinal)
    {
        ["url/historical.any.js"] = 5,
        ["url/url-constructor.any.js"] = 800,
        ["url/url-origin.any.js"] = 350,
        ["url/url-searchparams.any.js"] = 4,
        ["url/url-setters-stripping.any.js"] = 200,
        ["url/url-setters.any.js"] = 250,
        ["url/url-statics-canparse.any.js"] = 5,
        ["url/url-statics-parse.any.js"] = 5,
        ["url/url-tojson.any.js"] = 1,
        ["url/urlencoded-parser.any.js"] = 90,
        ["url/urlsearchparams-append.any.js"] = 4,
        ["url/urlsearchparams-constructor.any.js"] = 25,
        ["url/urlsearchparams-delete.any.js"] = 6,
        ["url/urlsearchparams-foreach.any.js"] = 5,
        ["url/urlsearchparams-get.any.js"] = 2,
        ["url/urlsearchparams-getall.any.js"] = 2,
        ["url/urlsearchparams-has.any.js"] = 4,
        ["url/urlsearchparams-set.any.js"] = 2,
        ["url/urlsearchparams-size.any.js"] = 3,
        ["url/urlsearchparams-sort.any.js"] = 15,
        ["url/urlsearchparams-stringifier.any.js"] = 12,

        ["encoding/api-basics.any.js"] = 6,
        ["encoding/api-invalid-label.any.js"] = 3000,
        ["encoding/api-replacement-encodings.any.js"] = 5,
        ["encoding/api-surrogates-utf8.any.js"] = 5,
        ["encoding/encodeInto.any.js"] = 100,
        ["encoding/textdecoder-arguments.any.js"] = 4,
        ["encoding/textdecoder-byte-order-marks.any.js"] = 3,
        ["encoding/textdecoder-copy.any.js"] = 2,
        ["encoding/textdecoder-eof.any.js"] = 2,
        ["encoding/textdecoder-fatal-streaming.any.js"] = 2,
        ["encoding/textdecoder-fatal.any.js"] = 30,
        ["encoding/textdecoder-ignorebom.any.js"] = 4,
        ["encoding/textdecoder-labels.any.js"] = 200,
        ["encoding/textdecoder-mistakes.any.js"] = 80,
        ["encoding/textdecoder-streaming.any.js"] = 30,
        ["encoding/textdecoder-utf16-surrogates.any.js"] = 8,
        ["encoding/textencoder-constructor-non-utf.any.js"] = 70,
        ["encoding/textencoder-utf16-surrogates.any.js"] = 6,
    };

    /// <summary>
    /// The tests that do not pass, each with the category it belongs to. See <see cref="WptDivergence"/> for
    /// what the categories mean and which of them are debt.
    /// </summary>
    /// <remarks>
    /// An entry must match at least one failing test and no passing one — see <see cref="RunSuiteFile"/>.
    /// That is what makes a glob safe to write: it cannot quietly widen to cover a case that works, so
    /// <c>* =&gt; windows-1252</c> below says exactly "every label of windows-1252 fails" and stops saying it
    /// the moment one of them stops.
    /// </remarks>
    private static readonly WptExclusion[] _exclusions =
    [
        // ---------------------------------------------------------------- url
        // The second half of the urlencoded parser suite feeds the same corpus through Request.formData()
        // and Response.formData(); the URLSearchParams half of every row passes.
        new("url/urlencoded-parser.any.js", "request.formData() with input: *", WptDivergence.NeedsFetchObjectModel),
        new("url/urlencoded-parser.any.js", "response.formData() with input: *", WptDivergence.NeedsFetchObjectModel),

        // `new URLSearchParams(DOMException)` reads the interface object's own enumerable properties, which
        // are its 25 legacy code constants, and the record conversion keeps [[OwnPropertyKeys]] order. The
        // values are all correct; Jint hands them back in alphabetical order where WebIDL requires the order
        // the constants are declared in (INDEX_SIZE_ERR first, DATA_CLONE_ERR last).
        // https://webidl.spec.whatwg.org/#es-constants
        new("url/urlsearchparams-constructor.any.js", "URLSearchParams constructor, DOMException as argument", WptDivergence.NeedsTriage),

        // ---------------------------------------------------------------- encoding
        // common/sab.js takes its SharedArrayBuffer constructor from WebAssembly.Memory. Note that
        // "Invalid encodeInto() destination: SharedArrayBuffer" is *not* here: it asserts a TypeError, and
        // the helper throws one, so it passes for the wrong reason — which is why these two globs are narrow
        // rather than one over the word.
        new("encoding/encodeInto.any.js", "encodeInto() into SharedArrayBuffer *", WptDivergence.NeedsWebAssembly),
        new("encoding/encodeInto.any.js", "Invalid encodeInto() destination: *, backed by: SharedArrayBuffer", WptDivergence.NeedsWebAssembly),
        new("encoding/textdecoder-copy.any.js", "Modify buffer after passing it in (SharedArrayBuffer)", WptDivergence.NeedsWebAssembly),
        new("encoding/textdecoder-streaming.any.js", "*(SharedArrayBuffer)", WptDivergence.NeedsWebAssembly),

        // https://encoding.spec.whatwg.org/#dom-textdecoder-decode step 1 copies the bytes, and WebIDL
        // converts the options dictionary before the operation runs — so a getter on the dictionary that
        // detaches the buffer leaves an empty byte sequence to decode. Jint reads the bytes after the
        // conversion instead, and decodes the buffer's former contents.
        new("encoding/textdecoder-arguments.any.js", "TextDecoder decode() with array buffer detached during arg conversion", WptDivergence.NeedsTriage),

        // https://encoding.spec.whatwg.org/#shared-utf-16-decoder, the end-of-queue step: "If UTF-16 lead
        // byte is non-null or UTF-16 lead surrogate is non-null, set them to null and return error" — one
        // error however many of the two are pending. Decoding [0x00, 0xd8, 0x00] as utf-16le leaves both
        // pending and must yield a single U+FFFD; Jint yields two.
        new("encoding/textdecoder-mistakes.any.js", "utf-16le does not produce more chars than truncated", WptDivergence.NeedsTriage),
        new("encoding/textdecoder-mistakes.any.js", "utf-16be does not produce more chars than truncated", WptDivergence.NeedsTriage),

        // Everything below is one missing feature: the Encoding Standard's legacy single-byte and multi-byte
        // decoders, which EncodingLabels documents as out of scope and issue #3106 implements. Each of these
        // fails with "the encoding label provided ('…') is invalid" and nothing else.
        new("encoding/textdecoder-eof.any.js", "TextDecoder end-of-queue handling", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-eof.any.js", "TextDecoder end-of-queue handling using stream: true", WptDivergence.NeedsLegacyEncodings),

        new("encoding/textdecoder-labels.any.js", "* => Big5", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => EUC-JP", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => EUC-KR", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => GBK", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => IBM866", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-2022-JP", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-10", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-13", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-14", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-15", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-16", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-2", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-3", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-4", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-5", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-6", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-7", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-8", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => ISO-8859-8-I", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => KOI8-R", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => KOI8-U", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => Shift_JIS", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => gb18030", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => macintosh", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1250", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1251", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1252", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1253", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1254", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1255", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1256", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1257", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-1258", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => windows-874", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => x-mac-cyrillic", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-labels.any.js", "* => x-user-defined", WptDivergence.NeedsLegacyEncodings),

        new("encoding/textdecoder-mistakes.any.js", "Single-byte encodings are ASCII supersets: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Fast path misdetection: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "windows-1252 maps bytes outside of latin1: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "windows-1252 does not contain unmapped chars: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "specific: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "selected single-byte: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "WPT mislabels: *", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "iso-8859-8-i decodes bytes the same way as iso-8859-8", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Concatenating two ISO-2022-JP outputs is not always valid", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gb18030 version and ranges", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gbk version and ranges", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gbk decoder is gb18030 decoder", WptDivergence.NeedsLegacyEncodings),
        // Named one at a time rather than as "stream: *": the utf-8 row of that family passes, and an
        // exclusion is not allowed to cover a test that works.
        new("encoding/textdecoder-mistakes.any.js", "stream: gbk", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: gb18030", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: big5", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: shift_jis", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: euc-kr", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: euc-jp", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: iso-2022-jp", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textdecoder-mistakes.any.js", "fatal stream: iso-2022-jp", WptDivergence.NeedsLegacyEncodings),

        // The encoder half of every row here passes: a TextEncoder ignores its argument and is always utf-8.
        // Only "supported for decode" is excluded, and the utf-8, utf-16le and utf-16be rows of that half
        // pass too, which is why the 36 names are spelled out rather than globbed.
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: IBM866", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-2", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-3", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-4", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-5", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-6", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-7", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-8", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-8-I", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-10", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-13", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-14", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-15", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-8859-16", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: KOI8-R", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: KOI8-U", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: macintosh", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-874", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1250", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1251", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1252", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1253", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1254", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1255", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1256", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1257", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: windows-1258", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: x-mac-cyrillic", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: GBK", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: gb18030", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: Big5", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: EUC-JP", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-2022-JP", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: Shift_JIS", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: EUC-KR", WptDivergence.NeedsLegacyEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: x-user-defined", WptDivergence.NeedsLegacyEncodings),
    ];

    public static IEnumerable<object[]> UrlSuiteFiles() => Cases("url");

    public static IEnumerable<object[]> EncodingSuiteFiles() => Cases("encoding");

    [Theory]
    [MemberData(nameof(UrlSuiteFiles))]
    public void RunsTheUrlSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(EncodingSuiteFiles))]
    public void RunsTheEncodingSuite(string file) => RunSuiteFile(file);

    /// <summary>
    /// The inventory check: what is vendored, what is run, and what is deliberately absent must all agree.
    /// </summary>
    /// <remarks>
    /// This is what a re-vendor runs into. A suite file that arrives without a minimum-test entry would
    /// otherwise be embedded and never run — a theory case is generated per file, but the file has to be
    /// declared to be held to anything — and one that arrives against a <see cref="_notVendored"/> reason
    /// would quietly go red for a cause somebody already decided about.
    /// </remarks>
    [Fact]
    public void EveryVendoredFileIsAccountedFor()
    {
        var problems = new List<string>();

        foreach (var path in WptCorpus.Paths)
        {
            // Checked over every vendored path, not only over the two suites' own directories, so that a
            // pattern naming a directory (encoding/legacy-mb-*) is checked by something.
            foreach (var (pattern, reason) in _notVendored)
            {
                if (WptExclusion.MatchesPattern(pattern, path))
                {
                    problems.Add($"{path} is vendored although \"{pattern}\" says it should not be ({reason})");
                }
            }

            if (path.EndsWith(".any.js", StringComparison.Ordinal) && !_minimumTests.ContainsKey(path))
            {
                problems.Add($"{path} is vendored but has no entry in the minimum-test table, so nothing runs it");
            }
        }

        foreach (var declared in _minimumTests.Keys)
        {
            if (!WptCorpus.Contains(declared))
            {
                problems.Add($"{declared} has a minimum-test entry but is not vendored");
            }
        }

        foreach (var exclusion in _exclusions)
        {
            if (!WptCorpus.Contains(exclusion.File))
            {
                problems.Add($"{exclusion.File} carries an exclusion but is not vendored");
            }
        }

        // The theory cases are generated from the corpus, so an empty corpus would be an empty, green run.
        WptCorpus.TestFiles("url").Should().HaveCountGreaterThan(15);
        WptCorpus.TestFiles("encoding").Should().HaveCountGreaterThan(15);

        string.Join(Environment.NewLine, problems).Should().BeEmpty();
    }

    [Fact]
    public void TheHarnessShimAndItsHelpersAreEmbedded()
    {
        WptCorpus.Prelude.Should().Contain("__wpt", "the driver reads its results back through that object");

        // The two suites' `// META: script=` lines name these; a build that stopped embedding one would
        // otherwise show up as every file in a suite failing for an unrelated-looking reason.
        foreach (var helper in new[]
                 {
                     "common/subset-tests.js",
                     "common/subset-tests-by-key.js",
                     "common/sab.js",
                     "encoding/resources/encodings.js",
                     "url/resources/urltestdata.json",
                     "url/resources/urltestdata-javascript-only.json",
                     "url/resources/setters_tests.json",
                 })
        {
            WptCorpus.Contains(helper).Should().BeTrue($"{helper} must be embedded");
        }
    }

    private static IEnumerable<object[]> Cases(string suite)
    {
        foreach (var file in WptCorpus.TestFiles(suite))
        {
            yield return [file];
        }
    }

    /// <summary>
    /// Runs one file and holds it to two rules. Every failing test must be named by an exclusion, and every
    /// exclusion must match at least one failing test and no passing one.
    /// </summary>
    /// <remarks>
    /// The second rule is the whole reason a glob is safe to write here. An entry that has stopped applying
    /// — because the engine was fixed, or because a corpus bump renamed or removed the case — fails the run
    /// instead of sitting there forever, and one that would widen to cover a test that works fails for that
    /// too, so <c>*</c> can never turn an exclusion into a blanket.
    /// </remarks>
    private static void RunSuiteFile(string file)
    {
        var outcome = WptHarness.Run(file);

        outcome.HarnessError.Should().BeNull($"{file} must run to completion");

        var exclusions = Array.FindAll(_exclusions, e => string.Equals(e.File, file, StringComparison.Ordinal));
        var matchedFailing = new bool[exclusions.Length];
        var matchedPassing = new List<string>?[exclusions.Length];
        var failures = new List<string>();

        foreach (var result in outcome.Results)
        {
            var excluded = false;
            for (var i = 0; i < exclusions.Length; i++)
            {
                if (!exclusions[i].Matches(result.Name))
                {
                    continue;
                }

                excluded = true;
                if (result.Passed)
                {
                    (matchedPassing[i] ??= []).Add(result.Name);
                }
                else
                {
                    matchedFailing[i] = true;
                }
            }

            if (!result.Passed && !excluded)
            {
                failures.Add($"[{result.Status}] {result.Name}: {result.Message}");
            }
        }

        var stale = new List<string>();
        for (var i = 0; i < exclusions.Length; i++)
        {
            if (matchedPassing[i] is { } passing)
            {
                stale.Add($"\"{exclusions[i].TestName}\" ({exclusions[i].Divergence}) covers {passing.Count} test(s) that pass, "
                    + $"the first being \"{passing[0]}\"");
            }
            else if (!matchedFailing[i])
            {
                stale.Add($"\"{exclusions[i].TestName}\" ({exclusions[i].Divergence}) matches no test in the file");
            }
        }

        // Before the failure and staleness reports, because a file that produced nothing would otherwise be
        // reported as a wall of exclusions that match no test rather than as the empty run it is.
        outcome.Results.Count.Should().BeGreaterThanOrEqualTo(
            _minimumTests[file],
            $"{file} must actually have run its corpus");

        Report(file, failures, stale);
    }

    private static void Report(string file, List<string> failures, List<string> stale)
    {
        if (failures.Count == 0 && stale.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.Append(file).AppendLine(":");

        if (stale.Count > 0)
        {
            message.Append("  ").Append(stale.Count)
                .AppendLine(" exclusion(s) that no longer apply — remove or narrow them:");
            foreach (var entry in stale)
            {
                message.Append("    ").AppendLine(entry);
            }
        }

        if (failures.Count > 0)
        {
            message.Append("  ").Append(failures.Count)
                .AppendLine(" failing test(s) — fix them, or add them to the exclusion table with a category:");
            foreach (var entry in failures)
            {
                message.Append("    ").AppendLine(entry);
            }
        }

        message.ToString().Should().BeEmpty();
    }
}
#endif
