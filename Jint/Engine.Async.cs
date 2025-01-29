using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interpreter;

namespace Jint;

public partial class Engine
{
    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="propertyName">The name of the function to call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public Task<JsValue> InvokeAsync(string propertyName, params object?[] arguments)
    {
        return InvokeAsync(propertyName, thisObj: null, arguments);
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="propertyName">The name of the function to call.</param>
    /// <param name="thisObj">The this value inside the function call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public Task<JsValue> InvokeAsync(string propertyName, object? thisObj, object?[] arguments)
    {
        var value = GetValue(propertyName);

        return InvokeAsync(value, thisObj, arguments);
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="value">The function to call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public Task<JsValue> InvokeAsync(JsValue value, params object?[] arguments)
    {
        return InvokeAsync(value, thisObj: null, arguments);
    }

    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="value">The function to call.</param>
    /// <param name="thisObj">The this value inside the function call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public async Task<JsValue> InvokeAsync(JsValue value, object? thisObj, object?[] arguments)
    {
        var callable = value as ICallable;
        if (callable is null)
        {
            ExceptionHelper.ThrowJavaScriptException(Realm.Intrinsics.TypeError, "Can only invoke functions");
        }

        async Task<JsValue> DoInvokeAsync()
        {
            var items = _jsValueArrayPool.RentArray(arguments.Length);
            for (var i = 0; i < arguments.Length; ++i)
            {
                items[i] = JsValue.FromObject(this, arguments[i]);
            }

            // ensure logic is in sync between Call, Construct, engine.Invoke and JintCallExpression!
            JsValue result;
            var thisObject = JsValue.FromObject(this, thisObj);
            if (callable is Function functionInstance)
            {
                var callStack = CallStack;
                callStack.Push(functionInstance, expression: null, ExecutionContext);
                try
                {
                    result = await functionInstance.CallAsync(thisObject, items).ConfigureAwait(false);
                }
                finally
                {
                    // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
                    if (callStack.Count > 0)
                    {
                        callStack.Pop();
                    }
                }
            }
            else
            {
                result = await callable.CallAsync(thisObject, items).ConfigureAwait(false);
            }

            _jsValueArrayPool.ReturnArray(items);
            return result;
        }

        return await ExecuteWithConstraintsAsync(Options.Strict, DoInvokeAsync).ConfigureAwait(false);
    }

    internal async Task<T> ExecuteWithConstraintsAsync<T>(bool strict, Func<Task<T>> callback)
    {
        ResetConstraints();

        var ownsContext = _activeEvaluationContext is null;
        _activeEvaluationContext ??= new EvaluationContext(this);

        try
        {
            using (new StrictModeScope(strict))
            {
                return await callback().ConfigureAwait(false);
            }
        }
        finally
        {
            if (ownsContext)
            {
                _activeEvaluationContext = null!;
            }
            ResetConstraints();
            _agent.ClearKeptObjects();
        }
    }

}
