using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// <c>TextEncoder</c> and <c>TextDecoder</c> — <see cref="WebApiFeatures.Encoding"/>,
/// https://encoding.spec.whatwg.org/#api.
///
/// <para>Four rows, because the three sizes exercise different halves of the cost.
/// <see cref="RoundTripSmall"/> is dominated by the per-call ceremony — WebIDL argument conversion,
/// allocating the result <c>Uint8Array</c>, wrapping the decoded string — which is what a script encoding
/// a few dozen bytes in a loop actually pays. <see cref="RoundTripLarge"/> moves ~140 KiB per direction, so
/// it is dominated by the transcoding itself and by the JS-string ↔ managed-buffer crossing.
/// <see cref="DecodeStreaming"/> feeds the same bytes back in ~1 KiB chunks cut at boundaries that split
/// multi-byte sequences, which is the only row that exercises the decoder's held-over-bytes state machine
/// and its final flush. <see cref="EncodeIntoSmall"/> is the allocation-free encoder path a host reaches
/// for when it owns the destination buffer.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine carrying
/// <see cref="WebApiFeatures.Encoding"/> alone, warmed with its own fixture and its own script and nothing
/// else — see <see cref="WebApiBenchmarkSupport"/>. Building the ~140 KiB source string, encoding it and
/// slicing it into chunks all happen in <c>[GlobalSetup]</c> and never enter the measurement, as does
/// engine construction.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory(WebApiBenchmarkSupport.Category)]
public class WebApiEncodingBenchmark
{
    /// <summary>
    /// Mixed ASCII and non-ASCII, so neither the encoder nor the decoder can take an all-ASCII shortcut for
    /// the whole input while most of it still is ASCII — the realistic shape of text a service handles.
    /// </summary>
    private const string SmallText =
        """
        var SMALL = 'The quick brown fox jumps over the lazy dog — ☕ 1234567890';
        var ENCODER = new TextEncoder();
        var DECODER = new TextDecoder();
        """;

    /// <summary>Roughly 140 KiB of the same mixture, built once.</summary>
    private const string LargeText =
        """
        var ENCODER = new TextEncoder();
        var DECODER = new TextDecoder();
        var parts = [];
        for (var i = 0; i < 4096; i++) { parts.push('lorem ipsum dolor sit amet — ☕ ' + i + '\n'); }
        var LARGE = parts.join('');
        """;

    private IsolatedScript _roundTripSmall;
    private IsolatedScript _roundTripLarge;
    private IsolatedScript _decodeStreaming;
    private IsolatedScript _encodeIntoSmall;

    [GlobalSetup]
    public void Setup()
    {
        _roundTripSmall = Row(
            SmallText +
            """

            function roundTripSmall() {
                var n = 0;
                for (var i = 0; i < 500; i++) { n += DECODER.decode(ENCODER.encode(SMALL)).length; }
                return n;
            }
            """,
            "roundTripSmall()");

        _roundTripLarge = Row(
            LargeText +
            """

            function roundTripLarge() {
                var n = 0;
                for (var i = 0; i < 4; i++) { n += DECODER.decode(ENCODER.encode(LARGE)).length; }
                return n;
            }
            """,
            "roundTripLarge()");

        // 997 is deliberately coprime with everything in sight, so the chunk boundaries land inside the
        // multi-byte sequences and every chunk but the last leaves the decoder holding bytes over. A
        // chunked decode that never straddles a sequence measures the easy half of the state machine.
        _decodeStreaming = Row(
            LargeText +
            """

            var LARGE_BYTES = ENCODER.encode(LARGE);
            var CHUNKS = [];
            for (var i = 0; i < LARGE_BYTES.length; i += 997) {
                CHUNKS.push(LARGE_BYTES.subarray(i, Math.min(i + 997, LARGE_BYTES.length)));
            }
            function decodeStreaming() {
                var n = 0;
                for (var r = 0; r < 4; r++) {
                    var decoder = new TextDecoder();
                    for (var i = 0; i < CHUNKS.length; i++) { n += decoder.decode(CHUNKS[i], { stream: true }).length; }
                    n += decoder.decode().length;
                }
                return n;
            }
            """,
            "decodeStreaming()");

        _encodeIntoSmall = Row(
            SmallText +
            """

            var TARGET = new Uint8Array(1024);
            function encodeIntoSmall() {
                var n = 0;
                for (var i = 0; i < 500; i++) { n += ENCODER.encodeInto(SMALL, TARGET).written; }
                return n;
            }
            """,
            "encodeIntoSmall()");
    }

    private static IsolatedScript Row(string fixture, string call)
        => WebApiBenchmarkSupport.DeterministicRow(WebApiFeatures.Encoding, fixture, call);

    /// <summary>500 encode + decode round trips of a ~60-character string.</summary>
    [Benchmark]
    public JsValue RoundTripSmall() => _roundTripSmall.Run();

    /// <summary>Four encode + decode round trips of ~140 KiB.</summary>
    [Benchmark]
    public JsValue RoundTripLarge() => _roundTripLarge.Run();

    /// <summary>Four streaming decodes of the same ~140 KiB in ~1 KiB chunks, plus the flush.</summary>
    [Benchmark]
    public JsValue DecodeStreaming() => _decodeStreaming.Run();

    /// <summary>500 encodes into a host-owned destination view, which allocates nothing per call.</summary>
    [Benchmark]
    public JsValue EncodeIntoSmall() => _encodeIntoSmall.Run();
}
