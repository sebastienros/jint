namespace Jint.Tests.Runtime;

/// <summary>
/// Pins <a href="https://tc39.es/ecma262/#sec-asynciteratorclose">AsyncIteratorClose</a> for a
/// <c>for await…of</c> loop that is abandoned before its iterator runs out.
/// <para>
/// Steps 4.c and 4.d call the iterator's <c>return</c> and then <em>Await</em> what it answered, and
/// steps 5-8 rank that against the loop's own completion: a throw completion the loop already carries
/// wins (step 5), otherwise a rejected <c>return()</c> becomes the loop's completion (step 6) and a
/// settled value that is not an Object is a <c>TypeError</c> (step 7).
/// </para>
/// <para>
/// Jint used to perform the <em>synchronous</em>
/// <a href="https://tc39.es/ecma262/#sec-iteratorclose">IteratorClose</a> here, which calls
/// <c>return()</c>, sees the promise it answered with, finds that it <em>is</em> an Object and stops.
/// So the whole of steps 4.d-7 was skipped: a <c>return()</c> that rejected was dropped on the floor
/// and <c>for await (…) { break; }</c> completed normally where V8 throws — issue #3098. The same hole
/// swallowed a sync iterable's close failure under <c>for await</c>, because
/// <a href="https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.return">%AsyncFromSyncIteratorPrototype%.return</a>
/// reports every one of those as a rejection of the promise it hands back.
/// </para>
/// </summary>
public class AsyncIteratorCloseTests
{
    private const string Harness = """
        // An async iterable whose iterator counts its closes and answers return() however the test
        // says. `opts.limit` ends the iteration after that many steps; `opts.nextRejects` fails the
        // step itself, which must never reach AsyncIteratorClose at all.
        function makeAsyncIterable(returnImpl, opts) {
            opts = opts || {};
            const record = { closeCount: 0, log: [] };
            let i = 0;
            record.iterable = {
                [Symbol.asyncIterator]() {
                    return {
                        next() {
                            if (opts.nextRejects) { return Promise.reject(new Error('from next')); }
                            i++;
                            if (opts.limit !== undefined && i > opts.limit) {
                                return Promise.resolve({ value: undefined, done: true });
                            }
                            return Promise.resolve({ value: i, done: false });
                        },
                        return: returnImpl === undefined ? undefined : function () {
                            record.closeCount++;
                            record.log.push('return');
                            return returnImpl();
                        }
                    };
                }
            };
            return record;
        }

        // The same shape with only a SYNC iterator, so for-await drives it through
        // CreateAsyncFromSyncIterator.
        function makeSyncIterable(returnImpl) {
            const record = { closeCount: 0, log: [] };
            record.iterable = {
                [Symbol.iterator]() {
                    return {
                        next() { return { value: 1, done: false }; },
                        return: returnImpl === undefined ? undefined : function () {
                            record.closeCount++;
                            record.log.push('return');
                            return returnImpl();
                        }
                    };
                }
            };
            return record;
        }

        const REJECT = function () { return Promise.reject(new Error('cleanup failed')); };

        // Flattens how a promise settled into one assertable string.
        async function outcome(fn) {
            try { return 'ok:' + (await fn()); }
            catch (e) { return 'threw:' + (e && e.name ? e.name : '?') + ':' + (e && e.message !== undefined ? e.message : e); }
        }

        // An iterable that never runs out and logs each return() call, the shape of the report in issue
        // #4354. next() answers a plain object, which the loop's own Await wraps; `sync` hands the iterator
        // out through Symbol.iterator instead, so for-await drives it through CreateAsyncFromSyncIterator.
        function closingIterable(log, returnImpl, value, sync) {
            const iterator = { next() { return { value: value === undefined ? 1 : value, done: false }; } };
            if (returnImpl !== undefined) {
                iterator.return = function () { log.push('return'); return returnImpl(); };
            }
            return sync
                ? { [Symbol.iterator]() { return iterator; } }
                : { [Symbol.asyncIterator]() { return iterator; } };
        }

        // Starts `start(log)` and, beside it, a chain of four microtasks logging t1..t4, then answers the
        // interleaving once both are over. Where an entry lands among the ticks is how many microtasks it
        // took, which is what the Await of AsyncIteratorClose step 4.d costs and the synchronous close did not.
        function interleave(start) {
            const log = [];
            const done = start(log);
            let p = Promise.resolve();
            for (let i = 1; i <= 4; i++) { const n = i; p = p.then(function () { log.push('t' + n); }); }
            return Promise.all([done, p]).then(function () { return log.join(','); });
        }

        function thrower() { throw 'E'; }
        const OBJECT = function () { return {}; };
        """;

