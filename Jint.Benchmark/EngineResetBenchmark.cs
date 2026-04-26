using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Simulates a server-side script execution loop where each iteration creates/reuses
/// an engine, loads setup scripts (utility functions), binds CLR objects,
/// then executes user-provided code.
///
/// Three workload profiles:
/// - Light: short scripts typical of tool dispatch (parse args, format output) — setup cost dominates
/// - Heavy: CPU-intensive scripts (large data processing) — script execution dominates
/// - IO: scripts that call async CLR methods (simulated) — allocations matter, wall-clock is I/O-bound
/// </summary>
[MemoryDiagnoser]
public class EngineResetBenchmark
{
    private Engine _reusableLight = null!;
    private Engine _reusableHeavy = null!;
    private Engine _reusableIO = null!;
    private Prepared<Script> _consoleShim;
    private Prepared<Script> _utilityLibrary;
    private Prepared<Script>[] _lightScripts = null!;
    private Prepared<Script> _heavyScript;
    private Prepared<Script> _ioScript;

    private static void ConfigureEngine(Options options)
    {
        options.LimitMemory(32_000_000);
        options.TimeoutInterval(TimeSpan.FromSeconds(10));
        options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(10);
        options.LimitRecursion(64);
        options.Strict();
        options.Interop.AllowGetType = false;
        options.Interop.AllowSystemReflection = false;
        options.Interop.AllowWrite = false;
        options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
    }

    private static void BindClrObjects(Engine engine)
    {
        engine.SetValue("__log", new Action<string>(_ => { }));
        engine.SetValue("config", new { MaxRetries = 3, Timeout = 5000, Region = "us-east-1" });
    }

    private static void BindIOClrObjects(Engine engine)
    {
        BindClrObjects(engine);
        engine.SetValue("fetchData", new Func<string, Task<string>>(key =>
            Task.FromResult("{\"id\":1,\"name\":\"test\",\"values\":[1,2,3,4,5]}")));
        engine.SetValue("sendResult", new Func<string, Task<bool>>(data =>
            Task.FromResult(true)));
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _consoleShim = Engine.PrepareScript("""
            var console = {
                log(...a) { __log(a.map(String).join(' ')); },
                error(...a) { __log('ERROR: ' + a.map(String).join(' ')); },
                warn(...a) { __log('WARN: ' + a.map(String).join(' ')); },
                info(...a) { __log('INFO: ' + a.map(String).join(' ')); }
            };
            """, "console-shim.js");

        _utilityLibrary = Engine.PrepareScript("""
            function parseCSV(text) {
                var lines = text.split('\n');
                var headers = lines[0].split(',');
                var result = [];
                for (var i = 1; i < lines.length; i++) {
                    if (!lines[i].trim()) continue;
                    var values = lines[i].split(',');
                    var obj = {};
                    for (var j = 0; j < headers.length; j++) {
                        obj[headers[j].trim()] = values[j] ? values[j].trim() : '';
                    }
                    result.push(obj);
                }
                return result;
            }
            function transformRecords(records, filterFn, mapFn) {
                return records.filter(filterFn).map(mapFn);
            }
            function summarize(data) {
                var total = data.reduce(function(s, r) { return s + (r.value || 0); }, 0);
                return JSON.stringify({ count: data.length, total: Math.round(total * 100) / 100, items: data });
            }
            """, "utility-library.js");

        _lightScripts = new[]
        {
            Engine.PrepareScript("""
                var csv = 'id,name,score\n1,Alice,95\n2,Bob,82\n3,Charlie,91';
                var data = parseCSV(csv);
                var result = transformRecords(data,
                    function(r) { return parseInt(r.score) > 85; },
                    function(r) { return { name: r.name, value: parseInt(r.score) }; });
                summarize(result);
                """),
            Engine.PrepareScript("""
                var items = [];
                for (var i = 0; i < 100; i++) {
                    items.push({ id: i, value: Math.sin(i) * 100, label: 'item_' + i });
                }
                var filtered = items.filter(function(x) { return x.value > 0; });
                JSON.stringify({ count: filtered.length, avg: filtered.reduce(function(s,x) { return s + x.value; }, 0) / filtered.length });
                """),
            Engine.PrepareScript("""
                var text = 'The quick brown fox jumps over the lazy dog';
                var words = text.split(' ');
                var freq = {};
                words.forEach(function(w) { freq[w] = (freq[w] || 0) + 1; });
                var sorted = Object.keys(freq).sort();
                JSON.stringify({ wordCount: words.length, uniqueWords: sorted.length, frequencies: freq });
                """),
            Engine.PrepareScript("""
                var matrix = [];
                for (var i = 0; i < 10; i++) {
                    matrix[i] = [];
                    for (var j = 0; j < 10; j++) {
                        matrix[i][j] = i * 10 + j;
                    }
                }
                var sum = 0;
                for (var i = 0; i < 10; i++) sum += matrix[i][i];
                JSON.stringify({ trace: sum, size: matrix.length });
                """),
            Engine.PrepareScript("""
                var data = { users: [], total: 0 };
                for (var i = 0; i < 50; i++) {
                    data.users.push({ id: i, name: 'user' + i, active: i % 3 !== 0 });
                }
                data.total = data.users.filter(function(u) { return u.active; }).length;
                JSON.stringify(data.total);
                """),
        };

        // CPU-heavy: process 5000 records with sorting, aggregation, nested loops
        _heavyScript = Engine.PrepareScript("""
            var records = [];
            for (var i = 0; i < 5000; i++) {
                records.push({
                    id: i,
                    category: 'cat' + (i % 20),
                    value: Math.sin(i) * 1000 + Math.cos(i * 0.5) * 500,
                    name: 'item_' + i + '_' + String.fromCharCode(65 + (i % 26)),
                    tags: [i % 10, i % 7, i % 3]
                });
            }
            var grouped = {};
            records.forEach(function(r) {
                if (!grouped[r.category]) grouped[r.category] = [];
                grouped[r.category].push(r);
            });
            var summaries = Object.keys(grouped).map(function(cat) {
                var items = grouped[cat];
                items.sort(function(a, b) { return b.value - a.value; });
                var total = items.reduce(function(s, r) { return s + r.value; }, 0);
                return {
                    category: cat,
                    count: items.length,
                    avg: Math.round(total / items.length * 100) / 100,
                    top3: items.slice(0, 3).map(function(r) { return r.name; })
                };
            });
            summaries.sort(function(a, b) { return b.avg - a.avg; });
            JSON.stringify({ categories: summaries.length, topCategory: summaries[0].category });
            """);

        // I/O-bound: script calls async CLR methods then processes the result
        _ioScript = Engine.PrepareScript("""
            (async function() {
                var raw = await fetchData('key1');
                var data = JSON.parse(raw);
                var processed = {
                    id: data.id,
                    upperName: data.name.toUpperCase(),
                    sum: data.values.reduce(function(s, v) { return s + v; }, 0),
                    doubled: data.values.map(function(v) { return v * 2; })
                };
                var sent = await sendResult(JSON.stringify(processed));
                return JSON.stringify({ success: sent, processed: processed });
            })()
            """);

        // Warm up reusable engines
        _reusableLight = new Engine(ConfigureEngine);
        BindClrObjects(_reusableLight);
        _reusableLight.Execute(_consoleShim);
        _reusableLight.Execute(_utilityLibrary);
        _reusableLight.Evaluate(_lightScripts[0]);

        _reusableHeavy = new Engine(ConfigureEngine);
        BindClrObjects(_reusableHeavy);
        _reusableHeavy.Execute(_consoleShim);
        _reusableHeavy.Execute(_utilityLibrary);
        _reusableHeavy.Evaluate(_heavyScript);

        _reusableIO = new Engine(ConfigureEngine);
        BindIOClrObjects(_reusableIO);
        _reusableIO.Execute(_consoleShim);
        _reusableIO.Execute(_utilityLibrary);
        _reusableIO.EvaluateAsync(_ioScript).GetAwaiter().GetResult();
    }

