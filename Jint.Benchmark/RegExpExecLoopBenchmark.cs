using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The tokenizer/scanner regex shapes missing from the match()-oriented suites: a global
/// <c>exec()</c> while-loop, <c>matchAll</c> iteration, named-group access per match, and a
/// sticky-flag scanner. Text is synthesized deterministically (~100 KB with embedded tokens);
/// one full scan per op.
///
/// <para><b>Engine isolation.</b> Every row gets its own engine — built by <c>CreateEngine</c>, which
/// re-runs <see cref="SetupSource"/> so each engine owns its own <c>text</c>/<c>tokenText</c> — and
/// warmed with its own script and nothing else (see <see cref="IsolatedScript"/>). It used to be one
/// engine warmed with all four row scripts, so each row was measured on an engine carrying the other
/// three rows' globals (every one of them declares <c>f</c>, so they collide outright), their
/// handler-tree entries and their per-call-site caches, which makes a row's number depend on which
/// siblings exist and on what a change did to <em>them</em>. The rows still measure warm scanning, and
/// engine construction and warm-up stay in <c>[GlobalSetup]</c>, outside the measurement. <b>Numbers
/// from this class are not comparable to any published before the harness changed.</b></para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class RegExpExecLoopBenchmark
{
    private IsolatedScript _execWhileLoop;
    private IsolatedScript _matchAllIterate;
    private IsolatedScript _namedGroupsAccess;
    private IsolatedScript _stickyExec;

    internal const string SetupSource = """
        var text;
        var tokenText;
        (function () {
            var seed = 20260711;
            var parts = [];
            for (var i = 0; i < 4000; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                parts.push('word' + (seed & 1023) + ' ');
                if ((seed & 7) === 0) { parts.push('id-' + (seed >>> 20) + ' '); }
                if ((seed & 15) === 0) { parts.push('2026-07-' + (10 + (seed & 15)) + ' '); }
            }
            text = parts.join('');
            var tokens = [];
            for (var i = 0; i < 2500; i++) {
                seed = (seed * 1664525 + 1013904223) | 0;
                tokens.push('tok' + (seed & 255) + ' ');
            }
            tokenText = tokens.join('');
        })();
        """;

    internal const string ExecWhileLoopSource = """
        function f() {
            var re = /id-(\d+)/g;
            var n = 0;
            var m;
            while ((m = re.exec(text)) !== null) { n += m[1].length; }
            return n;
        }
        f();
        """;

    internal const string NamedGroupsAccessSource = """
        function f() {
            var re = /(?<y>\d{4})-(?<mo>\d{2})-(?<d>\d{2})/g;
            var n = 0;
            var m;
            while ((m = re.exec(text)) !== null) { n += m.groups.y.length + m.groups.d.length; }
            return n;
        }
        f();
        """;

    /// <summary>Builds a fresh engine carrying the fixture every row needs, and nothing else.</summary>
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.Execute(SetupSource);
        return engine;
    }

    [GlobalSetup]
    public void Setup()
    {
        _execWhileLoop = IsolatedScript.Warm(Engine.PrepareScript(ExecWhileLoopSource), CreateEngine);

        _matchAllIterate = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var n = 0;
                for (var m of text.matchAll(/id-(\d+)/g)) { n += m[1].length; }
                return n;
            }
            f();
            """), CreateEngine);

        _namedGroupsAccess = IsolatedScript.Warm(Engine.PrepareScript(NamedGroupsAccessSource), CreateEngine);

        // sticky scanner: every exec must match exactly at lastIndex
        _stickyExec = IsolatedScript.Warm(Engine.PrepareScript("""
            function f() {
                var re = /\w+ /y;
                var n = 0;
                var m;
                while ((m = re.exec(tokenText)) !== null) { n++; }
                return n;
            }
            f();
            """), CreateEngine);
    }

    [Benchmark]
    public JsValue ExecWhileLoop() => _execWhileLoop.Run();

    [Benchmark]
    public JsValue MatchAllIterate() => _matchAllIterate.Run();

    [Benchmark]
    public JsValue NamedGroupsAccess() => _namedGroupsAccess.Run();

    [Benchmark]
    public JsValue StickyExec() => _stickyExec.Run();
}
