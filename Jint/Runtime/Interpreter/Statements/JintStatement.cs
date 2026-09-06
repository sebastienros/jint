using System.Runtime.CompilerServices;
using Acornima;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using Range = Acornima.Range;

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

        return ExecuteInternal(context);
    }

    protected abstract Completion ExecuteInternal(EvaluationContext context);

    /// <summary>
    /// Tight-loop entry: executes the statement for side effects only, skipping the per-statement
    /// ceremony (PrepareFor, constraint checks, Completion materialization) where an override can
    /// prove it dead. Callers guarantee a non-suspendable frame, no exact constraint/debug checks
    /// (amortized constraints stay live via the caller's own countdown-driven cadence), dead
    /// completion values, and a statement shape vetted by <see cref="JintForStatement"/>'s tight-body
    /// predicate (no break/continue/return/labels, so only Normal completions can occur). The base
    /// implementation falls back to the full <see cref="Execute"/> ceremony.
    /// <para>
    /// Overrides must call <see cref="EvaluationContext.ChargeStatement"/> first: it stands in for the
    /// statement-counting constraint that <see cref="Execute"/> would have charged here, and its cadence
    /// decides where a statement limit throws.
    /// </para>
    /// </summary>
    internal virtual void ExecuteDiscarded(EvaluationContext context)
    {
        Execute(context);
    }

    public ref readonly SourceLocation Location => ref _statement.LocationRef;

    /// <summary>
    /// Builds a statement that sits directly in the StatementList of a Block, a CaseClause or a
    /// DefaultClause - the position Annex B B.3.2 gives a sloppy-mode function declaration its
    /// var-scope alias in. A LabelledStatement is transparent for that purpose: <c>l: function f(){}</c>
    /// there is treated exactly as the unlabelled form, which is what every web implementation does and
    /// what B.3.1 legalizes the form for.
    /// </summary>
    protected internal static JintStatement BuildBlockLevel(Statement statement)
    {
        return statement.Type switch
        {
            NodeType.FunctionDeclaration => new JintFunctionDeclarationStatement((FunctionDeclaration) statement, blockLevel: true),
            NodeType.LabeledStatement => new JintLabeledStatement((LabeledStatement) statement, blockLevel: true),
            _ => Build(statement)
        };
    }

    protected internal static JintStatement Build(Statement statement)
    {
        // INVARIANT: statement handlers may carry per-engine mutable state (JintBlockStatement._cachedEnv,
        // JintForStatement._cachedLoopEnv — both cache environments that root their engine). They must
        // therefore never be cached on cross-engine shared state such as AST UserData; a prepared script's
        // AST is shared by every engine that runs it, and a shared handler would pin the last-caller engine
        // (issue #2560). The only handler ever placed in UserData is the stateless ConstantStatement
        // (Engine.Ast.cs); anything else must stay owned by its per-engine statement list.
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
            Throw.ArgumentOutOfRangeException(nameof(statement.Type), $"unsupported statement type '{statement.Type}'");
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

    internal static Node? GetSuspensionNode(ISuspendable? suspendable)
    {
        if (suspendable is not { IsResuming: true, LastSuspensionNode: not null })
        {
            return null;
        }

        // LastSuspensionNode may be an AST Node, a JintExpression (an await/yield expression),
        // or a JintStatement. The last case is what `for await...of` records: its implicit await
        // lives in the loop statement itself, not in a JintExpression. Without that case this
        // returned null for EVERY for-await suspension, so resume-aware statements
        // (JintIfStatement, JintSwitchStatement, ...) lost track of which branch they were in and
        // re-evaluated their test on resume - silently taking the other branch when that test
        // reads state the suspended code had already mutated.
        return suspendable.LastSuspensionNode as Node
            ?? (suspendable.LastSuspensionNode as JintExpression)?._expression as Node
            ?? (suspendable.LastSuspensionNode as JintStatement)?._statement;
    }

    internal static bool IsNodeInsideRange(Node node, in Range range)
    {
        return range.Start <= node.Range.Start && node.Range.End <= range.End;
    }
}
