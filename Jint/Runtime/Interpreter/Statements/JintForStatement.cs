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
    private JintVariableDeclaration? _initStatement;
    private JintExpression? _initExpression;

    private JintExpression? _test;
    private JintExpression? _increment;

    private ProbablyBlockStatement _body;
    private List<Key>? _boundNames;

    private bool _shouldCreatePerIterationEnvironment;

    public JintForStatement(ForStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _body = new ProbablyBlockStatement(_statement.Body);

        if (_statement.Init != null)
        {
            if (_statement.Init.Type == NodeType.VariableDeclaration)
            {
                var d = (VariableDeclaration) _statement.Init;
                if (d.Kind != VariableDeclarationKind.Var)
                {
                    _boundNames = new List<Key>();
                    d.GetBoundNames(_boundNames);
                }
                _initStatement = new JintVariableDeclaration(d);
                _shouldCreatePerIterationEnvironment = d.Kind == VariableDeclarationKind.Let;
            }
            else
            {
                _initExpression = JintExpression.Build((Expression) _statement.Init);
            }
        }

        if (_statement.Test != null)
        {
            _test = JintExpression.Build(_statement.Test);
        }

        if (_statement.Update != null)
        {
            _increment = JintExpression.Build(_statement.Update);
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

        // Get the resume node from the unified interface
        Node? resumeNode = null;
        if (suspendable is { IsResuming: true, LastSuspensionNode: not null })
        {
            // LastSuspensionNode could be a Node directly (yield) or a JintExpression (await)
            resumeNode = suspendable.LastSuspensionNode as Node
                ?? (suspendable.LastSuspensionNode as JintExpression)?._expression as Node;
        }

        // Only skip init when resuming from body/test/update, NOT from init
        var resumingInLoop = resumeNode is not null && IsNodeInsideForStatementExcludingInit(resumeNode);

        ForLoopSuspendData? suspendData = null;
        if (resumingInLoop && _boundNames != null)
        {
            suspendable?.TryGetSuspendData<ForLoopSuspendData>(this, out suspendData);
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

            completion = ForBodyEvaluation(context);
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

                    var data = suspendable.GetOrCreateSuspendData<ForLoopSuspendData>(this);
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
                    suspendable?.ClearSuspendData(this);
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
    private Completion ForBodyEvaluation(EvaluationContext context)
    {
        var v = JsValue.Undefined;

        if (_shouldCreatePerIterationEnvironment)
        {
            CreatePerIterationEnvironment(context);
        }

        var debugHandler = context.DebugMode ? context.Engine.Debugger : null;

        while (true)
        {
            // Only clear completed awaits cache when starting a NEW iteration, not when resuming.
            // When resuming from a nested await (e.g., "for (; await await await x;)"),
            // we need the cached values of already-completed awaits to continue evaluation.
            // Note: _completedAwaits is async-specific, so we access AsyncFunction directly here
            var asyncFn = context.Engine.ExecutionContext.AsyncFunction;
            if (asyncFn is null || !asyncFn._isResuming)
            {
                asyncFn?._completedAwaits?.Clear();
            }

            if (_test != null)
            {
                debugHandler?.OnStep(_test._expression);

                var testValue = _test.GetValue(context);

                // Check for async suspension in test expression
                if (context.IsSuspended())
                {
                    return new Completion(CompletionType.Return, JsValue.Undefined, ((JintStatement) this)._statement);
                }

                if (!TypeConverter.ToBoolean(testValue))
                {
                    return new Completion(CompletionType.Normal, v, ((JintStatement) this)._statement);
                }
            }

            var result = _body.Execute(context);
            if (!result.Value.IsEmpty)
            {
                v = result.Value;
            }

            // Check for suspension - if suspended, we need to exit the loop
            var suspendable = context.Engine.ExecutionContext.Suspendable;
            if (context.IsSuspended())
            {
                var suspendedValue = suspendable?.SuspendedValue ?? result.Value;
                return new Completion(CompletionType.Return, suspendedValue, ((JintStatement) this)._statement);
            }

            if (result.Type == CompletionType.Break && (context.Target == null || string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
            {
                return new Completion(CompletionType.Normal, result.Value, ((JintStatement) this)._statement);
            }

            if (result.Type != CompletionType.Continue || (context.Target != null && !string.Equals(context.Target, _statement?.LabelSet?.Name, StringComparison.Ordinal)))
            {
                if (result.Type != CompletionType.Normal)
                {
                    return result;
                }
            }

            if (_shouldCreatePerIterationEnvironment)
            {
                CreatePerIterationEnvironment(context);
            }

            if (_increment != null)
            {
                debugHandler?.OnStep(_increment._expression);
                _increment.Evaluate(context);

                // Check for suspension in update expression (e.g., yield in the update)
                if (context.IsSuspended())
                {
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

    private void CreatePerIterationEnvironment(EvaluationContext context)
    {
        var engine = context.Engine;
        var lastIterationEnv = (DeclarativeEnvironment) engine.ExecutionContext.LexicalEnvironment;
        var thisIterationEnv = JintEnvironment.NewDeclarativeEnvironment(engine, lastIterationEnv._outerEnv);

        lastIterationEnv.TransferTo(_boundNames!, thisIterationEnv);

        engine.UpdateLexicalEnvironment(thisIterationEnv);
    }
}