    private static string Run(string script) =>
        new Engine().Evaluate(Harness + "\n" + script).UnwrapIfPromise().AsString();

    /// <summary>
    /// The report from issue #3098, verbatim in shape: a loop left by <c>break</c> whose
    /// <c>return()</c> answers a rejected promise. Step 6 makes that rejection the loop's completion.
    /// </summary>
    [Test]
    public void ARejectedReturnBecomesTheCompletionOfALoopLeftByBreak()
    {
        Run("""
            (async function () {
                const record = makeAsyncIterable(REJECT);
                const result = await outcome(async function () {
                    for await (const x of record.iterable) { break; }
                    return 'no-throw';
                });
                return result + ',closeCount=' + record.closeCount;
            })()
            """).Should().Be("threw:Error:cleanup failed,closeCount=1");
    }

    /// <summary>
    /// Step 5 — "If completion is a throw completion, return ? completion" — comes before step 6, so a
    /// throw already in flight outranks the close's rejection. The <c>return()</c> is still called.
    /// </summary>
    [Test]
    public void AThrowFromTheBodyOutranksARejectedReturn()
    {
        Run("""
            (async function () {
                const record = makeAsyncIterable(REJECT);
                const result = await outcome(async function () {
                    for await (const x of record.iterable) { throw new Error('body error'); }
                    return 'no-throw';
                });
                return result + ',closeCount=' + record.closeCount;
            })()
            """).Should().Be("threw:Error:body error,closeCount=1");
    }

    /// <summary>
    /// The other direction of the same precedence: a <c>return</c> completion is abrupt but is not a
    /// throw completion, so step 5 does not fire and step 6 replaces it with the close's rejection.
    /// </summary>
    [Test]
    public void AReturnFromTheBodyLosesToARejectedReturn()
    {
        Run("""
            (async function () {
                const record = makeAsyncIterable(REJECT);
                const result = await outcome(async function () {
                    for await (const x of record.iterable) { return 'returned'; }
                    return 'no-throw';
                });
                return result + ',closeCount=' + record.closeCount;
            })()
            """).Should().Be("threw:Error:cleanup failed,closeCount=1");
    }

    /// <summary>
    /// A <c>break</c> naming the loop's own label, and one naming an enclosing label (which leaves the
    /// loop with a Break completion still carrying its target), both reach the close.
    /// </summary>
    [Test]
    public void ALabelledBreakStillPropagatesTheRejectedReturn()
    {
        Run("""
            (async function () {
                const own = makeAsyncIterable(REJECT);
                const ownResult = await outcome(async function () {
                    mine: for await (const x of own.iterable) { break mine; }
                    return 'no-throw';
                });

                const outerRecord = makeAsyncIterable(REJECT);
                const outerResult = await outcome(async function () {
                    outer: {
                        for await (const x of outerRecord.iterable) { break outer; }
                    }
                    return 'no-throw';
                });

                return 'own=' + ownResult + '|outer=' + outerResult
                    + '|counts=' + own.closeCount + ',' + outerRecord.closeCount;
            })()
            """).Should().Be("own=threw:Error:cleanup failed|outer=threw:Error:cleanup failed|counts=1,1");
    }

