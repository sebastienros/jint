using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates <c>String.prototype.replaceAll</c> over a string with ~8k matches — the shape where the
/// replacement loop runs often enough for its per-match costs to matter. The interesting row is the
/// <em>functional</em> replacer: the loop invokes a user callback once per match, so the dispatch
/// decision (and the argument array behind it) is paid per match unless it is hoisted.
///
/// <para><b>Rows.</b> <c>ReplaceAllFunction</c> is the callback row. <c>ReplaceAllPattern</c> is the
/// control: a string replacement containing <c>$</c>, which defeats the <c>string.Replace</c>
/// short-circuit and therefore walks the <em>same</em> match loop, appends through the same
/// <c>ValueStringBuilder</c>, and never constructs or consults a callback invoker at all. A change to
/// how the callback is dispatched cannot move it — if it does, the delta is measurement noise or an
/// object-layout effect, not a win. (The single-match <c>String.prototype.replace</c> is deliberately
/// not covered: one call per invocation leaves nothing to amortise, and on a fixture this size the row
/// would measure the two substring copies rather than the call.)</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs the fixture so each engine owns its own <c>haystack</c> — and warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). Engine construction and warm-up stay in
/// <c>[GlobalSetup]</c> and never enter the measurement; the rows measure warm replacement.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringReplaceBenchmark
{
    private IsolatedScript _replaceAllFunction;
    private IsolatedScript _replaceAllPattern;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine(static options => options.Strict = true);
        // 8192 repetitions of "abcdefgh" (~64K chars) => exactly 8192 non-overlapping "ab" matches.
        engine.Execute("""
            var haystack = "";
            for (var i = 0; i < 8192; i++) {
                haystack += "abcdefgh";
            }
            """);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _replaceAllFunction = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                return haystack.replaceAll("ab", function (match, offset, whole) { return match; }).length;
            })();
            """, strict: true), CreateEngine);

        _replaceAllPattern = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                return haystack.replaceAll("ab", "[$&]").length;
            })();
            """, strict: true), CreateEngine);
    }

    /// <summary>~8k invocations of an interpreted callback, one per match.</summary>
    [Benchmark]
    public JsValue ReplaceAllFunction() => _replaceAllFunction.Run();

    /// <summary>Control: same match loop, same builder, no callback.</summary>
    [Benchmark]
    public JsValue ReplaceAllPattern() => _replaceAllPattern.Run();
}
