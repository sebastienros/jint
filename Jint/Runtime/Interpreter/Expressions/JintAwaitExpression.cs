using System.Reflection;
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
                var underlyingObject = value.ToObject();

                if (underlyingObject is Task task)
                {
                    var taskType = underlyingObject.GetType();
                    var taskResult = taskType.GetProperty("Result", bindingAttr: BindingFlags.Public | BindingFlags.Instance);
                    if (taskResult != null)
                    {
                        try
                        {
                            var resultValue = taskResult.GetValue(underlyingObject);
                            value = JsValue.FromObject(engine, resultValue);
                        }
                        catch (TargetInvocationException ex)
                        {
                            ExceptionHelper.ThrowMeaningfulException(engine, ex);
                        }
                    }
                }

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
