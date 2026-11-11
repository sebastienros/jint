#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the completion an IteratorClose is performed with, and what that completion lets a <c>return</c> do
/// (https://tc39.es/ecma262/#sec-iteratorclose). Step 5: under a throw completion the original error is the result,
/// whatever <c>return</c> does. Steps 6 and 7: under a normal or a return completion, a throw from <c>return</c>, or a
/// result that is not an object, is the result. Three closes took this wrong: a generator's <c>return()</c> resumption
/// at a <c>yield</c> in a <c>for</c>-<c>of</c> body inside a <c>try</c>, a parameter's array pattern, and a
/// <c>for</c>-<c>of</c> left after its iteration's <c>await using</c> dispose suspended. Every expected value is what
/// Node 26 answers; a <c>TypeError</c> is matched by its class alone, since its message is the engine's own. Each row
/// reports the outcome, then the iterator calls in the order they were made.
/// </summary>
public class IteratorCloseCompletionTests
{
    /// <summary>
    /// Iterables whose <c>return</c> throws (<c>throwing</c>) or answers a number (<c>nonObject</c>), a finite one,
    /// an async one whose <c>return</c> throws, and a resource whose async dispose settles a microtask later. All log
    /// <c>next</c> and <c>return</c>.
    /// </summary>
    private const string Harness = """
        const log = [];
        const iterable = (value, ret) => ({
            [Symbol.iterator]() {
                return {
                    next() { log.push('next'); return { value, done: false }; },
                    return() { log.push('return'); return ret(); },
                };
            },
        });
        const throwing = value => iterable(value, () => { throw new Error('from return'); });
        const nonObject = value => iterable(value, () => 1);
        const finite = (count, ret = () => ({}), at = i => i) => ({
            [Symbol.iterator]() {
                let i = 0;
                return {
                    next() { log.push('next'); return i < count ? { value: at(++i), done: false } : { value: undefined, done: true }; },
                    return() { log.push('return'); return ret(); },
                };
            },
        });
        const asyncThrowing = value => ({
            [Symbol.asyncIterator]() {
                return {
                    next() { log.push('next'); return Promise.resolve({ value, done: false }); },
                    return() { log.push('return'); throw new Error('from return'); },
                };
            },
        });
        const fail = () => { throw new Error('body'); };
        const describe = e => e instanceof TypeError ? 'TypeError' : e.constructor.name + ' ' + e.message;
        const outcome = f => { try { return 'ok ' + f(); } catch (e) { return describe(e); } };
        const settle = p => p.then(v => 'ok ' + v, describe);
        const res = { [Symbol.asyncDispose]() { log.push('dispose'); return Promise.resolve(); } };

        """;

    private static string Run(string expression)
    {
        var engine = new Engine();
        return engine.Evaluate(Harness + "(" + expression + ") + ' / ' + log.join(',')").AsString();
    }

    private static string RunAsync(string asyncBody)
    {
        var engine = new Engine();
        return engine.Evaluate(Harness + "settle((async () => { " + asyncBody + " })()).then(r => r + ' / ' + log.join(','))")
            .UnwrapIfPromise()
            .AsString();
    }

    /// <summary>
    /// The control that must not change: step 5 returns a throw completion whatever <c>return</c> does, so the body's,
    /// the pattern's or the mapper's own error is what the caller sees — for a <c>for</c>-<c>of</c> body, a destructuring
    /// declaration, a destructuring assignment (an initializer, a target and a rest target), <c>Array.from</c>'s mapper
    /// and a generator's <c>throw()</c> resumption. These passed before the fix.
    /// </summary>
    [Theory]
    [InlineData("outcome(() => { for (const x of throwing()) { fail(); } })", "Error body / next,return")]
    [InlineData("outcome(() => { const [a = fail()] = throwing(); })", "Error body / next,return")]
    [InlineData("outcome(() => { let a; [a = fail()] = throwing(); })", "Error body / next,return")]
    [InlineData("outcome(() => { [{}[fail()]] = throwing(); })", "Error body / return")]
    [InlineData("outcome(() => { [...{}[fail()]] = throwing(); })", "Error body / return")]
    [InlineData("outcome(() => Array.from(throwing(), () => fail()))", "Error body / next,return")]
    [InlineData("""outcome(() => { function* g() { try { for (const x of throwing()) { yield x; } } catch (e) { return 'caught ' + e.message; } } const gen = g(); gen.next(); return JSON.stringify(gen.throw(new Error('thrown'))); })""", """ok {"value":"caught thrown","done":true} / next,return""")]
    public void UnderAThrowCompletionTheOriginalErrorWins(string expression, string expected)
    {
        Run(expression).Should().Be(expected);
    }

