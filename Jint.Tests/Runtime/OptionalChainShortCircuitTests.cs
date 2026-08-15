using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins ECMA-262 §13.3.9.1 (https://tc39.es/ecma262/#sec-optional-chaining-evaluation): an optional
/// chain short-circuits at the <c>?.</c> whose <em>own</em> base is nullish, and nowhere else. A link
/// that merely produces <c>undefined</c> — because the property is absent, because the callee returned
/// it, or because the value simply is <c>undefined</c> — continues the chain, and the next link throws.
/// <para>
/// The interpreter signals the short circuit by returning a marker up the chain. These tests exist
/// because that marker used to be <see cref="JsValue.Undefined"/> itself, compared with
/// <c>ReferenceEquals</c> — indistinguishable from a genuine <c>undefined</c>, so every expression that
/// happened to evaluate to the singleton was treated as a short circuit and swallowed the
/// <c>TypeError</c> it owed.
/// </para>
/// </summary>
public class OptionalChainShortCircuitTests
{
    /// <summary>
    /// Runs <paramref name="source"/> and reports the constructor name of whatever it threw, so a test
    /// pins the error <em>type</em> rather than a message that is free to change.
    /// </summary>
    private static string Throws(string source)
    {
        var engine = new Engine();
        return engine.Evaluate($$"""
            (function () {
                try {
                    {{source}};
                } catch (e) {
                    return e.constructor.name;
                }
                return 'did not throw';
            })()
            """).AsString();
    }

    private static JsValue Evaluate(string source) => new Engine().Evaluate(source);

    [Fact]
    public void CallingAValueThatIsUndefinedThrowsRatherThanShortCircuiting()
    {
        // The sequence expression yields the JsValue.Undefined singleton; nothing here is an optional
        // chain, so the call must throw.
        // (staging/sm/extensions/extension-methods-reject-null-undefined-this.js)
        Throws("(0, undefined)()").Should().Be("TypeError");
    }

    [Fact]
    public void CallingAnUndefinedNewTargetThrows()
    {
        // Engine.GetNewTarget hands back the JsValue.Undefined singleton for a [[Call]]ed function.
        // (staging/sm/class/newTargetDVG.js)
        Throws("(function () { new.target(); })()").Should().Be("TypeError");
    }

    [Fact]
    public void ANonOptionalLinkAfterAnOptionalOneStillThrows()
    {
        // `({})?.a` does not short-circuit — {} is not nullish — so it is a legitimate undefined and
        // the non-optional `['b']` that follows must throw.
        Throws("({})?.a['b']").Should().Be("TypeError");
        Throws("({})?.['a'].b").Should().Be("TypeError");
        Throws("({ a: { b: undefined } }).a?.b.b.c").Should().Be("TypeError");
    }

    [Fact]
    public void CallingTheUndefinedResultOfACompletedOptionalCallThrows()
    {
        // `b?.()` does not short-circuit: b is callable. It simply returns undefined, and calling that
        // undefined must throw.
        Throws("(({ a: { b: () => undefined } }).a.b?.())()").Should().Be("TypeError");
    }

    [Fact]
    public void ParenthesesEndTheChainSoTheNextLinkThrows()
    {
        // `(a?.b)` is a complete ChainExpression: its short circuit produces a real undefined, and the
        // member access outside the parentheses is not part of that chain.
        Throws("var a = null; (a?.b).c").Should().Be("TypeError");
        Throws("var a = null; (a?.b)()").Should().Be("TypeError");
    }

    [Fact]
    public void ShortCircuitStopsTheWholeChain()
    {
        Evaluate("undefined?.a").Should().BeUndefined();
        Evaluate("null?.a?.b").Should().BeUndefined();
        Evaluate("var a; a?.()").Should().BeUndefined();
        Evaluate("null?.a['b']().c").Should().BeUndefined();
        Evaluate("null?.['a'].b()['c']").Should().BeUndefined();
        Evaluate("null?.()().a['b']").Should().BeUndefined();
        Evaluate("({ a: { b: undefined } }).a.b?.()()()").Should().BeUndefined();
    }

    [Fact]
    public void OrdinaryChainsKeepResolving()
    {
        Evaluate("({ a: 1 })?.a").Should().Be(1);
        Evaluate("({ a: { b: [10, 20] } })?.a.b[1]").Should().Be(20);

        // A deep chain mixing optional and non-optional links, calls and computed reads.
        var deep = Evaluate("""
            (function () {
                var o = {
                    a: {
                        b: function () { return this._b.bind(this); },
                        _b: function () { return this.__b; },
                        __b: { c: 42 }
                    }
                };
                return [
                    o?.a?.['b']?.()?.()?.c,
                    o.a.b()().c,
                    o?.a.b?.()().c,
                    o?.missing?.['x']?.()?.()?.y
                ].join(',');
            })()
            """);

        deep.AsString().Should().Be("42,42,42,");
    }

    [Fact]
    public void ShortCircuitSkipsTheRestOfTheChainWithoutEvaluatingIt()
    {
        // `y` is not declared: reaching the computed property expression at all would raise a
        // ReferenceError, so `true` proves the whole tail was skipped.
        Evaluate("delete undefined?.x[y + 1]").Should().Be(true);

        // Every base is evaluated exactly once, and only up to the short circuit.
        var counts = Evaluate("""
            (function () {
                var calls = 0;
                var a = { b: { c: { d: function () { calls++; return a; } } } };
                a.b.c.d?.()?.b?.c?.d;

                var reads = 0;
                var g = { get b() { reads++; return { c: {} }; } };
                g.b?.c?.d;
                g.b?.c?.d;

                return calls + ',' + reads;
            })()
            """);

        counts.AsString().Should().Be("1,2");
    }

    [Fact]
    public void DeleteOfAShortCircuitedChainIsTrue()
    {
        Evaluate("delete undefined?.foo").Should().Be(true);
        Evaluate("delete null?.['foo']").Should().Be(true);
        Evaluate("delete null?.()").Should().Be(true);
        Evaluate("delete ({ a: { b: undefined } }).a?.b?.b").Should().Be(true);
    }

    [Fact]
    public void TheVerdictSurvivesHandlerTreeReuseAndPreparedScripts()
    {
        // Whether a link propagates its short circuit is decided once, when the handler tree is built, so
        // it has to hold for a re-run on a warmed engine and for a Prepared<Script> shared across engines.
        var prepared = Engine.PrepareScript("""
            (function () {
                var results = [];
                results.push(String(null?.a['b']().c));
                try { ({})?.a['b']; results.push('did not throw'); }
                catch (e) { results.push(e.constructor.name); }
                return results.join(',');
            })()
            """);

        var engine = new Engine();
        for (var i = 0; i < 3; i++)
        {
            engine.Evaluate(prepared).AsString().Should().Be("undefined,TypeError");
        }

        new Engine().Evaluate(prepared).AsString().Should().Be("undefined,TypeError");
    }

    [Fact]
    public void ShortCircuitedChainStaysAnOrdinaryUndefinedForItsConsumers()
    {
        // The short-circuit marker must never reach a consumer of the chain's value.
        Evaluate("typeof null?.a").Should().Be("undefined");
        Evaluate("null?.a === undefined").Should().Be(true);
        Evaluate("String(null?.a)").Should().Be("undefined");
        Evaluate("null?.a ?? 'fallback'").Should().Be("fallback");
        Evaluate("JSON.stringify({ x: null?.a })").Should().Be("{}");
        Evaluate("[null?.a].length").Should().Be(1);
        Evaluate("({ undefined: 3 })?.[null?.a]").Should().Be(3);
        Evaluate("((x) => typeof x)(null?.a)").Should().Be("undefined");
    }
}
