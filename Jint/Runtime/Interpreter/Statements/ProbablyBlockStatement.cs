using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Esprima.Ast;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// Helper to remove virtual dispatch from block statements when it's most common target.
/// This is especially true for things like for statements body
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly struct ProbablyBlockStatement
{
    private readonly JintStatement  _target;

    public ProbablyBlockStatement(Statement statement)
    {
        if (statement is BlockStatement blockStatement)
        {
            _target = new JintBlockStatement(blockStatement);
        }
        else
        {
            _target = JintStatement.Build(statement);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Completion Execute(EvaluationContext context)
    {
        if (_target is JintBlockStatement blockStatement)
        {
            return blockStatement.ExecuteBlock(context);
        }

        return _target.Execute(context);
    }
}
