using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The tokenizer/scanner regex shapes missing from the match()-oriented suites: a global
/// <c>exec()</c> while-loop, <c>matchAll</c> iteration, named-group access per match, and a
/// sticky-flag scanner. Text is synthesized deterministically (~100 KB with embedded tokens);
/// one full scan per op.
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
public class RegExpExecLoopBenchmark
{
    private Engine _engine = null!;
    private Prepared<Script> _execWhileLoop;
    private Prepared<Script> _matchAllIterate;
    private Prepared<Script> _namedGroupsAccess;
    private Prepared<Script> _stickyExec;

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

    [GlobalSetup]
    public void Setup()
    {
        _engine = new Engine();
        _engine.Execute(SetupSource);

        _execWhileLoop = Engine.PrepareScript(ExecWhileLoopSource);

        _matchAllIterate = Engine.PrepareScript("""
            function f() {
                var n = 0;
                for (var m of text.matchAll(/id-(\d+)/g)) { n += m[1].length; }
                return n;
            }
            f();
            """);

        _namedGroupsAccess = Engine.PrepareScript(NamedGroupsAccessSource);

        // sticky scanner: every exec must match exactly at lastIndex
        _stickyExec = Engine.PrepareScript("""
            function f() {
                var re = /\w+ /y;
                var n = 0;
                var m;
                while ((m = re.exec(tokenText)) !== null) { n++; }
                return n;
            }
            f();
            """);

        _engine.Evaluate(_execWhileLoop);
        _engine.Evaluate(_matchAllIterate);
        _engine.Evaluate(_namedGroupsAccess);
        _engine.Evaluate(_stickyExec);
    }

    [Benchmark]
    public JsValue ExecWhileLoop() => _engine.Evaluate(_execWhileLoop);

    [Benchmark]
    public JsValue MatchAllIterate() => _engine.Evaluate(_matchAllIterate);

    [Benchmark]
    public JsValue NamedGroupsAccess() => _engine.Evaluate(_namedGroupsAccess);

    [Benchmark]
    public JsValue StickyExec() => _engine.Evaluate(_stickyExec);
}