    /// <summary>
    /// The same, in an async function: a <c>for await</c> body's throw over a sync iterable and over an async one whose
    /// <c>return</c> throws, and a <c>for</c>-<c>of</c> or <c>for await</c> left by a throw after its iteration's
    /// <c>await using</c> dispose has suspended — a dispose that rejects included, whose rejection is the throw. These
    /// passed before the fix; the dispose rows go through the close the fix rewrites.
    /// </summary>
    [Theory]
    [InlineData("for await (const x of throwing()) { fail(); }", "Error body / next,return")]
    [InlineData("for await (const x of asyncThrowing()) { fail(); }", "Error body / next,return")]
    [InlineData("for (await using r of throwing(res)) { fail(); }", "Error body / next,dispose,return")]
    [InlineData("for (await using r of throwing({ [Symbol.asyncDispose]() { log.push('dispose'); return Promise.reject(new Error('dispose')); } })) { break; }", "Error dispose / next,dispose,return")]
    [InlineData("for await (await using r of asyncThrowing(res)) { fail(); }", "Error body / next,dispose,return")]
    public void UnderAThrowCompletionTheOriginalErrorWinsInAnAsyncFunction(string asyncBody, string expected)
    {
        RunAsync(asyncBody).Should().Be(expected);
    }

    /// <summary>
    /// Spread performs no IteratorClose at all (https://tc39.es/ecma262/#sec-runtime-semantics-argumentlistevaluation,
    /// https://tc39.es/ecma262/#sec-runtime-semantics-arrayaccumulation): it runs the iterator out, and a step that
    /// throws propagates unclosed. Controls.
    /// </summary>
    [Theory]
    [InlineData("outcome(() => { const it = { [Symbol.iterator]() { return { next() { log.push('next'); throw new Error('from next'); }, return() { log.push('return'); return {}; } }; } }; return [...it]; })", "Error from next / next")]
    [InlineData("outcome(() => { const it = { [Symbol.iterator]() { let i = 0; return { next() { log.push('next'); if (i++) throw new Error('from next'); return { value: 1, done: false }; }, return() { log.push('return'); return {}; } }; } }; return Math.max(...it); })", "Error from next / next,next")]
    [InlineData("outcome(() => [...finite(2)].join())", "ok 1,2 / next,next,next")]
    public void ASpreadClosesNothing(string expression, string expected)
    {
        Run(expression).Should().Be(expected);
    }

    /// <summary>
    /// Steps 6 and 7 under a normal or a return completion: a <c>for</c>-<c>of</c> left by <c>break</c>,
    /// <c>return</c>, a labelled <c>break</c> or a labelled <c>continue</c>, a destructuring declaration and assignment,
    /// and <c>yield*</c> handed a <c>return()</c>, or a <c>throw()</c> its delegate has no method for (which closes the
    /// delegate with a normal completion, https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-evaluation
    /// step 7.b.iii.4). <c>Array.from</c> over an iterable that runs out closes nothing. Controls.
    /// </summary>
    [Theory]
    [InlineData("outcome(() => { for (const x of throwing()) { break; } })", "Error from return / next,return")]
    [InlineData("outcome(() => (function () { for (const x of throwing()) { return 'returned'; } })())", "Error from return / next,return")]
    [InlineData("outcome(() => { outer: for (const o of [0]) { for (const x of throwing()) { break outer; } } })", "Error from return / next,return")]
    [InlineData("outcome(() => { outer: for (const o of [0]) { for (const x of throwing()) { continue outer; } } })", "Error from return / next,return")]
    [InlineData("outcome(() => { for (const x of nonObject()) { break; } })", "TypeError / next,return")]
    [InlineData("outcome(() => { const [a] = throwing(); })", "Error from return / next,return")]
    [InlineData("outcome(() => { let a; [a] = throwing(); })", "Error from return / next,return")]
    [InlineData("outcome(() => { const [a] = nonObject(); })", "TypeError / next,return")]
    [InlineData("outcome(() => { function* g() { yield* throwing(); } const gen = g(); gen.next(); return JSON.stringify(gen.return('r')); })", "Error from return / next,return")]
    [InlineData("outcome(() => { function* g() { yield* throwing(); } const gen = g(); gen.next(); return JSON.stringify(gen.throw(new Error('thrown'))); })", "Error from return / next,return")]
    [InlineData("outcome(() => Array.from(finite(2)).join())", "ok 1,2 / next,next,next")]
    public void UnderANormalOrAReturnCompletionWhatReturnThrowsPropagates(string expression, string expected)
    {
        Run(expression).Should().Be(expected);
    }

