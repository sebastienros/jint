#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Promise.all</c> and <c>Promise.allSettled</c> resolve their promise with a result array of one slot per element
/// (https://tc39.es/ecma262/#sec-promise.all, https://tc39.es/ecma262/#sec-promise.allsettled), and that
/// array is subject to <c>Options.Constraints.MaxArraySize</c> like every other array built from a count: past the
/// limit the run ends in <see cref="MemoryLimitExceededException"/>, where it used to hand the script an array
/// larger than the host allowed (issue #4493).
/// </summary>
public class PromiseCombinatorMaxArraySizeTests
{
    /// <summary>
    /// A constructor whose <c>resolve</c> hands back a thenable that settles synchronously, inside the
    /// combinator's own call to <c>then</c>: every element has settled before the iteration ends, so the array is
    /// built by the combinator's own call (the "all done" path) and not by an element's resolve function.
    /// </summary>
    private const string SyncSettlingConstructor = """
        function SyncPromise(executor) { return new Promise(executor); }
        SyncPromise.resolve = function (v) {
            return { then(onFulfilled) { onFulfilled(v); } };
        };
        """;

    /// <summary>
    /// A generator, not an array literal: the iterable itself must not be what trips the limit.
    /// </summary>
    private const string Elements = """
        function* elements(n) { for (let i = 0; i < n; i++) yield Promise.resolve(i); }
        function* plain(n) { for (let i = 0; i < n; i++) yield i; }
        """;

    private static Engine EngineWith(uint maxArraySize)
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = maxArraySize);
        engine.Execute(SyncSettlingConstructor);
        engine.Execute(Elements);
        return engine;
    }

    /// <summary>
    /// The array is built by the resolve function of the last element to settle.
    /// </summary>
    [TestCase("Promise.all(elements(3))")]
    [TestCase("Promise.allSettled(elements(3))")]
    public void AnArrayPastTheLimitIsRefusedWhenTheLastElementSettlesLater(string expression)
    {
        var engine = EngineWith(2);

        var thrown = Caught.Exception(() => engine.Evaluate(expression));

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Contain("3").And.Contain("2");
    }

    /// <summary>
    /// Every element settled inside the combinator's own call, so the array is built there and the exception is
    /// thrown out of <c>Promise.all</c> itself.
    /// </summary>
    [TestCase("Promise.all.call(SyncPromise, plain(3))")]
    [TestCase("Promise.allSettled.call(SyncPromise, plain(3))")]
    public void AnArrayPastTheLimitIsRefusedWhenEveryElementSettledSynchronously(string expression)
    {
        var engine = EngineWith(2);

        var thrown = Caught.Exception(() => engine.Evaluate(expression));

        thrown.Should().BeOfType<MemoryLimitExceededException>();
        thrown!.Message.Should().Contain("3").And.Contain("2");
    }

    /// <summary>
    /// The control: an array of exactly <c>MaxArraySize</c> elements is allowed, so the check is a strict "past".
    /// </summary>
    [TestCase("Promise.all(elements(3)).then(r => r.length + ':' + r.join())", "3:0,1,2")]
    [TestCase("Promise.all.call(SyncPromise, plain(3)).then(r => r.length + ':' + r.join())", "3:0,1,2")]
    [TestCase("Promise.allSettled(elements(3)).then(r => r.length + ':' + r.map(e => e.value).join())", "3:0,1,2")]
    [TestCase("Promise.allSettled.call(SyncPromise, plain(3)).then(r => r.length + ':' + r.map(e => e.value).join())", "3:0,1,2")]
    public void AnArrayOfExactlyTheLimitIsAllowed(string expression, string expected)
    {
        var engine = EngineWith(3);

        engine.Evaluate(expression).UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// The scenario of the report: <see cref="UntrustedCodeLimits"/> allows 10,000 elements, and a generator of one
    /// more resolved promise must stop the host rather than reach the script as a 10,001-element array. Only the
    /// array limit is set, so nothing but the array can be what ends the run.
    /// </summary>
    [TestCase("Promise.all(elements(10001))")]
    [TestCase("Promise.allSettled(elements(10001))")]
    public void OneElementPastTenThousandIsRefused(string expression)
    {
        var engine = new Engine(options => options.Constraints.MaxArraySize = 10_000);
        engine.Execute(Elements);

        Caught.Exception(() => engine.Evaluate(expression)).Should().BeOfType<MemoryLimitExceededException>();
    }

    /// <summary>
    /// A failed run leaves the engine usable: the exception ends the run, it does not poison the engine.
    /// </summary>
    [Test]
    public void TheEngineIsUsableAfterTheLimitFired()
    {
        var engine = EngineWith(2);

        Caught.Exception(() => engine.Evaluate("Promise.all(elements(3))")).Should().BeOfType<MemoryLimitExceededException>();

        engine.Evaluate("Promise.all(elements(2)).then(r => r.length)").UnwrapIfPromise().AsNumber().Should().Be(2);
    }
}
