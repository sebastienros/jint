#nullable enable

using AwesomeAssertions.Execution;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Every place the language awaits a value, timed against the microtask queue, with four kinds of promise awaited.
/// <see href="https://tc39.es/ecma262/#await">Await</see> starts with <c>? PromiseResolve(%Promise%, value)</c>, and
/// <see href="https://tc39.es/ecma262/#sec-promise-resolve">PromiseResolve</see> reads a promise's <c>constructor</c>:
/// a promise whose <c>constructor</c> is <c>%Promise%</c> is awaited as it is, one whose <c>constructor</c> is anything
/// else is wrapped in a new <c>%Promise%</c>, which reaches it through its <c>then</c> two microtasks later, and a
/// getter that throws makes the await itself a throw completion, before anything suspends.
/// <para>
/// Jint skipped that read for a native promise in an <c>await</c> expression, an async function's <c>await using</c>
/// disposal and several other places (sebastienros/jint#4361): a throwing getter was never called and the promise
/// resolved the await, and a promise subclass was awaited as if it were a <c>%Promise%</c>.
/// </para>
/// <para>
/// The kinds: <c>poisoned</c> is a native promise whose own <c>constructor</c> getter logs and throws <c>C</c>,
/// <c>observed</c> one whose getter logs and answers <c>Promise</c>, <c>subclassed</c> an instance of a
/// <c>Promise</c> subclass whose <c>then</c> logs, and <c>native</c> an untouched promise, the lane that must stay
/// as fast as it was. Each log is the body's own entries interleaved with a ladder of microtasks <c>t1</c>,
/// <c>t2</c>, … started just before the request; <c>called</c> is the statement after it. Every expected log is
/// Node 24's, bar one: Node reads the <c>constructor</c> of an <c>await using</c> dispose result twice, where
/// <see href="https://tc39.es/ecma262/#sec-disposeresources">DisposeResources</see> awaits it once
/// (<c>Await(result.[[Value]])</c>), so it is read once.
/// </para>
/// </summary>
public class AwaitPromiseResolveTests
{
    private const string Prelude = """
        function ladder(log, n) {
          let p = Promise.resolve();
          for (let i = 1; i <= n; i++) {
            const k = i;
            p = p.then(() => { log.push('t' + k); });
          }
          return p;
        }
        function show(v) { return v !== null && typeof v === 'object' ? JSON.stringify(v) : String(v); }
        function fmt(r) {
          return r !== null && typeof r === 'object' && 'done' in r ? '{' + show(r.value) + ',' + r.done + '}' : show(r);
        }
        function poisoned(log, value) {
          const p = Promise.resolve(value);
          Object.defineProperty(p, 'constructor', { get() { log.push('get constructor'); throw 'C'; } });
          return p;
        }
        function observed(log, value) {
          const p = Promise.resolve(value);
          Object.defineProperty(p, 'constructor', { get() { log.push('get constructor'); return Promise; } });
          return p;
        }
        function subclassed(log, value) {
          class Sub extends Promise {
            then(f, r) { log.push('then'); return super.then(f, r); }
          }
          return Sub.resolve(value);
        }
        function native(log, value) { return Promise.resolve(value); }
        // make builds the function or the generator, started says whether a generator is first run to its first
        // yield, and request makes the request whose answer is logged. The ladder is started just before it.
        async function probe(make, request, started) {
          const log = [];
          const it = make(log);
          if (started) {
            await it.next();
          }
          const done = ladder(log, 9);
          request(it, log).then(r => log.push('resolved ' + fmt(r)), e => log.push('rejected ' + show(e)));
          log.push('called');
          return done.then(() => log.join(', '));
        }

        """;

