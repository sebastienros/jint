#nullable enable

using Jint.Runtime;
#if NET8_0_OR_GREATER
using Microsoft.Extensions.Time.Testing;
#endif

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Pins, from an embedder's side, that an execution constraint which trips inside an iterator's <c>return</c> ends the
/// run when a throw completion is what is closing the iterator.
/// </summary>
/// <remarks>
/// <para>
/// IteratorClose step 5 (https://tc39.es/ecma262/#sec-iteratorclose) lets a throw completion win over whatever
/// <c>return</c> throws, and Jint discarded everything there, the exception of a constraint included. So a time,
/// cancellation, recursion-depth or array-size bound spent inside <c>return</c> bounded nothing: a script that caught the
/// error that had closed the iterator went on and finished normally, and one that did not handed the host its own error
/// in place of the bound's. A statement limit was lost in the second shape only, since the empty <c>catch</c> block of the
/// first checks it again; a memory limit was never lost outright, being checked again on the way out of the entry too.
/// </para>
/// <para>
/// Every site below but one leaves through such a throw, from each kind of function body the interpreter runs it in, in
/// both shapes, with nothing after the close. The exception is the zip whose shortest member ran out, which closes the
/// rest with a return completion. It is here beside the strict length mismatch because a bound propagating out of a
/// zip's IteratorCloseAll once left the iterator it tripped in for the zip's own catch to close a second time, and the
/// one-call assertion every row makes is what catches that. A bounded loop stands in for the <c>for (;;) {}</c> that found this, so a build
/// where a bound never trips fails rather than hangs, and the clock and the token are moved from inside <c>return</c> so
/// no row depends on how fast it runs. What a script could catch is still discarded, which the control rows pin over the
/// same sites.
/// </para>
/// </remarks>
public class HostIteratorCloseConstraintTests
{
    /// <summary>
    /// What every row shares: iterables whose <c>return</c> reports that it was reached and then runs the row's
    /// <c>trip</c>, and a <c>fail</c> that is the throw closing them.
    /// </summary>
    private const string Prelude = """
        function fail() { throw new Error('body'); }
        function makeIterator(produce) {
            return {
                next() { return { value: produce(), done: false }; },
                return() { reached(); trip(); return {}; },
            };
        }
        function makeIterable(produce) { return { [Symbol.iterator]() { return makeIterator(produce); } }; }
        const iterable = makeIterable(() => undefined);
        const entries = makeIterable(() => ({}));
        const finished = { [Symbol.iterator]() { return { next() { return { value: undefined, done: true }; } }; } };
        const asyncIterable = {
            [Symbol.asyncIterator]() {
                return {
                    next() { return Promise.resolve({ value: undefined, done: false }); },
                    return() { reached(); trip(); return Promise.resolve({}); },
                };
            },
        };
        function poisonedPromise() {
            const promise = Promise.resolve(1);
            Object.defineProperty(promise, 'constructor', { get: fail });
            return promise;
        }
        function rejectingDisposable() {
            return { [Symbol.asyncDispose]() { return Promise.reject(new Error('body')); } };
        }
        function spin() { for (let i = 0; i < 100000; i++) { } }
        function deep(n) { return n === 0 ? 0 : 1 + deep(n - 1); }
        var caught = 'nothing was caught';

        """;

    /// <summary>
    /// A close, as a statement list, and what a control row's <c>catch</c> sees from it: <c>fail</c>'s error for every
    /// close a throw performs, the <c>TypeError</c> for the one a strict zip throws itself, and nothing for the one close
    /// here that no throw performs, which therefore has no control row.
    /// </summary>
    private sealed record Site(string Statements, string? Caught = "body");

