#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// An async generator's <c>return(value)</c>, timed against the microtask queue. Resumed at a <c>yield</c>, the
/// generator first runs
/// <see href="https://tc39.es/ecma262/#sec-asyncgeneratorunwrapyieldresumption">AsyncGeneratorUnwrapYieldResumption</see>
/// (reached from <see href="https://tc39.es/ecma262/#sec-asyncgeneratoryield">AsyncGeneratorYield</see>): it awaits
/// <c>value</c> at the <c>yield</c>, so the <c>finally</c> blocks run a microtask later, with the awaited value as the
/// return completion, and a rejection is thrown at the <c>yield</c>, where a <c>try</c> around it catches it. The
/// body's return completion is then reported as it stands
/// (<see href="https://tc39.es/ecma262/#sec-asyncgeneratorstart">AsyncGeneratorStart</see>); only a
/// <c>return expr;</c> statement awaits its own operand. Before the generator has started, or after it has finished,
/// <see href="https://tc39.es/ecma262/#sec-asyncgeneratorawaitreturn">AsyncGeneratorAwaitReturn</see> awaits it
/// instead.
/// <para>
/// Jint ran the body straight away, inside the <c>return()</c> call, and awaited <c>value</c> only once the body had
/// finished, so the <c>finally</c> blocks ran before the caller's next statement and a rejection skipped every
/// <c>catch</c> and rejected the request (sebastienros/jint#4315).
/// </para>
/// <para>
/// Each log is the generator's own entries interleaved with a ladder of microtasks <c>t1</c>, <c>t2</c>, … that is
/// started just before <c>return()</c> is called; <c>called</c> is the statement after the call. Every expected log is
/// Node 24's, except where a row says it follows the specification instead.
/// </para>
/// </summary>
public class AsyncGeneratorReturnResumptionTests
{
    internal const string Prelude = """
        function ladder(log, n) {
          let p = Promise.resolve();
          for (let i = 1; i <= n; i++) {
            const k = i;
            p = p.then(() => { log.push('t' + k); });
          }
          return p;
        }
        function fmt(r) {
          return '{' + (r.value instanceof Promise ? 'a promise' : String(r.value)) + ',' + r.done + '}';
        }
        function closable(log) {
          return { [Symbol.iterator]() { return { next() { return { value: undefined, done: false }; }, return() { log.push('iter return'); return {}; } }; } };
        }
        function settle(log, request, done) {
          request.then(r => log.push('resolved ' + fmt(r)), e => log.push('rejected ' + e));
          log.push('called');
          return done.then(() => log.join(', '));
        }
        // makeGen builds the generator, started says whether it is first run to its first yield, and resume makes
        // the request whose answer is logged: return() at a suspended yield, unless the row does something else.
        async function probe(makeGen, resume, started) {
          const log = [];
          const it = makeGen(log);
          if (started) {
            await it.next();
          }
          const done = ladder(log, 7);
          return settle(log, resume(it, log), done);
        }

        """;

    internal static string Run(string script)
    {
        var engine = new Engine(options => options.ObserveCancellation(TestContext.CurrentContext.CancellationToken));
        return engine.Evaluate(Prelude + script).UnwrapIfPromise().AsString();
    }

    internal static string Probe(string generator, string resume, bool started)
        => Run("probe(log => " + generator + ", (it, log) => " + resume + ", " + (started ? "true" : "false") + ");");

