using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Custom-indexer access from JS — the IndexerAccessor reflection path. Plain List&lt;T&gt; and
/// string-keyed generic dictionaries take specialized wrapper paths (GenericListWrapper /
/// TypeDescriptor) and never hit IndexerAccessor; a custom this[string] / this[int] type does.
/// </summary>
[MemoryDiagnoser]
public class InteropIndexerBenchmark
{
    private const int OperationsPerInvoke = 1_000;

    public sealed class StringIndexedBag
    {
        private readonly Dictionary<string, int> _data = new() { ["alpha"] = 1, ["beta"] = 2 };

        public int this[string key]
        {
            get => _data[key];
            set => _data[key] = value;
        }

        public bool ContainsKey(string key) => _data.ContainsKey(key);
    }

    public sealed class IntIndexedBag
    {
        private readonly int[] _data = [1, 2, 3, 4];

        public int this[int index]
        {
            get => _data[index];
            set => _data[index] = value;
        }
    }

    private Engine _engineString = null!;
    private Engine _engineInt = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _engineString = new Engine();
        _engineString.SetValue("bag", new StringIndexedBag());
        _engineString.Execute("bag['alpha']; bag['alpha'] = 1;");

        _engineInt = new Engine();
        _engineInt.SetValue("bag", new IntIndexedBag());
        _engineInt.Execute("bag[1]; bag[1] = 2;");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void IndexerGet_StringKey()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineString.Execute("bag['alpha']");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void IndexerSet_StringKey()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineString.Execute("bag['alpha'] = 1");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void IndexerGet_IntKey()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineInt.Execute("bag[1]");
    }
}
