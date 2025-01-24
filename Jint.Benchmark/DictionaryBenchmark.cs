using Jint.Collections;
using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class DictionaryBenchmark
{
    private static readonly string[] _keys =
    {
        "some",
        "key and",
        "another",
        "varying",
        "---the --",
        "keys and more",
        "aa bbb",
        "asasd asd ",
        "asdsad asd as s",
        "asdadsasa",
        "23323232323",
        "asdadsada sa213"
    };

    [Params(0, 2, 3, 5, 8, 9, 10)]
    public int N { get; set; }

    [Benchmark]
    public void HybridDictionary()
    {
        var hybridDictionary = new HybridDictionary<object>();
        for (var i = 0; i < N; i++)
        {
            hybridDictionary[_keys[i]] = _keys;
        }

        foreach (var key in _keys)
        {
            hybridDictionary.ContainsKey(key);
        }
    }

    [Benchmark]
    public void Dictionary()
    {
        var dictionary = new Dictionary<string, object>();
        for (var i = 0; i < N; i++)
        {
            dictionary.Add(_keys[i], _keys);
        }

        foreach (var key in _keys)
        {
            dictionary.ContainsKey(key);
        }
    }

    [Benchmark]
    public void StringDictionarySlim()
    {
        var dictionary = new StringDictionarySlim<object>();
        for (var i = 0; i < N; i++)
        {
            dictionary[_keys[i]] =_keys;
        }

        foreach (var key in _keys)
        {
            dictionary.ContainsKey(key);
        }
    }
}
