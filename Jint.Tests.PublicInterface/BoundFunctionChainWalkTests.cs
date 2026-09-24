#nullable enable

using Jint;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A chain of bound functions is a linked list script builds in linear time (#4130), and two questions the
/// specification asks of one — <c>IsConstructor</c> (https://tc39.es/ecma262/#sec-isconstructor, answered for a
/// bound function by https://tc39.es/ecma262/#sec-boundfunctioncreate) and
/// <c>GetFunctionRealm</c> (https://tc39.es/ecma262/#sec-getfunctionrealm) — were answered by recursing over it,
/// one native frame per link and no stack probe between. So a deep enough chain ended the process on every route
/// that asked either question before a probed <c>[[Call]]</c> or <c>[[Construct]]</c> ran: <c>new f()</c>,
/// <c>Reflect.construct</c>, <c>class extends</c>, <c>super()</c>, <c>new Proxy(f, …)</c>, an array's species
/// constructor, a <c>ShadowRealm</c> call. Neither question observes anything along the way, so both are walks now.
/// <para>
/// Each case asserts what the route gives on a 1 MB thread: the answer where only the walk is involved, and the
/// catchable <c>RangeError</c> the forwarding hops' own probes raise where the route goes on to call through the
/// chain. Before the fix the unfixed signal is not a red assertion but a dead test host (<c>Stack overflow.</c>,
/// <c>BindFunction.get_IsConstructor</c> or <c>Function.GetFunctionRealm</c> repeated to the bottom), which is the
/// same signal <see cref="HostNativeRecursionGuardTests"/> documents for the forwarding chains it covers.
/// </para>
/// </summary>
public class BoundFunctionChainWalkTests
{
    // Twelve times the ~16,000 links a 1 MB thread held while each link was a native frame (tier-0 code on
    // net10.0, x64), and still cheap: building a bind chain is linear since #4130, about half a second in Release.
    private const string BoundChain = "var f = function () {}; for (var i = 0; i < 200000; i++) f = f.bind(null);";

    // A proxy and a bind in turn, 200,000 links in all, so GetFunctionRealm walks both kinds of link. Every proxy's
    // target is a bound function and the outermost link is one, so nothing else on these routes forwards through a
    // proxy chain — which keeps the case about this walk and not about the proxy forwarding #4076 covers.
    private const string MixedChain = "var f = function () {}; for (var i = 0; i < 100000; i++) f = new Proxy(f, {}).bind(null);";

    private const string StackExhausted = "RangeError:Maximum call stack size exceeded";

    // Routes that only ask the two questions: each answers, with the guard on or off, because no frame is left to bound.
    private static readonly Dictionary<string, (string Script, string Expected)> AnsweringRoutes = new()
    {
        ["Reflect.construct with the chain as newTarget"] = ("return String(Object.getPrototypeOf(Reflect.construct(function () {}, [], f)) === Object.prototype);", "true"),
        ["a proxy over the chain"] = ("return typeof new Proxy(f, {});", "function"),
        ["class extends the chain"] = ("try { class C extends f {} return 'defined'; } catch (e) { return e.name; }", "TypeError"),
        ["array species through the chain"] = ("var a = []; a.constructor = f; return String(Array.isArray(a.map(function (x) { return x; })));", "true"),
    };

    // Routes that ask, then call or construct through the chain, where BindFunction's own probes answer.
    private static readonly Dictionary<string, (string Script, string Expected)> ForwardingRoutes = new()
    {
        ["new"] = ("new f(); return 'constructed';", StackExhausted),
        ["Array.from with the chain as this"] = ("Array.from.call(f, []); return 'constructed';", StackExhausted),
        ["super() into the chain"] = ("class D extends Object { constructor() { super(); } } Object.setPrototypeOf(D, f); new D(); return 'constructed';", StackExhausted),
        ["a ShadowRealm call of the chain"] = ("new ShadowRealm().evaluate('(h) => h()')(f); return 'called';", "TypeError:Cross-Realm Error: Cross-Realm Error: Maximum call stack size exceeded"),
    };

    [Test]
    public void EveryRouteOverADeepBoundChainAnswersOrRaisesACatchableError()
    {
        var expected = AnsweringRoutes.Concat(ForwardingRoutes).ToDictionary(x => x.Key, x => x.Value.Expected);

        RunRoutes(BoundChain, AnsweringRoutes.Concat(ForwardingRoutes), stackOverflowGuard: true).Should().BeEquivalentTo(expected);
    }

    [Test]
    public void TheWalkingRoutesOverADeepBoundChainAnswerWithoutTheGuard()
    {
        var expected = AnsweringRoutes.ToDictionary(x => x.Key, x => x.Value.Expected);

        RunRoutes(BoundChain, AnsweringRoutes, stackOverflowGuard: false).Should().BeEquivalentTo(expected);
    }

    [Test]
    public void TheRealmOfADeepChainOfBoundFunctionsAndProxiesIsFound()
    {
        var routes = new Dictionary<string, (string Script, string Expected)>
        {
            ["Reflect.construct with the chain as newTarget"] = AnsweringRoutes["Reflect.construct with the chain as newTarget"],
            ["array species through the chain"] = AnsweringRoutes["array species through the chain"],
            ["new"] = ForwardingRoutes["new"],
        };

        RunRoutes(MixedChain, routes, stackOverflowGuard: true).Should().BeEquivalentTo(routes.ToDictionary(x => x.Key, x => x.Value.Expected));
    }

    private static Dictionary<string, string> RunRoutes(
        string chain,
        IEnumerable<KeyValuePair<string, (string Script, string Expected)>> routes,
        bool stackOverflowGuard)
    {
        var outcomes = new Dictionary<string, string>();
        DedicatedThread.Run(() =>
        {
            using var engine = new Engine(options => options.Constraints.StackOverflowGuard = stackOverflowGuard);
            engine.Execute(chain);

            foreach (var route in routes)
            {
                outcomes[route.Key] = engine.Evaluate("""
                    (function () {
                        try {
                    """ + route.Value.Script + """
                        } catch (error) { return error.name + ':' + error.message; }
                    })();
                    """).AsString();
            }

            engine.Evaluate("6 * 7").AsNumber().Should().Be(42);
        }, maxStackSize: 1024 * 1024);

        return outcomes;
    }
}
