using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Exercises the iterator-result hot path (for-of, spread, destructuring, Map/Set iteration). Each step of a
/// native iterator builds an IteratorResult; the value/done are read via Get(). Used to measure the inline
/// value/done storage that avoids two PropertyDescriptor allocations per step.
/// </summary>
[MemoryDiagnoser]
public class IterationBenchmark
{
    private const int N = 100_000;

    private readonly Prepared<Script> _forOfArray;
    private readonly Prepared<Script> _spreadArray;
    private readonly Prepared<Script> _mapIteration;
    private readonly Prepared<Script> _destructuring;

    public IterationBenchmark()
    {
        _forOfArray = Engine.PrepareScript(
            $"var a=[]; for (var i=0;i<{N};++i) a.push(i); var s=0; for (const x of a) {{ s+=x; }} s;");

        _spreadArray = Engine.PrepareScript(
            $"var a=[]; for (var i=0;i<{N};++i) a.push(i); var s=0; for (var k=0;k<10;++k) {{ var b=[...a]; s+=b.length; }} s;");

        _mapIteration = Engine.PrepareScript(
            $"var m=new Map(); for (var i=0;i<{N};++i) m.set(i,i); var s=0; for (const [k,v] of m) {{ s+=v; }} s;");

        _destructuring = Engine.PrepareScript(
            $"var a=[]; for (var i=0;i<{N};++i) a.push(i); var s=0; for (var k=0;k<{N};++k) {{ var [x,y,z]=a; s+=x+y+z; }} s;");
    }

    [Benchmark]
    public void ForOfArray() => new Engine().Evaluate(_forOfArray);

    [Benchmark]
    public void SpreadArray() => new Engine().Evaluate(_spreadArray);

    [Benchmark]
    public void MapIteration() => new Engine().Evaluate(_mapIteration);

    [Benchmark]
    public void Destructuring() => new Engine().Evaluate(_destructuring);
}