    /// <summary>
    /// The await sites an async function and an async generator share, each with the kind of promise standing in as
    /// <c>MAKE</c>.
    /// </summary>
    private static readonly Dictionary<string, string> SitesInBothKinds = new()
    {
        ["AnAwaitExpression"] =
            "try { log.push('got ' + await MAKE(log, 1)); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
        // AsyncIteratorClose awaits what the iterator's return() answers (step 5.c).
        ["AnAwaitInAForAwaitIteratorClose"] =
            "const src = { [Symbol.asyncIterator]() { return { next() { return { value: 1, done: false }; }, return() { log.push('return'); return MAKE(log, {}); } }; } }; try { for await (const x of src) { log.push('body ' + x); break; } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
        ["AnAwaitOfAForAwaitNextResult"] =
            "const src = { [Symbol.asyncIterator]() { let i = 0; return { next() { return MAKE(log, { value: i, done: i++ > 0 }); } }; } }; try { for await (const x of src) { log.push('body ' + x); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
        // DisposeResources awaits an async dispose method's result (step 4.e.ii.1) as a block ends ...
        ["ABlocksAwaitUsing"] =
            "try { { await using r = { [Symbol.asyncDispose]() { log.push('dispose'); return MAKE(log, 1); } }; log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
        // ... as the body ends ...
        ["TheBodysAwaitUsing"] =
            "await using r = { [Symbol.asyncDispose]() { log.push('dispose'); return MAKE(log, 1); } }; log.push('body');",
        // ... and as an iteration of a for-of or a for-await-of ends.
        ["AForOfAwaitUsing"] =
            "try { for (await using r of [{ [Symbol.asyncDispose]() { log.push('dispose'); return MAKE(log, 1); } }]) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
        // An iterator with no return(), so that the close the throw completion takes owes no await of its own: Jint
        // closes an async iterator on a throw without awaiting what return() answers.
        ["AForAwaitOfAwaitUsing"] =
            "const src = { [Symbol.asyncIterator]() { let i = 0; return { next() { return { value: { [Symbol.asyncDispose]() { log.push('dispose'); return MAKE(log, 1); } }, done: i++ > 0 }; } }; } }; try { for await (await using r of src) { log.push('body'); } } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
    };

    /// <summary>
    /// The await sites only an async generator has: its body, the request that reaches it, and whether the generator
    /// is first run to its first yield.
    /// </summary>
    private static readonly Dictionary<string, (string Body, string Request, bool Started)> SitesInAnAsyncGenerator = new()
    {
        // yield* awaits each answer of its delegate, whatever the request
        // (https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation, step 7).
        ["AYieldStarNextResult"] = (
            "const inner = { [Symbol.asyncIterator]() { return { next() { log.push('next'); return MAKE(log, { value: 'v', done: true }); } }; } }; try { log.push('got ' + (yield* inner)); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next()", false),
        ["AYieldStarNextResultThatYields"] = (
            "const inner = { [Symbol.asyncIterator]() { return { next() { log.push('next'); return MAKE(log, { value: 'v', done: false }); } }; } }; try { yield* inner; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next()", false),
        ["AYieldStarReturnResult"] = (
            "const inner = { [Symbol.asyncIterator]() { return { next() { return { value: 1, done: false }; }, return(v) { log.push('return ' + v); return MAKE(log, { value: v, done: true }); } }; } }; try { yield* inner; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(5)", true),
        ["AYieldStarThrowResult"] = (
            "const inner = { [Symbol.asyncIterator]() { return { next() { return { value: 1, done: false }; }, throw(e) { log.push('throw ' + e); return MAKE(log, { value: 'x', done: true }); } }; } }; try { log.push('got ' + (yield* inner)); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.throw('E')", true),
        // https://tc39.es/ecma262/#sec-asyncgeneratoryield awaits the operand.
        ["AYield"] = (
            "try { yield MAKE(log, 1); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next()", false),
        ["AReturnStatement"] = (
            "try { return MAKE(log, 1); } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.next()", false),
        ["AReturnResumptionAtAYield"] = (
            "try { yield 1; } catch (e) { log.push('caught ' + show(e)); } log.push('after');",
            "it.return(MAKE(log, 5))", true),
        ["AReturnBeforeTheStart"] = (
            "yield 1;",
            "it.return(MAKE(log, 5))", false),
    };

