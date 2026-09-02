namespace Jint.Runtime.Debugger;

/// <summary>
/// Walks a program and collects every position the debugger's step lane visits when the program runs
/// with every branch taken. Backs <see cref="DebugHandler.GetStepLocations(Program)"/>.
/// </summary>
/// <remarks>
/// This mirrors, statically, the two runtime hooks that pause execution: <c>DebugHandler.OnStep</c>, which
/// <c>Engine.RunPerStatementChecks</c> raises for every executed statement whose node type is not
/// <c>BlockStatement</c> plus the loop header expressions the loop handlers raise it for directly, and
/// <c>DebugHandler.OnReturnPoint</c>, which <c>ScriptFunction</c> raises at the end of every script function
/// body. Every arm below cites the call site it mirrors; a change to either side without the other is what
/// <c>StepLocationTests</c> fails on, running the corpus and comparing the two sets.
/// </remarks>
internal sealed class StepLocationCollector : AstVisitor
{
    private readonly List<StepLocation> _locations = new();

    /// <summary>
    /// The one node whose statement step is suppressed, set only by
    /// <see cref="VisitExportDefaultDeclaration"/>: a declaration that handler never executes as a statement.
    /// </summary>
    private Node? _suppressed;

    internal static StepLocation[] Collect(Program program)
    {
        var collector = new StepLocationCollector();
        collector.Visit(program);
        return collector.Sorted();
    }

    private StepLocation[] Sorted()
    {
        var result = _locations;
        result.Sort(static (x, y) =>
        {
            var byLine = x.Line.CompareTo(y.Line);
            if (byLine != 0)
            {
                return byLine;
            }

            var byColumn = x.Column.CompareTo(y.Column);
            return byColumn != 0 ? byColumn : ((int) x.Kind).CompareTo((int) y.Kind);
        });

        // Two grammar positions can name the same node - a for-in/of left that is a variable
        // declaration is recorded by both arms - and the runtime visits such a position once.
        var unique = 0;
        for (var i = 0; i < result.Count; i++)
        {
            if (unique == 0 || result[i] != result[unique - 1])
            {
                result[unique++] = result[i];
            }
        }

        var array = new StepLocation[unique];
        result.CopyTo(0, array, 0, unique);
        return array;
    }

    private void Add(in SourceLocation location, StepLocationKind kind)
    {
        var start = location.Start;
        _locations.Add(new StepLocation(location.SourceFile, start.Line, start.Column, kind));
    }

    /// <summary>
    /// The step <c>Engine.RunPerStatementChecks</c> raises before a statement executes, less the node
    /// types it filters out.
    /// </summary>
    private void AddStatement(Statement node)
    {
        if (ReferenceEquals(node, _suppressed))
        {
            return;
        }

        Add(in node.LocationRef, node.Type == NodeType.DebuggerStatement ? StepLocationKind.DebuggerStatement : StepLocationKind.Statement);
    }

    /// <summary>
    /// The end-of-body pause <c>ScriptFunction.Call</c> / <c>Construct</c> raise through
    /// <c>DebugHandler.OnReturnPoint</c>, which reports the body's <em>end</em> position.
    /// </summary>
    private void AddReturnPoint(in SourceLocation bodyLocation)
    {
        var end = bodyLocation.End;
        _locations.Add(new StepLocation(bodyLocation.SourceFile, end.Line, end.Column, StepLocationKind.Return));
    }

    /// <summary>
    /// A function body: a block body contributes only its return point, a concise (expression) arrow body
    /// additionally steps onto the expression itself, which is where
    /// <c>JintFunctionDefinition.EvaluateBody</c> runs the per-statement checks over
    /// <c>Function.Body</c>.
    /// </summary>
    private void AddFunctionBody(StatementOrExpression body)
    {
        if (body.Type != NodeType.BlockStatement)
        {
            Add(in body.LocationRef, StepLocationKind.Statement);
        }

        AddReturnPoint(in body.LocationRef);
    }

