using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// <c>crypto.getRandomValues</c> and <c>crypto.randomUUID</c> — <see cref="WebApiFeatures.Crypto"/>,
/// https://w3c.github.io/webcrypto/#Crypto-method-getRandomValues.
///
/// <para>Three rows, split where the cost is. <see cref="RandomValuesSmall"/> fills a 32-byte view 500
/// times, which is the token-and-nonce shape a script actually writes and is almost entirely per-call
/// ceremony: the WebIDL <c>ArrayBufferView</c> check, the detached/immutable-buffer checks and the quota
/// test. <see cref="RandomValuesLarge"/> fills the 65,536-byte maximum the standard allows in one call
/// (a larger view is a <c>QuotaExceededError</c>), eight times, so it is dominated by the generator
/// itself. <see cref="RandomUuid"/> is the other member of the interface, whose cost is formatting rather
/// than randomness.</para>
///
/// <para><b>The rows are deterministic even though their output is not.</b> Each accumulates the
/// <i>length</i> of what it produced — the number of bytes filled, or the 36 characters of a UUID — so the
/// reference check in <see cref="WebApiBenchmarkSupport.DeterministicRow"/> still bites: a
/// <c>getRandomValues</c> that stopped returning its argument, or a <c>randomUUID</c> that stopped
/// producing a UUID, fails the row rather than making it look faster.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine carrying
/// <see cref="WebApiFeatures.Crypto"/> alone, warmed with its own fixture and its own script and nothing
/// else — see <see cref="WebApiBenchmarkSupport"/>. Engine construction and allocating the destination
/// views stay in <c>[GlobalSetup]</c> and never enter the measurement.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory(WebApiBenchmarkSupport.Category)]
public class WebApiCryptoBenchmark
{
    private IsolatedScript _randomValuesSmall;
    private IsolatedScript _randomValuesLarge;
    private IsolatedScript _randomUuid;

    [GlobalSetup]
    public void Setup()
    {
        _randomValuesSmall = Row(
            """
            var SMALL = new Uint8Array(32);
            function randomValuesSmall() {
                var n = 0;
                for (var i = 0; i < 500; i++) { n += crypto.getRandomValues(SMALL).length; }
                return n;
            }
            """,
            "randomValuesSmall()");

        // 65,536 bytes is the per-call maximum the standard allows; anything larger is a QuotaExceededError.
        _randomValuesLarge = Row(
            """
            var LARGE = new Uint8Array(65536);
            function randomValuesLarge() {
                var n = 0;
                for (var i = 0; i < 8; i++) { n += crypto.getRandomValues(LARGE).length; }
                return n;
            }
            """,
            "randomValuesLarge()");

        _randomUuid = Row(
            """
            function randomUuid() {
                var n = 0;
                for (var i = 0; i < 200; i++) { n += crypto.randomUUID().length; }
                return n;
            }
            """,
            "randomUuid()");
    }

    private static IsolatedScript Row(string fixture, string call)
        => WebApiBenchmarkSupport.DeterministicRow(WebApiFeatures.Crypto, fixture, call);

    /// <summary>500 fills of a 32-byte view.</summary>
    [Benchmark]
    public JsValue RandomValuesSmall() => _randomValuesSmall.Run();

    /// <summary>Eight fills of the 64 KiB per-call maximum.</summary>
    [Benchmark]
    public JsValue RandomValuesLarge() => _randomValuesLarge.Run();

    /// <summary>200 UUIDs.</summary>
    [Benchmark]
    public JsValue RandomUuid() => _randomUuid.Run();
}