    // ── Light scripts: setup cost dominates ─────────────────────────────

    [Benchmark(Baseline = true)]
    public JsValue Light_NewEngine_10x()
    {
        JsValue last = JsValue.Undefined;
        for (var i = 0; i < 10; i++)
        {
            var engine = new Engine(ConfigureEngine);
            BindClrObjects(engine);
            engine.Execute(_consoleShim);
            engine.Execute(_utilityLibrary);
            last = engine.Evaluate(_lightScripts[i % _lightScripts.Length]);
        }
        return last;
    }

    [Benchmark]
    public JsValue Light_ResetState_10x()
    {
        JsValue last = JsValue.Undefined;
        for (var i = 0; i < 10; i++)
        {
            _reusableLight.ResetState();
            BindClrObjects(_reusableLight);
            _reusableLight.Execute(_consoleShim);
            _reusableLight.Execute(_utilityLibrary);
            last = _reusableLight.Evaluate(_lightScripts[i % _lightScripts.Length]);
        }
        return last;
    }

    // ── Heavy scripts: script execution dominates ───────────────────────

    [Benchmark]
    public JsValue Heavy_NewEngine_10x()
    {
        JsValue last = JsValue.Undefined;
        for (var i = 0; i < 10; i++)
        {
            var engine = new Engine(ConfigureEngine);
            BindClrObjects(engine);
            engine.Execute(_consoleShim);
            engine.Execute(_utilityLibrary);
            last = engine.Evaluate(_heavyScript);
        }
        return last;
    }

    [Benchmark]
    public JsValue Heavy_ResetState_10x()
    {
        JsValue last = JsValue.Undefined;
        for (var i = 0; i < 10; i++)
        {
            _reusableHeavy.ResetState();
            BindClrObjects(_reusableHeavy);
            _reusableHeavy.Execute(_consoleShim);
            _reusableHeavy.Execute(_utilityLibrary);
            last = _reusableHeavy.Evaluate(_heavyScript);
        }
        return last;
    }

    // ── I/O-bound scripts: allocations matter, CPU work is light ────────

    [Benchmark]
    public async Task<JsValue> IO_NewEngine_10x()
    {
        JsValue last = JsValue.Undefined;
        for (var i = 0; i < 10; i++)
        {
            var engine = new Engine(ConfigureEngine);
            BindIOClrObjects(engine);
            engine.Execute(_consoleShim);
            engine.Execute(_utilityLibrary);
            last = await engine.EvaluateAsync(_ioScript);
        }
        return last;
    }

    [Benchmark]
    public async Task<JsValue> IO_ResetState_10x()
    {
        JsValue last = JsValue.Undefined;
        for (var i = 0; i < 10; i++)
        {
            _reusableIO.ResetState();
            BindIOClrObjects(_reusableIO);
            _reusableIO.Execute(_consoleShim);
            _reusableIO.Execute(_utilityLibrary);
            last = await _reusableIO.EvaluateAsync(_ioScript);
        }
        return last;
    }
}
