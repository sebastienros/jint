#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins where an IteratorClose sits among the steps around it, for closes that had it in the wrong place: each iterator
/// <c>Iterator.zip</c> collected is closed once when its <c>padding</c> fails to be an iterable, a zip closing its
/// members after a failed step is still executing while their <c>return</c> runs, and a <c>for</c>-<c>of</c> left early
/// has its scope gone before the iterator's <c>return</c> runs. Of the rows below, the two zip paddings that are not
/// iterable, the five re-entrant calls made while a failed step closes the members, and the two generator resumptions
/// failed before the fix; the rest are controls.
/// </summary>
public class IteratorCloseOrderTests
{
    /// <summary>Iterables that log their <c>return</c>, for the zip rows.</summary>
    private const string ZipHarness = """
        const log = [];
        const mk = n => ({
            [Symbol.iterator]() {
                return { next() { return { value: n, done: false }; }, return() { log.push('return ' + n); return {}; } };
            },
        });
        let error = 'nothing';
        """;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.zip step 14.b: GetIterator(paddingOption, sync), and
    /// IfAbruptCloseIterators(paddingIter, iters) for each way it fails, which closes every collected iterator once, in
    /// reverse (https://tc39.es/ecma262/#sec-ifabruptcloseiterators). A padding with no <c>Symbol.iterator</c>, or whose
    /// <c>Symbol.iterator</c> answers a primitive, closed each one twice; one whose <c>Symbol.iterator</c> throws did not,
    /// and is the control.
    /// </summary>
    [TestCase("{}", "return 2, return 1 / TypeError")]
    [TestCase("{ [Symbol.iterator]() { return 1; } }", "return 2, return 1 / TypeError")]
    [TestCase("{ [Symbol.iterator]() { throw new RangeError('padding'); } }", "return 2, return 1 / RangeError")]
    public void ZipClosesEachIteratorOnceWhenItsPaddingIsNotAnIterable(string padding, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(ZipHarness + $$"""
            try { Iterator.zip([mk(1), mk(2)], { mode: 'longest', padding: {{padding}} }); }
            catch (e) { error = e.constructor.name; }
            log.join(', ') + ' / ' + error
            """).Should().Be(expected);
    }

    /// <summary>
    /// Members whose <c>return</c> logs, and one (<c>reenter</c>) whose <c>return</c> calls the zip's own
    /// <c>return()</c> or <c>next()</c> while the zip is closing its members, for the re-entrancy rows.
    /// </summary>
    private const string ReentrantZipHarness = """
        const log = [];
        let z;
        const mk = (name, reenter) => ({
            [Symbol.iterator]() {
                return {
                    next() { return { value: name, done: false }; },
                    return() {
                        log.push('return ' + name);
                        if (reenter) {
                            try { z[reenter](); log.push('re-entered'); } catch (e) { log.push('re-entry ' + e.constructor.name); }
                        }
                        return {};
                    },
                };
            },
        });
        const failing = { [Symbol.iterator]() { return { next() { throw new Error('step'); } }; } };
        const finished = { [Symbol.iterator]() { return { next() { return { value: 0, done: true }; } }; } };
        let error = 'nothing';
        """;

    /// <summary>
    /// The zip closes its open members when a step fails as IfAbruptCloseIterators inside its own closure
    /// (https://tc39.es/ecma262/#sec-iteratorzip step 3), so the generator is executing while they run, and a member's
    /// <c>return</c> that calls the zip's <c>return()</c> or <c>next()</c> gets GeneratorValidate's <c>TypeError</c>
    /// (https://tc39.es/ecma262/#sec-generatorvalidate step 6); the closes go on, each member closed once, and the
    /// step's own error is what the caller sees. test262's <c>zip/suspended-yield-iterator-close-calls-return.js</c> pins
    /// the same state for a close a <c>return()</c> resumes. Jint marked the zip completed before these closes: the
    /// re-entrant <c>return()</c> answered done after closing the remaining members itself, and the loop it emptied then
    /// raised a CLR <c>ArgumentOutOfRangeException</c> in place of the step's error. The last two rows close inside the
    /// step instead (a member that ran out, and a strict length mismatch), and are controls.
    /// </summary>
    [TestCase("[mk('A'), mk('B'), failing, mk('C', 'return')]", "", "return C, re-entry TypeError, return B, return A / Error")]
    [TestCase("[mk('A'), mk('B'), failing, mk('C', 'return')]", ", { mode: 'longest' }", "return C, re-entry TypeError, return B, return A / Error")]
    [TestCase("[mk('A'), mk('B'), failing, mk('C', 'return')]", ", { mode: 'strict' }", "return C, re-entry TypeError, return B, return A / Error")]
    [TestCase("[mk('A'), mk('B'), failing, mk('C', 'next')]", "", "return C, re-entry TypeError, return B, return A / Error")]
    [TestCase("[mk('A'), mk('B'), failing, mk('C', 'next')]", ", { mode: 'strict' }", "return C, re-entry TypeError, return B, return A / Error")]
    [TestCase("[finished, mk('B'), mk('C', 'return')]", "", "return C, re-entry TypeError, return B / nothing")]
    [TestCase("[mk('A'), mk('B', 'return'), finished]", ", { mode: 'strict' }", "return B, re-entry TypeError, return A / TypeError")]
    public void AReturnThatReentersTheZipWhileItClosesItsMembersMeetsAnExecutingGenerator(string members, string options, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(ReentrantZipHarness + $$"""
            z = Iterator.zip({{members}}{{options}});
            try { z.next(); } catch (e) { error = e.constructor.name; }
            log.join(', ') + ' / ' + error
            """).Should().Be(expected);
    }

