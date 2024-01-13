using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Jint.Extensions;

namespace Jint.Benchmark;

[RankColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class HashBenchmark
{

    [Params("i", "str", "Math", "encodeURIComponent")]
    public string Input { get; set; }

    [Benchmark(Baseline = true)]
    public int StringHashCode() => Input.GetHashCode();

    [Benchmark]
    public int StringOrdinalHashCode() => StringComparer.Ordinal.GetHashCode(Input);

    [Benchmark]
    public int Fnv1() => Hash.GetFNVHashCode(Input);

    /*
    [Benchmark]
    public ulong Hash3()
    {
        Span<byte> s1 = stackalloc byte[Input.Length * 2];
        Encoding.UTF8.TryGetBytes(Input, s1, out var written);
        return System.IO.Hashing.XxHash3.HashToUInt64(s1[..written]);
    }
    */
}