    public static TestCases<string, string, string, string> AtASuspendedYield => new()
    {
        {
            "PlainValue",
            "(async function* () { try { yield 1; log.push('UNREACHED'); } finally { log.push('finally'); } })()",
            "it.return(42)",
            "called, t1, finally, t2, resolved {42,true}, t3, t4, t5, t6, t7"
        },
        {
            "ResolvedPromise",
            "(async function* () { try { yield 1; log.push('UNREACHED'); } finally { log.push('finally'); } })()",
            "it.return(Promise.resolve(42))",
            "called, t1, finally, t2, resolved {42,true}, t3, t4, t5, t6, t7"
        },
        {
            // PromiseResolve wraps a thenable, whose then runs in a job of its own: one microtask more.
            "Thenable",
            "(async function* () { try { yield 1; log.push('UNREACHED'); } finally { log.push('finally'); } })()",
            "it.return({ then(r) { log.push('then called'); r(42); } })",
            "called, t1, then called, t2, finally, t3, resolved {42,true}, t4, t5, t6, t7"
        },
        {
            "RejectedPromiseWithoutACatch",
            "(async function* () { try { yield 1; log.push('UNREACHED'); } finally { log.push('finally'); } })()",
            "it.return(Promise.reject('X'))",
            "called, t1, finally, t2, rejected X, t3, t4, t5, t6, t7"
        },
        {
            "RejectedPromiseCaughtAtTheYield",
            "(async function* () { try { yield 1; log.push('UNREACHED'); } catch (e) { log.push('caught ' + e); } finally { log.push('finally'); } })()",
            "it.return(Promise.reject('X'))",
            "called, t1, caught X, finally, t2, resolved {undefined,true}, t3, t4, t5, t6, t7"
        },
        {
            // Caught, the rejection is just a throw: the generator carries on and the request is answered by its next yield.
            "RejectedPromiseCaughtAndTheGeneratorYieldsAgain",
            "(async function* () { try { yield 1; } catch (e) { log.push('caught ' + e); } yield 2; log.push('UNREACHED'); })()",
            "it.return(Promise.reject('X'))",
            "called, t1, caught X, t2, t3, resolved {2,false}, t4, t5, t6, t7"
        },
        {
            // The issue's own example.
            "RejectedPromiseCaughtWithAFinally",
            "(async function* () { try { yield 0; } catch (e) { log.push('caught ' + e); } finally { log.push('fin'); } })()",
            "it.return(Promise.reject('X'))",
            "called, t1, caught X, fin, t2, resolved {undefined,true}, t3, t4, t5, t6, t7"
        },
        {
            // PromiseResolve reads the constructor of a promise and throws, so the await fails before anything is
            // queued: the throw reaches the catch inside the return() call.
            "PromiseWhoseConstructorGetterThrows",
            "(async function* () { try { yield 1; } catch (e) { log.push('caught ' + e.message); } finally { log.push('finally'); } })()",
            "(() => { const p = Promise.resolve(42); Object.defineProperty(p, 'constructor', { get() { log.push('get constructor'); throw new Error('broken'); } }); return it.return(p); })()",
            "get constructor, caught broken, finally, called, t1, resolved {undefined,true}, t2, t3, t4, t5, t6, t7"
        },
        {
            "FinallyThatAwaits",
            "(async function* () { try { yield 1; } finally { log.push('fin 1'); await null; log.push('fin 2'); await null; log.push('fin 3'); } })()",
            "it.return(42)",
            "called, t1, fin 1, t2, fin 2, t3, fin 3, t4, resolved {42,true}, t5, t6, t7"
        },
        {
            "NestedFinally",
            "(async function* () { try { try { yield 1; } finally { log.push('inner fin'); await null; log.push('inner fin 2'); } } finally { log.push('outer fin'); } })()",
            "it.return(42)",
            "called, t1, inner fin, t2, inner fin 2, outer fin, t3, resolved {42,true}, t4, t5, t6, t7"
        },
        {
            "YieldInsideAnExpression",
            "(async function* () { try { log.push('got ' + (yield 1)); } finally { log.push('finally'); } })()",
            "it.return(42)",
            "called, t1, finally, t2, resolved {42,true}, t3, t4, t5, t6, t7"
        },
        {
            // The yield's return completion leaves the return statement before that statement's own await.
            "YieldInsideAReturnStatement",
            "(async function* () { try { return yield 1; } finally { log.push('finally'); } })()",
            "it.return(42)",
            "called, t1, finally, t2, resolved {42,true}, t3, t4, t5, t6, t7"
        },
        {
            "YieldInsideADestructuringDefault",
            "(async function* () { try { const [a = yield 1] = closable(log); log.push('UNREACHED'); } finally { log.push('finally'); } })()",
            "it.return(42)",
            "called, t1, iter return, finally, t2, resolved {42,true}, t3, t4, t5, t6, t7"
        },
        {
            // The pending return completion is reported as it stands once the finally completes; it is not awaited again.
            "FinallyThatYields",
            "(async function* () { try { yield 1; } finally { log.push('finally'); yield 2; log.push('after yield 2'); } })()",
            "it.return(42).then(r => { log.push('first ' + fmt(r)); return it.next(); })",
            "called, t1, finally, t2, t3, first {2,false}, after yield 2, t4, t5, t6, resolved {42,true}, t7"
        },
        {
            // A return statement in the finally replaces the return completion and awaits its own operand.
            "FinallyThatReturns",
            "(async function* () { try { yield 1; } finally { log.push('finally'); return 'fin'; } })()",
            "it.return(42)",
            "called, t1, finally, t2, t3, resolved {fin,true}, t4, t5, t6, t7"
        },
        {
            "DuringADelegation",
            "(() => { async function* inner() { try { yield 1; } finally { log.push('inner finally'); } } return (async function* () { try { yield* inner(); } finally { log.push('outer finally'); } })(); })()",
            "it.return(42)",
            "called, t1, t2, inner finally, t3, outer finally, t4, resolved {42,true}, t5, t6, t7"
        },
        {
            // The specification's answer, not Node's: the rejected await is a throw completion, which yield* hands to
            // the delegate's throw(). Node 24 calls the delegate's next('X') instead, so its log has no 'inner caught X'.
            "RejectedPromiseDuringADelegation",
            "(() => { async function* inner() { try { yield 1; } catch (e) { log.push('inner caught ' + e); } } return (async function* () { try { log.push('delegated ' + (yield* inner())); } finally { log.push('outer finally'); } })(); })()",
            "it.return(Promise.reject('X'))",
            "called, t1, inner caught X, t2, delegated undefined, outer finally, t3, resolved {undefined,true}, t4, t5, t6, t7"
        },
    };

