#if NET8_0_OR_GREATER
#nullable enable

using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// Runs the vendored web-platform-tests suites for the URL, Encoding and Web Cryptography standards, one
/// theory case per <c>.any.js</c> file, under the harness shim in <c>Prelude/testharness-shim.js</c>.
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
/// This is https://github.com/sebastienros/jint/issues/3104: the shim, then a corpus at a time. Failures are
/// expected and found; the ones that are not a missing feature are parked under
/// <see cref="WptDivergence.NeedsTriage"/> rather than fixed there, so that the change which first runs a
/// suite is not also the change that moves the engine. The four the URL and Encoding suites found were fixed
/// by https://github.com/sebastienros/jint/issues/3121; the two the WebCryptoAPI corpus found — the point at
/// which <c>SubtleCrypto</c> copies its caller's bytes, and ECDH's mismatched-curve error — are open, and the
/// category's own documentation says what they are.
/// </para>
/// <para>
/// <b>The WebCryptoAPI corpus is one suite per directory</b> rather than one for the lot, because
/// <see cref="WptCorpus.TestFiles"/> lists a directory's own files and never descends. That is deliberate:
/// a suite is a theory, a theory is a line in a test report, and eight of them tell a reader which operation
/// went red where one would not.
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
        // source-generated built-ins do not have. The star before ".any.js" is what reaches WebCryptoAPI's
        // "idlharness.https.any.js" and "idlharness.tentative.https.any.js" as well as the bare spelling the
        // url and encoding suites use.
        ("*/idlharness*.any.js", "needs the WebIDL conformance harness"),

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

        // ---------------------------------------------------------------- WebCryptoAPI
        // Upstream marks a file ".tentative" when it tests a proposal the specification has not adopted —
        // ML-KEM, ML-DSA, KMAC, cSHAKE, SHA-3, TurboSHAKE, KangarooTwelve, AES-OCB, ChaCha20-Poly1305,
        // Argon2, Ed448/X448. Jint registers what https://w3c.github.io/webcrypto/#algorithm-overview lists
        // and nothing else, so every one of these would be a red file saying only that.
        ("*.tentative.https.any.js", "tests a proposal the specification has not adopted"),

        // Curve25519. The BCL ships neither X25519 nor Ed25519 — there is no ECCurve for them and no
        // primitive to build on — so the files dedicated to those curves are out of scope for a crypto layer
        // written against it. The rows that sit inside a file which is otherwise about something else are
        // excluded one by one instead, under WptDivergence.NeedsCurve25519.
        ("*_Ed25519.https.any.js", "Ed25519: the BCL ships no Curve25519 primitive"),
        ("*_X25519.https.any.js", "X25519: the BCL ships no Curve25519 primitive"),
        ("*/eddsa*", "Ed25519 and Ed448 signatures"),
        ("*/cfrg_curves*", "X25519 and X448 key agreement"),
        ("*/okp_importKey*", "Ed25519/X25519/Ed448/X448 key import"),
        ("*/argon2*", "Argon2, a proposal"),

        // Whole directories, each for one reason. `serialization/` round-trips a CryptoKey through
        // structuredClone, which is the HTML serialization steps for a platform object rather than anything
        // crypto.subtle does; `encap_decap/` is the ML-KEM proposal; `secure_context/` is a .sub.html test
        // for a browsing context; `crashtests/` are regression reproductions rather than assertions; and
        // `tools/` is the corpus's own Python generator.
        ("WebCryptoAPI/serialization/*", "structured-clone of a CryptoKey, not a crypto.subtle operation"),
        ("WebCryptoAPI/encap_decap/*", "ML-KEM key encapsulation, a proposal"),
        ("WebCryptoAPI/secure_context/*", "a .sub.html test for a browsing context"),
        ("WebCryptoAPI/import_export/crashtests/*", "a crashtest rather than an assertion"),
        ("WebCryptoAPI/tools/*", "the corpus's own generator, not a test"),
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

        ["WebCryptoAPI/crypto_key_cached_slots.https.any.js"] = 2,
        ["WebCryptoAPI/getRandomValues.any.js"] = 30,
        ["WebCryptoAPI/historical.any.js"] = 3,
        ["WebCryptoAPI/normalize-algorithm-name.https.any.js"] = 4,
        ["WebCryptoAPI/randomUUID.https.any.js"] = 3,

        ["WebCryptoAPI/derive_bits_keys/derive_key_and_encrypt.https.any.js"] = 5,
        ["WebCryptoAPI/derive_bits_keys/derived_bits_length.https.any.js"] = 40,
        ["WebCryptoAPI/derive_bits_keys/ecdh_bits.https.any.js"] = 35,
        ["WebCryptoAPI/derive_bits_keys/ecdh_keys.https.any.js"] = 30,
        ["WebCryptoAPI/derive_bits_keys/hkdf.https.any.js"] = 3500,
        ["WebCryptoAPI/derive_bits_keys/pbkdf2.https.any.js"] = 8500,

        ["WebCryptoAPI/digest/digest.https.any.js"] = 110,

        ["WebCryptoAPI/encrypt_decrypt/aes_cbc.https.any.js"] = 60,
        ["WebCryptoAPI/encrypt_decrypt/aes_ctr.https.any.js"] = 50,
        ["WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js"] = 550,
        ["WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js"] = 550,
        ["WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js"] = 180,

        ["WebCryptoAPI/generateKey/failures_AES-CBC.https.any.js"] = 680,
        ["WebCryptoAPI/generateKey/failures_AES-CTR.https.any.js"] = 680,
        ["WebCryptoAPI/generateKey/failures_AES-GCM.https.any.js"] = 680,
        ["WebCryptoAPI/generateKey/failures_AES-KW.https.any.js"] = 230,
        ["WebCryptoAPI/generateKey/failures_ECDH.https.any.js"] = 170,
        ["WebCryptoAPI/generateKey/failures_ECDSA.https.any.js"] = 140,
        ["WebCryptoAPI/generateKey/failures_HMAC.https.any.js"] = 430,
        ["WebCryptoAPI/generateKey/failures_RSA-OAEP.https.any.js"] = 340,
        ["WebCryptoAPI/generateKey/failures_RSA-PSS.https.any.js"] = 110,
        ["WebCryptoAPI/generateKey/failures_RSASSA-PKCS1-v1_5.https.any.js"] = 110,
        ["WebCryptoAPI/generateKey/failures_bad_algorithm.https.any.js"] = 350,
        ["WebCryptoAPI/generateKey/successes_AES-CBC.https.any.js"] = 280,
        ["WebCryptoAPI/generateKey/successes_AES-CTR.https.any.js"] = 280,
        ["WebCryptoAPI/generateKey/successes_AES-GCM.https.any.js"] = 280,
        ["WebCryptoAPI/generateKey/successes_AES-KW.https.any.js"] = 70,
        ["WebCryptoAPI/generateKey/successes_ECDH.https.any.js"] = 100,
        ["WebCryptoAPI/generateKey/successes_ECDSA.https.any.js"] = 80,
        ["WebCryptoAPI/generateKey/successes_HMAC.https.any.js"] = 190,
        ["WebCryptoAPI/generateKey/successes_RSA-OAEP.https.any.js"] = 150,
        ["WebCryptoAPI/generateKey/successes_RSA-PSS.https.any.js"] = 36,
        ["WebCryptoAPI/generateKey/successes_RSASSA-PKCS1-v1_5.https.any.js"] = 36,

        ["WebCryptoAPI/import_export/ec_importKey.https.any.js"] = 260,
        ["WebCryptoAPI/import_export/ec_importKey_failures_ECDH.https.any.js"] = 900,
        ["WebCryptoAPI/import_export/ec_importKey_failures_ECDSA.https.any.js"] = 900,
        ["WebCryptoAPI/import_export/rsa_importKey.https.any.js"] = 1000,
        ["WebCryptoAPI/import_export/symmetric_importKey.https.any.js"] = 600,

        ["WebCryptoAPI/sign_verify/ecdsa.https.any.js"] = 320,
        ["WebCryptoAPI/sign_verify/hmac.https.any.js"] = 65,
        ["WebCryptoAPI/sign_verify/rsa_pkcs.https.any.js"] = 65,
        ["WebCryptoAPI/sign_verify/rsa_pss.https.any.js"] = 140,

        ["WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js"] = 230,
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
    /// <summary>The one platform any exclusion is scoped to today; see <c>WptExclusion.Platform</c>.</summary>
    private static readonly System.Runtime.InteropServices.OSPlatform MacOs = System.Runtime.InteropServices.OSPlatform.OSX;

    private static readonly WptExclusion[] _exclusions =
    [
        new("encoding/textdecoder-eof.any.js", "TextDecoder end-of-queue handling", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-eof.any.js", "TextDecoder end-of-queue handling using stream: true", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "big5 => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "big5-hkscs => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "chinese => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "cn-big5 => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csbig5 => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "cseuckr => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "cseucpkdfmtjapanese => EUC-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csgb2312 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csiso2022jp => ISO-2022-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csiso58gb231280 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csksc56011987 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "csshiftjis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "euc-jp => EUC-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "euc-kr => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gb18030 => gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gb2312 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gb_2312 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gb_2312-80 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "gbk => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "iso-2022-jp => ISO-2022-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "iso-ir-149 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "iso-ir-58 => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "korean => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ks_c_5601-1987 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ks_c_5601-1989 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ksc5601 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ksc_5601 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ms932 => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "ms_kanji => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "shift-jis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "shift_jis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "sjis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "windows-31j => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "windows-949 => EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "x-euc-jp => EUC-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "x-gbk => GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "x-sjis => Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-labels.any.js", "x-x-big5 => Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Concatenating two ISO-2022-JP outputs is not always valid", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: euc-kr", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: gbk", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Most legacy multi-byte encodings are ASCII supersets: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: euc-kr", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: gbk", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Replacement, push back ASCII characters: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: euc-kr", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: gbk", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "Sticky multibyte state: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "WPT mislabels: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "WPT mislabels: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "WPT mislabels: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "fatal stream: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gb18030 version and ranges", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gbk decoder is gb18030 decoder", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "gbk version and ranges", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "specific: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: euc-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: euc-kr", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: gbk", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: iso-2022-jp", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textdecoder-mistakes.any.js", "stream: shift_jis", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: Big5", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: EUC-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: EUC-KR", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: GBK", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: ISO-2022-JP", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: Shift_JIS", WptDivergence.NeedsLegacyMultiByteEncodings),
        new("encoding/textencoder-constructor-non-utf.any.js", "Encoding argument supported for decode: gb18030", WptDivergence.NeedsLegacyMultiByteEncodings),
        // ---------------------------------------------------------------- url
        // The second half of the urlencoded parser suite feeds the same corpus through Request.formData()
        // and Response.formData(); the URLSearchParams half of every row passes.
        new("url/urlencoded-parser.any.js", "request.formData() with input: *", WptDivergence.NeedsFetchObjectModel),
        new("url/urlencoded-parser.any.js", "response.formData() with input: *", WptDivergence.NeedsFetchObjectModel),

        // ---------------------------------------------------------------- encoding
        // common/sab.js takes its SharedArrayBuffer constructor from WebAssembly.Memory. Note that
        // "Invalid encodeInto() destination: SharedArrayBuffer" is *not* here: it asserts a TypeError, and
        // the helper throws one, so it passes for the wrong reason — which is why these two globs are narrow
        // rather than one over the word.
        new("encoding/encodeInto.any.js", "encodeInto() into SharedArrayBuffer *", WptDivergence.NeedsWebAssembly),
        new("encoding/encodeInto.any.js", "Invalid encodeInto() destination: *, backed by: SharedArrayBuffer", WptDivergence.NeedsWebAssembly),
        new("encoding/textdecoder-copy.any.js", "Modify buffer after passing it in (SharedArrayBuffer)", WptDivergence.NeedsWebAssembly),
        new("encoding/textdecoder-streaming.any.js", "*(SharedArrayBuffer)", WptDivergence.NeedsWebAssembly),

        // Everything below is one missing feature: the Encoding Standard's legacy single-byte and multi-byte
        // decoders, which EncodingLabels documents as out of scope and issue #3106 implements. Each of these
        // fails with "the encoding label provided ('…') is invalid" and nothing else.


        // Named one at a time rather than as "stream: *": the utf-8 row of that family passes, and an
        // exclusion is not allowed to cover a test that works.

        // The encoder half of every row here passes: a TextEncoder ignores its argument and is always utf-8.
        // Only "supported for decode" is excluded, and the utf-8, utf-16le and utf-16be rows of that half
        // pass too, which is why the 36 names are spelled out rather than globbed.

        // ---------------------------------------------------------------- WebCryptoAPI
        // Jint has no scheme and therefore no secure-context bit; the file's third test passes anyway.
        new("WebCryptoAPI/historical.any.js", "Non-secure context window does not have access to crypto.subtle", WptDivergence.NeedsSecureContextModel),
        new("WebCryptoAPI/historical.any.js", "Non-secure context window does not have access to CryptoKey", WptDivergence.NeedsSecureContextModel),

        // The nine typed-array rows of "Large length"; the rest of the file passes. Each asks for the
        // QuotaExceededError *interface*, and gets the name on a plain DOMException.
        new("WebCryptoAPI/getRandomValues.any.js", "Large length: *", WptDivergence.NeedsQuotaExceededErrorInterface),

        // Every "… during call" family below is the same defect: the caller's bytes are copied before the
        // algorithm is normalized rather than after, so a getter on the algorithm's `name` that rewrites or
        // transfers the buffer is too late to be seen. The "… after call" siblings pass, which is what makes
        // these globs safe: they name the half that the ordering decides. See WptDivergence.NeedsTriage.
        new("WebCryptoAPI/digest/digest.https.any.js", "* and altered buffer during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_cbc.https.any.js", "AES-CBC * with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_cbc.https.any.js", "AES-CBC * decryption with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_ctr.https.any.js", "AES-CTR * with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_ctr.https.any.js", "AES-CTR * decryption with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/import_export/symmetric_importKey.https.any.js", "Key data altered during call: *", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/sign_verify/ecdsa.https.any.js", "ECDSA * with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/sign_verify/ecdsa.https.any.js", "ECDSA * verification with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/sign_verify/hmac.https.any.js", "HMAC * with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/sign_verify/hmac.https.any.js", "HMAC * verification with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/sign_verify/rsa_pkcs.https.any.js", "RSASSA-PKCS1-v1_5 * with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/sign_verify/rsa_pkcs.https.any.js", "RSASSA-PKCS1-v1_5 * verification with * during call", WptDivergence.NeedsTriage),

        // ECDH's step 4 takes maximumLength from the *public* key and raises its OperationError before step 8
        // compares the curves, which is what the prose says and not what a browser answers. The P-256 row
        // passes only because the curve it is mismatched against is the wider one.
        new("WebCryptoAPI/derive_bits_keys/ecdh_bits.https.any.js", "P-384 mismatched curves", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/derive_bits_keys/ecdh_bits.https.any.js", "P-521 mismatched curves", WptDivergence.NeedsTriage),

        // The X25519 rows of a file that is otherwise about the `length` parameter of deriveBits.
        new("WebCryptoAPI/derive_bits_keys/derived_bits_length.https.any.js", "X25519 derivation with *", WptDivergence.NeedsCurve25519),

        // Compressed EC points, which the corpus itself treats as optional: these are recorded
        // PRECONDITION_FAILED rather than FAIL. The glob is the ", compressed)" the suite writes into the
        // parameter string; the uncompressed rows of every one of these keys pass.
        new("WebCryptoAPI/import_export/ec_importKey.https.any.js", "*, compressed), *", WptDivergence.NeedsCompressedEcPointImport),

        // AES-GCM's 32- and 64-bit tags. Six globs per tag rather than one "…-bit tag*", because the
        // "decryption with transferred ciphertext during call" row of those two tags *passes*: the operation
        // was going to throw anyway, and that is what the test asks for.
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 32-bit tag, 96-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 64-bit tag, 96-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters),

        // The four tag lengths between 96 and 120 bits work on Windows (CNG) and Linux (OpenSSL), whose
        // AesGcm.TagByteSizes is 12 to 16 — and not on macOS, where Apple's implementation answers 16 to 16
        // and the engine's ask-the-platform gate refuses everything shorter. These rows therefore PASS on two
        // legs and FAIL on the third, which no platform-neutral entry can say; they are scoped to macOS, where
        // the staleness rule still holds them to matching real failures. The "during call" variants are
        // absent here deliberately: those rows fail on every OS for the copy-order reason and are the
        // NeedsTriage entries below.
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),

        // The altered-ciphertext decryption rows, which for the unsupported tag lengths mirror the 32- and
        // 64-bit entries above exactly: the tag refusal fails them here where the copy-order defect fails
        // them elsewhere. Their transferred-ciphertext siblings appear in no entry on this platform because
        // the same refusal is the rejection they assert, so they pass — which is also why the copy-order
        // globs below excuse themselves from macOS.
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters, MacOs),

        // The same file's copy-order rows, for the five tag lengths that do work. Spelled per tag rather than
        // as one "* during call" so that the tag-length rows above are not silently filed under the wrong
        // cause: those two fail before the ordering could ever matter.
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 96-bit tag, 96-bit iv decryption with * during call", WptDivergence.NeedsTriage, ExceptPlatform: MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 104-bit tag, 96-bit iv decryption with * during call", WptDivergence.NeedsTriage, ExceptPlatform: MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 112-bit tag, 96-bit iv decryption with * during call", WptDivergence.NeedsTriage, ExceptPlatform: MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 120-bit tag, 96-bit iv decryption with * during call", WptDivergence.NeedsTriage, ExceptPlatform: MacOs),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 128-bit tag, 96-bit iv with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm.https.any.js", "AES-GCM *, 128-bit tag, 96-bit iv decryption with * during call", WptDivergence.NeedsTriage),

        // The whole of the 256-bit-iv file bar the rows that expect a throw for another reason — the
        // usage-matrix rows, the illegal-tag-length rows, and again "decryption with transferred ciphertext
        // during call", all of which pass.
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv decryption", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/aes_gcm_256_iv.https.any.js", "AES-GCM *, 256-bit iv with * during call", WptDivergence.NeedsPlatformCryptoParameters),

        // RSA-OAEP's label. The "a label" rows fail for the label, the "no label" and "empty label" rows only
        // for the copy order — which is why the two causes are told apart by the label rather than lumped
        // under one "* during call".
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label decryption", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label decryption with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label decryption with altered ciphertext during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label with * after call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and a label with * during call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and no label with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and no label decryption with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and empty label with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/encrypt_decrypt/rsa_oaep.https.any.js", "RSA-OAEP with SHA-* and empty label decryption with * during call", WptDivergence.NeedsTriage),

        // RSA-PSS's salt length. "no salt" is saltLength 0, which .NET cannot ask for, so every one of those
        // rows fails — except SHA-256's "wrong saltLength", whose wrong length happens to be SHA-256's own
        // and is therefore the one .NET does accept. The ", salted" rows fail only where the *wrong* salt
        // length is asked for, plus the copy-order family.
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt round trip", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt verification", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt verification failure with altered *", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-1 and no salt verification failure with wrong saltLength", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-384 and no salt verification failure with wrong saltLength", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-512 and no salt verification failure with wrong saltLength", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt verification with * call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-* and no salt with * call", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-*, salted verification failure with wrong saltLength", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-*, salted verification with * during call", WptDivergence.NeedsTriage),
        new("WebCryptoAPI/sign_verify/rsa_pss.https.any.js", "RSA-PSS with SHA-*, salted with * during call", WptDivergence.NeedsTriage),

        // wrapKey/unwrapKey, where the two platform limits above meet: the corpus wraps under AES-GCM with a
        // 128-bit iv and under RSA-OAEP with a label, so those two wrapping algorithms fail outright. The
        // four narrower globs are the rows that wrap an AES-GCM or RSA-OAEP *key* under something else and
        // then compare two non-extractable keys by using them, which reaches the same two limits.
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "* and AES-GCM", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "* and RSA-OAEP", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "Can wrap and unwrap AES-GCM keys as non-extractable using *", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "Can unwrap AES-GCM non-extractable keys using *", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "Can wrap and unwrap RSA-OAEP private key keys as non-extractable using *", WptDivergence.NeedsPlatformCryptoParameters),
        new("WebCryptoAPI/wrapKey_unwrapKey/wrapKey_unwrapKey.https.any.js", "Can unwrap RSA-OAEP private key non-extractable keys using *", WptDivergence.NeedsPlatformCryptoParameters),

        // The KeyUsage values ML-KEM proposes, which the enumeration does not have. One glob per file: the
        // suites build the name out of the usage list, and "apsulate" is the substring the four values share.
        new("WebCryptoAPI/generateKey/failures_AES-CBC.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_AES-CTR.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_AES-GCM.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_AES-KW.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_ECDH.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_ECDSA.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_HMAC.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_RSA-OAEP.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_RSA-PSS.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/generateKey/failures_RSASSA-PKCS1-v1_5.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/import_export/ec_importKey_failures_ECDH.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
        new("WebCryptoAPI/import_export/ec_importKey_failures_ECDSA.https.any.js", "Bad usages: *apsulate*", WptDivergence.NeedsKeyEncapsulation),
    ];

    public static IEnumerable<object[]> UrlSuiteFiles() => Cases("url");

    public static IEnumerable<object[]> EncodingSuiteFiles() => Cases("encoding");

    /// <summary>
    /// Every suite of the WebCryptoAPI corpus, as (suite directory) pairs. A sub-directory is a suite of its
    /// own — <see cref="WptCorpus.TestFiles"/> lists a directory's own <c>.any.js</c> files and never
    /// descends — so the root files and each of the seven operation directories get their own theory.
    /// </summary>
    private static readonly string[] _webCryptoSuites =
    [
        "WebCryptoAPI",
        "WebCryptoAPI/derive_bits_keys",
        "WebCryptoAPI/digest",
        "WebCryptoAPI/encrypt_decrypt",
        "WebCryptoAPI/generateKey",
        "WebCryptoAPI/import_export",
        "WebCryptoAPI/sign_verify",
        "WebCryptoAPI/wrapKey_unwrapKey",
    ];

    public static IEnumerable<object[]> WebCryptoSuiteFiles() => Cases("WebCryptoAPI");

    public static IEnumerable<object[]> WebCryptoDeriveSuiteFiles() => Cases("WebCryptoAPI/derive_bits_keys");

    public static IEnumerable<object[]> WebCryptoDigestSuiteFiles() => Cases("WebCryptoAPI/digest");

    public static IEnumerable<object[]> WebCryptoEncryptSuiteFiles() => Cases("WebCryptoAPI/encrypt_decrypt");

    public static IEnumerable<object[]> WebCryptoGenerateKeySuiteFiles() => Cases("WebCryptoAPI/generateKey");

    public static IEnumerable<object[]> WebCryptoImportExportSuiteFiles() => Cases("WebCryptoAPI/import_export");

    public static IEnumerable<object[]> WebCryptoSignVerifySuiteFiles() => Cases("WebCryptoAPI/sign_verify");

    public static IEnumerable<object[]> WebCryptoWrapKeySuiteFiles() => Cases("WebCryptoAPI/wrapKey_unwrapKey");

    [Theory]
    [MemberData(nameof(UrlSuiteFiles))]
    public void RunsTheUrlSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(EncodingSuiteFiles))]
    public void RunsTheEncodingSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoSuiteFiles))]
    public void RunsTheWebCryptoSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoDeriveSuiteFiles))]
    public void RunsTheWebCryptoDeriveSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoDigestSuiteFiles))]
    public void RunsTheWebCryptoDigestSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoEncryptSuiteFiles))]
    public void RunsTheWebCryptoEncryptDecryptSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoGenerateKeySuiteFiles))]
    public void RunsTheWebCryptoGenerateKeySuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoImportExportSuiteFiles))]
    public void RunsTheWebCryptoImportExportSuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoSignVerifySuiteFiles))]
    public void RunsTheWebCryptoSignVerifySuite(string file) => RunSuiteFile(file);

    [Theory]
    [MemberData(nameof(WebCryptoWrapKeySuiteFiles))]
    public void RunsTheWebCryptoWrapKeySuite(string file) => RunSuiteFile(file);

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

        // And a WebCryptoAPI sub-directory that lost its theory member would be a suite nothing runs, which
        // the minimum-test check above cannot see: it proves a file is declared, not that a theory reaches
        // it. Every declared suite must therefore produce cases, and every vendored .any.js must belong to
        // one of the declared suites.
        foreach (var suite in _webCryptoSuites)
        {
            WptCorpus.TestFiles(suite).Should().NotBeEmpty($"{suite} must produce theory cases");
        }

        foreach (var path in WptCorpus.Paths)
        {
            if (!path.StartsWith("WebCryptoAPI/", StringComparison.Ordinal)
                || !path.EndsWith(".any.js", StringComparison.Ordinal))
            {
                continue;
            }

            if (Array.TrueForAll(_webCryptoSuites, suite => !WptCorpus.TestFiles(suite).Contains(path)))
            {
                problems.Add($"{path} is vendored but belongs to no declared WebCryptoAPI suite, so no theory runs it");
            }
        }

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
                     "WebCryptoAPI/util/helpers.js",
                     "WebCryptoAPI/util/ec_key_fixtures.js",
                     "WebCryptoAPI/util/rsa_key_fixtures.js",
                     "WebCryptoAPI/util/okp_key_fixtures.js",
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

        // An entry scoped to another operating system is invisible here — it excludes nothing and the
        // staleness rules below do not ask it to match, which is what keeps the table exact per OS.
        var exclusions = Array.FindAll(_exclusions,
            e => string.Equals(e.File, file, StringComparison.Ordinal) && e.AppliesOnThisPlatform);
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
