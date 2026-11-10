#nullable enable

using Jint.Native.AsyncGenerator;
using Jint.Native.Generator;

namespace Jint.Tests.Runtime;

/// <summary>
/// Every evaluation of <c>yield*</c> calls GetIterator on its operand afresh
/// (<a href="https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation">YieldExpression : yield * AssignmentExpression</a>,
/// step 3), so a delegation that ended abruptly is over and nothing resumes into it (sebastienros/jint#4440).
/// <para>
/// A generator parks the delegation it is suspended in, the delegate's record, its [[NextMethod]] and the
/// <c>yield*</c> node, and a <c>yield*</c> that finds a record parked under its own node continues that delegation
/// rather than starting one. A delegation that ended by a throw left all three behind, so the next evaluation of the
/// same node, in a loop around a <c>try</c>/<c>catch</c>, resumed the delegate that had failed instead of delegating to
/// its new operand. Every row runs in a generator and in an async generator. The async generator's delegate is an async
/// iterable, so a method that throws reaches the <c>yield*</c> as a throw; over a sync iterable the
/// CreateAsyncFromSyncIterator wrapper turns it into a rejection, and those rows are the controls. The expected values
/// are Node 26's.
/// </para>
/// </summary>
public class YieldStarAbruptDelegationTests
{
    private const string Harness = """
        const yieldsX = function () { return { value: 'x', done: false }; };

        // The iterator each first delegation steps, and the resumption that ends it: none when its first step does.
        const ways = {
            nextThrows: [function () { return { next() { throw 'N'; } }; }, 'none'],
            nextNonObject: [function () { return { next() { return 1; } }; }, 'none'],
            nextDoneThrows: [function () { return { next() { return { get done() { throw 'D'; } }; } }; }, 'none'],
            nextValueThrows: [function () { return { next() { return { done: true, get value() { throw 'V'; } }; } }; }, 'none'],
            throwThrows: [function () { return { next: yieldsX, throw() { throw 'T'; } }; }, 'throw'],
            throwNonObject: [function () { return { next: yieldsX, throw() { return 1; } }; }, 'throw'],
            throwReadThrows: [function () { return { next: yieldsX, get throw() { throw 'TG'; } }; }, 'throw'],
            throwDoneThrows: [function () { return { next: yieldsX, throw() { return { get done() { throw 'TD'; } }; } }; }, 'throw'],
            throwValueThrows: [function () { return { next: yieldsX, throw() { return { done: true, get value() { throw 'TV'; } }; } }; }, 'throw'],
            noThrow: [function () { return { next: yieldsX }; }, 'throw'],
            noThrowReturnThrows: [function () { return { next: yieldsX, return() { throw 'RT'; } }; }, 'throw'],
            noThrowReturnReadThrows: [function () { return { next: yieldsX, get return() { throw 'RTG'; } }; }, 'throw'],
            noThrowReturnRejects: [function () { return { next: yieldsX, return() { const p = Promise.reject('RR'); p.catch(function () {}); return p; } }; }, 'throw'],
            noThrowReturnObject: [function () { return { next: yieldsX, return() { return {}; } }; }, 'throw'],
            returnThrows: [function () { return { next: yieldsX, return() { throw 'R'; } }; }, 'return'],
            returnNonObject: [function () { return { next: yieldsX, return() { return 1; } }; }, 'return'],
            returnReadThrows: [function () { return { next: yieldsX, get return() { throw 'RG'; } }; }, 'return'],
            returnDoneThrows: [function () { return { next: yieldsX, return() { return { get done() { throw 'RD'; } }; } }; }, 'return']
        };

        const name = function (e) { return typeof e === 'string' ? e : e.constructor.name; };

        // A delegate for the first evaluation of a yield*: the way's iterator, behind an iterable of the given kind.
        function delegateFor(kind, way) {
            const it = ways[way][0]();
            return kind === 'async' ? { [Symbol.asyncIterator]() { return it; } } : { [Symbol.iterator]() { return it; } };
        }
        """;

