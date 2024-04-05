using Jint.Native;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintAwaitExpression : JintExpression
{
    private JintExpression _awaitExpression = null!;
    private bool _initialized;

    public JintAwaitExpression(AwaitExpression expression) : base(expression)
    {
        _initialized = false;
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            _awaitExpression = Build(((AwaitExpression) _expression).Argument);
            _initialized = true;
        }

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

            return value.UnwrapIfPromise();
        }
        catch (PromiseRejectedException e)
        {
            ExceptionHelper.ThrowJavaScriptException(engine, e.RejectedValue, _expression.Location);
            return null;
        }
    }
}