    // --- statements ------------------------------------------------------------------------------------
    //
    // Every statement type JintStatement.Build produces a handler for, minus BlockStatement (which
    // RunPerStatementChecks filters by node type, and which covers FunctionBody and NestedBlockStatement
    // too). StaticBlock never reaches JintStatement.Build; see VisitStaticBlock.

    protected override object VisitVariableDeclaration(VariableDeclaration node)
    {
        AddStatement(node);
        base.VisitVariableDeclaration(node);
        return node;
    }

    protected override object VisitExpressionStatement(ExpressionStatement node)
    {
        AddStatement(node);
        base.VisitExpressionStatement(node);
        return node;
    }

    protected override object VisitEmptyStatement(EmptyStatement node)
    {
        AddStatement(node);
        base.VisitEmptyStatement(node);
        return node;
    }

    protected override object VisitDebuggerStatement(DebuggerStatement node)
    {
        AddStatement(node);
        base.VisitDebuggerStatement(node);
        return node;
    }

    protected override object VisitReturnStatement(ReturnStatement node)
    {
        AddStatement(node);
        base.VisitReturnStatement(node);
        return node;
    }

    protected override object VisitThrowStatement(ThrowStatement node)
    {
        AddStatement(node);
        base.VisitThrowStatement(node);
        return node;
    }

    protected override object VisitBreakStatement(BreakStatement node)
    {
        AddStatement(node);
        base.VisitBreakStatement(node);
        return node;
    }

    protected override object VisitContinueStatement(ContinueStatement node)
    {
        AddStatement(node);
        base.VisitContinueStatement(node);
        return node;
    }

    protected override object VisitIfStatement(IfStatement node)
    {
        AddStatement(node);
        base.VisitIfStatement(node);
        return node;
    }

    protected override object VisitSwitchStatement(SwitchStatement node)
    {
        AddStatement(node);
        base.VisitSwitchStatement(node);
        return node;
    }

    protected override object VisitTryStatement(TryStatement node)
    {
        AddStatement(node);
        base.VisitTryStatement(node);
        return node;
    }

    protected override object VisitLabeledStatement(LabeledStatement node)
    {
        AddStatement(node);
        base.VisitLabeledStatement(node);
        return node;
    }

    protected override object VisitWithStatement(WithStatement node)
    {
        AddStatement(node);
        base.VisitWithStatement(node);
        return node;
    }

    protected override object VisitFunctionDeclaration(FunctionDeclaration node)
    {
        AddStatement(node);
        AddFunctionBody(node.Body);
        base.VisitFunctionDeclaration(node);
        return node;
    }

    protected override object VisitClassDeclaration(ClassDeclaration node)
    {
        AddStatement(node);
        base.VisitClassDeclaration(node);
        return node;
    }

    // --- loops -----------------------------------------------------------------------------------------

    protected override object VisitWhileStatement(WhileStatement node)
    {
        AddStatement(node);
        // JintWhileStatement.ExecuteInternal steps onto the test before every evaluation of it.
        Add(in node.Test.LocationRef, StepLocationKind.Statement);
        base.VisitWhileStatement(node);
        return node;
    }

    protected override object VisitDoWhileStatement(DoWhileStatement node)
    {
        AddStatement(node);
        // JintDoWhileStatement.ExecuteInternal steps onto the test after every iteration of the body.
        Add(in node.Test.LocationRef, StepLocationKind.Statement);
        base.VisitDoWhileStatement(node);
        return node;
    }

    protected override object VisitForStatement(ForStatement node)
    {
        AddStatement(node);

        // JintForStatement.ForBodyEvaluation steps onto the test and the update expression. The init is
        // stepped onto only when it is a declaration - an init expression is evaluated, never stepped -
        // and VisitVariableDeclaration is what records that case.
        if (node.Test is not null)
        {
            Add(in node.Test.LocationRef, StepLocationKind.Statement);
        }

        if (node.Update is not null)
        {
            Add(in node.Update.LocationRef, StepLocationKind.Statement);
        }

        base.VisitForStatement(node);
        return node;
    }

