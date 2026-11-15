#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// A thrown primitive keeps the call stack built where the throw first left a function body, as an object or an
/// error does, rather than having it rebuilt at every body the throw leaves (issue #4461). The rebuild rendered
/// every frame still on the stack at each boundary, so a primitive thrown through d frames cost O(d^2), and the
/// host was handed the stack of the outermost boundary only.
/// </summary>
public class PrimitiveThrowCallStackTests
{
    private const string Functions = """
        function inner(v) { throw v; }
        function middle(v) { return inner(v); }
        function outer(v) { return middle(v); }
        """;

    private static string StackOf(Engine engine, string call)
    {
        var exception = Caught.Exception(() => engine.Execute(call)).Should().BeOfType<JavaScriptException>().Subject;
        return exception.JavaScriptStackTrace!;
    }

    [TestCase("1")]
    [TestCase("'boom'")]
    [TestCase("true")]
    [TestCase("undefined")]
    [TestCase("null")]
    [TestCase("1n")]
    [TestCase("Symbol('s')")]
    public void APrimitiveReportsTheFramesAnObjectReports(string thrown)
    {
        var engine = new Engine();
        engine.Execute(Functions);

        var objectStack = StackOf(engine, "outer({ a: 1 })");
        var primitiveStack = StackOf(engine, $"outer({thrown})");

        primitiveStack.Should().Be(objectStack);
        primitiveStack.Should().Contain("inner").And.Contain("middle").And.Contain("outer");
    }

    /// <summary>
    /// A <c>finally</c> that throws and catches a primitive of its own while the first throw is on its way out
    /// does not change the stack the first one carries, even when the two values are the same instance.
    /// </summary>
    [Test]
    public void AThrowCaughtInsideAFinallyDoesNotReplaceTheCarriedStack()
    {
        var engine = new Engine();
        engine.Execute("""
            function other() { throw 1; }
            function inner() { try { throw 1; } finally { try { other(); } catch (e) { } } }
            function outer() { return inner(); }
            """);

        var stack = StackOf(engine, "outer()");

        stack.Should().Contain("inner").And.Contain("outer");
        stack.Should().NotContain("other");
    }

    /// <summary>
    /// Catching a primitive and throwing it again is a new throw, which gets the stack of where it was thrown
    /// again.
    /// </summary>
    [Test]
    public void ARethrownPrimitiveGetsTheStackOfTheRethrow()
    {
        var engine = new Engine();
        engine.Execute("""
            function inner() { throw 'boom'; }
            function middle() { return inner(); }
            function catcher() { try { middle(); } catch (e) { rethrow(e); } }
            function rethrow(e) { throw e; }
            """);

        var stack = StackOf(engine, "catcher()");

        stack.Should().Contain("rethrow").And.Contain("catcher");
        stack.Should().NotContain("inner").And.NotContain("middle");
    }

    /// <summary>
    /// Deep enough that rebuilding the stack at every boundary would be the slow part; the outcome is checked,
    /// not the time, so the test stays deterministic.
    /// </summary>
    [TestCase("1")]
    [TestCase("'boom'")]
    public void ADeepPrimitiveThrowIsCaughtInScript(string thrown)
    {
        var result = false;
        DedicatedThread.Run(() =>
        {
            var engine = new Engine(o => o.Constraints.MaxRecursionDepth = -1);
            engine.Execute($"function f(n) {{ if (n === 0) throw {thrown}; return f(n - 1); }}");
            result = engine.Evaluate($"var caught; try {{ f(2000); }} catch (e) {{ caught = e; }} caught === {thrown}").AsBoolean();
        }, maxStackSize: 256 * 1024 * 1024);

        result.Should().BeTrue();
    }
}
