using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-12.10
/// </summary>
internal sealed class JintWithStatement : JintStatement<WithStatement>
{
    private readonly ProbablyBlockStatement _body;
    private readonly JintExpression _object;

    public JintWithStatement(WithStatement statement) : base(statement)
    {
        _body = new ProbablyBlockStatement(statement.Body);
        _object = JintExpression.Build(statement.Object);
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;

        ObjectEnvironment newEnv;
        Environment oldEnv;

        // A replay must resume into the environment it suspended in, not a fresh one. Every
        // environment-owning statement inside the body saves this object environment as its own
        // outer environment across a suspension and restores it on exit, so a rebuilt one turns
        // those saves into references to an environment that is no longer on the chain — the
        // restore then detaches the chain from the global environment. Mirrors JintBlockStatement.
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out WithSuspendData? suspendData)
            && suspendData?.WithEnvironment is not null)
        {
            newEnv = suspendData.WithEnvironment;

            // Captured on suspension; the current environment is the fallback if it is somehow absent.
            oldEnv = suspendData.OuterEnvironment ?? engine.ExecutionContext.LexicalEnvironment;
        }
        else
        {
            var jsValue = _object.GetValue(context);

            // The object expression can itself suspend (`with (await p) { … }`). Building an
            // environment over the placeholder value and running the body against it would be
            // wrong twice over — the body runs before the object is known, and it runs again on
            // resume — so unwind and let the replay redo the whole statement.
            if (context.IsSuspended())
            {
                return new Completion(CompletionType.Return, JsValue.Undefined, _statement);
            }

            var obj = TypeConverter.ToObject(engine.Realm, jsValue);
            oldEnv = engine.ExecutionContext.LexicalEnvironment;
            newEnv = JintEnvironment.NewObjectEnvironment(engine, obj, oldEnv, provideThis: true, withEnvironment: true);
        }

        engine.UpdateLexicalEnvironment(newEnv);

        Completion c;
        try
        {
            c = _body.Execute(context);
        }
        catch (JavaScriptException e)
        {
            c = new Completion(CompletionType.Throw, e.Error, _statement);
        }
        finally
        {
            if (context.IsSuspended() && suspendable is not null)
            {
                var data = suspendable.Data.GetOrCreate<WithSuspendData>(this);
                data.WithEnvironment = newEnv;
                data.OuterEnvironment = oldEnv;
            }
            else
            {
                suspendable?.Data.Clear(this);
            }

            engine.UpdateLexicalEnvironment(oldEnv);
        }

        return c.UpdateEmpty(JsValue.Undefined);
    }
}