using BenchmarkDotNet.Attributes;

namespace Jint.Benchmark;

/// <summary>
/// Isolates the cost of compiling regex literals (and dynamic <c>new RegExp</c>) when a fresh Engine runs the
/// same script from source on every iteration — the common "one Engine per request/sandbox" shape. Without a
/// process-wide compiled-Regex cache each iteration re-adapts every pattern to a .NET Regex; with one, only the
/// first iteration pays. Matching work is kept minimal so the measurement reflects compilation, not execution.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class RegExpFreshEngineBenchmark
{
    private const string Script = """
        var patterns = [
            /foo\d+bar/g,
            /[a-z]+@[a-z0-9.]+\.[a-z]{2,}/i,
            /^\s*(\w+)\s*=\s*(.+)$/m,
            /\b\d{4}-\d{2}-\d{2}\b/,
            /(https?):\/\/([^\/\s]+)(\/[^\s]*)?/i,
            /<([a-z][a-z0-9]*)\b[^>]*>(.*?)<\/\1>/is,
            /[À-ſ]+/u,
            /(?:abc|def|ghi)+\d*/g,
            /\$\{([^}]+)\}/g,
            /^(?!.*\bnot\b).*$/m,
            /[A-Z][a-z]+(?:\s[A-Z][a-z]+)*/,
            /\d+(?:\.\d+)?(?:e[+-]?\d+)?/i
        ];
        var dyn = new RegExp("a" + "b+c", "g");
        var hits = 0;
        for (var i = 0; i < patterns.length; i++) {
            if (patterns[i].test("sample-123-text")) hits++;
        }
        if (dyn.test("abbbc")) hits++;
        hits;
        """;

    private Prepared<Script> _prepared;

    [GlobalSetup]
    public void Setup()
    {
        _prepared = Engine.PrepareScript(Script, strict: true);
    }

    // Fresh Engine + fresh parse each call: every regex literal is re-adapted unless a shared cache holds it.
    [Benchmark]
    public void FreshEngineSource()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(Script);
    }

    // Fresh Engine but prepared script: the per-AST-node cache already survives, so this isolates the
    // remaining per-Engine cost and acts as a no-regression guard for the cached path.
    [Benchmark]
    public void FreshEnginePrepared()
    {
        var engine = new Engine(static options => options.Strict());
        engine.Execute(_prepared);
    }
}
