using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Compiling patterns the process-wide <c>RegExpParseCache</c> has never seen. Every other compile row in the suite
/// hits that cache after warm-up (<c>construct-100</c> uses ten distinct sources), which hides what a script building
/// patterns at run time pays; here each operation compiles 50 patterns made unique by a counter, so every one misses.
///
/// <para><b>Engine isolation.</b> One engine per row, built and warmed in <c>[GlobalSetup]</c> with that row's script
/// alone (see <see cref="IsolatedScript"/>).</para>
///
/// <para><b>Control.</b> <c>control-concat</c> builds the same 50 strings and compiles none, so a change to the regex
/// engine cannot reach it.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class RegExpColdCompileBenchmark
{
    private const string SetupSource = "var n = 0; var sink;";

    // Each row compiles 50 distinct patterns per operation; the counter keeps them distinct across operations too.
    private static readonly Dictionary<string, string> Shapes = new(StringComparer.Ordinal)
    {
        ["control-concat"] = "for (var i = 0; i < 50; i++) sink = '[a-z]+@[a-z0-9.]+\\\\.[a-z]{2,}' + (n++); sink.length",
        ["literal"] = "for (var i = 0; i < 50; i++) sink = new RegExp('needle' + (n++)); sink.source.length",
        ["email"] = "for (var i = 0; i < 50; i++) sink = new RegExp('[a-z]+@[a-z0-9.]+\\\\.[a-z]{2,}' + (n++)); sink.source.length",
        ["email-i"] = "for (var i = 0; i < 50; i++) sink = new RegExp('[a-z]+@[a-z0-9.]+\\\\.[a-z]{2,}' + (n++), 'i'); sink.source.length",
        ["classes-i"] = "for (var i = 0; i < 50; i++) sink = new RegExp('[\\\\u00c0-\\\\u024f][a-z\\\\u0370-\\\\u03ff]+\\\\w*' + (n++), 'i'); sink.source.length",
        ["uprop-u"] = "for (var i = 0; i < 50; i++) sink = new RegExp('\\\\p{L}+\\\\p{Nd}*' + (n++), 'u'); sink.source.length",
        ["alternation"] = "for (var i = 0; i < 50; i++) sink = new RegExp('(?:get|set|has|delete|clear|keys|values|entries)\\\\(' + (n++)); sink.source.length",
        ["tokenizer"] = "for (var i = 0; i < 50; i++) sink = new RegExp('\"(?:[^\"\\\\\\\\]|\\\\\\\\.)*\"|\\'(?:[^\\'\\\\\\\\]|\\\\\\\\.)*\\'|\\\\d+(?:\\\\.\\\\d+)?|[A-Za-z_$][\\\\w$]*' + (n++), 'g'); sink.source.length",
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
