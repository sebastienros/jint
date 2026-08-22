using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// <c>crypto.subtle</c> — <see cref="WebApiFeatures.Crypto"/>,
/// https://w3c.github.io/webcrypto/#subtlecrypto-interface.
///
/// <para>Six rows, chosen as the operations an embedder actually runs per request rather than as a tour of
/// the interface: <see cref="DigestSha256"/>, <see cref="HmacSign"/> and <see cref="HmacVerify"/> are the
/// whole of HS256; <see cref="RsaVerify"/> and <see cref="EcdsaVerify"/> are RS256 and ES256 token
/// validation, the shape a gateway runs on every inbound call; <see cref="AesGcmEncrypt"/> is the sealing
/// half of an encrypted cookie or cache entry. The payload is the same 115-byte JWS signing input
/// throughout, so the rows differ by algorithm and by nothing else, and the spread between them is the
/// spread an embedder would see when choosing one.</para>
///
/// <para><b>Each row measures one call <em>and its settlement</em>.</b> The measured script is a single
/// <c>crypto.subtle</c> call whose completion value is the promise it returns, and the benchmark method
/// unwraps that promise host-side with <c>UnwrapIfPromise</c>. Nothing is waited for: every operation here
/// is synchronous CPU work over bytes already in memory, so the promise is already fulfilled when the call
/// hands it back (see <c>SubtleCryptoPrototype</c>'s remarks — "Return promise and perform the remaining
/// steps in parallel" exists so a browser's main thread is not blocked, and an engine that runs script on
/// one thread cannot observe the difference). The unwrap therefore adds the host-side settle ceremony —
/// the drain's host-call bracket and its cancellation-constraint lookup, no event-loop turn — and that
/// ceremony is deliberately inside the number, because it is what an embedder taking the value back into
/// C# pays. Read a row as "one operation, call to value", never as the cost of the primitive alone.</para>
///
/// <para><b>Keys are imported in <c>[GlobalSetup]</c>, on the row's own engine.</b> <c>importKey</c> is
/// itself promise-returning, so each key is settled host-side there and bound as a global with
/// <c>Engine.SetValue</c> — the measured script never imports, parses a JWK or reaches a key derivation.
/// <see cref="HmacVerify"/>'s signature is produced the same way, by signing once at setup.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine carrying <see cref="WebApiFeatures.Crypto"/>
/// alone, warmed with its own fixture and its own script and nothing else — see
/// <see cref="WebApiBenchmarkSupport"/>. Engine construction, the corpus and the key imports all stay in
/// <c>[GlobalSetup]</c> and never enter the measurement.</para>
///
/// <para><b>Every row asserts its own answer, every operation.</b> Unlike
/// <see cref="WebApiBenchmarkSupport.DeterministicRow"/>, which pins whatever the first run produced, these
/// rows know what they owe in advance — 32 bytes of digest or HMAC tag, <c>true</c> from a verification,
/// plaintext-plus-16 from AES-GCM — so <see cref="SubtleRow"/> carries the expected value as a constant and
/// checks it on the first settle and on every one after. A verification that started answering
/// <c>false</c> would otherwise take a cheaper path through the algorithm and quietly make the row look
/// faster.</para>
///
/// <para><b>This class is restricted to the public embedding surface on purpose.</b> <c>Jint.Benchmark</c>
/// has <c>InternalsVisibleTo</c>, so host code written here can reach members no embedder could; nothing
/// below uses more than <c>Engine</c>, <c>Engine.SetValue</c>, <c>Engine.Evaluate</c>,
/// <c>Engine.PrepareScript</c> (through <see cref="IsolatedScript"/>), <c>JsValue</c>'s accessors and
/// <c>UnwrapIfPromise</c> — the exact set an embedder unwrapping a <c>crypto.subtle</c> result has. The
/// restriction bites in one place: the settled value is inspected through <c>JsValue.Get("byteLength")</c>
/// rather than by casting to the engine's own buffer type, which costs a property read per operation and is
/// the honest price of measuring what an embedder can reach.</para>
///
/// <para><b>The key material is published test-vector material</b> — RFC 7515 Appendix A.2 for the RSA key,
/// its signing input and its signature, Appendix A.3 for the ECDSA equivalents, the same vectors
/// <c>Jint.Tests</c> verifies against. The symmetric keys and the AES-GCM IV are fixed counting patterns so
/// the rows are reproducible; reusing one IV across operations is a benchmark artefact and never a pattern
/// to copy into anything that keeps its ciphertext.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory(WebApiBenchmarkSupport.Category)]
public class WebApiSubtleCryptoBenchmark
{
    /// <summary>
    /// The conversions the fixtures need, and nothing else. They run once per row in <c>[GlobalSetup]</c>.
    /// Bytes come from character codes rather than from <c>TextEncoder</c> so that each row's engine carries
    /// the crypto feature alone and no row can be measuring a neighbour's installation cost.
    /// </summary>
    private const string Prelude =
        """
        function ascii(s) { return Uint8Array.from(s, function (c) { return c.charCodeAt(0); }); }
        function bytes(hex) {
            var out = new Uint8Array(hex.length / 2);
            for (var i = 0; i < out.length; i++) { out[i] = parseInt(hex.slice(i * 2, i * 2 + 2), 16); }
            return out;
        }
        function counting(n) {
            var out = new Uint8Array(n);
            for (var i = 0; i < n; i++) { out[i] = i; }
            return out;
        }
        """;

