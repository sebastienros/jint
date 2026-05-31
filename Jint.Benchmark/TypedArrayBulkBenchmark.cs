using Acornima;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Exercises the bulk byte-copy / byte-fill / vectorized-search paths of TypedArray methods on a large array,
/// so the per-element work dominates over parsing. Scripts are prepared once to isolate the operation cost.
/// </summary>
[MemoryDiagnoser]
public class TypedArrayBulkBenchmark
{
    [Params(10_000)]
    public int Size { get; set; }

    private Engine _engine = null!;
    private Prepared<Script> _set;
    private Prepared<Script> _setSelf;
    private Prepared<Script> _slice;
    private Prepared<Script> _copyWithin;
    private Prepared<Script> _fill;
    private Prepared<Script> _reverse;
    private Prepared<Script> _toReversed;
    private Prepared<Script> _with;
    private Prepared<Script> _indexOf;
    private Prepared<Script> _includes;
    private Prepared<Script> _construct;
    private int[] _clrInts = null!;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute($@"
var src = new Int32Array({Size});
for (var i = 0; i < {Size}; i++) {{ src[i] = i; }}
var dst = new Int32Array({Size});
var f64 = new Float64Array({Size});
");

        _clrInts = new int[Size];
        for (var i = 0; i < Size; i++)
        {
            _clrInts[i] = i;
        }

        _set = Engine.PrepareScript("dst.set(src);");
        // self-overlapping set (exercises the same-buffer same-type path)
        _setSelf = Engine.PrepareScript("src.set(src.subarray(0, src.length - 1), 1);");
        _slice = Engine.PrepareScript("src.slice();");
        _copyWithin = Engine.PrepareScript("src.copyWithin(1, 0);");
        _fill = Engine.PrepareScript("f64.fill(3.5);");
        _reverse = Engine.PrepareScript("src.reverse();");
        _toReversed = Engine.PrepareScript("src.toReversed();");
        _with = Engine.PrepareScript("src.with(0, 1);");
        // worst case: search misses, scanning the whole array
        _indexOf = Engine.PrepareScript("src.indexOf(-1);");
        _includes = Engine.PrepareScript("src.includes(-1);");
        // construction from a CLR array goes through FillTypedArrayInstance
        _construct = Engine.PrepareScript("new Int32Array(src);");
    }

    [Benchmark]
    public void Set() => _engine.Execute(_set);

    [Benchmark]
    public void SetSelfOverlapping() => _engine.Execute(_setSelf);

    [Benchmark]
    public void Slice() => _engine.Execute(_slice);

    [Benchmark]
    public void CopyWithin() => _engine.Execute(_copyWithin);

    [Benchmark]
    public void Fill() => _engine.Execute(_fill);

    [Benchmark]
    public void Reverse() => _engine.Execute(_reverse);

    [Benchmark]
    public void ToReversed() => _engine.Execute(_toReversed);

    [Benchmark]
    public void With() => _engine.Execute(_with);

    [Benchmark]
    public void IndexOf() => _engine.Execute(_indexOf);

    [Benchmark]
    public void Includes() => _engine.Execute(_includes);

    [Benchmark]
    public void ConstructFromTypedArray() => _engine.Execute(_construct);

    // Exercises Tier E: constructing a typed array directly from a CLR array (interop), which routes through
    // FillTypedArrayInstance. The JS path above goes through InitializeTypedArrayFromTypedArray instead.
    [Benchmark]
    public JsTypedArray ConstructFromClrArray() => _engine.Intrinsics.Int32Array.Construct(_clrInts);
}
