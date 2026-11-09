#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins step 8.c of
/// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset">ForIn/OfBodyEvaluation</a>
/// for a <c>for await</c> loop: once the Await of a step has settled, "If nextResult is not an Object, throw a TypeError
/// exception". That is a plain throw, not an IfAbruptCloseIterator, so the iterator's <c>return</c> is not called.
/// <para>
/// Jint called <c>next</c> again instead, for as long as the step went on settling to something other than an object
/// (sebastienros/jint#4489). Both of the loop's Await continuations stored the settled value as an object, so a
/// primitive arrived as <c>null</c>, which the resume read as "no step in flight". Every source here throws from
/// <c>next</c> on its 21st call, so a loop that keeps stepping ends with that error instead of spinning. Every loop runs
/// in an async function and, beside it, in an async generator that yields each value. Expected values are Node 26's, bar
/// the <c>return</c> it calls in <see cref="ASyncIterablesNonObjectStepThrowsWithoutClosing"/>.
/// </para>
/// </summary>
public class ForAwaitNonObjectStepTests
{
    private const string Harness = """
        function describe(e) { return e instanceof Error ? e.name + ': ' + e.message : String(e); }

        // What a step settles to after the first `good` ones: answered by `next` itself, or by the promise it answers.
        const settlements = {
            promise5: () => Promise.resolve(5),
            direct5: () => 5,
            promiseUndefined: () => Promise.resolve(undefined),
            directUndefined: () => undefined,
            promiseNull: () => Promise.resolve(null),
            directNull: () => null,
            promiseSymbol: () => Promise.resolve(Symbol('s')),
            directSymbol: () => Symbol('s'),
            // The controls: objects of other kinds, each answering done.
            doneFunction: () => Promise.resolve(Object.assign(function () { }, { done: true })),
            doneNumberObject: () => Object.assign(Object(5), { done: true }),
            doneProxy: () => Promise.resolve(new Proxy({}, { get: (target, key) => key === 'done' })),
        };

        // An async iterable whose next answers 1, 2, ... for its first `good` steps and then `settlement`. Each call to
        // next and to return is logged; the 21st call to next throws, so a loop that never stops stepping still ends.
        function source(settlement, good, log) {
            let i = 0;
            return {
                [Symbol.asyncIterator]() {
                    return {
                        next() {
                            i++;
                            log.push('next' + i);
                            if (i > 20) throw new Error('runaway');
                            return i <= good ? Promise.resolve({ value: i, done: false }) : settlements[settlement]();
                        },
                        return() { log.push('return'); return Promise.resolve({ value: undefined, done: true }); }
                    };
                }
            };
        }

        // The consumers, each logging the values it saw.
        const loops = {
            asyncFunction: async function (src, log) {
                for await (const x of src) { log.push('v' + x); }
            },
            asyncGenerator: async function (src, log) {
                async function* g() { for await (const x of src) { log.push('v' + x); yield x; } }
                for await (const y of g()) { log.push('y' + y); }
            },
            awaitingBody: async function (src, log) {
                for await (const x of src) { log.push('v' + x); await null; log.push('a' + x); }
            },
            awaitingGeneratorBody: async function (src, log) {
                async function* g() { for await (const x of src) { log.push('v' + x); await null; yield x; } }
                for await (const y of g()) { log.push('y' + y); }
            },
        };

        async function run(loop, settlement, good) {
            const log = [];
            try { await loops[loop](source(settlement, good, log), log); log.push('completed'); }
            catch (e) { log.push(describe(e)); }
            return log.join(',');
        }
        """;

    private static readonly string[] _loops = ["asyncFunction", "asyncGenerator", "awaitingBody", "awaitingGeneratorBody"];

    private static readonly (string Name, string Shown)[] _settlements =
    [
        ("promise5", "5"),
        ("direct5", "5"),
        ("promiseUndefined", "undefined"),
        ("directUndefined", "undefined"),
        ("promiseNull", "null"),
        ("directNull", "null"),
        ("promiseSymbol", "Symbol(s)"),
        ("directSymbol", "Symbol(s)"),
    ];

    /// <summary>Every consumer against every settlement: the loop, the settlement's name, and how the message shows it.</summary>
    public static TheoryData<string, string, string> Cases
    {
        get
        {
            var rows = new TheoryData<string, string, string>();
            foreach (var loop in _loops)
            {
                foreach (var (name, shown) in _settlements)
                {
                    rows.Add(loop, name, shown);
                }
            }

            return rows;
        }
    }

    private static string Run(string script) =>
        new Engine().Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    private static string TypeError(string shown) => "TypeError: Iterator result " + shown + " is not an object";

    /// <summary>
    /// The first step settles to a primitive: one call to <c>next</c>, the <c>TypeError</c>, and no <c>return</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void AFirstStepSettlingToANonObjectThrowsWithoutClosing(string loop, string settlement, string shown)
    {
        Run($"run('{loop}', '{settlement}', 0)").Should().Be("next1," + TypeError(shown));
    }

    /// <summary>
    /// The second step settles to a primitive after a first one gave a value, which the body has consumed — and, in the
    /// awaiting rows, suspended in — before the loop steps again.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ALaterStepSettlingToANonObjectThrowsWithoutClosing(string loop, string settlement, string shown)
    {
        var consumed = loop switch
        {
            "asyncFunction" => "v1",
            "asyncGenerator" => "v1,y1",
            "awaitingBody" => "v1,a1",
            _ => "v1,y1",
        };

        Run($"run('{loop}', '{settlement}', 1)").Should().Be("next1," + consumed + ",next2," + TypeError(shown));
    }

