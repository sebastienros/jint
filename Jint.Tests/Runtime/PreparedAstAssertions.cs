using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Tests.Runtime;

/// <summary>
/// Assertions over what a prepared program carries on its AST. Shared because the same two questions come up from
/// opposite directions: a parse-only preparation has to leave the tree empty, and a program that has been run has
/// to have filled it in — the latter being what keeps the retention tests in
/// <see cref="GarbageCollectionTests"/> from passing on a tree nothing was ever published onto.
/// </summary>
internal static class PreparedAstAssertions
{
    /// <summary>
    /// Asserts no node carries anything at all, which is the whole of what a parse-only preparation promises.
    /// </summary>
    public static void ShouldCarryNothing(this Program program)
    {
        foreach (var node in Descendants(program))
        {
            node.UserData.Should().BeNull($"a parse-only preparation publishes nothing, but a {node.Type} carried something");
        }
    }

    /// <summary>
    /// Asserts every function and every nested block carries the interpreter state that belongs on it, and that
    /// there was something to check in the first place. Publication is either the analyzer's, at preparation time,
    /// or the first engine's, on the way through — the point of asserting it after the runs is that both routes
    /// end in the same shared place, and that the retention tests are looking at a populated tree.
    /// </summary>
    public static void ShouldCarryPublishedInterpreterState(this Program program)
    {
        var functions = 0;
        var blocks = 0;

        foreach (var node in Descendants(program))
        {
            switch (node)
            {
                case FunctionDeclaration or FunctionExpression or ArrowFunctionExpression:
                    node.UserData.Should().BeOfType<JintFunctionDefinition.State>($"the {node.Type} at {node.Location} was executed");
                    functions++;
                    break;

                case NestedBlockStatement:
                    node.UserData.Should().BeOfType<JintBlockStatement.BlockState>($"the block at {node.Location} was executed");
                    blocks++;
                    break;
            }
        }

        functions.Should().BeGreaterThan(0, "the program is supposed to contain functions whose state is shared");
        blocks.Should().BeGreaterThan(0, "the program is supposed to contain blocks whose state is shared");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.ChildNodes)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
