#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins where an IteratorClose sits among the steps around it, for two closes that had it in the wrong place: each
/// iterator <c>Iterator.zip</c> collected is closed once when its <c>padding</c> fails to be an iterable, and a
/// <c>for</c>-<c>of</c> left early has its scope gone before the iterator's <c>return</c> runs. Of the rows below, the
/// two zip paddings that are not iterable and the two generator resumptions failed before the fix; the rest are controls.
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
