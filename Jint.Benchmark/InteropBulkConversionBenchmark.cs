using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Bulk JS-array → CLR collection argument conversion — exercises DefaultTypeConverter's
/// object[]-to-generic-collection path (Activator.CreateInstance(List&lt;&gt;) + per-element
/// recursive Convert).
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

    private Engine _engine = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engine = new Engine();
        _engine.SetValue("sink", new Sink());
        _engine.Execute("const data = []; for (let i = 0; i < 1000; i++) data.push(i);");

        // Warm the conversion caches.
        _engine.Execute("sink.TakeList(data); sink.TakeArray(data);");
    }

    [Benchmark]
    public void JsArrayToListOfInt()
    {
        _engine.Execute("sink.TakeList(data)");
    }

    [Benchmark]
    public void JsArrayToIntArray()
    {
        _engine.Execute("sink.TakeArray(data)");
    }
}
