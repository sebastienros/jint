#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Promise.all</c>, <c>allSettled</c> and <c>any</c> settle when the spec's <c>remainingElementsCount</c> reaches
/// 0: once every element has settled and the iteration is done, not before and not more than once (issue #4459).
/// </summary>
public class PromiseCombinatorSettleCountTests
{
    /// <summary>
    /// A constructor whose <c>resolve</c> hands back a thenable that settles synchronously, inside the
    /// combinator's own call to <c>then</c>, so every element settles before the iteration has moved on.
    /// The combinator must still wait for the end of the iteration and keep every element.
    /// </summary>
    private const string SyncSettlingConstructor = """
        function SyncPromise(executor) { return new Promise(executor); }
        SyncPromise.resolve = function (v) {
            return { then(onFulfilled, onRejected) { if (v instanceof Error) onRejected(v.message); else onFulfilled(v); } };
        };
        """;

    [TestCase("Promise.all.call(SyncPromise, [1, 2, 3]).then(r => r.join())", "1,2,3")]
    [TestCase("Promise.allSettled.call(SyncPromise, [1, new Error('x'), 3]).then(r => r.map(e => e.status + ':' + (e.value ?? e.reason)).join())", "fulfilled:1,rejected:x,fulfilled:3")]
    [TestCase("Promise.any.call(SyncPromise, [new Error('a'), new Error('b')]).catch(e => e.constructor.name + ':' + e.errors.join())", "AggregateError:a,b")]
    public void ElementsSettlingSynchronouslyDoNotSettleTheCombinatorEarly(string expression, string expected)
    {
        var engine = new Engine();
        engine.Execute(SyncSettlingConstructor);

        engine.Evaluate(expression).UnwrapIfPromise().AsString().Should().Be(expected);
    }

    [TestCase("Promise.all([]).then(r => 'resolved ' + r.length)", "resolved 0")]
    [TestCase("Promise.allSettled([]).then(r => 'resolved ' + r.length)", "resolved 0")]
    [TestCase("Promise.any([]).catch(e => e.constructor.name + ' ' + e.errors.length)", "AggregateError 0")]
    public void AnEmptyIterableSettlesOnce(string expression, string expected)
    {
        new Engine().Evaluate(expression).UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// Elements settling out of order still land at their own index, and the combinator settles only after the
    /// last one.
    /// </summary>
    [TestCase("Promise.all(order.map(v => delay(v))).then(r => r.join())", "3,1,2")]
    [TestCase("Promise.allSettled(order.map(v => delay(v))).then(r => r.map(e => e.value).join())", "3,1,2")]
    [TestCase("Promise.any(order.map(v => delay(v).then(x => { throw x; }))).catch(e => e.errors.join())", "3,1,2")]
    public void ElementsSettlingOutOfOrderKeepTheirIndex(string expression, string expected)
    {
        var engine = new Engine();
        engine.Execute("""
            var order = [3, 1, 2];
            // settles after v microtask turns, so the elements settle in the order 1, 2, 3
            function delay(v) { var p = Promise.resolve(v); for (var i = 0; i < v; i++) p = p.then(x => x); return p; }
            """);

        engine.Evaluate(expression).UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// Enough elements that the old per-settle scan would have made this the slow part; the result is checked,
    /// not the time, so the test stays deterministic.
    /// </summary>
    [TestCase("Promise.all(ps).then(r => r.length + ':' + r[0] + ':' + r[r.length - 1])", "20000:0:19999")]
    [TestCase("Promise.allSettled(ps).then(r => r.length + ':' + r[r.length - 1].value)", "20000:19999")]
    [TestCase("Promise.any(ps.map(p => p.then(x => { throw x; }))).catch(e => e.errors.length + ':' + e.errors[19999])", "20000:19999")]
    public void ManyElementsAllSettle(string expression, string expected)
    {
        var engine = new Engine();
        engine.Execute("var ps = []; for (var i = 0; i < 20000; i++) ps.push(Promise.resolve(i));");

        engine.Evaluate(expression).UnwrapIfPromise().AsString().Should().Be(expected);
    }
}