    public static TestCases<string, string, string, string> BeforeTheFirstYield => new()
    {
        {
            "PlainValue",
            "(async function* () { log.push('UNREACHED'); yield 1; })()",
            "it.return(42)",
            "called, t1, t2, resolved {42,true}, t3, t4, t5, t6, t7"
        },
        {
            // PromiseResolve hands a promise back as it is; resolving a new promise with it took two microtasks more.
            "ResolvedPromise",
            "(async function* () { log.push('UNREACHED'); yield 1; })()",
            "it.return(Promise.resolve(42))",
            "called, t1, t2, resolved {42,true}, t3, t4, t5, t6, t7"
        },
        {
            "RejectedPromise",
            "(async function* () { log.push('UNREACHED'); yield 1; })()",
            "it.return(Promise.reject('X'))",
            "called, t1, t2, rejected X, t3, t4, t5, t6, t7"
        },
    };

    [TestCaseSource(nameof(AtASuspendedYield)), CancelAfter(10000)]
    public void ReturnAtASuspendedYield(string name, string generator, string resume, string expected)
    {
        Probe(generator, resume, started: true).Should().Be(expected, name);
    }

    [TestCaseSource(nameof(BeforeTheFirstYield)), CancelAfter(10000)]
    public void ReturnBeforeTheGeneratorStarts(string name, string generator, string resume, string expected)
    {
        Probe(generator, resume, started: false).Should().Be(expected, name);
    }

    [Test, CancelAfter(10000)]
    public void ReturnAfterTheGeneratorCompleted()
    {
        // AsyncGeneratorAwaitReturn again, from the completed state.
        const string Script = """
            (async () => {
              const results = [];
              for (const value of [Promise.resolve(42), Promise.reject('X')]) {
                const log = [];
                const it = (async function* () { })();
                await it.next();
                const done = ladder(log, 5);
                results.push(await settle(log, it.return(value), done));
              }
              return results.join(' | ');
            })();
            """;

        Run(Script).Should().Be("called, t1, t2, resolved {42,true}, t3, t4, t5 | called, t1, t2, rejected X, t3, t4, t5");
    }

