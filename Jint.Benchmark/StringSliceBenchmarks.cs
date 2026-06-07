using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Isolates String.prototype.slice/substring/substr cost over a large (128K char) string —
/// the dromaeo-object-string shape where large slice results are assigned and discarded
/// (measured: slice = 89.9%, substring = 9.2% of its 1.26 GB/op allocations).
/// SliceSmall guards the small-result path; SliceThenRead guards lazy-materialization cost
/// when the result is actually consumed.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringSliceBenchmarks
{
    private Engine _engine = null!;
    private Prepared<Script> _sliceLargeDiscard;
    private Prepared<Script> _substringLargeDiscard;
    private Prepared<Script> _sliceSmall;
    private Prepared<Script> _sliceThenRead;

    [GlobalSetup]
    public void Setup()
    {
        _sliceLargeDiscard = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.slice(0);
                    ret = str.slice(12000, -1);
                }
                return ret.length;
            })();
            """, strict: true);

        _substringLargeDiscard = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.substring(0);
                    ret = str.substring(12000, str.length - 1);
                }
                return ret.length;
            })();
            """, strict: true);

        _sliceSmall = Engine.PrepareScript("""
            (function() {
                var ret = null;
                for (var i = 0; i < 5000; i++) {
                    ret = str.slice(15000, 15005);
                    ret = str.slice(-1);
                    ret = str.substr(12000, 5);
                }
                return ret.length;
            })();
            """, strict: true);

        _sliceThenRead = Engine.PrepareScript("""
            (function() {
                var n = 0;
                for (var i = 0; i < 5000; i++) {
                    var t = str.slice(12000, -1);
                    n += t.length;
                    n += t.charCodeAt(100);
                }
                return n;
            })();
            """, strict: true);

        _engine = new Engine(static options => options.Strict());
        // Build the shared ~128K char base string once (dromaeo-style doubling).
        _engine.Execute("""
            var str = "aB3$xQ9pLm0_kEwZ";
            while (str.length < 131072) {
                str += str;
            }
            """);
        _engine.Evaluate(_sliceLargeDiscard);
        _engine.Evaluate(_substringLargeDiscard);
        _engine.Evaluate(_sliceSmall);
        _engine.Evaluate(_sliceThenRead);
    }

    [Benchmark]
    public JsValue SliceLargeDiscard() => _engine.Evaluate(_sliceLargeDiscard);

    [Benchmark]
    public JsValue SubstringLargeDiscard() => _engine.Evaluate(_substringLargeDiscard);

    [Benchmark]
    public JsValue SliceSmall() => _engine.Evaluate(_sliceSmall);

    [Benchmark]
    public JsValue SliceThenRead() => _engine.Evaluate(_sliceThenRead);
}
