#nullable enable

using Jint.Runtime.Interpreter;

namespace Jint.Tests.Runtime.Interpreter;

/// <summary>
/// <see cref="MaySuspendVisitor"/> is the build-time gate that keeps a handler's resume bookkeeping off
/// the path that cannot suspend, so what it counts and what it skips is pinned here on the shape the
/// declaration handler asks about: a whole declarator, binding pattern and initializer both.
/// </summary>
public class MaySuspendVisitorTests
{
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