    /// <summary>
    /// A generator's <c>return()</c> resumption at a <c>yield</c> in a <c>for</c>-<c>of</c> body completes the body
    /// with a return completion (https://tc39.es/ecma262/#sec-generatorresumeabrupt), so the loop closes its iterator
    /// with it (https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset
    /// step 8.m), and a <c>return</c> that throws — or answers a number — replaces it: the <c>catch</c> or
    /// <c>finally</c> around the loop sees that throw, and the generator runs on from there. Jint closed the iterator
    /// while the resumption's return request was still set, and the statement around the loop then turned the throw
    /// back into the return: <c>gen.return('r')</c> answered <c>{ value: 'r', done: true }</c> and no <c>catch</c> ran.
    /// The first ten rows failed so. The last three are controls: a <c>yield</c> inside a <c>try</c>-<c>finally</c> of
    /// its own (whose <c>finally</c> already drops the request), the loop outside any <c>try</c>, and an iterator whose
    /// <c>return</c> succeeds. The row whose <c>yield</c> is in the loop's binding pattern leaves <c>next</c> unlogged:
    /// resuming inside that pattern steps the iterator again on this branch, which is not what the row is about.
    /// </summary>
    [Theory]
    [InlineData("try { for (const x of throwing()) { yield x; } } catch (e) { return 'caught ' + e.message; }", """ok {"value":"caught from return","done":true} / next,return""")]
    [InlineData("try { for (const x of throwing()) yield x; } catch (e) { return 'caught ' + e.message; }", """ok {"value":"caught from return","done":true} / next,return""")]
    [InlineData("try { log.push('try'); for (const x of throwing()) { yield x; } } catch (e) { return 'caught ' + e.message; }", """ok {"value":"caught from return","done":true} / try,next,return""")]
    [InlineData("try { for (const x of throwing()) { yield x; } } finally { log.push('finally'); }", "Error from return / next,return,finally")]
    [InlineData("try { for (const x of throwing()) { yield x; } } catch (e) { log.push('caught'); } return 'after';", """ok {"value":"after","done":true} / next,return,caught""")]
    [InlineData("try { for (const x of throwing()) { yield x; } } catch (e) { yield 'caught ' + e.message; }", """ok {"value":"caught from return","done":false} / next,return""")]
    [InlineData("try { for (const o of [0]) for (const x of throwing()) { yield x; } } catch (e) { return 'caught ' + e.message; }", """ok {"value":"caught from return","done":true} / next,return""")]
    [InlineData("try { switch (0) { case 0: for (const x of throwing()) { yield x; } } } catch (e) { return 'caught ' + e.message; }", """ok {"value":"caught from return","done":true} / next,return""")]
    [InlineData("try { for (const x of nonObject()) { yield x; } } catch (e) { return 'caught ' + e.constructor.name; }", """ok {"value":"caught TypeError","done":true} / next,return""")]
    [InlineData("let a; const it = { [Symbol.iterator]() { return { next() { return { value: [undefined], done: false }; }, return() { log.push('return'); throw new Error('from return'); } }; } }; try { for ([a = yield 'lhs'] of it) { } } catch (e) { return 'caught ' + e.message; }", """ok {"value":"caught from return","done":true} / return""")]
    [InlineData("try { for (const x of throwing()) { try { yield x; } finally { log.push('inner finally'); } } } catch (e) { return 'caught ' + e.message; }", """ok {"value":"caught from return","done":true} / next,inner finally,return""")]
    [InlineData("for (const x of throwing()) { yield x; }", "Error from return / next,return")]
    [InlineData("try { for (const x of finite(3)) { yield x; } } catch (e) { return 'caught ' + e.message; }", """ok {"value":"r","done":true} / next,return""")]
    public void AThrowFromTheLoopsReturnReplacesAGeneratorsReturnResumption(string generatorBody, string expected)
    {
        Run("outcome(() => { function* g() { " + generatorBody + " } const gen = g(); gen.next(); return JSON.stringify(gen.return('r')); })")
            .Should().Be(expected);
    }

