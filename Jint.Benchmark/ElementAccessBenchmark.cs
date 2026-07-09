using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates computed dense-array element access (<c>a[i]</c>, <c>a[i] = v</c>, <c>m[i][j]</c>) — the
/// dromaeo-3d-cube MMulti shape, where chained numeric-index reads/writes previously paid the full
/// Reference + Engine.GetValue/PutValue pipeline per access. Values are masked to stay in the
/// small-integer cache so the signal is the access cost, not JsNumber boxing. Arrays are pre-filled
/// so reads/writes hit existing dense slots (the fast path); the chained case mirrors MMulti's
/// <c>m[i][j]</c> double-indexing.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ElementAccessBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _read;
    private Prepared<Script> _write;
    private Prepared<Script> _readModifyWrite;
    private Prepared<Script> _chainedRead;
    private Prepared<Script> _appendWrite;

    [GlobalSetup]
    public void Setup()
    {
        _read = Engine.PrepareScript("""
            var a = []; for (var k = 0; k < 1024; k++) a[k] = k;
            var s = 0;
            for (var i = 0; i < 1000000; i++) s = (s + a[i & 1023]) & 1023;
            s;
            """, strict: true);

        _write = Engine.PrepareScript("""
            var a = []; for (var k = 0; k < 1024; k++) a[k] = 0;
            for (var i = 0; i < 1000000; i++) a[i & 1023] = i & 1023;
            a[0];
            """, strict: true);

        _readModifyWrite = Engine.PrepareScript("""
            var a = []; for (var k = 0; k < 1024; k++) a[k] = 0;
            for (var i = 0; i < 1000000; i++) { var j = i & 1023; a[j] = (a[j] + 1) & 1023; }
            a[0];
            """, strict: true);

        _chainedRead = Engine.PrepareScript("""
            var m = []; for (var r = 0; r < 4; r++) { m[r] = []; for (var c = 0; c < 4; c++) m[r][c] = r * 4 + c; }
            var s = 0;
            for (var i = 0; i < 1000000; i++) s = (s + m[i & 3][(i >> 2) & 3]) & 1023;
            s;
            """, strict: true);

        // the MMulti/VMulti build pattern: every write is an append at the current length
        // into a fresh array (index masking would turn appends into overwrites, so the signal
        // includes the JsNumber boxing of the stored values like the cube itself does)
        _appendWrite = Engine.PrepareScript("""
            var sink = 0;
            for (var i = 0; i < 100000; i++) {
                var m = [];
                m[0] = i; m[1] = i; m[2] = i; m[3] = i;
                sink = (sink + m[3]) & 1023;
            }
            sink;
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        _engine.Evaluate(_read);
        _engine.Evaluate(_write);
        _engine.Evaluate(_readModifyWrite);
        _engine.Evaluate(_chainedRead);
        _engine.Evaluate(_appendWrite);
    }

    [Benchmark]
    public JsValue Read() => _engine.Evaluate(_read);

    [Benchmark]
    public JsValue Write() => _engine.Evaluate(_write);

    [Benchmark]
    public JsValue ReadModifyWrite() => _engine.Evaluate(_readModifyWrite);

    [Benchmark]
    public JsValue ChainedRead() => _engine.Evaluate(_chainedRead);

    [Benchmark]
    public JsValue AppendWrite() => _engine.Evaluate(_appendWrite);
}
