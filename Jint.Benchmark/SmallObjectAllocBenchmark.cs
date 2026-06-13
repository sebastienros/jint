using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Allocates many small objects via object literals in a tight loop. Object literals take the
/// <c>BuildObjectFast</c> path, which sizes the property bag to the known property count, so this
/// exercises the small-property-bag backing (linked list vs array) on the most common object shape.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "Gen0", "Gen1", "Gen2")]
public class SmallObjectAllocBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _script;

    [Params(1, 2, 3, 5, 8)]
    public int PropertyCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < PropertyCount; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append('p').Append(i).Append(": i + ").Append(i);
        }

        var source = $$"""
            var sink = 0;
            for (var i = 0; i < 50000; i++) {
                var o = { {{sb}} };
                sink += o.p0;
            }
            sink;
            """;

        _script = Engine.PrepareScript(source);
        _engine = new Engine();
    }

    [Benchmark]
    public JsValue CreateSmallObjects() => _engine.Evaluate(_script);
}
