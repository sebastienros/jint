using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates String.prototype.split with a string separator over a large (~1M char) string —
/// the dromaeo-object-string "String Split on Char" shape (<c>tmpstr.split("a")</c>), which
/// produces tens of thousands of short segments. The non-empty-separator path previously routed
/// through <see cref="string.Split(string[], StringSplitOptions)"/>, allocating a throwaway
/// <c>string[]</c> result plus an internal match-position buffer on every call.
/// SplitOnChar/SplitOnMultiChar guard the segment-production cost; SplitEmpty guards the
/// already-optimal single-char branch (cached single-char JsStrings); SplitThenJoin guards the
/// consume-the-result case (segments are materialized) so the production change stays neutral there.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs the fixture so each engine owns its own <c>bigstr</c> — and warmed with its own script and
/// nothing else (see <see cref="IsolatedScript"/>). It used to be one engine warmed with all four row
/// scripts, so each row was measured on an engine carrying the other three rows' handler-tree entries
/// and per-call-site caches, and on a <c>bigstr</c> whose string representation the other rows had
/// already touched — which makes a row's number depend on which siblings exist and on what a change did
/// to <em>them</em>. The rows still measure warm splitting, and engine construction and warm-up stay in
/// <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers from this class are not comparable to any
/// published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringSplitBenchmark
{
    private IsolatedScript _splitOnChar;
    private IsolatedScript _splitOnMultiChar;
    private IsolatedScript _splitEmpty;
    private IsolatedScript _splitThenJoin;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine(static options => options.Strict = true);
        // Deterministic ~1M char source cycling 'a'..'y' (dromaeo-style: 'a' every 25 chars,
        // average 24-char segments; "xy" also appears once per cycle).
        engine.Execute("""
            var bigstr = "";
            for (var i = 0; i < 16384; i++) {
                bigstr += String.fromCharCode(97 + (i % 25));
            }
            while (bigstr.length < 1048576) {
                bigstr += bigstr;
            }
            """);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _splitOnChar = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 20; i++) {
                    ret = bigstr.split("a");
                }
                return ret.length;
            })();
            """, strict: true), CreateEngine);

        _splitOnMultiChar = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 20; i++) {
                    ret = bigstr.split("xy");
                }
                return ret.length;
            })();
            """, strict: true), CreateEngine);

        _splitEmpty = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5; i++) {
                    ret = bigstr.split("");
                }
                return ret.length;
            })();
            """, strict: true), CreateEngine);

        _splitThenJoin = IsolatedScript.Warm(Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 20; i++) {
                    ret = bigstr.split("a").join("a");
                }
                return ret.length;
            })();
            """, strict: true), CreateEngine);
    }

    [Benchmark]
    public JsValue SplitOnChar() => _splitOnChar.Run();

    [Benchmark]
    public JsValue SplitOnMultiChar() => _splitOnMultiChar.Run();

    [Benchmark]
    public JsValue SplitEmpty() => _splitEmpty.Run();

    [Benchmark]
    public JsValue SplitThenJoin() => _splitThenJoin.Run();
}