    protected override object VisitForInStatement(ForInStatement node)
    {
        AddStatement(node);
        AddForInOfLeft(node.Left);
        base.VisitForInStatement(node);
        return node;
    }

    protected override object VisitForOfStatement(ForOfStatement node)
    {
        AddStatement(node);
        AddForInOfLeft(node.Left);
        base.VisitForOfStatement(node);
        return node;
    }

    /// <summary>
    /// <c>JintForInForOfStatement</c> steps onto the left node itself once per iteration, whether it is a
    /// declaration, an identifier or a destructuring pattern.
    /// </summary>
    private void AddForInOfLeft(Node left) => Add(in left.LocationRef, StepLocationKind.Statement);

    // --- modules ---------------------------------------------------------------------------------------

    protected override object VisitImportDeclaration(ImportDeclaration node)
    {
        AddStatement(node);
        base.VisitImportDeclaration(node);
        return node;
    }

    protected override object VisitExportAllDeclaration(ExportAllDeclaration node)
    {
        AddStatement(node);
        base.VisitExportAllDeclaration(node);
        return node;
    }

    protected override object VisitExportNamedDeclaration(ExportNamedDeclaration node)
    {
        // JintExportNamedDeclaration executes the declaration it wraps as an ordinary statement, so both
        // the export and the declaration are stepped onto.
        AddStatement(node);
        base.VisitExportNamedDeclaration(node);
        return node;
    }

    protected override object VisitExportDefaultDeclaration(ExportDefaultDeclaration node)
    {
        AddStatement(node);

        // JintExportDefaultDeclaration builds an exported class itself, and an anonymous exported function
        // declaration is bound as *default* by the module's environment setup, which makes the handler
        // return before it evaluates anything - so neither is executed as a statement. A NAMED function
        // declaration is bound under its own name instead, so it does run, and does step.
        var previous = _suppressed;
        _suppressed = node.Declaration is ClassDeclaration or FunctionDeclaration { Id: null }
            ? node.Declaration
            : null;
        try
        {
            base.VisitExportDefaultDeclaration(node);
            return node;
        }
        finally
        {
            _suppressed = previous;
        }
    }

    // --- functions and classes -------------------------------------------------------------------------

    protected override object VisitFunctionExpression(FunctionExpression node)
    {
        AddFunctionBody(node.Body);
        base.VisitFunctionExpression(node);
        return node;
    }

    protected override object VisitArrowFunctionExpression(ArrowFunctionExpression node)
    {
        AddFunctionBody(node.Body);
        base.VisitArrowFunctionExpression(node);
        return node;
    }

    protected override object VisitStaticBlock(StaticBlock node)
    {
        // The block runs as the body of the synthesized function ClassDefinition builds for it, so its
        // statements are stepped onto but the block itself never is, and the return point is its end.
        AddReturnPoint(in node.LocationRef);
        base.VisitStaticBlock(node);
        return node;
    }

    protected override object VisitPropertyDefinition(PropertyDefinition node)
    {
        AddClassFieldInitializer(node.Value);
        base.VisitPropertyDefinition(node);
        return node;
    }

    protected override object VisitAccessorProperty(AccessorProperty node)
    {
        AddClassFieldInitializer(node.Value);
        base.VisitAccessorProperty(node);
        return node;
    }

    /// <summary>
    /// A class field initializer runs as the body of a synthesized function whose single statement is a
    /// <c>return</c> of the initializer expression, so the expression is stepped onto as a statement and
    /// its end is a return point.
    /// </summary>
    private void AddClassFieldInitializer(Expression? value)
    {
        if (value is null)
        {
            return;
        }

        Add(in value.LocationRef, StepLocationKind.Statement);
        AddReturnPoint(in value.LocationRef);
    }
}
