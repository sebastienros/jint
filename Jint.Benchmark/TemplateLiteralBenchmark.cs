using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Template-literal building beyond the trivial single-interpolation case: many slots, nesting,
/// number-to-string interpolation, and tagged templates (whose spec-cached strings array should
/// not be re-materialized per call). 100k evaluations per op inside a function frame.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine, warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all four row
/// scripts, so each row was measured on an engine carrying the other three rows' globals (every one of
/// them declares <c>f</c>, so they collide outright), their handler-tree entries and their per-call-site
/// caches — including the per-site tagged-template strings cache this class exists to observe. That
/// makes a row's number depend on which siblings exist and on what a change did to <em>them</em>. The
/// rows still measure warm template building, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
///
/// <para>The Accumulate* rows build a 64 KB string with a template that takes its own previous value
/// (<c>s = `${s}${chunk}`</c>), which used to copy the accumulator on every evaluation and so cost O(n^2)
/// (issue #4460); they pair with <c>StringConcatLargeBenchmark.AssignAppendSmallChunks</c>, the same 4,096 x 16
/// characters through <c>+</c>. The four rows above them never substitute a string long enough to be joined
/// without copying, so they are the control for the short path.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class TemplateLiteralBenchmark
{
    private IsolatedScript _manyInterpolations;
    private IsolatedScript _nestedTemplate;
    private IsolatedScript _numberInterpolation;
    private IsolatedScript _taggedTemplate;
    private IsolatedScript _accumulateAppend;
    private IsolatedScript _accumulatePrepend;

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
        _manyInterpolations = IsolatedScript.Warm(ManyInterpolationsSource);

        // a template whose interpolated expression is itself a template
        _nestedTemplate = IsolatedScript.Warm("""
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
        _numberInterpolation = IsolatedScript.Warm("""
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

        _taggedTemplate = IsolatedScript.Warm(TaggedTemplateSource);

        _accumulateAppend = IsolatedScript.Warm("""
            function f() {
                var chunk = 'abcdefghijklmnop';
                var s = '';
                for (var i = 0; i < 4096; i++) {
                    s = `${s}${chunk}`;
                }
                return s.length;
            }
            f();
            """);

        _accumulatePrepend = IsolatedScript.Warm("""
            function f() {
                var chunk = 'abcdefghijklmnop';
                var s = '';
                for (var i = 0; i < 4096; i++) {
                    s = `${chunk}${s}`;
                }
                return s.length;
            }
            f();
            """);
    }

    [Benchmark]
    public JsValue ManyInterpolations() => _manyInterpolations.Run();

    [Benchmark]
    public JsValue NestedTemplate() => _nestedTemplate.Run();

    [Benchmark]
    public JsValue NumberInterpolation() => _numberInterpolation.Run();

    [Benchmark]
    public JsValue TaggedTemplate() => _taggedTemplate.Run();

    [Benchmark]
    public JsValue AccumulateAppend() => _accumulateAppend.Run();

    [Benchmark]
    public JsValue AccumulatePrepend() => _accumulatePrepend.Run();
}