    /// <summary>
    /// Step 7 checks the value the Await <em>settled</em> with, not the promise the <c>return()</c>
    /// handed over — which is exactly the check the old synchronous close could never make, since
    /// every promise is an Object.
    /// </summary>
    [Test]
    public void TheReturnResultIsCheckedAfterTheAwaitNotBefore()
    {
        Run("""
            (async function () {
                const rows = [];
                rows.push('promise-of-non-object=' + await outcome(async function () {
                    for await (const x of makeAsyncIterable(function () { return Promise.resolve(42); }).iterable) { break; }
                    return 'no-throw';
                }));
                rows.push('plain-non-object=' + await outcome(async function () {
                    for await (const x of makeAsyncIterable(function () { return 42; }).iterable) { break; }
                    return 'no-throw';
                }));
                rows.push('promise-of-object=' + await outcome(async function () {
                    for await (const x of makeAsyncIterable(function () { return Promise.resolve({}); }).iterable) { break; }
                    return 'no-throw';
                }));
                rows.push('thenable-of-non-object=' + await outcome(async function () {
                    for await (const x of makeAsyncIterable(function () { return { then: function (res) { res(123); } }; }).iterable) { break; }
                    return 'no-throw';
                }));
                return rows.join('|');
            })()
            """).Should().Be(
            "promise-of-non-object=threw:TypeError:Iterator returned non-object"
            + "|plain-non-object=threw:TypeError:Iterator returned non-object"
            + "|promise-of-object=ok:no-throw"
            + "|thenable-of-non-object=threw:TypeError:Iterator returned non-object");
    }

    /// <summary>
    /// Step 4.b (<c>return</c> is undefined, "return ? completion") and step 4.c throwing on its own.
    /// </summary>
    [Test]
    public void AnAbsentReturnLeavesTheCompletionAloneAndAThrowingOneReplacesIt()
    {
        Run("""
            (async function () {
                const absent = await outcome(async function () {
                    for await (const x of makeAsyncIterable(undefined).iterable) { break; }
                    return 'no-throw';
                });
                const throwing = await outcome(async function () {
                    for await (const x of makeAsyncIterable(function () { throw new Error('from return'); }).iterable) { break; }
                    return 'no-throw';
                });
                const notCallable = await outcome(async function () {
                    const it = { [Symbol.asyncIterator]() { return { next() { return Promise.resolve({ value: 1, done: false }); }, return: 1 }; } };
                    for await (const x of it) { break; }
                    return 'no-throw';
                });
                return 'absent=' + absent + '|throws=' + throwing + '|notCallable=' + notCallable.split(':')[1];
            })()
            """).Should().Be("absent=ok:no-throw|throws=threw:Error:from return|notCallable=TypeError");
    }

    /// <summary>
    /// The close's Await is a real suspension of the async function, not a value the loop peeks at:
    /// everything the awaited chain does happens before the loop's own completion is observed. Without
    /// step 4.d the loop finishes first and <c>awaited</c> lands after <c>after</c>.
    /// </summary>
    [Test]
    public void TheCloseSuspendsTheAsyncFunctionOnItsAwait()
    {
        Run("""
            (async function () {
                const log = [];
                const iterable = {
                    [Symbol.asyncIterator]() {
                        return {
                            next() { return Promise.resolve({ value: 1, done: false }); },
                            return() {
                                log.push('return');
                                return Promise.resolve({}).then(function (v) { log.push('awaited'); return v; });
                            }
                        };
                    }
                };
                await (async function () { for await (const x of iterable) { break; } log.push('after'); })();
                return log.join(',');
            })()
            """).Should().Be("return,awaited,after");
    }