    [Test, CancelAfter(10000)]
    public void AReturnStatementAwaitsItsOperandOnce()
    {
        // `return expr;` awaits expr once, in the statement
        // (https://tc39.es/ecma262/#sec-return-statement-runtime-semantics-evaluation). A promise operand used to be
        // resolved into a fresh promise first, which took two microtasks more.
        const string Script = """
            (async () => {
              const log = [];
              const it = (async function* () { yield 1; return Promise.resolve(5); })();
              await it.next();
              const done = ladder(log, 5);
              return settle(log, it.next(), done);
            })();
            """;

        Run(Script).Should().Be("called, t1, t2, resolved {5,true}, t3, t4, t5");
    }

    [Test, CancelAfter(10000)]
    public void AReturnStatementAfterADelegationAwaitsItsOperand()
    {
        // The body that runs on from a finished yield* used to report a return statement's operand without
        // awaiting it at all, so the iterator result's value was the promise itself.
        const string Script = """
            (async () => {
              const log = [];
              async function* inner() { yield 1; }
              const it = (async function* () { yield* inner(); return Promise.resolve(5); })();
              await it.next();
              const done = ladder(log, 7);
              return settle(log, it.next(), done);
            })();
            """;

        Run(Script).Should().Be("called, t1, t2, t3, resolved {5,true}, t4, t5, t6, t7");
    }

    [Test, CancelAfter(10000)]
    public void TheReturnedValueIsAwaitedOnceWhenTheFinallyReplacesIt()
    {
        const string Script = """
            (async () => {
              const log = [];
              const it = (async function* () { try { yield 1; } finally { log.push('fin'); return Promise.resolve('F'); } })();
              await it.next();
              const done = ladder(log, 5);
              return settle(log, it.return(42), done);
            })();
            """;

        Run(Script).Should().Be("called, t1, fin, t2, t3, resolved {F,true}, t4, t5");
    }

    [Test, CancelAfter(10000)]
    public void AReturnQueuedBehindTheYieldsOwnAwaitIsAwaitedAtThatYield()
    {
        // return() arrives while the yield is still awaiting its own operand, so it queues behind the next() that
        // started the generator: the yield answers next() first, and only then is the queued return() awaited at it.
        const string Script = """
            (async () => {
              const log = [];
              const it = (async function* () { try { log.push('start'); yield 1; log.push('UNREACHED'); } finally { log.push('finally'); } })();
              const done = ladder(log, 5);
              it.next().then(r => log.push('next ' + fmt(r)));
              return settle(log, it.return(42), done);
            })();
            """;

        Run(Script).Should().Be("start, called, t1, t2, next {1,false}, finally, t3, resolved {42,true}, t4, t5");
    }

    [Test, CancelAfter(10000)]
    public void ANextAfterAReturnOrAThrowTheDelegateAnsweredIsANext()
    {
        // What a yield* sends its delegate is what the consumer sent it. Once the delegate had answered a throw() or
        // a return() with a value, every later next() was forwarded as that throw() or return() again.
        const string Script = """
            (async () => {
              const log = [];
              function source() {
                let n = 0;
                return {
                  [Symbol.asyncIterator]() { return this; },
                  next(v) { log.push('next(' + v + ')'); return { value: 'n' + (++n), done: n > 3 }; },
                  throw(e) { log.push('throw(' + e + ')'); return { value: 't', done: false }; },
                  return(v) { log.push('return(' + v + ')'); return { value: 'r', done: false }; },
                };
              }
              const it = (async function* () { log.push('gave ' + (yield* source())); })();
              const out = [];
              const show = r => out.push(r.value + ':' + r.done);
              show(await it.next('a'));
              show(await it.throw('E'));
              show(await it.next('b'));
              show(await it.return('R'));
              show(await it.next('c'));
              show(await it.next('d'));
              return out.join(' ') + ' | ' + log.join(' ');
            })();
            """;

        Run(Script).Should().Be("n1:false t:false n2:false r:false n3:false undefined:true | next(undefined) throw(E) next(b) return(R) next(c) next(d) gave n4");
    }
}
