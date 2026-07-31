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
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all five scripts, so a
/// row was measured on an engine carrying its siblings' globals — all five declare <c>i</c>, three declare
/// <c>a</c> and <c>k</c>, and two declare <c>m</c>, so a row inherited whichever sibling wrote the array
/// last and with it whatever backing storage that left behind — as well as their handler-tree and
/// per-call-site state. The rows still measure warm indexed access, and engine construction and warm-up
/// stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to
/// any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class ElementAccessBenchmark
{
    private IsolatedScript _read;
    private IsolatedScript _write;
    private IsolatedScript _readModifyWrite;
    private IsolatedScript _chainedRead;
    private IsolatedScript _appendWrite;

    private static Engine CreateEngine() => new(static options => options.Strict());

    [GlobalSetup]
    public void Setup()
    {
        _read = IsolatedScript.Warm(Engine.PrepareScript("""
            var a = []; for (var k = 0; k < 1024; k++) a[k] = k;
            var s = 0;
            for (var i = 0; i < 1000000; i++) s = (s + a[i & 1023]) & 1023;
            s;
            """, strict: true), CreateEngine);

        _write = IsolatedScript.Warm(Engine.PrepareScript("""
            var a = []; for (var k = 0; k < 1024; k++) a[k] = 0;
            for (var i = 0; i < 1000000; i++) a[i & 1023] = i & 1023;
            a[0];
            """, strict: true), CreateEngine);

        _readModifyWrite = IsolatedScript.Warm(Engine.PrepareScript("""
            var a = []; for (var k = 0; k < 1024; k++) a[k] = 0;
            for (var i = 0; i < 1000000; i++) { var j = i & 1023; a[j] = (a[j] + 1) & 1023; }
            a[0];
            """, strict: true), CreateEngine);

        _chainedRead = IsolatedScript.Warm(Engine.PrepareScript("""
            var m = []; for (var r = 0; r < 4; r++) { m[r] = []; for (var c = 0; c < 4; c++) m[r][c] = r * 4 + c; }
            var s = 0;
            for (var i = 0; i < 1000000; i++) s = (s + m[i & 3][(i >> 2) & 3]) & 1023;
            s;
            """, strict: true), CreateEngine);

        // the MMulti/VMulti build pattern: every write is an append at the current length
        // into a fresh array (index masking would turn appends into overwrites, so the signal
        // includes the JsNumber boxing of the stored values like the cube itself does)
        _appendWrite = IsolatedScript.Warm(Engine.PrepareScript("""
            var sink = 0;
            for (var i = 0; i < 100000; i++) {
                var m = [];
                m[0] = i; m[1] = i; m[2] = i; m[3] = i;
                sink = (sink + m[3]) & 1023;
            }
            sink;
            """, strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue Read() => _read.Run();

    [Benchmark]
    public JsValue Write() => _write.Run();

    [Benchmark]
    public JsValue ReadModifyWrite() => _readModifyWrite.Run();

    [Benchmark]
    public JsValue ChainedRead() => _chainedRead.Run();

    [Benchmark]
    public JsValue AppendWrite() => _appendWrite.Run();
}
