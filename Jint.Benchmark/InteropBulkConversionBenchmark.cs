using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Bulk JS-array → CLR collection argument conversion — exercises DefaultTypeConverter's
/// object[]-to-generic-collection path (Activator.CreateInstance(List&lt;&gt;) + per-element
/// recursive Convert).
///
/// <para><b>Engine isolation.</b> Both rows get their own engine — built by <see cref="CreateEngine"/>,
/// which re-runs the fixture script so each engine owns its own <c>data</c> array — warmed with that
/// row's own script and nothing else. It used to be one engine warmed with
/// <c>sink.TakeList(data); sink.TakeArray(data);</c>, so each row was measured with the other row's
/// conversion caches already populated. The rows still measure warm conversion, and engine construction
/// and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement.</para>
/// </summary>
[MemoryDiagnoser]
public class InteropBulkConversionBenchmark
{
    public sealed class Sink
    {
        public int Count { get; private set; }

        public void TakeList(List<int> values) => Count = values.Count;
        public void TakeArray(int[] values) => Count = values.Length;
    }

    private Engine _engineList = null!;
    private Engine _engineArray = null!;

    /// <summary>Builds a fresh engine carrying the fixture both rows need, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.SetValue("sink", new Sink());
        engine.Execute("const data = []; for (let i = 0; i < 1000; i++) data.push(i);");
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Warm the conversion caches — each engine with its own row's script and nothing else.
        _engineList = CreateEngine();
        _engineList.Execute("sink.TakeList(data)");

        _engineArray = CreateEngine();
        _engineArray.Execute("sink.TakeArray(data)");
    }

    [Benchmark]
    public void JsArrayToListOfInt()
    {
        _engineList.Execute("sink.TakeList(data)");
    }

    [Benchmark]
    public void JsArrayToIntArray()
    {
        _engineArray.Execute("sink.TakeArray(data)");
    }
}