    /// <summary>
    /// A plain sync iterable under <c>for await</c> is driven through CreateAsyncFromSyncIterator, whose
    /// <c>return</c> reports a throwing sync <c>return()</c>, a non-object result and a rejected
    /// <c>value</c> alike as a rejection of the promise it answers with. All three were swallowed.
    /// </summary>
    [Test]
    public void ASyncIterableUnderForAwaitPropagatesItsCloseFailure()
    {
        Run("""
            (async function () {
                const throwing = makeSyncIterable(function () { throw new Error('sync return throw'); });
                const throwingResult = await outcome(async function () {
                    for await (const x of throwing.iterable) { break; }
                    return 'no-throw';
                });

                const nonObject = makeSyncIterable(function () { return 42; });
                const nonObjectResult = await outcome(async function () {
                    for await (const x of nonObject.iterable) { break; }
                    return 'no-throw';
                });

                const rejectedValue = makeSyncIterable(function () {
                    return { done: true, value: Promise.reject(new Error('rejected value')) };
                });
                const rejectedValueResult = await outcome(async function () {
                    for await (const x of rejectedValue.iterable) { break; }
                    return 'no-throw';
                });

                return 'throws=' + throwingResult
                    + '|nonObject=' + nonObjectResult.split(':')[1]
                    + '|rejectedValue=' + rejectedValueResult
                    + '|counts=' + throwing.closeCount + ',' + nonObject.closeCount + ',' + rejectedValue.closeCount;
            })()
            """).Should().Be(
            "throws=threw:Error:sync return throw|nonObject=TypeError"
            + "|rejectedValue=threw:Error:rejected value|counts=1,1,1");
    }

    /// <summary>
    /// The same for a <c>for await…of</c> in an async <em>generator</em> body, which suspends through a
    /// different resume path than an async function's.
    /// </summary>
    [Test]
    public void AnAsyncGeneratorBodyPropagatesTheRejectedClose()
    {
        Run("""
            (async function () {
                const record = makeAsyncIterable(REJECT);
                const result = await outcome(async function () {
                    async function* g(src) { for await (const x of src) { break; } yield 'unreachable'; }
                    const it = g(record.iterable);
                    await it.next();
                    return 'no-throw';
                });
                return result + ',closeCount=' + record.closeCount;
            })()
            """).Should().Be("threw:Error:cleanup failed,closeCount=1");
    }

    /// <summary>
    /// The invariant #3047 established, in its async shape: an abrupt completion produced by the step
    /// itself sets the record's [[Done]] and is propagated with <c>?</c>, so it never reaches
    /// AsyncIteratorClose. Running out does not close either — ForIn/OfBodyEvaluation step 8.e returns
    /// the iteration result before any close.
    /// </summary>
    [Test]
    public void AFailedStepAndAnExhaustedIteratorStillCloseNothing()
    {
        Run("""
            (async function () {
                const rejecting = makeAsyncIterable(REJECT, { nextRejects: true });
                const rejectingResult = await outcome(async function () {
                    for await (const x of rejecting.iterable) { }
                    return 'no-throw';
                });

                const finite = makeAsyncIterable(REJECT, { limit: 2 });
                const seen = [];
                const finiteResult = await outcome(async function () {
                    for await (const x of finite.iterable) { seen.push(x); }
                    return 'seen=' + seen.join('');
                });

                return 'stepFailure=' + rejectingResult + ',closeCount=' + rejecting.closeCount
                    + '|exhausted=' + finiteResult + ',closeCount=' + finite.closeCount;
            })()
            """).Should().Be("stepFailure=threw:Error:from next,closeCount=0|exhausted=ok:seen=12,closeCount=0");
    }

    /// <summary>
    /// A loop head declaring <c>await using</c> suspends on the per-iteration dispose before it can
    /// close, so its abrupt exits leave through a second code path (the dispose resume) that owes the
    /// same AsyncIteratorClose. The dispose runs first, then the close.
    /// </summary>
    [Test]
    public void ALoopSuspendedOnAnAsyncDisposeStillPropagatesTheRejectedClose()
    {
        Run("""
            (async function () {
                const log = [];
                const iterable = {
                    [Symbol.asyncIterator]() {
                        return {
                            next() {
                                return Promise.resolve({
                                    value: { async [Symbol.asyncDispose]() { log.push('disposed'); } },
                                    done: false
                                });
                            },
                            return() { log.push('return'); return Promise.reject(new Error('cleanup failed')); }
                        };
                    }
                };
                const result = await outcome(async function () {
                    for await (await using x of iterable) { break; }
                    return 'no-throw';
                });
                return log.join(',') + '|' + result;
            })()
            """).Should().Be("disposed,return|threw:Error:cleanup failed");
    }

