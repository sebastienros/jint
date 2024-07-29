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
        var engine = context.Engine;
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
        Environment? loopEnv = null;
        var engine = context.Engine;
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
        }

        try
        {
            if (_initExpression != null)
            {
                _initExpression?.GetValue(context);
            }
            else
            {
                _initStatement?.Execute(context);
            }

            return ForBodyEvaluation(context);
        }
        finally
        {
            if (oldEnv is not null)
            {
                engine.UpdateLexicalEnvironment(oldEnv);
            }
        }
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