    /// <summary>
    /// RFC 7515 Appendix A.2's signing input: <c>base64url(header) + '.' + base64url(payload)</c>, 115 ASCII
    /// bytes. It is a real JWT's signing input rather than a round number, which is the point — the digest,
    /// HMAC and AES-GCM rows all take it as their "small payload" so that every row in the class hashes or
    /// seals the same bytes.
    /// </summary>
    private const string Rs256SigningInput =
        "eyJhbGciOiJSUzI1NiJ9.eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ";

    /// <summary>RFC 7515 Appendix A.2's 2048-bit RSA public key, as the JWK members <c>importKey</c> reads.</summary>
    private const string Rs256PublicJwk =
        "kty: 'RSA', n: 'ofgWCuLjybRlzo0tZWJjNiuSfb4p4fAkd_wWJcyQoTbji9k0l8W26mPddxHmfHQp-Vaw-4qPCJrcS2mJPMEzP1Pt0Bm4d4QlL-yRT-SFd2lZS-pCgNMsD1W_YpRPEwOWvG6b32690r2jZ47soMZo9wGzjb_7OMg0LOL-bSf63kpaSHSXndS5z5rexMdbBYUsLA9e-KXBdQOS-UTo7WTBEMa2R2CapHg665xsmtdVMTBQY4uDZlxvb3qCo5ZwKh9kG4LT6_I5IhlJH7aGhyxXFvUK-DWNmoudF8NAco9_h9iaGNj8q2ethFkMLs91kzk2PAcDTW9gb54h4FRWyuXpoQ', e: 'AQAB'";

    /// <summary>RFC 7515 Appendix A.2's signature over <see cref="Rs256SigningInput"/> — 256 bytes.</summary>
    private const string Rs256SignatureHex =
        "702e218943e88fd11eb5d82dbf7845f34106ae1b81fff7731116add1717d83656d420afd3c96eedd73a2663e5166687b000b87226e0187ed1073f945e582adfcef16d85a798ee8c66ddb3db8975b17d09402beedd5d9d97007108db28160d5f8040ca7445762b81fbe7ff9d92e0ae76f24f25b33bbe6f44ae61eb1040acb20044d3ef9128ed40130795bd4bd3b41eecad066ab651981fde48df77f372dc38b9fafdd3befb18b5da3cc3c2eb02f9e3a41d612caad15911273a05f23b9e838faaf849d698429ef5a1e88798236c3d40e604522a544c8f27a7a2db80663d16cf7caea56de405cb2215a45b2c25566b55ac1a748a070dfc8a32a469543d019eefb47";

    /// <summary>RFC 7515 Appendix A.3's signing input — the ES256 header makes it a different message.</summary>
    private const string Es256SigningInput =
        "eyJhbGciOiJFUzI1NiJ9.eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ";