    /// <summary>
    /// The report from issue #4354, verbatim in shape. Step 5 returns a throw completion whatever the close
    /// did, but only <em>after</em> step 4.d has awaited what <c>return()</c> answered — so the throw reaches
    /// the <c>catch</c> one microtask after <c>return</c>, behind <c>t1</c>. Jint closed such a loop with the
    /// synchronous IteratorClose, which skips that Await, and the <c>catch</c> ran ahead of <c>t1</c>.
    /// Expected values are V8's (Node 26).
    /// </summary>
    [Test]
    public void ALoopLeftByAThrowAwaitsWhatReturnAnsweredBeforeTheThrowPropagates()
    {
        Run("""
            interleave(function (log) {
                const src = closingIterable(log, OBJECT);
                return (async function () {
                    try { for await (var x of src) throw 'E'; } catch (e) { log.push('caught ' + e); }
                })();
            })
            """).Should().Be("return,t1,caught E,t2,t3,t4");
    }

    /// <summary>
    /// Every way a throw leaves the loop owes the same Await, and they reach the loop two different ways: a
    /// <c>throw</c> statement, or a call inside a block, arrives as a throw completion, while a call that is the
    /// whole body, a destructuring binding that fails and an assignment target whose setter throws arrive as an
    /// exception unwinding through the loop. All of them are an abrupt status that
    /// <a href="https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset">ForIn/OfBodyEvaluation</a>
    /// hands to AsyncIteratorClose, and each row is one microtask later than the synchronous close made it.
    /// </summary>
    [Test]
    public void EveryWayAThrowLeavesTheLoopPaysTheAwait()
    {
        Run("""
            (async function () {
                const shapes = {
                    statement: async function (src) { for await (var x of src) throw 'E'; },
                    block: async function (src) { for await (var x of src) { throw 'E'; } },
                    call: async function (src) { for await (var x of src) thrower(); },
                    blockCall: async function (src) { for await (var x of src) { thrower(); } },
                    binding: async function (src) { for await (const [a] of src) { } },
                    target: async function (src) { const o = { set x(v) { throw 'E'; } }; for await (o.x of src) { } },
                };
                const rows = [];
                for (const name of Object.keys(shapes)) {
                    rows.push(name + '=' + await interleave(function (log) {
                        return shapes[name](closingIterable(log, OBJECT))
                            .catch(function (e) { log.push('caught ' + (typeof e === 'string' ? e : e.name)); });
                    }));
                }
                return rows.join('|');
            })()
            """).Should().Be(
            "statement=return,t1,t2,caught E,t3,t4"
            + "|block=return,t1,t2,caught E,t3,t4"
            + "|call=return,t1,t2,caught E,t3,t4"
            + "|blockCall=return,t1,t2,caught E,t3,t4"
            + "|binding=return,t1,t2,caught TypeError,t3,t4"
            + "|target=return,t1,t2,caught E,t3,t4");
    }

