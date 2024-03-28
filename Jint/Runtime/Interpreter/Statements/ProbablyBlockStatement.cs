using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// Helper to remove virtual dispatch from block statements when it's most common target.
/// This is especially true for things like for statements body
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct ProbablyBlockStatement
{
    private readonly JintStatement? _statement = null;
    private readonly JintBlockStatement? _blockStatement = null;

    public ProbablyBlockStatement(Statement statement)
    {
        if (statement is NestedBlockStatement blockStatement)
        {
            _blockStatement = new JintBlockStatement(blockStatement);
        }
        else
        {
            _statement = JintStatement.Build(statement);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Completion Execute(EvaluationContext context)
    {
        if (_blockStatement is not null)
        {
            return _blockStatement.ExecuteBlock(context);
        }

        return _statement!.Execute(context);
    }
}