    /// <summary>
    /// A site and a kind of promise, then the log as an async function and as an async generator.
    /// </summary>
    public static TestCases<string, string, string> InBothKinds => new()
    {
        { "AnAwaitExpression/poisoned", "get constructor, caught C, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9", "get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AnAwaitExpression/observed", "get constructor, called, t1, got 1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9", "get constructor, called, t1, got 1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AnAwaitExpression/subclassed", "called, t1, then, t2, t3, got 1, after, t4, resolved undefined, t5, t6, t7, t8, t9", "called, t1, then, t2, t3, got 1, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9" },
        { "AnAwaitExpression/native", "called, t1, got 1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9", "called, t1, got 1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AnAwaitInAForAwaitIteratorClose/poisoned", "called, t1, body 1, return, get constructor, caught C, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9", "called, t1, body 1, return, get constructor, caught C, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AnAwaitInAForAwaitIteratorClose/observed", "called, t1, body 1, return, get constructor, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9", "called, t1, body 1, return, get constructor, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9" },
        { "AnAwaitInAForAwaitIteratorClose/subclassed", "called, t1, body 1, return, t2, then, t3, t4, after, t5, resolved undefined, t6, t7, t8, t9", "called, t1, body 1, return, t2, then, t3, t4, after, t5, resolved {undefined,true}, t6, t7, t8, t9" },
        { "AnAwaitInAForAwaitIteratorClose/native", "called, t1, body 1, return, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9", "called, t1, body 1, return, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9" },
        { "AnAwaitOfAForAwaitNextResult/poisoned", "get constructor, caught C, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9", "get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AnAwaitOfAForAwaitNextResult/observed", "get constructor, called, t1, body 0, get constructor, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9", "get constructor, called, t1, body 0, get constructor, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9" },
        { "AnAwaitOfAForAwaitNextResult/subclassed", "called, t1, then, t2, t3, body 0, t4, then, t5, t6, after, t7, resolved undefined, t8, t9", "called, t1, then, t2, t3, body 0, t4, then, t5, t6, after, t7, resolved {undefined,true}, t8, t9" },
        { "AnAwaitOfAForAwaitNextResult/native", "called, t1, body 0, t2, after, t3, resolved undefined, t4, t5, t6, t7, t8, t9", "called, t1, body 0, t2, after, t3, resolved {undefined,true}, t4, t5, t6, t7, t8, t9" },
        { "ABlocksAwaitUsing/poisoned", "body, dispose, get constructor, caught C, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9", "body, dispose, get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "ABlocksAwaitUsing/observed", "body, dispose, get constructor, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9", "body, dispose, get constructor, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "ABlocksAwaitUsing/subclassed", "body, dispose, called, t1, then, t2, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9", "body, dispose, called, t1, then, t2, t3, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9" },
        { "ABlocksAwaitUsing/native", "body, dispose, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9", "body, dispose, called, t1, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "TheBodysAwaitUsing/poisoned", "body, dispose, get constructor, called, t1, rejected C, t2, t3, t4, t5, t6, t7, t8, t9", "body, dispose, get constructor, called, t1, rejected C, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "TheBodysAwaitUsing/observed", "body, dispose, get constructor, called, t1, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9", "body, dispose, get constructor, called, t1, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "TheBodysAwaitUsing/subclassed", "body, dispose, called, t1, then, t2, t3, t4, resolved undefined, t5, t6, t7, t8, t9", "body, dispose, called, t1, then, t2, t3, t4, resolved {undefined,true}, t5, t6, t7, t8, t9" },
        { "TheBodysAwaitUsing/native", "body, dispose, called, t1, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9", "body, dispose, called, t1, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
    };

    /// <summary>
    /// A site and a kind of promise, then the log as an async function. These sites are in an async generator too, but
    /// there a <c>for</c>-<c>of</c> disposes its iteration's <c>await using</c> resources without suspending, which is
    /// sebastienros/jint#4358; that half joins <see cref="InBothKinds"/> with its fix.
    /// </summary>
    public static TestCases<string, string> InAnAsyncFunction => new()
    {
        { "AForOfAwaitUsing/poisoned", "body, dispose, get constructor, caught C, after, called, t1, resolved undefined, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AForOfAwaitUsing/observed", "body, dispose, get constructor, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9" },
        { "AForOfAwaitUsing/subclassed", "body, dispose, called, t1, then, t2, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9" },
        { "AForOfAwaitUsing/native", "body, dispose, called, t1, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9" },
        { "AForAwaitOfAwaitUsing/poisoned", "called, t1, body, dispose, get constructor, caught C, after, t2, resolved undefined, t3, t4, t5, t6, t7, t8, t9" },
        { "AForAwaitOfAwaitUsing/observed", "called, t1, body, dispose, get constructor, t2, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9" },
        { "AForAwaitOfAwaitUsing/subclassed", "called, t1, body, dispose, t2, then, t3, t4, t5, after, t6, resolved undefined, t7, t8, t9" },
        { "AForAwaitOfAwaitUsing/native", "called, t1, body, dispose, t2, t3, after, t4, resolved undefined, t5, t6, t7, t8, t9" },
    };

    /// <summary>
    /// A site only an async generator has and a kind of promise, then the log.
    /// </summary>
    public static TestCases<string, string> InAnAsyncGenerator => new()
    {
        { "AYieldStarNextResult/poisoned", "next, get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarNextResult/observed", "next, get constructor, called, t1, got v, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarNextResult/subclassed", "next, called, t1, then, t2, t3, got v, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9" },
        { "AYieldStarNextResult/native", "next, called, t1, got v, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarNextResultThatYields/poisoned", "next, get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarNextResultThatYields/observed", "next, get constructor, called, t1, t2, resolved {v,false}, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarNextResultThatYields/subclassed", "next, called, t1, then, t2, t3, t4, resolved {v,false}, t5, t6, t7, t8, t9" },
        { "AYieldStarNextResultThatYields/native", "next, called, t1, t2, resolved {v,false}, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarReturnResult/poisoned", "called, t1, return 5, get constructor, caught C, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarReturnResult/observed", "called, t1, return 5, get constructor, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarReturnResult/subclassed", "called, t1, return 5, t2, then, t3, t4, t5, resolved {5,true}, t6, t7, t8, t9" },
        { "AYieldStarReturnResult/native", "called, t1, return 5, t2, t3, resolved {5,true}, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarThrowResult/poisoned", "throw E, get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarThrowResult/observed", "throw E, get constructor, called, t1, got x, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AYieldStarThrowResult/subclassed", "throw E, called, t1, then, t2, t3, got x, after, t4, resolved {undefined,true}, t5, t6, t7, t8, t9" },
        { "AYieldStarThrowResult/native", "throw E, called, t1, got x, after, t2, resolved {undefined,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AYield/poisoned", "get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AYield/observed", "get constructor, called, t1, t2, resolved {1,false}, t3, t4, t5, t6, t7, t8, t9" },
        { "AYield/subclassed", "called, t1, then, t2, t3, t4, resolved {1,false}, t5, t6, t7, t8, t9" },
        { "AYield/native", "called, t1, t2, resolved {1,false}, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnStatement/poisoned", "get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnStatement/observed", "get constructor, called, t1, t2, resolved {1,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnStatement/subclassed", "called, t1, then, t2, t3, t4, resolved {1,true}, t5, t6, t7, t8, t9" },
        { "AReturnStatement/native", "called, t1, t2, resolved {1,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnResumptionAtAYield/poisoned", "get constructor, caught C, after, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnResumptionAtAYield/observed", "get constructor, called, t1, t2, resolved {5,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnResumptionAtAYield/subclassed", "called, t1, then, t2, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9" },
        { "AReturnResumptionAtAYield/native", "called, t1, t2, resolved {5,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnBeforeTheStart/poisoned", "get constructor, called, t1, rejected C, t2, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnBeforeTheStart/observed", "get constructor, called, t1, t2, resolved {5,true}, t3, t4, t5, t6, t7, t8, t9" },
        { "AReturnBeforeTheStart/subclassed", "called, t1, then, t2, t3, t4, resolved {5,true}, t5, t6, t7, t8, t9" },
        { "AReturnBeforeTheStart/native", "called, t1, t2, resolved {5,true}, t3, t4, t5, t6, t7, t8, t9" },
    };

    /// <summary>
    /// A module's own awaits: a top-level <c>await</c>, and the disposal of a module-level <c>await using</c> as the
    /// module body ends. The ladder is the module's own, so the log is the module's alone; the second column is how
    /// the module's evaluation ends.
    /// </summary>
    public static TestCases<string, string, string> InAModule => new()
    {
        { "ATopLevelAwait/poisoned", "get constructor, caught C, after, t1, t2, t3, t4, t5, t6", "resolved" },
        { "ATopLevelAwait/observed", "get constructor, t1, got 1, after, t2, t3, t4, t5, t6", "resolved" },
        { "ATopLevelAwait/subclassed", "t1, then, t2, t3, got 1, after, t4, t5, t6", "resolved" },
        { "ATopLevelAwait/native", "t1, got 1, after, t2, t3, t4, t5, t6", "resolved" },
        { "AModulesAwaitUsing/poisoned", "t1, awaited, body, dispose, get constructor, t2, t3, t4, t5, t6", "rejected C" },
        // Node reads the constructor twice here as well.
        { "AModulesAwaitUsing/observed", "t1, awaited, body, dispose, get constructor, t2, t3, t4, t5, t6", "resolved" },
        { "AModulesAwaitUsing/subclassed", "t1, awaited, body, dispose, t2, then, t3, t4, t5, t6", "resolved" },
        { "AModulesAwaitUsing/native", "t1, awaited, body, dispose, t2, t3, t4, t5, t6", "resolved" },
    };

    private static readonly Dictionary<string, string> ModuleSites = new()
    {
        ["ATopLevelAwait"] =
            "const log = globalThis.moduleLog = []; const done = ladder(log, 6); try { log.push('got ' + await MAKE(log, 1)); } catch (e) { log.push('caught ' + show(e)); } log.push('after'); await done;",
        // The `await null` keeps Jint on the asynchronous module evaluation: a module whose only await is an `await
        // using` is evaluated synchronously, which is sebastienros/jint#4367.
        ["AModulesAwaitUsing"] =
            "const log = globalThis.moduleLog = []; ladder(log, 6); await null; log.push('awaited'); await using r = { [Symbol.asyncDispose]() { log.push('dispose'); return MAKE(log, 1); } }; log.push('body');",
    };

    [TestCaseSource(nameof(InBothKinds)), CancelAfter(10000)]
    public void AnAwaitResolvesAPromiseThroughItsConstructor(string row, string asAFunction, string asAGenerator)
    {
        var body = Body(SitesInBothKinds, row);
        using var _ = new AssertionScope();
        Run("probe(log => (async function () { " + body + " }), it => it(), false);")
            .Should().Be(asAFunction, row + ", in an async function");
        Run("probe(log => (async function* () { " + body + " })(), it => it.next(), false);")
            .Should().Be(asAGenerator, row + ", in an async generator");
    }

    [TestCaseSource(nameof(InAnAsyncFunction)), CancelAfter(10000)]
    public void AnAsyncFunctionResolvesAPromiseThroughItsConstructor(string row, string expected)
    {
        Run("probe(log => (async function () { " + Body(SitesInBothKinds, row) + " }), it => it(), false);")
            .Should().Be(expected, row);
    }

    [TestCaseSource(nameof(InAnAsyncGenerator)), CancelAfter(10000)]
    public void AnAsyncGeneratorResolvesAPromiseThroughItsConstructor(string row, string expected)
    {
        var (site, kind) = Split(row);
        var (body, request, started) = SitesInAnAsyncGenerator[site];
        Run("probe(log => (async function* () { " + body.Replace("MAKE", kind) + " })(), (it, log) => "
            + request.Replace("MAKE", kind) + ", " + (started ? "true" : "false") + ");")
            .Should().Be(expected, row);
    }

    [TestCaseSource(nameof(InAModule)), CancelAfter(10000)]
    public void AModuleResolvesAPromiseThroughItsConstructor(string row, string expectedLog, string expectedOutcome)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.Modules.Add("m", Prelude + Body(ModuleSites, row));

        string outcome;
        try
        {
            engine.Modules.Import("m");
            outcome = "resolved";
        }
        catch (JavaScriptException e)
        {
            outcome = "rejected " + e.Error;
        }

        // The module's ladder may still have steps queued once its evaluation has settled; a script run drains them.
        engine.Evaluate("0");
        using var _ = new AssertionScope();
        engine.Evaluate("moduleLog.join(', ')").AsString().Should().Be(expectedLog, row);
        outcome.Should().Be(expectedOutcome, row);
    }

    /// <summary>
    /// The lane that awaits an untouched promise without reading its <c>constructor</c> trusts what it last saw on
    /// <c>Promise.prototype</c>, so every way of changing the <c>constructor</c> an await reads, after an await has
    /// warmed that lane, must be seen by the next await. Every expected answer is Node 24's.
    /// </summary>
    [TestCase("Promise.prototype.constructor = Other;", "other")]
    [TestCase("Object.defineProperty(Promise.prototype, 'constructor', { value: Other });", "other")]
    // The await reads the getter, and so does the then() a wrapping %Promise% calls on p.
    [TestCase("Object.defineProperty(Promise.prototype, 'constructor', { get() { if (this === p) log.push('get'); return Other; } });", "get, get, other")]
    [TestCase("Object.defineProperty(Promise.prototype, 'constructor', { get() { if (this === p) log.push('get'); return Promise; } });", "get, same")]
    [TestCase("delete Promise.prototype.constructor;", "other")]
    [TestCase("Promise.prototype.unrelated = 1;", "same")]
    [TestCase("Promise.prototype.constructor = Other; Promise.prototype.constructor = Promise;", "same")]
    [TestCase("Object.setPrototypeOf(p, Object.create(Promise.prototype));", "same")]
    [TestCase("Object.setPrototypeOf(p, Object.create(Promise.prototype, { constructor: { value: Other } }));", "other")]
    [TestCase("p.constructor = Other;", "other")]
    [TestCase("p.unrelated = 1;", "same")]
    [CancelAfter(10000)]
    public void ChangingTheConstructorIsSeenByTheNextAwait(string change, string expected)
    {
        // A promise awaited as it is resumes the await before a job queued after it, and one wrapped in a new %Promise%
        // after it, so the order of the two says which of the two the await did.
        var script = """
            (async () => {
              const log = [];
              function Other() {}
              await Promise.resolve(0);
              const p = Promise.resolve(1);
              CHANGE
              const a = (async () => { await p; log.push('awaited'); })();
              const b = Promise.resolve().then(() => log.push('queued'));
              await a;
              await b;
              const order = log.filter(e => e !== 'get');
              return log.filter(e => e === 'get').concat(order[0] === 'awaited' ? 'same' : 'other').join(', ');
            })();
            """.Replace("CHANGE", change);

        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        engine.Evaluate(script).UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// A promise made in another realm inherits that realm's <c>Promise.prototype</c>, so its <c>constructor</c> is not
    /// the <c>%Promise%</c> the await resolves against, and the await wraps it, as Node does a promise from another
    /// <c>vm</c> context.
    /// </summary>
    [TestCase(true, "other")]
    [TestCase(false, "same")]
    [CancelAfter(10000)]
    public void APromiseFromAnotherRealmIsWrapped(bool fromTheOtherRealm, string expected)
    {
        var host = new RealmHost();
        var engine = new Engine(options =>
        {
            options.UseHostFactory(_ => host);
            options.ObserveCancellation(TestContext.CurrentContext.CancellationToken);
        });
        var other = host.CreateAdditionalRealm();
        engine.SetValue("p", fromTheOtherRealm
            ? engine.Advanced.WithRealm(other, scoped => scoped.Evaluate("Promise.resolve(1)"))
            : engine.Evaluate("Promise.resolve(1)"));

        engine.Evaluate("""
            (async () => {
              const log = [];
              const a = (async () => { await p; log.push('awaited'); })();
              const b = Promise.resolve().then(() => log.push('queued'));
              await a;
              await b;
              return log[0] === 'awaited' ? 'same' : 'other';
            })();
            """).UnwrapIfPromise().AsString().Should().Be(expected);
    }

    private sealed class RealmHost : Host
    {
        public Realm CreateAdditionalRealm() => base.CreateRealm();
    }

    private static string Body(Dictionary<string, string> sites, string row)
    {
        var (site, kind) = Split(row);
        return sites[site].Replace("MAKE", kind);
    }

    private static (string Site, string Kind) Split(string row)
    {
        var slash = row.IndexOf('/');
        return (row.Substring(0, slash), row.Substring(slash + 1));
    }

    private static string Run(string script)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        return engine.Evaluate(Prelude + script).UnwrapIfPromise().AsString();
    }
}