    /// <summary>
    /// The same shapes in an async <em>generator</em> body, which suspends on the close's Await through its own
    /// resume path, plus a <c>catch</c> inside the generator that sees the throw once the Await is over.
    /// </summary>
    [Test]
    public void AnAsyncGeneratorBodyPaysTheSameAwait()
    {
        Run("""
            (async function () {
                const shapes = {
                    statement: async function* (src) { for await (var x of src) throw 'E'; },
                    block: async function* (src) { for await (var x of src) { throw 'E'; } },
                    call: async function* (src) { for await (var x of src) thrower(); },
                    binding: async function* (src) { for await (const [a] of src) { } },
                    caughtInside: async function* (src, log) {
                        try { for await (var x of src) throw 'E'; } catch (e) { log.push('caught ' + e); }
                    },
                };
                const rows = [];
                for (const name of Object.keys(shapes)) {
                    rows.push(name + '=' + await interleave(function (log) {
                        return shapes[name](closingIterable(log, OBJECT), log).next()
                            .catch(function (e) { log.push('caught ' + (typeof e === 'string' ? e : e.name)); });
                    }));
                }
                return rows.join('|');
            })()
            """).Should().Be(
            "statement=return,t1,t2,caught E,t3,t4"
            + "|block=return,t1,t2,caught E,t3,t4"
            + "|call=return,t1,t2,caught E,t3,t4"
            + "|binding=return,t1,t2,caught TypeError,t3,t4"
            + "|caughtInside=return,t1,caught E,t2,t3,t4");
    }

    /// <summary>
    /// What <c>return()</c> answers decides how long the throw waits, and never what it is. Whatever it answers
    /// normally is awaited — a non-object too, since step 7's check comes after step 5 has already returned — and
    /// a thenable costs its own two jobs and has its <c>then</c> called, which the synchronous close never did.
    /// A rejection is awaited and then discarded. Where step 3 or 4.c is abrupt (a <c>return()</c> that throws, a
    /// throwing getter) or there is no <c>return</c> at all, there is no Await and nothing changes; and a promise
    /// whose <c>constructor</c> getter throws makes the Await itself abrupt before it suspends (Await step 2,
    /// <a href="https://tc39.es/ecma262/#sec-promise-resolve">PromiseResolve</a>), so that one does not wait either.
    /// </summary>
    [Test]
    public void WhatReturnAnsweredDecidesHowLongTheThrowWaits()
    {
        Run("""
            (async function () {
                const answers = {
                    object: OBJECT,
                    nonObject: function () { return 42; },
                    fulfilled: function () { return Promise.resolve({}); },
                    rejected: function () { return Promise.reject('R'); },
                    thenable: 'thenable',
                    throws: function () { throw 'R'; },
                    absent: undefined,
                    constructorThrows: function () {
                        const p = Promise.resolve({});
                        Object.defineProperty(p, 'constructor', { get() { throw 'C'; } });
                        return p;
                    },
                };
                const rows = [];
                for (const name of Object.keys(answers)) {
                    rows.push(name + '=' + await interleave(function (log) {
                        const impl = answers[name] === 'thenable'
                            ? function () { return { then(resolve) { log.push('then'); resolve({}); } }; }
                            : answers[name];
                        const src = closingIterable(log, impl);
                        return (async function () { for await (var x of src) throw 'E'; })()
                            .catch(function (e) { log.push('caught ' + e); });
                    }));
                }
                rows.push('getterThrows=' + await interleave(function (log) {
                    const src = { [Symbol.asyncIterator]() {
                        return { next() { return { value: 1, done: false }; }, get return() { log.push('get'); throw 'R'; } };
                    } };
                    return (async function () { for await (var x of src) throw 'E'; })()
                        .catch(function (e) { log.push('caught ' + e); });
                }));
                return rows.join('|');
            })()
            """).Should().Be(
            "object=return,t1,t2,caught E,t3,t4"
            + "|nonObject=return,t1,t2,caught E,t3,t4"
            + "|fulfilled=return,t1,t2,caught E,t3,t4"
            + "|rejected=return,t1,t2,caught E,t3,t4"
            + "|thenable=return,t1,then,t2,t3,caught E,t4"
            + "|throws=return,t1,caught E,t2,t3,t4"
            + "|absent=t1,caught E,t2,t3,t4"
            + "|constructorThrows=return,t1,caught E,t2,t3,t4"
            + "|getterThrows=get,t1,caught E,t2,t3,t4");
    }