    /// <summary>Each close an iterator gets on the way out of a throw, plus one zip close that no throw performs.</summary>
    private static readonly Dictionary<string, Site> SyncSites = new(StringComparer.Ordinal)
    {
        ["for-of with a throwing body"] = new("for (const x of iterable) { fail(); }"),
        ["array destructuring"] = new("const [a = fail()] = iterable;"),
        ["parameter destructuring"] = new("(function ([a = fail()]) { })(iterable);"),
        ["yield* closed by its consumer"] = new("function* g() { yield* iterable; } for (const x of g()) { fail(); }"),
        ["Array.from"] = new("Array.from(iterable, fail);"),
        ["Map constructor"] = new("class M extends Map { set() { fail(); } } new M(entries);"),
        ["Set constructor"] = new("class S extends Set { add() { fail(); } } new S(iterable);"),
        ["Iterator.prototype.take argument"] = new("Iterator.prototype.take.call(makeIterator(() => undefined), { valueOf: fail });"),
        ["AsyncIterator.prototype.take argument"] = new("AsyncIterator.prototype.take.call(makeIterator(() => undefined), { valueOf: fail });"),
        ["iterator helper step"] = new("Iterator.from(iterable).map(fail).next();"),
        ["Iterator.zip argument"] = new("Iterator.zip([iterable, { get [Symbol.iterator]() { return fail(); } }]);"),
        ["Iterator.zip step"] = new("Iterator.zip([iterable, { [Symbol.iterator]() { return { next: fail }; } }]).next();"),
        ["Iterator.zip strict length mismatch"] = new("Iterator.zip([iterable, finished], { mode: 'strict' }).next();", "TypeError"),
        // IteratorCloseAll with a return completion: the shortest member ran out, and no throw is leaving.
        ["Iterator.zip shortest exhaustion"] = new("Iterator.zip([finished, iterable]).next();", Caught: null),
    };

    /// <summary>The closes only an async function or an async generator can reach.</summary>
    private static readonly Dictionary<string, Site> AsyncSites = new(StringComparer.Ordinal)
    {
        ["for await over an async iterable"] = new("for await (const x of asyncIterable) { fail(); }"),
        ["for await over a sync iterable"] = new("for await (const x of iterable) { fail(); }"),
        ["for await over a rejected value"] = new("for await (const x of makeIterable(() => Promise.reject(new Error('body')))) { }"),
        ["for await over a value whose constructor throws"] = new("for await (const x of makeIterable(poisonedPromise)) { }"),
        ["for-of whose await using dispose rejects"] = new("for (await using x of makeIterable(rejectingDisposable)) { }"),
        ["Promise.all"] = new("class P extends Promise { static resolve() { fail(); } } await P.all(iterable);"),
        ["Array.fromAsync"] = new("await Array.fromAsync(asyncIterable, fail);"),
    };

    /// <summary>The bodies a site runs in: the script itself, and each kind of function that suspends.</summary>
    private static readonly Dictionary<string, string> Contexts = new(StringComparer.Ordinal)
    {
        ["script"] = "/*BODY*/",
        ["generator"] = "(function* () { /*BODY*/ })().next();",
        ["async function"] = "(async () => { /*BODY*/ })();",
        ["async generator"] = "(async function* () { /*BODY*/ })().next();",
    };

    private static readonly string[] SyncContexts = ["script", "generator", "async function"];

    private static readonly string[] AsyncContexts = ["async function", "async generator"];

    /// <summary>The bounds, each tripped from inside <c>return</c> by the row's <c>trip</c>.</summary>
    private static readonly string[] Bounds =
    [
        "LimitStatements",
        "ObserveCancellation",
        "MaxRecursionDepth",
        "MaxArraySize",
#if NET8_0_OR_GREATER
        // A host clock the row moves, and a per-thread allocation counter .NET Framework does not have.
        "LimitExecutionTime",
        "LimitMemory",
#endif
    ];

    /// <summary>
    /// What a script can catch, thrown from inside <c>return</c>: a <c>throw</c>, and the <c>RangeError</c> the stack
    /// overflow guard raises for a recursion nothing else bounds.
    /// </summary>
    private static readonly string[] Controls = ["throw", "StackOverflowGuard"];

    private static IEnumerable<(string Site, string Context)> Placements()
    {
        foreach (var site in SyncSites.Keys)
        {
            foreach (var context in SyncContexts)
            {
                yield return (site, context);
            }
        }

        foreach (var site in AsyncSites.Keys)
        {
            foreach (var context in AsyncContexts)
            {
                yield return (site, context);
            }
        }
    }

