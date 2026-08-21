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
    /// <para>
    /// The test asks an algorithm for a parameter .NET's own primitives refuse, and the refusal is what
    /// <c>Jint/WebApi/Crypto/</c> documents on the class that makes it rather than something the engine could
    /// choose differently. Four of them, each named in the message the operation rejects with:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// AES-GCM takes a <b>96-bit iv and nothing else</b> (<c>AesGcm.NonceByteSizes</c> is 12 to 12), where
    /// https://w3c.github.io/webcrypto/#aes-gcm allows one "up to 2^64-1 bytes long". That is the whole of
    /// <c>aes_gcm_256_iv</c>, and the reason a <c>wrapKey</c> under AES-GCM cannot work either.
    /// </description></item>
    /// <item><description>
    /// AES-GCM takes a <b>96- to 128-bit tag</b> (<c>AesGcm.TagByteSizes</c> is 12 to 16), where the
    /// specification's list also holds 32 and 64 — and on macOS <b>only 128 bits</b>, Apple's implementation
    /// answering 16 to 16, which is why the 96- to 120-bit rows are the platform-scoped entries the
    /// <c>Platform</c> parameter exists for.
    /// </description></item>
    /// <item><description>
    /// RSA-OAEP takes <b>no label</b>: .NET exposes OAEP through <c>RSAEncryptionPadding</c>, which has no
    /// place for one, so a present and non-empty <c>label</c> member is an <c>OperationError</c>.
    /// </description></item>
    /// <item><description>
    /// RSA-PSS takes a <b>salt as long as the hash and no other length</b> —
    /// <c>RSASignaturePadding.Pss</c> is defined that way — where <c>RsaPssParams.saltLength</c> is any
    /// unsigned long.
    /// </description></item>
    /// </list>
    /// <para>
    /// These are the corpus meeting a platform rather than a gap in Jint. Removing one means reaching past
    /// the BCL to a hand-written primitive, which is not a thing to do quietly.
    /// </para>
    /// </summary>
    NeedsPlatformCryptoParameters,

    /// <summary>
    /// The test asks for a <c>KeyUsage</c> the enumeration does not have. The corpus at this pin passes
    /// <c>encapsulateKey</c>, <c>decapsulateKey</c>, <c>encapsulateBits</c> and <c>decapsulateBits</c> to
    /// <c>generateKey</c> and <c>importKey</c> and expects a <c>SyntaxError</c> — "recognized, but not one
    /// this algorithm supports". WebIDL's own conversion says otherwise for an engine that does not have
    /// them: https://w3c.github.io/webcrypto/#dfn-KeyUsage declares eight values and none of these is among
    /// them, so a ninth is outside the enumeration and a <c>TypeError</c>, which is what Jint answers. The
    /// values arrive with the ML-KEM proposal, whose own tests are in <c>.tentative.</c> files this corpus
    /// does not vendor — these rows are that proposal leaking into the stable ones. The entries stop applying
    /// when the specification adopts the values (Jint would then have to accept and refuse them) or when
    /// upstream moves the rows, and either is the right moment to revisit them.
    /// </summary>
    NeedsKeyEncapsulation,

    /// <summary>
    /// The test asserts the WebIDL <c>QuotaExceededError</c> <i>interface</i> —
    /// https://webidl.spec.whatwg.org/#quotaexceedederror, which since 2025 derives from <c>DOMException</c>
    /// and carries <c>quota</c> and <c>requested</c>. <c>CryptoInstance.GetRandomValues</c> documents the
    /// choice it makes instead: the name on a plain <c>DOMException</c>, which is what every browser did
    /// until that change and what the <c>getRandomValues</c> algorithm's own wording asks for. The interface
    /// is a DOM-wide change rather than a crypto one, so it is not this feature's to make.
    /// </summary>
    NeedsQuotaExceededErrorInterface,

    /// <summary>
    /// The test is over Curve25519 — X25519 or Ed25519. The BCL ships neither, so the whole family is out of
    /// scope for a crypto layer built on it; the files dedicated to those curves are not vendored at all (see
    /// <c>Vendor/README.md</c>) and this category is for the rows that sit inside a file which is otherwise
    /// about something else.
    /// </summary>
    NeedsCurve25519,

    /// <summary>
    /// The test imports an EC key whose point is in compressed form, which
    /// https://w3c.github.io/webcrypto/#ecdsa-operations makes optional — the corpus says so itself by giving
    /// up through <c>assert_implements_optional</c> when the import raises a <c>DataError</c>, so these are
    /// recorded <c>PRECONDITION_FAILED</c> rather than <c>FAIL</c>. .NET's <c>ECDsa</c>/<c>ECDiffieHellman</c>
    /// import paths want an uncompressed point, and decompressing one means implementing the curve's square
    /// root by hand.
    /// </summary>
    NeedsCompressedEcPointImport,

    /// <summary>
    /// The test asserts what a <b>non-secure</b> context sees, which Jint has no way to be: it has no scheme,
    /// no origin and therefore no secure-context bit, and <c>crypto.subtle</c> is simply there once the
    /// feature is enabled. Upstream runs <c>historical.any.js</c> over plain http for exactly the property it
    /// asserts, so this is the corpus meeting an environment it was not written for rather than a gap to
    /// close. Note the file's third test passes on its own merits: Jint exposes no <c>SubtleCrypto</c>
    /// constructor either way.
    /// </summary>
    NeedsSecureContextModel,

    /// <summary>
    /// A genuine failure that is not attributable to a feature Jint has decided not to have. Every entry
    /// here is a bug or a specification detail to chase, and the phase of the harness work that stood the
    /// suites up deliberately recorded them rather than fixing them: the point was to find out what they
    /// say, and mixing engine fixes into the change that first ran them would have hidden which of the two
    /// moved. The four the first phase recorded — WebIDL constant order, <c>TextDecoder.decode()</c> reading
    /// its input before the options dictionary was converted, and the shared UTF-16 decoder's end-of-queue
    /// step for both endiannesses — were fixed by https://github.com/sebastienros/jint/issues/3121.
    /// <para>
    /// The WebCryptoAPI corpus filed two more, and both are one-line summaries of a real disagreement:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Every "… during call" row.</b> <c>SubtleCrypto</c> copies the caller's <c>data</c>/<c>keyData</c>
    /// <i>before</i> normalizing the algorithm; the specification copies it after — for <c>encrypt</c>,
    /// normalization is step 2 and "let data be the result of getting a copy of the bytes held by the data
    /// parameter" is step 4 (https://w3c.github.io/webcrypto/#SubtleCrypto-method-encrypt, and the same shape
    /// in <c>decrypt</c>, <c>sign</c>, <c>verify</c>, <c>digest</c> and <c>importKey</c>). The corpus makes
    /// the order observable by putting a getter on the algorithm's <c>name</c> that rewrites or transfers the
    /// buffer. Note <c>SubtleCryptoKeyTests.TheDataArgumentIsCopiedBeforeAnyGetterCanRun</c> pins the order
    /// Jint has today, so fixing this moves that test too.
    /// </description></item>
    /// <item><description>
    /// <b>ECDH's two mismatched-curve rows.</b> <c>EcAlgorithm</c> follows the specification exactly —
    /// <i>maximumLength</i> comes from the <b>public</b> key's domain parameters and its <c>OperationError</c>
    /// is raised before the curves are compared, so a P-521 base key handed a P-256 public key is refused for
    /// its length rather than for the mismatch. The corpus expects the <c>InvalidAccessError</c> of the later
    /// step, which is what browsers answer. The P-256 row passes only because its other curve is wider.
    /// Somebody has to decide whether Jint follows the prose or the browsers, and probably raise it upstream.
    /// </description></item>
    /// </list>
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
/// <param name="Platform">
/// The one operating system this entry applies on, or <see langword="null"/> — almost always null — for an
/// entry that applies everywhere. A platform-scoped entry exists for the case where the <i>platform's</i>
/// crypto draws its limits differently per OS: Apple's AES-GCM takes only a 128-bit tag where CNG and
/// OpenSSL take 96 to 128 bits, so the sub-128-bit-tag rows pass on Windows and Linux and fail on macOS —
/// no platform-neutral entry can name them without covering passing tests somewhere. On any other OS a
/// scoped entry is invisible: it excludes nothing and the staleness rule does not ask it to match, so the
/// discipline stays exact on every leg rather than being loosened to their union.
/// </param>
/// <param name="ExceptPlatform">
/// The one operating system this entry does <b>not</b> apply on, or <see langword="null"/>. The mirror image
/// of <paramref name="Platform"/>, and its worked example is the same file's copy-order rows: a
/// <c>decryption … during call</c> test that fails everywhere else <i>passes</i> on macOS, because the
/// platform's tag refusal produces the very <c>OperationError</c> the test asserts — for the wrong reason,
/// which the assertion cannot see. The entry would be stale there, so it excuses itself from that leg.
/// </param>
internal sealed record WptExclusion(
    string File,
    string TestName,
    WptDivergence Divergence,
    System.Runtime.InteropServices.OSPlatform? Platform = null,
    System.Runtime.InteropServices.OSPlatform? ExceptPlatform = null)
{
    internal bool Matches(string testName) => MatchesPattern(TestName, testName);

    /// <summary>Whether this entry participates on the operating system the run is on.</summary>
    internal bool AppliesOnThisPlatform =>
        (Platform is not { } platform || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(platform))
        && (ExceptPlatform is not { } excluded || !System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(excluded));

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
