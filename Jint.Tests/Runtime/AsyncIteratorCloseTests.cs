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
/// <a href="https://tc39.es/ecma262/#sec-%25asyncfromsynciteratorprototype%25.return">%AsyncFromSyncIteratorPrototype%.return</a>
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
}
