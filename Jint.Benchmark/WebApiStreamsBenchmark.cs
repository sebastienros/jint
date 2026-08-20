using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// <c>ReadableStream</c> pump throughput — <see cref="WebApiFeatures.Streams"/>,
/// https://streams.spec.whatwg.org/#rs.
///
/// <para>Three rows over the same 256-chunk source, because the three ways a script drains a stream cost
/// different things. <see cref="PumpWithReader"/> is the explicit reader loop
/// (<c>getReader()</c> + <c>await reader.read()</c>), so it measures one promise and one read request per
/// chunk. <see cref="PumpWithAsyncIteration"/> is <c>for await</c> over the stream itself
/// (https://streams.spec.whatwg.org/#rs-asynciterator), which adds WebIDL's default asynchronous iterator
/// object on top of that reader. <see cref="PipeThroughTransform"/> runs the chunks through a
/// <c>TransformStream</c> first (https://streams.spec.whatwg.org/#readable-stream-pipe-to), so each chunk
/// crosses a writable side, a transformer and a second readable side before the consumer sees it.</para>
///
/// <para><b>Nothing here sleeps, starts a thread or reads a clock.</b> Every promise a stream hands out is
/// an ordinary engine promise settled by a job on the engine's own queue, and <c>Engine.Evaluate</c> drains
/// that queue before it returns — so one measured operation is one complete pump, start to finish, on the
/// calling thread.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine carrying
/// <see cref="WebApiFeatures.Streams"/> alone, warmed with its own fixture and its own script and nothing
/// else — see <see cref="WebApiBenchmarkSupport"/>. Engine construction stays in <c>[GlobalSetup]</c> and
/// never enters the measurement. The rows' answers cannot be the script's completion value — the script
/// completes before the event loop runs — so each pump stores its total in <c>result</c>, which
/// <c>[GlobalSetup]</c> checks once against the sum the chunk count implies.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory(WebApiBenchmarkSupport.Category)]
public class WebApiStreamsBenchmark
{
    /// <summary>Chunks per pump. 256 keeps an operation in the millisecond range without hiding per-chunk cost.</summary>
    private const int Chunks = 256;

    /// <summary>Sum of <c>0..Chunks-1</c> — what a pump that lost or duplicated a chunk would fail to produce.</summary>
    private const int ExpectedSum = Chunks * (Chunks - 1) / 2;

    /// <summary>
    /// The source every row drains. Enqueuing from <c>start</c> fills the queue before any read arrives, so
    /// the rows measure the consuming side rather than a producer's back-pressure schedule.
    /// </summary>
    private static readonly string Source =
        $$"""
          var CHUNKS = {{Chunks}};
          var result = -1;
          function makeSource() {
              return new ReadableStream({
                  start(controller) {
                      for (var i = 0; i < CHUNKS; i++) { controller.enqueue(i); }
                      controller.close();
                  }
              });
          }
          """;

    private IsolatedScript _pumpWithReader;
    private IsolatedScript _pumpWithAsyncIteration;
    private IsolatedScript _pipeThroughTransform;

    [GlobalSetup]
    public void Setup()
    {
        _pumpWithReader = Row(
            """

            async function pumpWithReader() {
                var reader = makeSource().getReader();
                var sum = 0;
                for (;;) {
                    var next = await reader.read();
                    if (next.done) { break; }
                    sum += next.value;
                }
                return sum;
            }
            """,
            "pumpWithReader()",
            ExpectedSum);

        _pumpWithAsyncIteration = Row(
            """

            async function pumpWithAsyncIteration() {
                var sum = 0;
                for await (const chunk of makeSource()) { sum += chunk; }
                return sum;
            }
            """,
            "pumpWithAsyncIteration()",
            ExpectedSum);

        // The transform doubles each chunk, so the expected total doubles too — which is what proves the
        // chunks actually went through the transformer rather than round the side of it.
        _pipeThroughTransform = Row(
            """

            function makeTransform() {
                return new TransformStream({ transform(chunk, controller) { controller.enqueue(chunk * 2); } });
            }
            async function pipeThroughTransform() {
                var sum = 0;
                for await (const chunk of makeSource().pipeThrough(makeTransform())) { sum += chunk; }
                return sum;
            }
            """,
            "pipeThroughTransform()",
            ExpectedSum * 2);
    }

    private static IsolatedScript Row(string functions, string call, int expected)
    {
        var script = IsolatedScript.Warm(
            $"{call}.then(function (total) {{ result = total; }});",
            WebApiBenchmarkSupport.WithFixture(WebApiFeatures.Streams, Source + functions));

        WebApiBenchmarkSupport.Expect(script.Engine, "result", expected.ToString(CultureInfo.InvariantCulture));
        return script;
    }

    /// <summary>256 chunks through an explicit <c>getReader()</c> loop.</summary>
    [Benchmark]
    public JsValue PumpWithReader() => _pumpWithReader.Run();

    /// <summary>256 chunks through <c>for await</c> on the stream itself.</summary>
    [Benchmark]
    public JsValue PumpWithAsyncIteration() => _pumpWithAsyncIteration.Run();

    /// <summary>256 chunks through a <c>TransformStream</c> and out the other side.</summary>
    [Benchmark]
    public JsValue PipeThroughTransform() => _pipeThroughTransform.Run();
}
