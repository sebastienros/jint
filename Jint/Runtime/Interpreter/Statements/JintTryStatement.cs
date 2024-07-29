using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// https://tc39.es/ecma262/#sec-try-statement
/// </summary>
internal sealed class JintTryStatement : JintStatement<TryStatement>
{
    private JintBlockStatement _block = null!;
    private JintBlockStatement? _catch;
    private JintBlockStatement? _finalizer;

    public JintTryStatement(TryStatement statement) : base(statement)
    {

    }

    protected override void Initialize(EvaluationContext context)
    {
        _block = new JintBlockStatement(_statement.Block);
        if (_statement.Finalizer != null)
        {
            _finalizer = new JintBlockStatement(_statement.Finalizer);
        }
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;

        var b = _block.Execute(context);

        if (b.Type == CompletionType.Throw)
        {
            b = ExecuteCatch(context, b, engine);
        }

        if (_finalizer != null)
        {
            var f = _finalizer.Execute(context);
            if (f.Type == CompletionType.Normal)
            {
                return b;
            }

            return f.UpdateEmpty(JsValue.Undefined);
        }

        return b.UpdateEmpty(JsValue.Undefined);
    }

    private Completion ExecuteCatch(EvaluationContext context, Completion b, Engine engine)
    {
        // execute catch
        if (_statement.Handler is not null)
        {
            // initialize lazily
            if (_catch is null)
            {
                _catch = new JintBlockStatement(_statement.Handler.Body);
            }

            // https://tc39.es/ecma262/#sec-runtime-semantics-catchclauseevaluation

            var thrownValue = b.Value;
            var oldEnv = engine.ExecutionContext.LexicalEnvironment;
            var catchEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv, catchEnvironment: true);

            var boundNames = new List<Key>();
            _statement.Handler.Param.GetBoundNames(boundNames);

            for (var i = 0; i < boundNames.Count; i++)
            {
                catchEnv.CreateMutableBinding(boundNames[i]);
            }

            engine.UpdateLexicalEnvironment(catchEnv);

            var catchParam = _statement.Handler?.Param;
            catchParam.BindingInitialization(context, thrownValue, catchEnv);

            b = _catch.Execute(context);

            engine.UpdateLexicalEnvironment(oldEnv);
        }

        return b;
    }
}