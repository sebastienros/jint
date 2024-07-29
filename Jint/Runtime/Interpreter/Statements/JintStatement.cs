using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal abstract class JintStatement<T> : JintStatement where T : Statement
{
    internal new readonly T _statement;

    protected JintStatement(T statement) : base(statement)
    {
        _statement = statement;
    }
}

internal abstract class JintStatement
{
    internal readonly Statement _statement;
    private bool _initialized;

    protected JintStatement(Statement statement)
    {
        _statement = statement;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    public Completion Execute(EvaluationContext context)
    {
        if (_statement.Type != NodeType.BlockStatement)
        {
            context.PrepareFor(_statement);
            context.RunBeforeExecuteStatementChecks(_statement);
        }

        if (!_initialized)
        {
            Initialize(context);
            _initialized = true;
        }

        return ExecuteInternal(context);
    }

    protected abstract Completion ExecuteInternal(EvaluationContext context);

    public ref readonly SourceLocation Location => ref _statement.LocationRef;

    /// <summary>
    /// Opportunity to build one-time structures and caching based on lexical context.
    /// </summary>
    /// <param name="context"></param>
    protected virtual void Initialize(EvaluationContext context)
    {
    }

    protected internal static JintStatement Build(Statement statement)
    {
        if (statement.UserData is JintStatement preparedStatement)
        {
            return preparedStatement;
        }

        JintStatement? result = statement.Type switch
        {
            NodeType.BlockStatement => new JintBlockStatement((NestedBlockStatement) statement),
            NodeType.ReturnStatement => new JintReturnStatement((ReturnStatement) statement),
            NodeType.VariableDeclaration => new JintVariableDeclaration((VariableDeclaration) statement),
            NodeType.BreakStatement => new JintBreakStatement((BreakStatement) statement),
            NodeType.ContinueStatement => new JintContinueStatement((ContinueStatement) statement),
            NodeType.DoWhileStatement => new JintDoWhileStatement((DoWhileStatement) statement),
            NodeType.EmptyStatement => new JintEmptyStatement((EmptyStatement) statement),
            NodeType.ExpressionStatement => new JintExpressionStatement((ExpressionStatement) statement),
            NodeType.ForStatement => new JintForStatement((ForStatement) statement),
            NodeType.ForInStatement => new JintForInForOfStatement((ForInStatement) statement),
            NodeType.ForOfStatement => new JintForInForOfStatement((ForOfStatement) statement),
            NodeType.IfStatement => new JintIfStatement((IfStatement) statement),
            NodeType.LabeledStatement => new JintLabeledStatement((LabeledStatement) statement),
            NodeType.SwitchStatement => new JintSwitchStatement((SwitchStatement) statement),
            NodeType.FunctionDeclaration => new JintFunctionDeclarationStatement((FunctionDeclaration) statement),
            NodeType.ThrowStatement => new JintThrowStatement((ThrowStatement) statement),
            NodeType.TryStatement => new JintTryStatement((TryStatement) statement),
            NodeType.WhileStatement => new JintWhileStatement((WhileStatement) statement),
            NodeType.WithStatement => new JintWithStatement((WithStatement) statement),
            NodeType.DebuggerStatement => new JintDebuggerStatement((DebuggerStatement) statement),
            NodeType.ClassDeclaration => new JintClassDeclarationStatement((ClassDeclaration) statement),
            NodeType.ExportNamedDeclaration => new JintExportNamedDeclaration((ExportNamedDeclaration) statement),
            NodeType.ExportAllDeclaration => new JintExportAllDeclaration((ExportAllDeclaration) statement),
            NodeType.ExportDefaultDeclaration => new JintExportDefaultDeclaration((ExportDefaultDeclaration) statement),
            NodeType.ImportDeclaration => new JintImportDeclaration((ImportDeclaration) statement),
            _ => null
        };

        if (result is null)
        {
            ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(statement.Type), $"unsupported statement type '{statement.Type}'");
        }

        return result;
    }

    internal static JsValue? FastResolve(StatementOrExpression statement)
    {
        if (statement is ReturnStatement rs && rs.Argument is Literal l)
        {
            return JintLiteralExpression.ConvertToJsValue(l);
        }

        return null;
    }
}