    /// <summary>
    /// A sync iterable under <c>for await</c> is closed through
    /// <a href="https://tc39.es/ecma262/#sec-%asyncfromsynciteratorprototype%.return">%AsyncFromSyncIteratorPrototype%.return</a>,
    /// whose promise settles a job after the sync <c>return()</c> answers; the loop awaits that promise too.
    /// </summary>
    [Test]
    public void ASyncIterableUnderForAwaitAwaitsItsWrappedReturn()
    {
        Run("""
            interleave(function (log) {
                const src = closingIterable(log, OBJECT, 1, true);
                return (async function () { for await (var x of src) throw 'E'; })()
                    .catch(function (e) { log.push('caught ' + e); });
            })
            """).Should().Be("t1,return,t2,t3,t4,caught E");
    }

    /// <summary>
    /// A loop head declaring <c>using</c> or <c>await using</c> disposes its binding before it closes, and the
    /// throw then still waits on the close. The <c>await using</c> loop leaves through the resume after its
    /// disposal's Await, a second code path from the one the inline <c>using</c> disposal takes.
    /// </summary>
    [Test]
    public void ALoopThatDisposesBeforeItClosesStillAwaitsTheClose()
    {
        Run("""
            (async function () {
                const usingRow = await interleave(function (log) {
                    const src = closingIterable(log, OBJECT, { [Symbol.dispose]() { log.push('disposed'); } });
                    return (async function () { for await (using x of src) { throw 'E'; } })()
                        .catch(function (e) { log.push('caught ' + e); });
                });
                const awaitUsingRow = await interleave(function (log) {
                    const src = closingIterable(log, OBJECT, { async [Symbol.asyncDispose]() { log.push('disposed'); } });
                    return (async function () { for await (await using x of src) { throw 'E'; } })()
                        .catch(function (e) { log.push('caught ' + e); });
                });
                return 'using=' + usingRow + '|awaitUsing=' + awaitUsingRow;
            })()
            """).Should().Be(
            "using=disposed,return,t1,t2,caught E,t3,t4"
            + "|awaitUsing=disposed,t1,return,t2,t3,caught E,t4");
    }

    /// <summary>
    /// A rejected promise that <c>return()</c> answers under a throw is awaited before step 5 discards it, and an
    /// awaited rejection is a handled one, so the host's rejection tracker reports nothing for it. The synchronous
    /// close never looked at the promise, which left it an unhandled rejection the tracker reported. The second
    /// rejection, which nothing handles, is the control proving the tracker is listening.
    /// </summary>
    [Test]
    public void ARejectedReturnUnderAThrowIsAwaitedAndSoHandled()
    {
        var engine = new Engine();
        var reported = new List<string>();
        engine.Tasks.PromiseRejectionTracker += (_, args) => reported.Add(args.Operation + ":" + args.Value);

        engine.Evaluate(Harness + """

            (async function () {
                try { for await (var x of closingIterable([], function () { return Promise.reject('R'); })) throw 'E'; }
                catch (e) { }
                Promise.reject('unhandled');
            })();
            """);
        engine.Tasks.ProcessTasks();

        reported.Should().Equal("Reject:unhandled");
    }

    /// <summary>
    /// The throw that waited on the close is the one that left the loop: the same object, whether it arrived as a
    /// completion or as an exception unwinding through the loop.
    /// </summary>
    [Test]
    public void TheThrowThatWaitedIsTheOneThatLeftTheLoop()
    {
        Run("""
            (async function () {
                const thrown = new Error('body');
                const seen = [];
                try {
                    for await (var x of closingIterable([], OBJECT)) (function () { throw thrown; })();
                } catch (e) { seen.push(e === thrown); }
                try {
                    for await (var x of closingIterable([], OBJECT)) { throw thrown; }
                } catch (e) { seen.push(e === thrown); }
                return seen.join(',');
            })()
            """).Should().Be("true,true");
    }
}