    /// <summary>
    /// The control: any object is a step, whatever its kind — a function, a <c>Number</c> wrapper around the very
    /// primitive the other rows settle to, a proxy — and each of these answers done, so the loop completes.
    /// </summary>
    [Theory]
    [InlineData("asyncFunction", "doneFunction")]
    [InlineData("asyncFunction", "doneNumberObject")]
    [InlineData("asyncFunction", "doneProxy")]
    [InlineData("asyncGenerator", "doneFunction")]
    [InlineData("asyncGenerator", "doneNumberObject")]
    [InlineData("asyncGenerator", "doneProxy")]
    public void AnObjectOfAnyKindIsAStep(string loop, string settlement)
    {
        Run($"run('{loop}', '{settlement}', 1)").Should().Be("next1," + (loop == "asyncGenerator" ? "v1,y1" : "v1") + ",next2,completed");
    }

    /// <summary>
    /// The throw leaves nothing behind for the statement: run again from the same function, the loop takes a fresh
    /// iterator, steps it once and throws again, rather than finding the first run's step still in flight. The rounds
    /// are a <c>while</c> loop because an async generator's <c>for</c> loop around a <c>for await</c> repeats its last
    /// round without end, whatever the steps settle to (sebastienros/jint#4504).
    /// </summary>
    [Theory]
    [InlineData("asyncFunction")]
    [InlineData("asyncGenerator")]
    public void TheLoopRunsAgainAfterTheTypeError(string loop)
    {
        Run($$"""
            (async function () {
                const log = [];
                if ('{{loop}}' === 'asyncFunction') {
                    let round = 0;
                    while (round < 2) {
                        try { for await (const x of source('promise5', 1, log)) { log.push('v' + x); } }
                        catch (e) { log.push(e.name); }
                        round++;
                    }
                } else {
                    async function* g() {
                        let round = 0;
                        while (round < 2) {
                            try { for await (const x of source('promise5', 1, log)) { log.push('v' + x); yield x; } }
                            catch (e) { log.push(e.name); }
                            round++;
                        }
                    }
                    for await (const y of g()) { log.push('y' + y); }
                }
                return log.join(',');
            })()
            """).Should().Be(loop == "asyncFunction"
            ? "next1,v1,next2,TypeError,next1,v1,next2,TypeError"
            : "next1,v1,y1,next2,TypeError,next1,v1,y1,next2,TypeError");
    }

    /// <summary>
    /// A module's top-level <c>for await</c> runs in the module's own async evaluation, through the async function
    /// continuation: caught there, the module completes with the log; uncaught, the import fails with the TypeError.
    /// </summary>
    [Theory]
    [InlineData("promise5", "5")]
    [InlineData("direct5", "5")]
    [InlineData("promiseUndefined", "undefined")]
    [InlineData("directSymbol", "Symbol(s)")]
    public void AModulesTopLevelLoopThrowsWithoutClosing(string settlement, string shown)
    {
        var engine = new Engine();
        engine.Execute(Harness);
        engine.Modules.Add("caught", $$"""
            const log = [];
            try { for await (const x of source('{{settlement}}', 1, log)) { log.push('v' + x); } log.push('completed'); }
            catch (e) { log.push(describe(e)); }
            export const result = log.join(',');
            """);
        engine.Modules.Import("caught").Get("result").Should().Be("next1,v1,next2," + TypeError(shown));

        engine.Modules.Add("uncaught", $$"""
            export const log = [];
            globalThis.uncaughtLog = log;
            for await (const x of source('{{settlement}}', 1, log)) { log.push('v' + x); }
            """);
        var exception = Record.Exception(() => engine.Modules.Import("uncaught"));
        exception.Should().BeOfType<JavaScriptException>().Which.Message.Should().Be("Iterator result " + shown + " is not an object");
        engine.Evaluate("uncaughtLog.join(',')").Should().Be("next1,v1,next2");
    }

    /// <summary>
    /// The control: a sync iterable reaches the loop through CreateAsyncFromSyncIterator, whose <c>next</c> runs
    /// IteratorNext on the sync iterator and so throws the <c>TypeError</c> before anything is awaited; IfAbruptRejectPromise
    /// hands it to the loop as a rejection (https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.next), and no
    /// step of either closes the sync iterator. Node 26 calls its <c>return</c> here, as it does when <c>next</c> throws.
    /// </summary>
    [Theory]
    [InlineData("asyncFunction", "5")]
    [InlineData("asyncFunction", "undefined")]
    [InlineData("asyncGenerator", "5")]
    [InlineData("asyncGenerator", "undefined")]
    public void ASyncIterablesNonObjectStepThrowsWithoutClosing(string loop, string answer)
    {
        Run($$"""
            (async function () {
                const log = [];
                let i = 0;
                const src = {
                    [Symbol.iterator]() {
                        return {
                            next() { i++; log.push('next' + i); if (i > 20) throw new Error('runaway'); return i === 1 ? { value: 1, done: false } : {{answer}}; },
                            return() { log.push('return'); return {}; }
                        };
                    }
                };
                try { await loops.{{loop}}(src, log); log.push('completed'); } catch (e) { log.push(describe(e)); }
                return log.join(',');
            })()
            """).Should().Be("next1,v1," + (loop == "asyncGenerator" ? "y1," : "") + "next2," + TypeError(answer));
    }
}
