using Esprima.Ast;
using Jint.Native.Promise;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintAwaitExpression : JintExpression
{
    private JintExpression _awaitExpression = null!;

    public JintAwaitExpression(AwaitExpression expression) : base(expression)
    {
        _initialized = false;
    }

    protected override void Initialize(EvaluationContext context)
    {
        _awaitExpression = Build(((AwaitExpression) _expression).Argument);
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        var asyncContext = engine.ExecutionContext;

        try
        {
            var value = _awaitExpression.GetValue(context);

            if (value is not JsPromise)
            {
                var promiseInstance = new JsPromise(engine);
                promiseInstance.Resolve(value);
                value = promiseInstance;
            }

            engine.RunAvailableContinuations();
            return value.UnwrapIfPromise();
        }
        catch (PromiseRejectedException e)
        {
            ExceptionHelper.ThrowJavaScriptException(engine, e.RejectedValue, _expression.Location);
            return null;
        }
    }
}
