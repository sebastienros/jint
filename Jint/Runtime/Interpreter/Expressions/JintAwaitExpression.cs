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
        return EvaluateInternalAsync(context).Preserve().GetAwaiter().GetResult();
    }

    protected override async ValueTask<object> EvaluateInternalAsync(EvaluationContext context)
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
            var value = await _awaitExpression.GetValueAsync(context).ConfigureAwait(false);

            if (value is not JsPromise)
            {
                var promiseInstance = new JsPromise(engine);
                promiseInstance.Resolve(value);
                value = promiseInstance;
            }

            return await value.UnwrapIfPromiseAsync().ConfigureAwait(false);
        }
        catch (PromiseRejectedException e)
        {
            ExceptionHelper.ThrowJavaScriptException(engine, e.RejectedValue, _expression.Location);
            return null;
        }
    }
}
