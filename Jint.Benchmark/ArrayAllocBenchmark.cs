using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Measures allocation of array creation, which previously paid one per-array <c>length</c>
/// PropertyDescriptor. Fresh engine per invocation (mirrors ObjectAccessBenchmark) so each [Benchmark]
/// reports the full allocation of building N arrays.
/// </summary>
[MemoryDiagnoser]
public class ArrayAllocBenchmark
{
    private const int Iterations = 200_000;

    private readonly Prepared<Script> _literalEmpty;
    private readonly Prepared<Script> _newArray;
    private readonly Prepared<Script> _literalSmall;
    private readonly Prepared<Script> _pushLoop;

    public ArrayAllocBenchmark()
    {
        _literalEmpty = Engine.PrepareScript(
            $"var s=0; for (var i=0;i<{Iterations};++i){{ var a=[]; a.length=4; s+=a.length; }} s;");

        _newArray = Engine.PrepareScript(
            $"var s=0; for (var i=0;i<{Iterations};++i){{ var a=new Array(8); s+=a.length; }} s;");

        _literalSmall = Engine.PrepareScript(
            $"var s=0; for (var i=0;i<{Iterations};++i){{ var a=[i,i+1,i+2]; s+=a.length; }} s;");

        _pushLoop = Engine.PrepareScript(
            $"var s=0; for (var i=0;i<{Iterations};++i){{ var a=[]; a.push(i); a.push(i+1); s+=a.length; }} s;");
    }

    [Benchmark]
    public void LiteralEmpty() => new Engine().Evaluate(_literalEmpty);

    [Benchmark]
    public void NewArray() => new Engine().Evaluate(_newArray);

    [Benchmark]
    public void LiteralSmall() => new Engine().Evaluate(_literalSmall);

    [Benchmark]
    public void PushLoop() => new Engine().Evaluate(_pushLoop);
}