    public static TestCases<string, string, string> ConstraintRows()
    {
        var rows = new TestCases<string, string, string>();
        foreach (var bound in Bounds)
        {
            foreach (var (site, context) in Placements())
            {
                rows.Add(bound, site, context);
            }
        }

        return rows;
    }

    public static TestCases<string, string, string> ControlRows()
    {
        var rows = new TestCases<string, string, string>();
        foreach (var control in Controls)
        {
            foreach (var (site, context) in Placements())
            {
                if (SiteOf(site).Caught is not null)
                {
                    rows.Add(control, site, context);
                }
            }
        }

        return rows;
    }

    public static TestCases<string> BoundRows() => new(Bounds);

    /// <summary>
    /// A script that catches the error closing the iterator, and has nothing left to run, does not finish: the bound
    /// reaches the host as its own exception, and it tripped inside <c>return</c>, which exactly one call reached.
    /// </summary>
    [TestCaseSource(nameof(ConstraintRows))]
    public void AConstraintThatTripsInsideReturnEndsACatchingScript(string bound, string site, string context)
    {
        using var harness = new Harness(bound);

        var thrown = Caught.Exception(() => harness.Engine.Evaluate(harness.Script(Place(site, context, handler: ""))));

        thrown.Should().BeOfType(harness.Expected!, "a bound spent inside return() is no completion the closing throw may discard");
        harness.ReturnCalls.Should().Be(1, "the bound has to trip inside return(), not before the close reaches it");
    }

    /// <summary>
    /// A script that lets the error closing the iterator escape hands the host the bound's exception, not its own error.
    /// </summary>
    [TestCaseSource(nameof(ConstraintRows))]
    public void AConstraintThatTripsInsideReturnReachesTheHostInPlaceOfTheScriptsError(string bound, string site, string context)
    {
        using var harness = new Harness(bound);

        var thrown = Caught.Exception(() => harness.Engine.Evaluate(harness.Script(Place(site, context, handler: null))));

        thrown.Should().BeOfType(harness.Expected!, "a bound spent inside return() is no completion the closing throw may discard");
        harness.ReturnCalls.Should().Be(1, "the bound has to trip inside return(), not before the close reaches it");
    }

    /// <summary>
    /// The same sites with what a script can catch thrown from <c>return</c>: discarded, and the error that closed the
    /// iterator is the one the script sees (IteratorClose step 5).
    /// </summary>
    [TestCaseSource(nameof(ControlRows))]
    public void WhatAScriptCanCatchFromReturnIsStillDiscarded(string control, string site, string context)
    {
        using var harness = new Harness(control);

        harness.Engine.Evaluate(harness.Script(
            Place(site, context, handler: "caught = e instanceof TypeError ? 'TypeError' : e && e.message;")));

        harness.Engine.Evaluate("caught").Should().Be(SiteOf(site).Caught!);
        harness.ReturnCalls.Should().Be(1);
    }

    /// <summary>
    /// Each row's trip, run outside any close, ends the run with the exception the matrix expects, so a row that fails
    /// says something about the close rather than about the trip.
    /// </summary>
    [TestCaseSource(nameof(BoundRows))]
    public void EachTripEndsARunOutsideAnyClose(string bound)
    {
        using var harness = new Harness(bound);

        var thrown = Caught.Exception(() => harness.Engine.Evaluate(harness.Script("trip();")));

        thrown.Should().BeOfType(harness.Expected!);
    }

    /// <summary>
    /// A bound tripping inside a dispose that a throw completion runs ends the run too. DisposeResources only ever caught
    /// a script's throw, so this held before the close sites were fixed; it is pinned beside them.
    /// </summary>
    [TestCaseSource(nameof(BoundRows))]
    public void AConstraintThatTripsInsideADisposeEndsTheRun(string bound)
    {
        using var harness = new Harness(bound);

        var thrown = Caught.Exception(() => harness.Engine.Evaluate(harness.Script(
            "try { using r = { [Symbol.dispose]() { reached(); trip(); } }; fail(); } catch (e) { }")));

        thrown.Should().BeOfType(harness.Expected!);
        harness.ReturnCalls.Should().Be(1);
    }

