#nullable enable

using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Tests.Runtime.Interpreter;

/// <summary>
/// <see cref="MaySuspendVisitor"/> is the build-time gate that keeps a handler's resume bookkeeping off
/// the path that cannot suspend, so what it counts and what it skips is pinned here on the shapes the
/// handlers ask about: a whole declarator, binding pattern and initializer both, and a call's argument list.
/// </summary>
public class MaySuspendVisitorTests
{
    /// <summary>
    /// Only the arguments decide: a callee that suspends resumes through its own handler, so a call whose
    /// arguments cannot suspend keeps every fast lane.
    /// </summary>
    [TestCase("async function m() { f(await x); }", true)]
    [TestCase("function* g() { o.m(1).m(yield 0); }", true)]
    [TestCase("async function m() { f(g(h(), await x)); }", true)]
    [TestCase("async function m() { f(...(await x)); }", true)]
    [TestCase("async function m() { f(...[await x]); }", true)]
    [TestCase("async function m() { f(class { [await k]() {} }); }", true)]
    [TestCase("async function m() { (await o).m(1); }", false)]
    [TestCase("async function m() { o[await k](1); }", false)]
    [TestCase("async function m() { f(1, x, o.p, g()); }", false)]
    [TestCase("async function m() { f(async () => await x); }", false)]
    [TestCase("function* g() { f(function* () { yield 1; }); }", false)]
    [TestCase("async function m() { f(); }", false)]
    public void DecidesWhetherACallsArgumentsCanSuspendTheEnclosingFrame(string code, bool maySuspend)
    {
        var call = Find<CallExpression>(new Parser().ParseScript(code));

        JintCallExpression.ArgumentsMaySuspend(call.Arguments.AsSpan()).Should().Be(maySuspend);
    }

    [TestCase("async function m() { new C(1, await x); }", true)]
    [TestCase("function* g() { new C(...(yield 0)); }", true)]
    [TestCase("async function m() { new (await C)(1); }", false)]
    [TestCase("async function m() { new C(1, async () => await x); }", false)]
    [TestCase("async function m() { new C; }", false)]
    public void BuildsTheSuspendableNewHandlerOnlyForArgumentsThatCanSuspend(string code, bool suspendable)
    {
        var expression = Find<NewExpression>(new Parser().ParseScript(code));

        JintNewExpression.Build(expression).Should().BeOfType(suspendable ? typeof(JintSuspendableNewExpression) : typeof(JintNewExpression));
    }

    [TestCase("async function m() { let a = await f(); }", true)]
    [TestCase("function* g() { let a = yield 1; }", true)]
    [TestCase("async function m() { let a = (side(), await f()); }", true)]
    [TestCase("async function m() { let a = c ? await f() : 1; }", true)]
    [TestCase("async function m() { let { x = await f() } = o; }", true)]
    [TestCase("async function m() { let [x = await f()] = o; }", true)]
    [TestCase("async function m() { let { [await k]: x } = o; }", true)]
    [TestCase("function* g() { let { x = yield 1 } = o; }", true)]
    [TestCase("async function m() { let a = class { [await k]() {} }; }", true)]
    [TestCase("async function m() { let a = side() + 1; }", false)]
    [TestCase("async function m() { let a; }", false)]
    [TestCase("async function m() { let { x = 1 } = o; }", false)]
    [TestCase("async function m() { let a = async () => await f(); }", false)]
    [TestCase("async function m() { let a = async function () { await f(); }; }", false)]
    [TestCase("function* g() { let a = function* () { yield 1; }; }", false)]
    [TestCase("async function m() { let a = class { m() { return 1; } }; }", false)]
    public void DecidesWhetherADeclaratorCanSuspendTheEnclosingFrame(string code, bool maySuspend)
    {
        var declarator = FirstDeclarator(new Parser().ParseScript(code));

        MaySuspendVisitor.MaySuspend(declarator).Should().Be(maySuspend);
    }

    private static VariableDeclarator FirstDeclarator(Node node)
    {
        return FindDeclarator(node) ?? throw new InvalidOperationException("The snippet declares nothing.");
    }

    /// <summary>
    /// The first <typeparamref name="T"/> in document order, which for the snippets above is the outermost one.
    /// </summary>
    private static T Find<T>(Node node) where T : Node
    {
        return TryFind<T>(node) ?? throw new InvalidOperationException($"The snippet holds no {typeof(T).Name}.");
    }

    private static T? TryFind<T>(Node node) where T : Node
    {
        if (node is T match)
        {
            return match;
        }

        foreach (var child in node.ChildNodes)
        {
            var found = TryFind<T>(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static VariableDeclarator? FindDeclarator(Node node)
    {
        if (node is VariableDeclaration declaration)
        {
            return declaration.Declarations[0];
        }

        foreach (var child in node.ChildNodes)
        {
            var found = FindDeclarator(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
