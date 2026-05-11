using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// https://tc39.es/ecma262/#sec-forbodyevaluation
/// </summary>
internal sealed class JintForStatement : JintStatement<ForStatement>
{
    private readonly JintVariableDeclaration? _initStatement;
    private readonly JintExpression? _initExpression;

    private readonly JintExpression? _test;
    private readonly JintExpression? _increment;

    private readonly ProbablyBlockStatement _body;
    private readonly List<Key>? _boundNames;

    private readonly bool _shouldCreatePerIterationEnvironment;
    private readonly bool _canReuseIterationEnvironment;

    public JintForStatement(ForStatement statement) : base(statement)
    {
        _body = new ProbablyBlockStatement(statement.Body);

        if (statement.Init != null)
        {
            if (statement.Init.Type == NodeType.VariableDeclaration)
            {
                var d = (VariableDeclaration) statement.Init;
                if (d.Kind != VariableDeclarationKind.Var)
                {
                    _boundNames = new List<Key>();
                    d.GetBoundNames(_boundNames);
                }
                _initStatement = new JintVariableDeclaration(d);
                _shouldCreatePerIterationEnvironment = d.Kind == VariableDeclarationKind.Let;

                // If no closures in the loop body/test/update capture the iteration environment,
                // we can reuse the same environment each iteration instead of allocating a new one
                if (_shouldCreatePerIterationEnvironment)
                {
                    _canReuseIterationEnvironment = !ForLoopMayCapture(statement);
                }
            }
            else
            {
                _initExpression = JintExpression.Build((Expression) statement.Init);
            }
        }

        if (statement.Test != null)
        {
            _test = JintExpression.Build(statement.Test);
        }

        if (statement.Update != null)
        {
            _increment = JintExpression.Build(statement.Update);
        }
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        Environment? oldEnv = null;
        DeclarativeEnvironment? loopEnv = null;
        var engine = context.Engine;

        // Check if we're resuming from a yield/await inside this for statement
        // If resuming from body, test, or update, skip initialization to avoid resetting loop variables
        // If resuming from init expression, we must re-execute init to complete pending nested awaits
        var suspendable = engine.ExecutionContext.Suspendable;

        var resumeNode = GetSuspensionNode(suspendable);

        // Only skip init when resuming from body/test/update, NOT from init
        var resumingInLoop = resumeNode is not null && IsNodeInsideForStatementExcludingInit(resumeNode);
        var resumingInBody = resumeNode is not null && IsNodeInsideRange(resumeNode, _statement.Body.Range);
        var resumingInUpdate = resumeNode is not null && _statement.Update is not null && IsNodeInsideRange(resumeNode, _statement.Update.Range);

        ForLoopSuspendData? suspendData = null;
        if (resumingInLoop)
        {
            suspendable?.Data.TryGet(this, out suspendData);
        }

        if (_boundNames != null)
        {
            oldEnv = engine.ExecutionContext.LexicalEnvironment;
            loopEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
            var loopEnvRec = loopEnv;
            var kind = _initStatement!._statement.Kind;
            for (var i = 0; i < _boundNames.Count; i++)
            {
                var name = _boundNames[i];
                // const, using, and await using all create immutable bindings
                if (kind is VariableDeclarationKind.Const or VariableDeclarationKind.Using or VariableDeclarationKind.AwaitUsing)
                {
                    loopEnvRec.CreateImmutableBinding(name);
                }
                else
                {
                    loopEnvRec.CreateMutableBinding(name);
                }
            }

            engine.UpdateLexicalEnvironment(loopEnv);

            // Restore loop variable values if resuming
            if (resumingInLoop && suspendData?.BoundValues is not null)
            {
                foreach (var kvp in suspendData.BoundValues)
                {
                    loopEnvRec.InitializeBinding(kvp.Key, kvp.Value, DisposeHint.Normal);
                }
            }
        }

        var completion = Completion.Empty();
        try
        {
            // Skip initialization if resuming from inside the loop (body, test, or update)
            if (!resumingInLoop)
            {
                if (_initExpression != null)
                {
                    _initExpression?.GetValue(context);

                    // Check for async suspension in init expression
                    if (context.IsSuspended())
                    {
                        return new Completion(CompletionType.Return, JsValue.Undefined, _statement);
                    }
                }
                else
                {
                    _initStatement?.Execute(context);

                    // Check for async suspension in init statement
                    if (context.IsSuspended())
                    {
                        return new Completion(CompletionType.Return, JsValue.Undefined, _statement);
                    }
                }
            }

            completion = ForBodyEvaluation(context, suspendData?.AccumulatedValue ?? JsValue.Undefined, skipTestOnce: resumingInBody, resumeUpdateOnce: resumingInUpdate);
            return completion;
        }
        finally
        {
            if (oldEnv is not null)
            {
                // Save loop variable values if generator/async function is suspended (don't save on normal completion)
                if (context.IsSuspended() && _boundNames != null && suspendable is not null)
                {
                    // Use the CURRENT lexical environment, not loopEnv, because
                    // CreatePerIterationEnvironment may have created new environments during the loop
                    var currentEnv = engine.ExecutionContext.LexicalEnvironment;

                    var data = suspendable.Data.GetOrCreate<ForLoopSuspendData>(this);
                    data.BoundValues ??= new Dictionary<Key, JsValue>();
                    for (var i = 0; i < _boundNames.Count; i++)
                    {
                        var name = _boundNames[i];
                        var value = currentEnv.GetBindingValue(name, strict: false);
                        data.BoundValues[name] = value;
                    }
                }
                else if (!context.IsSuspended())
                {
                    // Clear suspend data on normal completion
                    suspendable?.Data.Clear(this);
                }

                loopEnv!.DisposeResources(completion);
                engine.UpdateLexicalEnvironment(oldEnv);
            }
        }
    }