    private const string DelegateTwiceHarness = Harness + """

        // One yield* evaluated twice, in a loop around a try/catch: first over the way's delegate, whose delegation
        // ends abruptly, then over [1, 2]. Answers every result the generator gave, until it is done.
        async function delegateTwice(kind, way) {
            const resumption = ways[way][1];
            const operand = function (i) { return i === 0 ? delegateFor(kind, way) : [1, 2]; };
            const g = kind === 'sync'
                ? (function* () {
                    for (let i = 0; i < 2; i++) { try { yield* operand(i); } catch (e) { yield 'caught:' + name(e); } }
                    return 'end';
                })()
                : (async function* () {
                    for (let i = 0; i < 2; i++) { try { yield* operand(i); } catch (e) { yield 'caught:' + name(e); } }
                    return 'end';
                })();
            const out = [];
            let r = await g.next();
            out.push(r.value + ':' + r.done);
            if (resumption !== 'none') { r = await g[resumption]('X'); out.push(r.value + ':' + r.done); }
            for (let n = 0; n < 6 && !r.done; n++) { r = await g.next(); out.push(r.value + ':' + r.done); }
            return out.join(' ');
        }
        """;

    /// <summary>
    /// The repro from sebastienros/jint#4440: the second iteration's <c>yield*</c> delegates to <c>[1, 2]</c>, where Jint
    /// resumed the generator whose body had thrown, which answered done at once. In the async generator the throw comes
    /// back through the CreateAsyncFromSyncIterator wrapper as a rejection, so that row is the control.
    /// </summary>
    [TestCase("sync", "1,2")]
    [TestCase("async", "1,2")]
    public void TheSecondEvaluationOfAYieldStarWhoseDelegateThrewDelegatesToItsNewOperand(string kind, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($$"""
            (async function () {
                const thrower = function* () { throw 'boom'; };
                const out = [];
                if ('{{kind}}' === 'sync') {
                    const g = function* () { for (let i = 0; i < 2; i++) { try { yield* (i === 0 ? thrower() : [1, 2]); } catch {} } };
                    for (const x of g()) { out.push(x); }
                } else {
                    const g = async function* () { for (let i = 0; i < 2; i++) { try { yield* (i === 0 ? thrower() : [1, 2]); } catch {} } };
                    for await (const x of g()) { out.push(x); }
                }
                return out.join(',');
            })()
            """).UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// Every way a delegation ends abruptly: a step of the delegate that throws, answers a non-object or has a
    /// <c>done</c> or <c>value</c> getter that throws; a forwarded <c>throw()</c> whose <c>throw</c> read, call or answer
    /// fails, or which closes a delegate with no <c>throw</c> (step 7.b.iii), whose <c>return</c> may fail too; and a
    /// forwarded <c>return()</c> whose <c>return</c> read, call or answer fails. The body catches the throw, and the
    /// next iteration's <c>yield*</c> delegates to <c>[1, 2]</c>. The rows whose ending already forgot the delegation are
    /// controls: an answer that is done (whose <c>value</c> getter then throws), a delegate with no <c>throw</c>, and,
    /// in an async generator, every way that fails once an answer has settled (a non-object, a throwing getter), a
    /// forwarded <c>return()</c>, and every <c>asyncOverSync</c> row. The async <c>noThrowReturnThrows</c> row failed
    /// for a reason of its own: AsyncIteratorClose (https://tc39.es/ecma262/#sec-asynciteratorclose, step 6) swallowed
    /// what the <c>return</c> threw, and the <c>yield*</c> threw its <c>TypeError</c> instead. That close awaits what the
    /// <c>return</c> answered (step 4.d, sebastienros/jint#4498), so the async <c>noThrowReturnRejects</c> row catches the
    /// rejection, and both <c>noThrowReturn*</c> rows end the delegation through the resumption after the await.
    /// </summary>
    [TestCase("sync", "nextThrows", "caught:N:false 1:false 2:false end:true")]
    [TestCase("async", "nextThrows", "caught:N:false 1:false 2:false end:true")]
    [TestCase("asyncOverSync", "nextThrows", "caught:N:false 1:false 2:false end:true")]
    [TestCase("sync", "nextNonObject", "caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("async", "nextNonObject", "caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("sync", "nextDoneThrows", "caught:D:false 1:false 2:false end:true")]
    [TestCase("async", "nextDoneThrows", "caught:D:false 1:false 2:false end:true")]
    [TestCase("asyncOverSync", "nextDoneThrows", "caught:D:false 1:false 2:false end:true")]
    [TestCase("sync", "nextValueThrows", "caught:V:false 1:false 2:false end:true")]
    [TestCase("async", "nextValueThrows", "caught:V:false 1:false 2:false end:true")]
    [TestCase("sync", "throwThrows", "x:false caught:T:false 1:false 2:false end:true")]
    [TestCase("async", "throwThrows", "x:false caught:T:false 1:false 2:false end:true")]
    [TestCase("asyncOverSync", "throwThrows", "x:false caught:T:false 1:false 2:false end:true")]
    [TestCase("sync", "throwNonObject", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("async", "throwNonObject", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("asyncOverSync", "throwNonObject", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("sync", "throwReadThrows", "x:false caught:TG:false 1:false 2:false end:true")]
    [TestCase("async", "throwReadThrows", "x:false caught:TG:false 1:false 2:false end:true")]
    [TestCase("sync", "throwDoneThrows", "x:false caught:TD:false 1:false 2:false end:true")]
    [TestCase("async", "throwDoneThrows", "x:false caught:TD:false 1:false 2:false end:true")]
    [TestCase("sync", "throwValueThrows", "x:false caught:TV:false 1:false 2:false end:true")]
    [TestCase("async", "throwValueThrows", "x:false caught:TV:false 1:false 2:false end:true")]
    [TestCase("sync", "noThrow", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("async", "noThrow", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("sync", "noThrowReturnThrows", "x:false caught:RT:false 1:false 2:false end:true")]
    [TestCase("async", "noThrowReturnThrows", "x:false caught:RT:false 1:false 2:false end:true")]
    [TestCase("asyncOverSync", "noThrowReturnThrows", "x:false caught:RT:false 1:false 2:false end:true")]
    [TestCase("sync", "noThrowReturnReadThrows", "x:false caught:RTG:false 1:false 2:false end:true")]
    [TestCase("async", "noThrowReturnReadThrows", "x:false caught:RTG:false 1:false 2:false end:true")]
    [TestCase("sync", "noThrowReturnRejects", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("async", "noThrowReturnRejects", "x:false caught:RR:false 1:false 2:false end:true")]
    [TestCase("asyncOverSync", "noThrowReturnRejects", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("sync", "noThrowReturnObject", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("async", "noThrowReturnObject", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("sync", "returnThrows", "x:false caught:R:false 1:false 2:false end:true")]
    [TestCase("async", "returnThrows", "x:false caught:R:false 1:false 2:false end:true")]
    [TestCase("asyncOverSync", "returnThrows", "x:false caught:R:false 1:false 2:false end:true")]
    [TestCase("sync", "returnNonObject", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("async", "returnNonObject", "x:false caught:TypeError:false 1:false 2:false end:true")]
    [TestCase("sync", "returnReadThrows", "x:false caught:RG:false 1:false 2:false end:true")]
    [TestCase("async", "returnReadThrows", "x:false caught:RG:false 1:false 2:false end:true")]
    [TestCase("sync", "returnDoneThrows", "x:false caught:RD:false 1:false 2:false end:true")]
    [TestCase("async", "returnDoneThrows", "x:false caught:RD:false 1:false 2:false end:true")]
    public void AYieldStarEvaluatedAgainAfterItsDelegationEndedAbruptlyStartsANewOne(string kind, string way, string expected)
    {
        var engine = new Engine();
        engine.Evaluate(DelegateTwiceHarness + "\n" + $"delegateTwice('{kind}', '{way}')")
            .UnwrapIfPromise().AsString().Should().Be(expected);
    }

    /// <summary>
    /// A delegation that ended abruptly leaves nothing parked on the generator, even where the <c>yield*</c> never runs
    /// again: the body catches the throw and goes on to a plain <c>yield</c>, and the generator holds on to neither the
    /// delegate nor its <c>next</c>. Jint kept both for as long as the generator lived.
    /// </summary>
    [TestCase("sync", "nextThrows")]
    [TestCase("async", "nextThrows")]
    [TestCase("sync", "throwThrows")]
    [TestCase("async", "throwThrows")]
    [TestCase("sync", "noThrowReturnThrows")]
    [TestCase("async", "noThrowReturnReadThrows")]
    [TestCase("async", "noThrowReturnRejects")]
    [TestCase("async", "noThrowReturnObject")]
    [TestCase("sync", "returnThrows")]
    [TestCase("async", "returnReadThrows")]
    public void ADelegationEndedByACaughtThrowLeavesNothingParkedOnTheGenerator(string kind, string way)
    {
        var engine = new Engine();
        engine.Evaluate(Harness + "\n" + $$"""
            (async function () {
                const resumption = ways['{{way}}'][1];
                globalThis.gen = '{{kind}}' === 'sync'
                    ? (function* () { try { yield* delegateFor('{{kind}}', '{{way}}'); } catch (e) { yield 'caught:' + name(e); } yield 'after'; })()
                    : (async function* () { try { yield* delegateFor('{{kind}}', '{{way}}'); } catch (e) { yield 'caught:' + name(e); } yield 'after'; })();
                const out = [];
                let r = await gen.next();
                if (resumption !== 'none') { r = await gen[resumption]('X'); }
                out.push(r.value);
                out.push((await gen.next()).value);
                return out.join(' ');
            })()
            """).UnwrapIfPromise().AsString().Should().StartWith("caught:").And.EndWith(" after");

        ParkedDelegation(engine).Should().BeEmpty();
    }

    /// <summary>
    /// The delegate's <c>next</c>, <c>throw</c> or <c>return</c> fails with an exception no script can catch: the
    /// statement limit trips inside it, or a host function it calls throws. That exception is what leaves
    /// <c>Evaluate</c>, and the delegation it ended leaves nothing parked on the generator. The async generator's
    /// <c>return</c> rows are the controls: a forwarded <c>return()</c> that threw already forgot the delegation.
    /// </summary>
    [TestCase("sync", "next", "statementLimit")]
    [TestCase("sync", "next", "host")]
    [TestCase("sync", "throw", "statementLimit")]
    [TestCase("sync", "throw", "host")]
    [TestCase("sync", "return", "statementLimit")]
    [TestCase("sync", "return", "host")]
    [TestCase("async", "next", "statementLimit")]
    [TestCase("async", "next", "host")]
    [TestCase("async", "throw", "statementLimit")]
    [TestCase("async", "throw", "host")]
    [TestCase("async", "return", "statementLimit")]
    [TestCase("async", "return", "host")]
    public void AnUncatchableExceptionThatEndsADelegationLeavesNothingParkedOnTheGenerator(string kind, string method, string failure)
    {
        var engine = new Engine(options => options.LimitStatements(100_000));
        engine.SetValue("hostFail", new Action(() => throw new InvalidOperationException("host failed")));

        // The first step yields, unless `next` itself is the method that fails; the resumption reaches the others.
        engine.Evaluate($$"""
            const fail = '{{failure}}' === 'host' ? function () { hostFail(); } : function () { let n = 0; for (;;) { n++; } };
            let steps = 0;
            const it = {
                next() { if ('{{method}}' === 'next' && steps++ > 0) { fail(); } return { value: 'x', done: false }; },
                throw() { fail(); },
                return() { fail(); }
            };
            const operand = '{{kind}}' === 'sync' ? { [Symbol.iterator]() { return it; } } : { [Symbol.asyncIterator]() { return it; } };
            globalThis.gen = '{{kind}}' === 'sync' ? (function* () { yield* operand; })() : (async function* () { yield* operand; })();
            gen.next();
            """);

        var resumption = method == "next" ? "gen.next()" : $"gen.{method}('X')";
        var thrown = Caught.Exception(() => engine.Evaluate(resumption));

        if (failure == "host")
        {
            thrown.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("host failed");
        }
        else
        {
            thrown.Should().BeOfType<Jint.Runtime.StatementsCountOverflowException>();
        }

        ParkedDelegation(engine).Should().BeEmpty();
    }

    // What the generator `gen` still holds of a delegation: the names of the parked fields that are set.
    private static string ParkedDelegation(Engine engine)
    {
        var parked = new List<string>();
        switch (engine.GetValue("gen"))
        {
            case GeneratorInstance generator:
                Add(generator._delegatingIterator, "iterator");
                Add(generator._delegatingNextMethod, "next");
                Add(generator._delegatingYieldNode, "node");
                break;
            case AsyncGeneratorInstance asyncGenerator:
                Add(asyncGenerator._delegatingIterator, "iterator");
                Add(asyncGenerator._delegatingNextMethod, "next");
                Add(asyncGenerator._delegatingYieldNode, "node");
                break;
            default:
                throw new InvalidOperationException("gen is not a generator");
        }

        return string.Join(",", parked);

        void Add(object? field, string name)
        {
            if (field is not null)
            {
                parked.Add(name);
            }
        }
    }
}