    /// <summary>RFC 7515 Appendix A.3's P-256 public key.</summary>
    private const string Es256PublicJwk =
        "kty: 'EC', crv: 'P-256', x: 'f83OJ3D2xF1Bg8vub9tLe1gHMzV76e8Tus9uPHvRVEU', y: 'x_FEzRu9m36HLN_tue659LNpXW6pCyStikYjKIWI5a0'";

    /// <summary>RFC 7515 Appendix A.3's signature over <see cref="Es256SigningInput"/> — r ‖ s, 64 bytes.</summary>
    private const string Es256SignatureHex =
        "0ed1215379636c483c2f7f155807d402a3b228033af97c7e17819ac3169ea665c50a07d38c3c70e5d8f12daf084a5480a66590c5f293509a8f3f7f8a83a354d5";

    /// <summary>What a SHA-256 digest and an HMAC-SHA-256 tag both weigh.</summary>
    private const int Sha256Bytes = 32;

    /// <summary>AES-GCM appends a 128-bit authentication tag, which is the default <c>tagLength</c>.</summary>
    private const int AesGcmTagBytes = 16;

    /// <summary>What a fulfilled <c>verify</c> summarizes to.</summary>
    private const int Verified = 1;

    private SubtleRow _digestSha256;
    private SubtleRow _hmacSign;
    private SubtleRow _hmacVerify;
    private SubtleRow _rsaVerify;
    private SubtleRow _ecdsaVerify;
    private SubtleRow _aesGcmEncrypt;

    [GlobalSetup]
    public void Setup()
    {
        _digestSha256 = new SubtleRow(
            "crypto.subtle.digest('SHA-256', MESSAGE)",
            Sha256Bytes,
            $"var MESSAGE = ascii('{Rs256SigningInput}');");

        // One 32-byte HMAC key serves both HS256 rows, but each row imports its own onto its own engine.
        var hmacCorpus =
            $"""
             var MESSAGE = ascii('{Rs256SigningInput}');
             var RAW = counting(32);
             """;
        var hmacKey =
            "crypto.subtle.importKey('raw', RAW, { name: 'HMAC', hash: 'SHA-256' }, false, ['sign', 'verify'])";

        _hmacSign = new SubtleRow(
            "crypto.subtle.sign('HMAC', KEY, MESSAGE)",
            Sha256Bytes,
            hmacCorpus,
            new Settled("KEY", hmacKey));

        // The signature is produced at setup by the very key the row then verifies with, so the row measures
        // a verification that succeeds rather than one that fails early on a length or a tag mismatch.
        _hmacVerify = new SubtleRow(
            "crypto.subtle.verify('HMAC', KEY, SIGNATURE, MESSAGE)",
            Verified,
            hmacCorpus,
            new Settled("KEY", hmacKey),
            new Settled("SIGNATURE", "crypto.subtle.sign('HMAC', KEY, MESSAGE)"));

        _rsaVerify = new SubtleRow(
            "crypto.subtle.verify('RSASSA-PKCS1-v1_5', KEY, SIGNATURE, MESSAGE)",
            Verified,
            $"""
             var MESSAGE = ascii('{Rs256SigningInput}');
             var SIGNATURE = bytes('{Rs256SignatureHex}');
             """,
            new Settled(
                "KEY",
                $"crypto.subtle.importKey('jwk', {{ {Rs256PublicJwk} }}, {{ name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }}, false, ['verify'])"));

        _ecdsaVerify = new SubtleRow(
            "crypto.subtle.verify({ name: 'ECDSA', hash: 'SHA-256' }, KEY, SIGNATURE, MESSAGE)",
            Verified,
            $"""
             var MESSAGE = ascii('{Es256SigningInput}');
             var SIGNATURE = bytes('{Es256SignatureHex}');
             """,
            new Settled(
                "KEY",
                $"crypto.subtle.importKey('jwk', {{ {Es256PublicJwk} }}, {{ name: 'ECDSA', namedCurve: 'P-256' }}, false, ['verify'])"));

        // AES-GCM-128: a 16-byte key and the 12-byte nonce the standard recommends. Both are fixed, and the
        // same nonce is reused on every operation — which is what makes the row reproducible and what would
        // be a catastrophic mistake anywhere the ciphertext outlives the process.
        _aesGcmEncrypt = new SubtleRow(
            "crypto.subtle.encrypt({ name: 'AES-GCM', iv: IV }, KEY, MESSAGE)",
            Rs256SigningInput.Length + AesGcmTagBytes,
            $"""
             var MESSAGE = ascii('{Rs256SigningInput}');
             var IV = counting(12);
             """,
            new Settled("KEY", "crypto.subtle.importKey('raw', counting(16), 'AES-GCM', false, ['encrypt'])"));
    }

