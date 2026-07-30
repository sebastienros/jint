using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the eval / new Function compile pipeline (dromaeo-core-eval shape).
/// SameSource variants hit a repeated identical source (cacheable); DistinctSources
/// variants generate a unique source per call and guard the cache-miss path.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and nothing
/// else (see <see cref="IsolatedScript"/>). The compile caches this class is about are engine-owned, and
/// the two SameSource rows eval <em>byte-identical</em> source, so on the single engine this used to
/// share, whichever was warmed first populated the cache the other was then measured against — on top of
/// the globals the scripts collide on (all three declare <c>res</c> and <c>n</c>; the SameSource pair also
/// share <c>cmd</c>). The rows still measure the warm pipeline, and engine construction and warm-up stay
/// in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class EvalCacheBenchmarks
{
    private IsolatedScript _evalSameSource;
    private IsolatedScript _newFunctionSameSource;
    private IsolatedScript _evalDistinctSources;

    private static Engine CreateEngine() => new(static options => options.Strict());

    [GlobalSetup]
    public void Setup()
    {
        // The eval'd source mirrors dromaeo-core-eval's cmd: parse-heavy relative to its execution.
        const string Source = """
            var cmd = "var s='';for(var i=0;i<50;i++){s+='a';}res=s;";
            """;

        _evalSameSource = IsolatedScript.Warm(Engine.PrepareScript(Source + """
            var res = null;
            for (var n = 0; n < 50; n++) {
                eval(cmd);
            }
            res;
            """, strict: true), CreateEngine);

        _newFunctionSameSource = IsolatedScript.Warm(Engine.PrepareScript(Source + """
            var res = null;
            for (var n = 0; n < 50; n++) {
                (new Function(cmd))();
            }
            res;
            """, strict: true), CreateEngine);

        // globalThis counter keeps every eval'd source unique across benchmark ops so this
        // permanently measures the cache-miss path (including any eviction policy).
        _evalDistinctSources = IsolatedScript.Warm(Engine.PrepareScript("""
            globalThis.__evalCounter = globalThis.__evalCounter || 0;
            var res = null;
            for (var n = 0; n < 50; n++) {
                var u = ++globalThis.__evalCounter;
                res = eval("var v" + u + " = " + u + "; v" + u + " + 1;");
            }
            res;
            """, strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue EvalSameSource() => _evalSameSource.Run();

    [Benchmark]
    public JsValue NewFunctionSameSource() => _newFunctionSameSource.Run();

    [Benchmark]
    public JsValue EvalDistinctSources() => _evalDistinctSources.Run();
}
