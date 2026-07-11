using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Template-literal building beyond the trivial single-interpolation case: many slots, nesting,
/// number-to-string interpolation, and tagged templates (whose spec-cached strings array should
/// not be re-materialized per call). 100k evaluations per op inside a function frame.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class TemplateLiteralBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _manyInterpolations;
    private Prepared<Script> _nestedTemplate;
    private Prepared<Script> _numberInterpolation;
    private Prepared<Script> _taggedTemplate;

    internal const string ManyInterpolationsSource = """
        function f() {
            var a = 'aa', b = 'bb', c = 'cc', d = 'dd', e = 'ee', g = 'gg', h = 'hh';
            var n = 0;
            for (var i = 0; i < 100000; i++) {
                var k = i & 15;
                var r = `${a}-${b}-${c}-${d}-${e}-${g}-${h}-${k}`;
                n += r.length;
            }
            return n;
        }
        f();
        """;

    internal const string TaggedTemplateSource = """
        function tag(strings, a, b) { return strings.length + a + b; }
        function f() {
            var s = 0;
            for (var i = 0; i < 100000; i++) {
                s += tag`t ${i & 7} u ${i & 3} v`;
            }
            return s;
        }
        f();
        """;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();

        _manyInterpolations = Engine.PrepareScript(ManyInterpolationsSource);

        // a template whose interpolated expression is itself a template
        _nestedTemplate = Engine.PrepareScript("""
            function f() {
                var x = 'x';
                var n = 0;
                for (var i = 0; i < 100000; i++) {
                    var r = `outer ${`inner ${x}${i & 7}`} tail`;
                    n += r.length;
                }
                return n;
            }
            f();
            """);

        // number-to-string per iteration
        _numberInterpolation = Engine.PrepareScript("""
            function f() {
                var n = 0;
                for (var i = 0; i < 100000; i++) {
                    var r = `v=${i}`;
                    n += r.length;
                }
                return n;
            }
            f();
            """);

        _taggedTemplate = Engine.PrepareScript(TaggedTemplateSource);

        _engine.Evaluate(_manyInterpolations);
        _engine.Evaluate(_nestedTemplate);
        _engine.Evaluate(_numberInterpolation);
        _engine.Evaluate(_taggedTemplate);
    }

    [Benchmark]
    public JsValue ManyInterpolations() => _engine.Evaluate(_manyInterpolations);

    [Benchmark]
    public JsValue NestedTemplate() => _engine.Evaluate(_nestedTemplate);

    [Benchmark]
    public JsValue NumberInterpolation() => _engine.Evaluate(_numberInterpolation);

    [Benchmark]
    public JsValue TaggedTemplate() => _engine.Evaluate(_taggedTemplate);
}