    /// <summary>
    /// The same in an async generator, side by side: the first row failed the same way; the braceless body under a
    /// <c>finally</c> and a <c>throw()</c> resumption (where the original error wins) are controls.
    /// </summary>
    [Theory]
    [InlineData("try { for (const x of throwing()) { yield x; } } catch (e) { return 'caught ' + e.message; }", "return('r')", """ok {"value":"caught from return","done":true} / next,return""")]
    [InlineData("try { for (const x of throwing()) yield x; } finally { log.push('finally'); }", "return('r')", "Error from return / next,return,finally")]
    [InlineData("try { for (const x of throwing()) { yield x; } } catch (e) { return 'caught ' + e.message; }", "throw(new Error('thrown'))", """ok {"value":"caught thrown","done":true} / next,return""")]
    public void AThrowFromTheLoopsReturnReplacesAnAsyncGeneratorsReturnResumption(string generatorBody, string request, string expected)
    {
        RunAsync("async function* g() { " + generatorBody + " } const gen = g(); await gen.next(); return JSON.stringify(await gen." + request + ");")
            .Should().Be(expected);
    }

    /// <summary>
    /// A parameter's array pattern over an iterator is IteratorBindingInitialization
    /// (https://tc39.es/ecma262/#sec-runtime-semantics-iteratorbindinginitialization): each element steps the iterator
    /// and then runs its initializer or nested pattern, and only once the elements are done does BindingInitialization
    /// close an iterator that is not done, with their completion
    /// (https://tc39.es/ecma262/#sec-runtime-semantics-bindinginitialization step 4). An initializer that throws is a
    /// throw completion, so its error wins over what <c>return</c> does. Jint stepped every element first, closed with
    /// a normal completion, and only then ran the initializers: <c>return</c>'s error (or the <c>TypeError</c> for its
    /// number) replaced the initializer's, an initializer ran after the close, an element after a throwing one was
    /// stepped, an iterator the elements had run out was closed, and an empty pattern closed nothing. The first fourteen
    /// rows failed; the rest (an iterator the pattern leaves open with a normal completion, a parameter default, and an
    /// array, which is read by index) are controls.
    /// </summary>
    [Theory]
    [InlineData("outcome(() => (function ([a = fail()]) {})(throwing()))", "Error body / next,return")]
    [InlineData("outcome(() => (function ([a = fail()]) {})(nonObject()))", "Error body / next,return")]
    [InlineData("outcome(() => (([a = fail()]) => {})(throwing()))", "Error body / next,return")]
    [InlineData("outcome(() => ({ m([a = fail()]) {} }).m(throwing()))", "Error body / next,return")]
    [InlineData("outcome(() => (function ([[a] = fail()]) {})(throwing()))", "Error body / next,return")]
    [InlineData("outcome(() => (function ([a, b = fail()]) {})(throwing()))", "Error body / next,next,return")]
    [InlineData("outcome(() => (function ([a = fail(), b]) {})(throwing()))", "Error body / next,return")]
    [InlineData("outcome(() => (function ([, a = fail()]) {})(throwing()))", "Error body / next,next,return")]
    [InlineData("outcome(() => { function* g([a = fail()]) {} g(throwing()); })", "Error body / next,return")]
    [InlineData("outcome(() => (function ([a = log.push('init')]) {})(throwing()))", "Error from return / next,init,return")]
    [InlineData("outcome(() => (function ([a, b]) { return a + ',' + b; })(finite(1)))", "ok 1,undefined / next,next")]
    [InlineData("outcome(() => (function ([]) {})(finite(1)))", "ok undefined / return")]
    [InlineData("outcome(() => (function ([a, ...r]) { return a + ',' + r; })(finite(3)))", "ok 1,2,3 / next,next,next,next")]
    [InlineData("outcome(() => (function ([...[a = fail()]]) {})(finite(1, () => ({}), () => undefined)))", "Error body / next,next")]
    [InlineData("outcome(() => (function ([a]) {})(throwing()))", "Error from return / next,return")]
    [InlineData("outcome(() => (function ([a]) {})(nonObject()))", "TypeError / next,return")]
    [InlineData("outcome(() => (function ([a, b]) { return a + ',' + b; })(finite(3)))", "ok 1,2 / next,next,return")]
    [InlineData("outcome(() => (function ([a] = throwing()) {})())", "Error from return / next,return")]
    [InlineData("outcome(() => (function ([a = fail()]) {})([undefined]))", "Error body / ")]
    public void AParameterPatternClosesWithTheCompletionItsElementsLeaveWith(string expression, string expected)
    {
        Run(expression).Should().Be(expected);
    }

