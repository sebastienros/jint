using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// A call through a bare name takes its callee from the identifier's read path instead of a Reference
/// until a <c>with</c> statement has run. Each script runs on a fresh engine and on one that has already run
/// a <c>with</c> statement, which keeps every call on the Reference path, and the two must agree — results
/// and error messages alike.
/// </summary>
public class IdentifierCalleeLaneTests
{
    private static string Run(string script, bool referencePath)
    {
        var engine = new Engine();
        if (referencePath)
        {
            engine.Execute("with ({}) {}");
        }

        try
        {
            return engine.Evaluate(script).ToString();
        }
        catch (JavaScriptException e)
        {
            return "threw " + e.Error.Get("name") + ": " + e.Message;
        }
    }

    [TestCase("function f() { 'use strict'; return this; } String(f())")]
    [TestCase("function f() { return this === globalThis; } f()")]
    [TestCase("(function () { const f = () => 1; return f() + f(); })()")]
    [TestCase("(function () { function outer(x) { const inner = y => x + y; return inner(1); } return outer(2); })()")]
    [TestCase("var x = 1; x()")]
    [TestCase("var o = {}; o()")]
    [TestCase("missing()")]
    [TestCase("(function () { f(); let f = () => 1; })()")]
    [TestCase("(function () { const f = 1; return f(); })()")]
    [TestCase("var s = ''; var x = 1; try { x(s += 'a', s += 'b'); } catch (e) { s += e.constructor.name; } s")]
    [TestCase("var n = 0; Object.defineProperty(globalThis, 'g', { get() { n++; return 1; }, configurable: true }); try { g(); } catch (e) {} n")]
    [TestCase("var n = 0; Object.defineProperty(globalThis, 'h', { get() { n++; return () => n; }, configurable: true }); h() + h()")]
    [TestCase("var f; String(f?.())")]
    [TestCase("var f = () => 2; f?.()")]
    [TestCase("var f = null; var s = ''; f?.(s += 'evaluated'); s")]
    [TestCase("function fact(n) { return n <= 1 ? 1 : n * fact(n - 1); } fact(10)")]
    public void LaneAgreesWithTheReferencePath(string script)
    {
        Run(script, referencePath: false).Should().Be(Run(script, referencePath: true));
    }

    [Test]
    public void WithObjectIsTheThisValueOnceAWithStatementHasRun()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function outer() { return 'outer'; }
            const before = outer();
            const o = { outer() { return this === o ? 'with' : 'other'; } };
            let inside;
            with (o) { inside = outer(); }
            before + ',' + inside + ',' + outer();
            """).AsString();

        result.Should().Be("outer,with,outer");
    }

    [Test]
    public void ClosureCreatedInsideWithSeesTheWithObjectWhenCalledLater()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            function f() { return 'global'; }
            const o = { f() { return 'with'; } };
            let g;
            with (o) { g = function () { return f(); }; }
            g();
            """).AsString();

        result.Should().Be("with");
    }
}
