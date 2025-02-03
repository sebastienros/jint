using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint;

public partial class Engine
{
    /// <summary>
    /// Invoke the current value as function.
    /// </summary>
    /// <param name="propertyName">The name of the function to call.</param>
    /// <param name="arguments">The arguments of the function call.</param>
    /// <returns>The value returned by the function call.</returns>
    public ValueTask<JsValue> InvokeAsync(string propertyName, params object?[] arguments)
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
    public ValueTask<JsValue> InvokeAsync(string propertyName, object? thisObj, object?[] arguments)
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
    public ValueTask<JsValue> InvokeAsync(JsValue value, params object?[] arguments)
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
    public async ValueTask<JsValue> InvokeAsync(JsValue value, object? thisObj, object?[] arguments)
    {
        var callable = value as ICallable;
        if (callable is null)
        {
            ExceptionHelper.ThrowJavaScriptException(Realm.Intrinsics.TypeError, "Can only invoke functions");
        }

        async ValueTask<JsValue> DoInvokeAsync()
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
                    result = await callable.CallAsync(thisObject, items).ConfigureAwait(false);
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

    /// <summary>
    /// Invokes the callable and returns the resulting object.
    /// </summary>
    /// <param name="callable">The callable.</param>
    /// <param name="thisObject">Value bound as this.</param>
    /// <param name="arguments">The arguments of the call.</param>
    /// <returns>The value returned by the call.</returns>
    public ValueTask<JsValue> CallAsync(JsValue callable, JsValue thisObject, JsValue[] arguments)
    {
        ValueTask<JsValue> Callback()
        {
            if (!callable.IsCallable)
            {
                ExceptionHelper.ThrowArgumentException(callable + " is not callable");
            }

            return CallAsync((ICallable) callable, thisObject, arguments, null);
        }

        return ExecuteWithConstraintsAsync(Options.Strict, Callback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask<JsValue> CallAsync(ICallable callable, JsValue thisObject, JsValue[] arguments, JintExpression? expression)
    {
        if (callable is Function functionInstance)
        {
            return CallAsync(functionInstance, thisObject, arguments, expression);
        }

        return callable.CallAsync(thisObject, arguments);
    }

    internal async ValueTask<JsValue> CallAsync(
       Function function,
       JsValue thisObject,
       JsValue[] arguments,
       JintExpression? expression)
    {
        // ensure logic is in sync between Call, Construct, engine.Invoke and JintCallExpression!

        var recursionDepth = CallStack.Push(function, expression, ExecutionContext);

        if (recursionDepth > Options.Constraints.MaxRecursionDepth)
        {
            // automatically pops the current element as it was never reached
            ExceptionHelper.ThrowRecursionDepthOverflowException(CallStack);
        }

        JsValue result;
        try
        {
            result = await function.CallAsync(thisObject, arguments).ConfigureAwait(false);
        }
        finally
        {
            // if call stack was reset due to recursive call to engine or similar, we might not have it anymore
            if (CallStack.Count > 0)
            {
                CallStack.Pop();
            }
        }

        return result;
    }

    internal async ValueTask<T> ExecuteWithConstraintsAsync<T>(bool strict, Func<ValueTask<T>> callback)
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