    /// <summary>
    /// The same for an async function's and an async arrow's parameters, whose throw rejects the call's promise: the
    /// first two rows failed, the last is the control.
    /// </summary>
    [Theory]
    [InlineData("return await (async function ([a = fail()]) {})(throwing());", "Error body / next,return")]
    [InlineData("return await (async ([a = fail()]) => {})(throwing());", "Error body / next,return")]
    [InlineData("return await (async function ([a]) {})(throwing());", "Error from return / next,return")]
    public void AnAsyncParameterPatternClosesWithTheCompletionItsElementsLeaveWith(string asyncBody, string expected)
    {
        RunAsync(asyncBody).Should().Be(expected);
    }

    /// <summary>
    /// A <c>for</c>-<c>of</c> whose head declares <c>await using</c> awaits each iteration's dispose
    /// (https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset
    /// step 8.k), and then closes the iterator with the completion the iteration left with (step 8.m) — a
    /// <c>break</c>, a <c>return</c>, a labelled <c>break</c> or a labelled <c>continue</c>, none of them a throw, so a
    /// <c>return</c> that throws or answers a number is the loop's result. In an async function the loop resumes
    /// after that await on a path of its own, which swallowed both: the first six rows completed normally, a
    /// labelled <c>continue</c> went on with this loop's next iteration rather than leaving it (so the iterator was
    /// never closed, and an endless one never ended), and a <c>catch</c> around the loop saw nothing. These eight
    /// rows failed. The rest are controls: a body that awaits before it leaves, a sync <c>using</c>, an async
    /// generator, and a <c>for await</c>, which closes asynchronously.
    /// </summary>
    [Theory]
    [InlineData("for (await using r of throwing(res)) { break; } return 'completed';", "Error from return / next,dispose,return")]
    [InlineData("for (await using r of throwing(res)) { return 'returned'; }", "Error from return / next,dispose,return")]
    [InlineData("outer: for (const o of [0]) { for (await using r of throwing(res)) { break outer; } } return 'completed';", "Error from return / next,dispose,return")]
    [InlineData("for (await using r of nonObject(res)) { break; } return 'completed';", "TypeError / next,dispose,return")]
    [InlineData("for (await using r of nonObject(res)) { return 'returned'; }", "TypeError / next,dispose,return")]
    [InlineData("try { for (await using r of throwing(res)) { break; } } catch (e) { return 'caught ' + e.message; } return 'completed';", "ok caught from return / next,dispose,return")]
    [InlineData("let n = 0; outer: for (const o of [0, 1]) { for (await using r of finite(3, () => { throw new Error('from return'); }, () => res)) { n++; continue outer; } } return 'completed ' + n;", "Error from return / next,dispose,return")]
    [InlineData("let n = 0; outer: for (const o of [0, 1]) { for (await using r of finite(3, () => ({}), () => res)) { n++; continue outer; } } return 'completed ' + n;", "ok completed 2 / next,dispose,return,next,dispose,return")]
    [InlineData("for (await using r of throwing(res)) { await null; break; } return 'completed';", "Error from return / next,dispose,return")]
    [InlineData("for (using r of throwing({ [Symbol.dispose]() { log.push('dispose'); } })) { break; } return 'completed';", "Error from return / next,dispose,return")]
    [InlineData("async function* g() { for (await using r of throwing(res)) { break; } return 'completed'; } return JSON.stringify(await g().next());", "Error from return / next,dispose,return")]
    [InlineData("for await (await using r of asyncThrowing(res)) { break; } return 'completed';", "Error from return / next,dispose,return")]
    [InlineData("for await (const x of asyncThrowing()) { break; } return 'completed';", "Error from return / next,return")]
    public void AForOfLeftAfterASuspendedDisposeClosesWithTheCompletionItLeftWith(string asyncBody, string expected)
    {
        RunAsync(asyncBody).Should().Be(expected);
    }
}
