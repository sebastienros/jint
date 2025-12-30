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

        // Check if we're resuming from a yield inside this for statement (body, test, or update)
        // If so, skip the initialization to avoid resetting loop variables
        var generator = engine.ExecutionContext.Generator;
        var resumingInLoop = generator is not null
            && generator._isResuming
            && generator._lastYieldNode is Node yieldNode
            && IsNodeInsideForStatement(yieldNode);

        ForLoopSuspendData? suspendData = null;
        if (resumingInLoop && _boundNames != null)
        {
            generator!.TryGetForLoopSuspendData(this, out suspendData);
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
                if (kind == VariableDeclarationKind.Const)
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
                }
                else
                {
                    _initStatement?.Execute(context);
                }
            }

            completion = ForBodyEvaluation(context);
            return completion;
        }
        finally
        {
            if (oldEnv is not null)
            {
                // Save loop variable values if generator is suspended (don't save on normal completion)
                if (generator is not null && context.IsGeneratorSuspended() && _boundNames != null)
                {
                    // Use the CURRENT lexical environment, not loopEnv, because
                    // CreatePerIterationEnvironment may have created new environments during the loop
                    var currentEnv = engine.ExecutionContext.LexicalEnvironment;
                    var data = generator.GetOrCreateForLoopSuspendData(this);
                    data.BoundValues ??= new Dictionary<Key, JsValue>();
                    for (var i = 0; i < _boundNames.Count; i++)
                    {
                        var name = _boundNames[i];
                        var value = currentEnv.GetBindingValue(name, strict: false);
                        data.BoundValues[name] = value;
                    }
                }
                else if (generator is not null && !context.IsGeneratorSuspended())
                {
                    // Clear suspend data on normal completion
                    generator.ClearForLoopSuspendData(this);
                }

                loopEnv!.DisposeResources(completion);
                engine.UpdateLexicalEnvironment(oldEnv);
            }
        }
    }

    /// <summary>
    /// Checks if the given node is inside this for statement (body, test, or update).
    /// Used to determine if we're resuming from a yield inside the loop.
    /// </summary>
    private bool IsNodeInsideForStatement(Node node)
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
            if (_test != null)
            {
                debugHandler?.OnStep(_test._expression);

                if (!TypeConverter.ToBoolean(_test.GetValue(context)))
                {
                    return new Completion(CompletionType.Normal, v, ((JintStatement) this)._statement);
                }
            }

            var result = _body.Execute(context);
            if (!result.Value.IsEmpty)
            {
                v = result.Value;
            }

            // Check for generator suspension - if the generator is suspended, we need to exit the loop
            if (context.IsGeneratorSuspended())
            {
                var generator = context.Engine.ExecutionContext.Generator;
                var suspendedValue = generator?._suspendedValue ?? result.Value;
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

                // Check for generator suspension in update expression (e.g., yield in the update)
                if (context.IsGeneratorSuspended())
                {
                    var generator = context.Engine.ExecutionContext.Generator;
                    var suspendedValue = generator?._suspendedValue ?? JsValue.Undefined;
                    return new Completion(CompletionType.Return, suspendedValue, ((JintStatement) this)._statement);
                }

                // Check for generator return request
                var gen = context.Engine.ExecutionContext.Generator;
                if (gen?._returnRequested == true)
                {
                    var returnValue = gen._suspendedValue ?? JsValue.Undefined;
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