    /// <summary>
    /// The line the filter draws: Jint's own <see cref="TimeoutException"/> ends the run because Jint raised it, not
    /// because of its type, so a host's own one from a CLR <c>return</c> is a host failure the closing throw still wins
    /// over, as it wins over a host's <c>Dispose</c>.
    /// </summary>
    [Test]
    public void AHostsOwnTimeoutExceptionFromReturnIsStillDiscarded()
    {
        var engine = new Engine();
        engine.SetValue("hostReturn", new Func<object>(() => throw new TimeoutException("the host's own")));

        engine.Evaluate("""
            const iterable = { [Symbol.iterator]() { return { next() { return { done: false }; }, return: hostReturn }; } };
            let caught;
            try { for (const x of iterable) { throw new Error('body'); } } catch (e) { caught = e.message; }
            caught
            """).Should().Be("body");
    }

    /// <summary>
    /// The site in its context, inside a <c>try</c> whose <c>catch</c> runs <paramref name="handler"/>, or with no
    /// <c>try</c> at all for a <see langword="null"/> one.
    /// </summary>
    private static Site SiteOf(string site) => SyncSites.TryGetValue(site, out var sync) ? sync : AsyncSites[site];

    private static string Place(string site, string context, string? handler)
    {
        var statements = SiteOf(site).Statements;
        var body = handler is null ? statements : "try { " + statements + " } catch (e) { " + handler + " }";
        return Contexts[context].Replace("/*BODY*/", body);
    }

    /// <summary>One engine configured for one row, and what that row's <c>return</c> runs.</summary>
    private sealed class Harness : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly string _trip;

        public Harness(string bound)
        {
            Action<Options>? configure = null;
#if NET8_0_OR_GREATER
            var clock = new FakeTimeProvider();
#endif
            switch (bound)
            {
                case "LimitStatements":
                    configure = options => options.LimitStatements(1_000);
                    _trip = "spin();";
                    Expected = typeof(StatementsCountOverflowException);
                    break;
                case "ObserveCancellation":
                    configure = options => options.ObserveCancellation(_cancellation.Token);
                    _trip = "cancel(); spin();";
                    Expected = typeof(ExecutionCanceledException);
                    break;
                case "MaxRecursionDepth":
                    configure = options => options.Constraints.MaxRecursionDepth = 32;
                    _trip = "deep(1000);";
                    Expected = typeof(RecursionDepthOverflowException);
                    break;
                case "MaxArraySize":
                    configure = options => options.Constraints.MaxArraySize = 1_000;
                    _trip = "new Array(5000);";
                    Expected = typeof(MemoryLimitExceededException);
                    break;
#if NET8_0_OR_GREATER
                case "LimitExecutionTime":
                    configure = options =>
                    {
                        options.Constraints.TimeProvider = clock;
                        options.LimitExecutionTime(TimeSpan.FromMinutes(1));
                    };
                    _trip = "advanceTheClock(); spin();";
                    Expected = typeof(TimeoutException);
                    break;
                case "LimitMemory":
                    configure = options => options.LimitMemory(8_000_000);
                    _trip = "const kept = []; for (let i = 0; i < 20000; i++) { kept.push(new Array(256).fill(i)); }";
                    Expected = typeof(MemoryLimitExceededException);
                    break;
#endif
                case "throw":
                    _trip = "throw new Error('from return');";
                    break;
                case "StackOverflowGuard":
                    _trip = "(function overflow() { return 1 + overflow(); })();";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bound), bound, null);
            }

            Engine = configure is null ? new Engine() : new Engine(configure);
            Engine.SetValue("reached", new Action(() => ReturnCalls++));
            Engine.SetValue("cancel", new Action(_cancellation.Cancel));
#if NET8_0_OR_GREATER
            Engine.SetValue("advanceTheClock", new Action(() => clock.Advance(TimeSpan.FromHours(1))));
#endif
        }

        public Engine Engine { get; }

        /// <summary>The exception the bound ends a run with; <see langword="null"/> for a control.</summary>
        public Type? Expected { get; }

        public int ReturnCalls { get; private set; }

        public string Script(string statements) => Prelude + "function trip() { " + _trip + " }\n" + statements;

        public void Dispose() => _cancellation.Dispose();
    }
}
