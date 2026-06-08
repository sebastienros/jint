using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the eval / new Function compile pipeline (dromaeo-core-eval shape).
/// SameSource variants hit a repeated identical source (cacheable); DistinctSources
/// variants generate a unique source per call and guard the cache-miss path.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class EvalCacheBenchmarks
{
    private Engine _engine = null!;
    private Prepared<Script> _evalSameSource;
    private Prepared<Script> _newFunctionSameSource;
    private Prepared<Script> _evalDistinctSources;

    [GlobalSetup]
    public void Setup()
    {
        // The eval'd source mirrors dromaeo-core-eval's cmd: parse-heavy relative to its execution.
        const string Source = """
            var cmd = "var s='';for(var i=0;i<50;i++){s+='a';}res=s;";
            """;

        _evalSameSource = Engine.PrepareScript(Source + """
            var res = null;
            for (var n = 0; n < 50; n++) {
                eval(cmd);
            }
            res;
            """, strict: true);

        _newFunctionSameSource = Engine.PrepareScript(Source + """
            var res = null;
            for (var n = 0; n < 50; n++) {
                (new Function(cmd))();
            }
            res;
            """, strict: true);

        // globalThis counter keeps every eval'd source unique across benchmark ops so this
        // permanently measures the cache-miss path (including any eviction policy).
        _evalDistinctSources = Engine.PrepareScript("""
            globalThis.__evalCounter = globalThis.__evalCounter || 0;
            var res = null;
            for (var n = 0; n < 50; n++) {
                var u = ++globalThis.__evalCounter;
                res = eval("var v" + u + " = " + u + "; v" + u + " + 1;");
            }
            res;
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        _engine.Evaluate(_evalSameSource);
        _engine.Evaluate(_newFunctionSameSource);
        _engine.Evaluate(_evalDistinctSources);
    }

    [Benchmark]
    public JsValue EvalSameSource() => _engine.Evaluate(_evalSameSource);

    [Benchmark]
    public JsValue NewFunctionSameSource() => _engine.Evaluate(_newFunctionSameSource);

    [Benchmark]
    public JsValue EvalDistinctSources() => _engine.Evaluate(_evalDistinctSources);
}
