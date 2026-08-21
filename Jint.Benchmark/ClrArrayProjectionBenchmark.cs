using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Measures the first crossing of a CLR array on an already constructed engine. Each row has its own engine,
/// and cycles through more source arrays than the recent-wrapper cache can retain, so every invocation measures
/// projection rather than a cache hit. The host arrays are allocated in setup and stay outside the measurement.
/// This complements the warm traversal rows in <see cref="InteropWrapperChurnBenchmark"/>: Copy pays O(N) time
/// and allocation here, while LiveView avoids that upfront work; after crossing, the native Copy snapshot can
/// traverse as fast as or faster than the wrapper.
/// </summary>
[MemoryDiagnoser]
public class ClrArrayProjectionBenchmark
{
    private const int SourceCount = 16;

    private Engine _copyEngine = null!;
    private Engine _liveViewEngine = null!;
    private int[][] _copySources = null!;
    private int[][] _liveViewSources = null!;
    private int _copyIndex;
    private int _liveViewIndex;

    [Params(0, 10, 100, 1000)]
    public int Length { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _copyEngine = new Engine();
        _liveViewEngine = new Engine(options => options.Interop.ArrayConversion = ArrayConversionMode.LiveView);
        _copySources = CreateSources();
        _liveViewSources = CreateSources();
    }

    [Benchmark(Baseline = true)]
    public JsValue FirstCrossing_Copy()
        => JsValue.FromObject(_copyEngine, _copySources[_copyIndex++ & (SourceCount - 1)]);

    [Benchmark]
    public JsValue FirstCrossing_LiveView()
        => JsValue.FromObject(_liveViewEngine, _liveViewSources[_liveViewIndex++ & (SourceCount - 1)]);

    private int[][] CreateSources()
    {
        var sources = new int[SourceCount][];
        for (var i = 0; i < sources.Length; i++)
        {
            sources[i] = new int[Length];
        }

        return sources;
    }
}