    /// <summary>An iterable whose <c>return</c> throws, for the for-of rows.</summary>
    private const string ForOfHarness = """
        const it = {
            [Symbol.iterator]() {
                return { next() { return { value: 1, done: false }; }, return() { throw new Error('from return'); } };
            },
        };
        """;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-forin-div-ofbodyevaluation-lhs-stmt-iterator-lhskind-labelset step
    /// 8.l restores the running execution context's LexicalEnvironment, after 8.k has disposed the iteration's resources
    /// and before 8.m closes the iterator. So a <c>return</c> that throws under a <c>break</c>, a <c>return</c> or a
    /// labelled <c>continue</c> leaves the loop's own scope behind: nothing the script runs next sees its binding. These
    /// rows passed before the generator rows below were fixed too, because a completion the loop's body returns was always
    /// restored from before the close; they are the controls that the fix keeps it that way, and every expected value is
    /// what Node 26 answers.
    /// </summary>
    [TestCase("let seen; try { for (let x of it) { break; } } catch (e) { seen = typeof x; } seen", "undefined")]
    [TestCase("try { for (let x of it) { break; } } catch (e) { } typeof x", "undefined")]
    [TestCase("function f() { try { for (let x of it) { return 'returned'; } } catch (e) { return typeof x; } } f()", "undefined")]
    [TestCase("let g; try { for (let x of it) { break; } } catch (e) { g = () => typeof x; } g()", "undefined")]
    [TestCase("let y = 'outer'; try { for (let y of it) { break; } } catch (e) { } y", "outer")]
    [TestCase("const x = 'outer'; let seen; try { for (const x of it) { break; } } catch (e) { seen = x; } seen", "outer")]
    [TestCase("(function* () { try { for (let x of it) { break; } } catch (e) { return typeof x; } })().next().value", "undefined")]
    [TestCase("outer: for (const o of [0]) { try { for (let x of it) { continue outer; } } catch (e) { var seen = typeof x; } } seen", "undefined")]
    public void AThrowingReturnUnderABreakOrAReturnLeavesTheLoopsScopeBehind(string script, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(ForOfHarness + script).Should().Be(expected);
    }

    /// <summary>
    /// A generator's <c>return()</c> resumption at a <c>yield</c> that is the loop's whole body arrives as an exception
    /// rather than as the body's completion, and closes the iterator with a return completion. A <c>return</c> that throws
    /// then replaces it, and the <c>catch</c> around the loop sees the scope outside it, as Node 26 does. These are the
    /// rows that failed: Jint restored the scope after the close on this path, and a throw out of the close skipped the
    /// restore, so the <c>catch</c> saw the loop's binding (<c>'number'</c>, and <c>1</c> for the shadowed one).
    /// </summary>
    [TestCase("function* g() { try { for (let x of it) yield x; } catch (e) { return typeof x; } }", "undefined")]
    [TestCase("function* g() { let y = 'outer'; try { for (let y of it) yield y; } catch (e) { return y; } }", "outer")]
    public void AThrowingReturnUnderAGeneratorsReturnResumptionLeavesTheLoopsScopeBehind(string generator, string expected)
    {
        var engine = new Engine();

        engine.Evaluate(ForOfHarness + generator + " const gen = g(); gen.next(); gen.return('r').value")
            .Should().Be(expected);
    }

    /// <summary>
    /// The same, from an async function: the loop's scope is gone before a throwing <c>return</c> runs.
    /// </summary>
    [Test]
    public void AThrowingReturnInAnAsyncFunctionLeavesTheLoopsScopeBehind()
    {
        var engine = new Engine();

        engine.Evaluate(ForOfHarness + """
            (async () => { try { for (let x of it) { break; } } catch (e) { return typeof x; } })()
            """).UnwrapIfPromise().Should().Be("undefined");
    }
}
