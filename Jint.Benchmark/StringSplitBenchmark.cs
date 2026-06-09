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
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringSplitBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _splitOnChar;
    private Prepared<Script> _splitOnMultiChar;
    private Prepared<Script> _splitEmpty;
    private Prepared<Script> _splitThenJoin;

    [GlobalSetup]
    public void Setup()
    {
        _splitOnChar = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 20; i++) {
                    ret = bigstr.split("a");
                }
                return ret.length;
            })();
            """, strict: true);

        _splitOnMultiChar = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 20; i++) {
                    ret = bigstr.split("xy");
                }
                return ret.length;
            })();
            """, strict: true);

        _splitEmpty = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5; i++) {
                    ret = bigstr.split("");
                }
                return ret.length;
            })();
            """, strict: true);

        _splitThenJoin = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 20; i++) {
                    ret = bigstr.split("a").join("a");
                }
                return ret.length;
            })();
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        // Deterministic ~1M char source cycling 'a'..'y' (dromaeo-style: 'a' every 25 chars,
        // average 24-char segments; "xy" also appears once per cycle).
        _engine.Execute("""
            var bigstr = "";
            for (var i = 0; i < 16384; i++) {
                bigstr += String.fromCharCode(97 + (i % 25));
            }
            while (bigstr.length < 1048576) {
                bigstr += bigstr;
            }
            """);
        _engine.Evaluate(_splitOnChar);
        _engine.Evaluate(_splitOnMultiChar);
        _engine.Evaluate(_splitEmpty);
        _engine.Evaluate(_splitThenJoin);
    }

    [Benchmark]
    public JsValue SplitOnChar() => _engine.Evaluate(_splitOnChar);

    [Benchmark]
    public JsValue SplitOnMultiChar() => _engine.Evaluate(_splitOnMultiChar);

    [Benchmark]
    public JsValue SplitEmpty() => _engine.Evaluate(_splitEmpty);

    [Benchmark]
    public JsValue SplitThenJoin() => _engine.Evaluate(_splitThenJoin);
}
