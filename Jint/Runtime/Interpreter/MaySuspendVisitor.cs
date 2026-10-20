namespace Jint.Runtime.Interpreter;

/// <summary>
/// Decides at build time whether evaluating a node could suspend the generator or async frame it runs in.
/// </summary>
/// <remarks>
/// <para>
/// A <c>yield</c> or <c>await</c> anywhere inside the node counts, unless it sits in a nested function,
/// whose suspensions belong to that function's own frame. Class nodes are descended conservatively: a
/// computed member key evaluates in the enclosing frame.
/// </para>
/// <para>
/// The answer lets a handler keep its resume bookkeeping off the path that cannot suspend.
/// <c>JintObjectExpression</c> decides its shape-backed build with it, and <c>JintVariableDeclaration</c>
/// whether a declaration list has to remember the declarator it suspended at.
/// </para>
/// </remarks>
internal static class MaySuspendVisitor
{
    internal static bool MaySuspend(Node node)
    {
        if (node.Type is NodeType.YieldExpression or NodeType.AwaitExpression)
        {
            return true;
        }

        if (node.Type is NodeType.FunctionDeclaration or NodeType.FunctionExpression or NodeType.ArrowFunctionExpression)
        {
            return false;
        }

        foreach (var childNode in node.ChildNodes)
        {
            if (MaySuspend(childNode))
            {
                return true;
            }
        }

        return false;
    }
}
