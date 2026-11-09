using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// Measures replacement over about 8,000 matches. The functional and substitution-pattern rows
/// share the general string match loop. The literal string and regex rows measure their respective
/// fast paths. Each row warms its own engine and script in setup.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class StringReplaceBenchmark
{
    private IsolatedScript _replaceAllFunction;
    private IsolatedScript _replaceAllPattern;
    private IsolatedScript _replaceAllLiteral;
    private IsolatedScript _replaceRegexLiteral;

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

        _replaceAllLiteral = IsolatedScript.Warm(
            Engine.PrepareScript("""haystack.replaceAll("ab", "x").length;""", strict: true), CreateEngine);
        _replaceRegexLiteral = IsolatedScript.Warm(
            Engine.PrepareScript("""haystack.replace(/ab/g, "x").length;""", strict: true), CreateEngine);
    }

    /// <summary>~8k invocations of an interpreted callback, one per match.</summary>
    [Benchmark]
    public JsValue ReplaceAllFunction() => _replaceAllFunction.Run();

    /// <summary>Control: same match loop, same builder, no callback.</summary>
    [Benchmark]
    public JsValue ReplaceAllPattern() => _replaceAllPattern.Run();

    [Benchmark]
    public JsValue ReplaceAllLiteral() => _replaceAllLiteral.Run();

    [Benchmark]
    public JsValue ReplaceRegexLiteral() => _replaceRegexLiteral.Run();
}
