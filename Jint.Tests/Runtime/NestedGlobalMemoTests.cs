namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the nested-scope global-read chain memo in JintIdentifierExpression:
/// a pinned identity-stable chain must still observe every legal way a binding can appear
/// between the reading scope and the global environment.
/// </summary>
public class NestedGlobalMemoTests
{
    [Fact]
    public void SloppyEvalInjectionIntoPinnedChainIsObserved()
    {
        var engine = new Engine();
        // The chain [pooled loop env] -> [run's reused fn env] -> [host's fn env] -> global stays
        // identity-stable across the two run() calls, so only the injection epoch can reveal the
        // eval-hoisted `g` in host's var env. Without it the second run() would keep reading the
        // global through the memo.
        var result = engine.Evaluate("""
            var g = 1;
            function host() {
                var out = '';
                function run() {
                    for (let i = 0; i < 3; i++) { out += typeof g + ';'; }
                }
                run();
                eval("var g = 'injected';");
                run();
                return out;
            }
            host();
            """).AsString();

        result.Should().Be("number;number;number;string;string;string;");
    }

    [Fact]
    public void SloppyEvalInjectionRedirectsNestedWrites()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var gw = 0;
            function hostw() {
                function run(v) {
                    for (let i = 0; i < 1; i++) { gw = v; }
                }
                run(1);
                eval("var gw = 'local';");
                run(2);
                return gw + ':' + globalThis.gw;
            }
            hostw();
            """).AsString();

        // after injection the write must land on host's binding; the global keeps run(1)'s value
        result.Should().Be("2:1");
    }

    [Fact]
    public void AnnexBBlockFunctionShadowIsObserved()
    {
        var engine = new Engine();
        // sloppy block-level function: the var binding pre-exists from function entry (AnnexB),
        // so reads before the block see undefined and after it the function
        var result = engine.Evaluate("""
            var out2 = '';
            function host3() {
                function run() {
                    for (let i = 0; i < 2; i++) { out2 += typeof hoisted + ';'; }
                }
                run();
                { function hoisted() {} }
                run();
                return out2;
            }
            host3();
            """).AsString();

        result.Should().Be("undefined;undefined;function;function;");
    }

    [Fact]
    public void WithEnvironmentBlocksTheMemo()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            var w = 'global';
            function f() {
                var out = '';
                for (let i = 0; i < 2; i++) { out += w + ';'; }
                with ({ w: 'with' }) {
                    for (let i = 0; i < 2; i++) { out += w + ';'; }
                }
                for (let i = 0; i < 2; i++) { out += w + ';'; }
                return out;
            }
            f();
            """).AsString();

        result.Should().Be("global;global;with;with;global;global;");
    }

    [Fact]
    public void SharedPreparedScriptResolvesPerEngine()
    {
        var prepared = Engine.PrepareScript("""
            (function () {
                var t = '';
                for (let i = 0; i < 3; i++) { t += gv; }
                return t;
            })();
            """);

        var engine1 = new Engine();
        engine1.Execute("var gv = 'a';");
        var engine2 = new Engine();
        engine2.Execute("var gv = 'b';");

        for (var i = 0; i < 3; i++)
        {
            engine1.Evaluate(prepared).AsString().Should().Be("aaa");
            engine2.Evaluate(prepared).AsString().Should().Be("bbb");
        }
    }

    [Fact]
    public void GlobalLexicalShadowAfterMemoizationIsObserved()
    {
        var engine = new Engine();
        engine.Execute("""
            globalThis.gp = 'prop';
            function r() {
                var t = '';
                for (let i = 0; i < 2; i++) { t += gp; }
                return t;
            }
            """);

        engine.Evaluate("r();").AsString().Should().Be("propprop");

        // a global let now shadows the plain global property; _lexicalMutations invalidates
        engine.Execute("let gp = 'lexical';");
        engine.Evaluate("r();").AsString().Should().Be("lexicallexical");
    }

    [Fact]
    public void DeletedGlobalThrowsAfterMemoization()
    {
        var engine = new Engine();
        engine.Execute("""
            globalThis.gd = 'here';
            function r() {
                var t = '';
                for (let i = 0; i < 2; i++) { t += gd; }
                return t;
            }
            """);

        engine.Evaluate("r();").AsString().Should().Be("herehere");

        engine.Execute("delete globalThis.gd;");
        var ex = Invoking(() => engine.Evaluate("r();")).Should().ThrowExactly<Jint.Runtime.JavaScriptException>().Which;
        ex.Message.Should().Contain("gd is not defined");
    }

    [Fact]
    public void PooledLoopEnvReattachmentKeepsCorrectChain()
    {
        var engine = new Engine();
        // the same pooled loop env instance is re-attached under different function activations;
        // the memo's chain-link identity must miss (or match correctly) — values stay per-call
        var result = engine.Evaluate("""
            var acc = '';
            function makeReader(tag) {
                return function reader() {
                    for (let i = 0; i < 2; i++) { acc += tag + ':' + gshared + ';'; }
                };
            }
            var gshared = 'X';
            var r1 = makeReader('a');
            var r2 = makeReader('b');
            r1(); r2(); r1();
            acc;
            """).AsString();

        result.Should().Be("a:X;a:X;b:X;b:X;a:X;a:X;");
    }
}
