using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class ObjectEnumerationBenchmark
{
    private const int N = 100;

    private Engine _engine = null!;
    private Prepared<Script> _keys;
    private Prepared<Script> _values;
    private Prepared<Script> _entries;
    private Prepared<Script> _keysFiltered;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute("""
var obj = {};
for (var i = 0; i < 100; i++) {
    obj['key' + i] = i;
}
var filtered = {};
for (var i = 0; i < 100; i++) {
    Object.defineProperty(filtered, 'key' + i, { value: i, enumerable: i % 2 === 0 });
}
""");

        _keys = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ Object.keys(obj); }}");
        _values = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ Object.values(obj); }}");
        _entries = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ Object.entries(obj); }}");
        // half the keys are non-enumerable: exercises the filtered/oversized-result path
        _keysFiltered = Engine.PrepareScript($"for (var n = 0; n < {N}; n++) {{ Object.keys(filtered); }}");
    }

    [Benchmark]
    public void Keys() => _engine.Execute(_keys);

    [Benchmark]
    public void Values() => _engine.Execute(_values);

    [Benchmark]
    public void Entries() => _engine.Execute(_entries);

    [Benchmark]
    public void KeysFiltered() => _engine.Execute(_keysFiltered);
}