    /// <summary>SHA-256 over the 115-byte signing input.</summary>
    [Benchmark]
    public JsValue DigestSha256() => _digestSha256.Settle();

    /// <summary>HMAC-SHA-256 signature over the same bytes, with a pre-imported raw key.</summary>
    [Benchmark]
    public JsValue HmacSign() => _hmacSign.Settle();

    /// <summary>HMAC-SHA-256 verification of a signature produced at setup.</summary>
    [Benchmark]
    public JsValue HmacVerify() => _hmacVerify.Settle();

    /// <summary>RSASSA-PKCS1-v1_5 SHA-256 verification against a pre-imported 2048-bit public key.</summary>
    [Benchmark]
    public JsValue RsaVerify() => _rsaVerify.Settle();

    /// <summary>ECDSA P-256 SHA-256 verification against a pre-imported public key.</summary>
    [Benchmark]
    public JsValue EcdsaVerify() => _ecdsaVerify.Settle();

    /// <summary>AES-GCM-128 encryption of the same 115-byte payload under a fixed key and nonce.</summary>
    [Benchmark]
    public JsValue AesGcmEncrypt() => _aesGcmEncrypt.Settle();

    /// <summary>
    /// A global whose value is the settlement of a promise-returning <c>crypto.subtle</c> call — a key, or a
    /// signature over the corpus. Bound in order, so a later binding may use an earlier one.
    /// </summary>
    private readonly record struct Settled(string Name, string Expression);

    /// <summary>
    /// One <c>crypto.subtle</c> row: the call's script, the private engine that call runs on, and the value
    /// every settlement of it must produce.
    /// </summary>
    private readonly struct SubtleRow
    {
        private readonly IsolatedScript _script;
        private readonly string _call;
        private readonly double _expected;

        internal SubtleRow(string call, int expected, string corpus, params Settled[] bindings)
        {
            _script = IsolatedScript.Warm(call, Factory(corpus, bindings));
            _call = call;
            _expected = expected;

            // Settling once here is what makes a broken row fail in [GlobalSetup] rather than mid-run: the
            // warm-up above evaluates the script but never unwraps its promise, so a rejection would
            // otherwise go unnoticed until the first measured operation.
            Settle();
        }

        /// <summary>
        /// Runs the row's call and unwraps the promise it returned. See this class's remarks for why the
        /// unwrap is inside the measurement.
        /// </summary>
        internal JsValue Settle()
        {
            var settled = _script.Run().UnwrapIfPromise();

            var summary = Summarize(settled);
            if (summary != _expected)
            {
                throw new InvalidOperationException(
                    $"crypto.subtle benchmark row is not doing its work: '{_call}' settled to a value summarizing as " +
                    summary.ToString(CultureInfo.InvariantCulture) + ", expected " +
                    _expected.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return settled;
        }

        /// <summary>
        /// Reduces what an operation resolved with to one comparable number: a <c>verify</c>'s boolean, or
        /// the byte length of the buffer a digest, a signature or a ciphertext came back as.
        /// </summary>
        private static double Summarize(JsValue settled)
        {
            if (settled.IsBoolean())
            {
                return settled.AsBoolean() ? 1 : 0;
            }

            return settled.Get("byteLength").AsNumber();
        }

        /// <summary>
        /// Builds the row's engine: the crypto feature alone, the prelude and the corpus, then each binding's
        /// promise settled host-side and bound as a global.
        /// </summary>
        private static Func<Engine> Factory(string corpus, Settled[] bindings)
        {
            return () =>
            {
                var engine = WebApiBenchmarkSupport.Create(WebApiFeatures.Crypto);
                engine.Execute(Prelude);
                engine.Execute(corpus);

                foreach (var binding in bindings)
                {
                    engine.SetValue(binding.Name, engine.Evaluate(binding.Expression).UnwrapIfPromise());
                }

                return engine;
            };
        }
    }
}
