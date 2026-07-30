using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Custom-indexer access from JS — the IndexerAccessor reflection path. Plain List&lt;T&gt; and
/// string-keyed generic dictionaries take specialized wrapper paths (GenericListWrapper /
/// TypeDescriptor) and never hit IndexerAccessor; a custom this[string] / this[int] type does.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine and its own bag, warmed with that row's
/// own script and nothing else. The two string-key rows used to share one engine warmed with
/// <c>bag['alpha']; bag['alpha'] = 1;</c>, so the get row was measured with the setter lane already
/// resolved and the set row with the getter lane already resolved. The rows still measure warm indexer
/// access, and engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the
/// measurement.</para>
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

    private Engine _engineStringGet = null!;
    private Engine _engineStringSet = null!;
    private Engine _engineIntGet = null!;

    /// <summary>Builds a fresh engine exposing a string-indexed bag as <c>bag</c>.</summary>
    private static Engine CreateStringEngine()
    {
        var engine = new Engine();
        engine.SetValue("bag", new StringIndexedBag());
        return engine;
    }

    /// <summary>Builds a fresh engine exposing an int-indexed bag as <c>bag</c>.</summary>
    private static Engine CreateIntEngine()
    {
        var engine = new Engine();
        engine.SetValue("bag", new IntIndexedBag());
        return engine;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Warm the indexer accessors — each engine with its own row's script and nothing else.
        _engineStringGet = CreateStringEngine();
        _engineStringGet.Execute("bag['alpha']");

        _engineStringSet = CreateStringEngine();
        _engineStringSet.Execute("bag['alpha'] = 1");

        _engineIntGet = CreateIntEngine();
        _engineIntGet.Execute("bag[1]");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void IndexerGet_StringKey()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineStringGet.Execute("bag['alpha']");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void IndexerSet_StringKey()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineStringSet.Execute("bag['alpha'] = 1");
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void IndexerGet_IntKey()
    {
        for (var i = 0; i < OperationsPerInvoke; i++) _engineIntGet.Execute("bag[1]");
    }
}