    /// <summary>
    /// Checks if the given node is inside this for statement's body, test, or update (but NOT init).
    /// Used to determine if we're resuming from a yield/await inside the loop.
    /// When resuming from init, we must re-execute init to complete nested awaits.
    /// When resuming from body/test/update, we skip init to avoid resetting variables.
    /// </summary>
    private bool IsNodeInsideForStatementExcludingInit(Node node)
    {
        var nodeRange = node.Range;

        // Check if inside body
        var bodyRange = _statement.Body.Range;
        if (bodyRange.Start <= nodeRange.Start && nodeRange.End <= bodyRange.End)
        {
            return true;
        }

        // Check if inside test expression
        if (_statement.Test != null)
        {
            var testRange = _statement.Test.Range;
            if (testRange.Start <= nodeRange.Start && nodeRange.End <= testRange.End)
            {
                return true;
            }
        }

        // Check if inside update expression
        if (_statement.Update != null)
        {
            var updateRange = _statement.Update.Range;
            if (updateRange.Start <= nodeRange.Start && nodeRange.End <= updateRange.End)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-forbodyevaluation
    /// </summary>
    private Completion ForBodyEvaluation(EvaluationContext context, JsValue initialValue, bool skipTestOnce, bool resumeUpdateOnce)
    {
        var v = initialValue;

        if (!resumeUpdateOnce && _shouldCreatePerIterationEnvironment && !_canReuseIterationEnvironment)
        {
            CreatePerIterationEnvironment(context);
        }

        var debugHandler = context.DebugMode ? context.Engine.Debugger : null;

        while (true)
        {
            context.Engine.ExecutionContext.ClearCompletedAwaitsIfNotResuming();

            if (!resumeUpdateOnce && !skipTestOnce && _test != null)
            {
                debugHandler?.OnStep(_test._expression);

                if (!_test.GetBooleanValue(context))
                {
                    // Check for async suspension in test expression
                    if (context.IsSuspended())
                    {
                        SaveAccumulatedValue(context, v);
                        return new Completion(CompletionType.Return, JsValue.Undefined, ((JintStatement) this)._statement);
                    }

                    context.Engine.ExecutionContext.Suspendable?.Data.Clear(this);
                    return new Completion(CompletionType.Normal, v, ((JintStatement) this)._statement);
                }
            }

            skipTestOnce = false;

            var suspendable = context.Engine.ExecutionContext.Suspendable;
            if (!resumeUpdateOnce)
            {
                var result = _body.Execute(context);
                if (!result.Value.IsEmpty)
                {
                    v = result.Value;
                }

                // Check for suspension - if suspended, we need to exit the loop
                if (context.IsSuspended())
                {
                    SaveAccumulatedValue(context, v);
                    var suspendedValue = suspendable?.SuspendedValue ?? result.Value;
                    return new Completion(CompletionType.Return, suspendedValue, ((JintStatement) this)._statement);
                }

                if (result.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    suspendable?.Data.Clear(this);
                    return new Completion(CompletionType.Normal, result.Value, ((JintStatement) this)._statement);
                }

                if (result.Type != CompletionType.Continue || (context.Target != null && !string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
                {
                    if (result.Type != CompletionType.Normal)
                    {
                        return result;
                    }
                }

                if (_shouldCreatePerIterationEnvironment && !_canReuseIterationEnvironment)
                {
                    CreatePerIterationEnvironment(context);
                }
            }
            resumeUpdateOnce = false;

            if (_increment != null)
            {
                debugHandler?.OnStep(_increment._expression);
                _increment.Evaluate(context);

                // Check for suspension in update expression (e.g., yield in the update)
                if (context.IsSuspended())
                {
                    SaveAccumulatedValue(context, v);
                    var suspendedValue = suspendable?.SuspendedValue ?? JsValue.Undefined;
                    return new Completion(CompletionType.Return, suspendedValue, ((JintStatement) this)._statement);
                }

                // Check for return request (e.g., generator.return() was called)
                if (suspendable?.ReturnRequested == true)
                {
                    var returnValue = suspendable.SuspendedValue ?? JsValue.Undefined;
                    return new Completion(CompletionType.Return, returnValue, ((JintStatement) this)._statement);
                }
            }
        }
    }

    private void SaveAccumulatedValue(EvaluationContext context, JsValue value)
    {
        var suspendable = context.Engine.ExecutionContext.Suspendable;
        if (suspendable is not null)
        {
            suspendable.Data.GetOrCreate<ForLoopSuspendData>(this).AccumulatedValue = value;
        }
    }

    /// <summary>
    /// Checks if any part of the for-loop (init, test, update, body) contains closures
    /// that could capture the per-iteration environment.
    /// </summary>
    private static bool ForLoopMayCapture(ForStatement statement)
    {
        // Check init declarators (e.g., for (let i = 0, f = function() { return i }; ...))
        if (statement.Init is VariableDeclaration vd)
        {
            foreach (var decl in vd.Declarations)
            {
                if (decl.Init is not null)
                {
                    if (JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(decl.Init)
                        || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(decl.Init))
                    {
                        return true;
                    }
                }
            }
        }

        if (statement.Test is not null
            && (JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(statement.Test)
                || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(statement.Test)))
        {
            return true;
        }

        if (statement.Update is not null
            && (JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(statement.Update)
                || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(statement.Update)))
        {
            return true;
        }

        return JintFunctionDefinition.EnvironmentEscapeAstVisitor.IsCapturing(statement.Body)
            || JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(statement.Body);
    }

    private void CreatePerIterationEnvironment(EvaluationContext context)
    {
        var engine = context.Engine;
        var lastIterationEnv = (DeclarativeEnvironment) engine.ExecutionContext.LexicalEnvironment;
        var thisIterationEnv = JintEnvironment.NewDeclarativeEnvironment(engine, lastIterationEnv._outerEnv);

        lastIterationEnv.TransferTo(_boundNames!, thisIterationEnv);

        engine.UpdateLexicalEnvironment(thisIterationEnv);
    }
}
