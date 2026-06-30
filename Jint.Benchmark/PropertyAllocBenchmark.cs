using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Increment 0 of VALUE_TYPED_DESCRIPTOR_SPIKE: measures the allocation footprint of the two hottest
/// data-property-creating shapes in real JS:
///   - constructor objects (this.a=a;this.b=b;this.c=c) — the access-binary-trees / TreeNode pattern that
///     falsified the array-backed property bag (#2532),
///   - object literals ({a:..,b:..,c:..}) — the BuildObjectFast path.
/// Fresh engine per invocation (mirrors ObjectAccessBenchmark) so each [Benchmark] reports the full
/// allocation of building N small objects. Compare the Allocated column against the PropertyDescriptor
/// share reported by --profile-propdesc to decide GO/NO-GO.
/// </summary>
[MemoryDiagnoser]
public class PropertyAllocBenchmark
{
    private const int Iterations = 200_000;

    private readonly Prepared<Script> _constructor3;
    private readonly Prepared<Script> _literal3;
    private readonly Prepared<Script> _constructor1;
    private readonly Prepared<Script> _literal8;
    private readonly Prepared<Script> _constructor3Cached;
    private readonly Prepared<Script> _literal2Cached;

    public PropertyAllocBenchmark()
    {
        _constructor3 = Engine.PrepareScript(
            $"function T(a,b,c){{this.a=a;this.b=b;this.c=c;}} var s=0; for (var i=0;i<{Iterations};++i){{ var t=new T(i,i+1,i+2); s+=t.a; }} s;");

        _literal3 = Engine.PrepareScript(
            $"var s=0; for (var i=0;i<{Iterations};++i){{ var t={{a:i,b:i+1,c:i+2}}; s+=t.a; }} s;");

        _constructor1 = Engine.PrepareScript(
            $"function N(v){{this.value=v;}} var s=0; for (var i=0;i<{Iterations};++i){{ var n=new N(i); s+=n.value; }} s;");

        _literal8 = Engine.PrepareScript(
            $"var s=0; for (var i=0;i<{Iterations};++i){{ var t={{a:i,b:i,c:i,d:i,e:i,f:i,g:i,h:i}}; s+=t.a; }} s;");

        // Cached-value variants: field values stay in the small-integer cache (m = i & 1023), so JsNumber
        // boxing of the stored values does not dilute the signal — the Allocated delta isolates the
        // per-object construction cost (object + slot storage), which in-object properties cuts to a single
        // allocation for objects within the in-object capacity.
        _constructor3Cached = Engine.PrepareScript(
            $"function T(a,b,c){{this.a=a;this.b=b;this.c=c;}} var s=0; for (var i=0;i<{Iterations};++i){{ var m=i&1023; var t=new T(m,m,m); s=(s+t.a)&1023; }} s;");

        _literal2Cached = Engine.PrepareScript(
            $"var s=0; for (var i=0;i<{Iterations};++i){{ var m=i&1023; var t={{a:m,b:m}}; s=(s+t.a)&1023; }} s;");
    }

    [Benchmark]
    public void Constructor3() => new Engine().Evaluate(_constructor3);

    [Benchmark]
    public void Literal3() => new Engine().Evaluate(_literal3);

    [Benchmark]
    public void Constructor1() => new Engine().Evaluate(_constructor1);

    [Benchmark]
    public void Literal8() => new Engine().Evaluate(_literal8);

    [Benchmark]
    public void Constructor3Cached() => new Engine().Evaluate(_constructor3Cached);

    [Benchmark]
    public void Literal2Cached() => new Engine().Evaluate(_literal2Cached);
}
