using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// One row per pattern shape whose cost the regex engine decides on its own: greedy runs, case-insensitive and
/// nullable-prefix scans, literal searches, per-match overhead in split and global replace, captures, Unicode
/// properties and run-time compilation. Each runs over the same deterministic 64 KiB lowercase subject.
///
/// <para><b>Engine isolation.</b> Each row is a <see cref="Shape"/> parameter, and BenchmarkDotNet runs every parameter
/// case in its own process with its own <c>[GlobalSetup]</c>, so a row's engine is built and warmed with that row's
/// script alone (see <see cref="IsolatedScript"/>). Engine construction and warm-up stay outside the measurement.</para>
///
/// <para><b>Control.</b> <c>control-indexOf</c> scans the same subject with <c>String.prototype.indexOf</c> and touches
/// no regex code at all, so a change to the regex engine cannot reach it; a delta on it is the run's floor.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class RegExpShapeBenchmark
{
    private const string SetupSource = """
        var seed = 7;
        function rnd() { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed; }
        var parts = [];
        for (var i = 0; i < 65536; i++) parts.push(String.fromCharCode(97 + rnd() % 25));
        var str = parts.join('');
        var csv = 'a , b,c ,d, e'.repeat(2000);
        """;

    private static readonly Dictionary<string, string> Shapes = new(StringComparer.Ordinal)
    {
        ["control-indexOf"] = "str.indexOf('zzzzzzzz')",
        ["greedy-dotstar-test"] = "/a.*a/.test(str)",
        ["greedy-dotstar-exec"] = "/a.*a/.exec(str)[0].length",
        ["literal-test"] = "/abcdefghij/.test(str)",
        ["literal-replace-g"] = "str.replace(/abcdefghij/g, 'x').length",
        ["icase-literal-test"] = "/ZZQ/i.test(str)",
        ["class-match-g"] = "str.match(/[aeiou]/g).length",
        ["capture-match-g"] = "str.match(/(\\w)\\w/g).length",
        ["char-split"] = "str.split(/a/).length",
        ["nullable-split"] = "csv.split(/\\s*,\\s*/).length",
        ["function-replace-g"] = "str.replace(/a/g, function (m) { return 'b'; }).length",
        ["template-replace-g"] = "str.replace(/(a)(b)?/g, '$2$1').length",
        ["exec-loop"] = "var re = /(b)(c)/g, n = 0, m; while ((m = re.exec(str)) !== null) n++; n",
        ["uprop-match-g"] = "str.match(/\\p{L}+/gu).length",
        // Groups inside an alternation inside a repeat: the backtracking frames trail registers here, which GregsEx #4
        // made variable-length (and measured 7-12% slower on this shape).
        ["grouped-alternation-repeat"] = "str.replace(/((a)|b)+c/g, '$1').length",
        ["construct-100"] = "for (var i = 0; i < 100; i++) new RegExp('x' + (i % 10) + 'y').test('ax3y'); 1",
    };

    public static IEnumerable<string> ShapeNames => Shapes.Keys;

    [ParamsSource(nameof(ShapeNames))]
    public string Shape { get; set; } = null!;

    private IsolatedScript _script;

    [GlobalSetup]
    public void Setup()
    {
        _script = IsolatedScript.Warm(Engine.PrepareScript(Shapes[Shape]), static () =>
        {
            var engine = new Engine();
            engine.Execute(SetupSource);
            return engine;
        });
    }

    [Benchmark]
    public JsValue Run() => _script.Run();
}
