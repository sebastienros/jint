using Jint.Native.AsyncFunction;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Native.Disposable;

internal sealed class DisposeCapability
{
    private readonly Engine _engine;
    private readonly List<DisposableResource> _disposableResourceStack = [];

    /// <summary>
    /// Set to true after DisposeResources if an async-dispose resource with no method
    /// was encountered, indicating the caller should introduce an Await tick per spec.
    /// </summary>
    public bool NeedsAsyncTick { get; private set; }

    public DisposeCapability(Engine engine)
    {
        _engine = engine;
    }

    public void AddDisposableResource(JsValue v, DisposeHint hint, ICallable? method = null)
    {
        DisposableResource resource;
        if (method is null)
        {
            if (v.IsNullOrUndefined() && hint == DisposeHint.Sync)
            {
                return;
            }

            resource = CreateDisposableResource(v, hint);
        }
        else
        {
            resource = CreateDisposableResource(JsValue.Undefined, hint, method);
        }

        _disposableResourceStack.Add(resource);
    }

    private DisposableResource CreateDisposableResource(JsValue v, DisposeHint hint, ICallable? method = null)
    {
        if (method is null)
        {
            if (v.IsNullOrUndefined())
            {
                v = JsValue.Undefined;
                method = null;
            }
            else
            {
                if (!v.IsObject())
                {
                    Throw.TypeError(_engine.Realm, "Expected an object for disposable resource.");
                    return default;
                }
                method = v.AsObject().GetDisposeMethod(hint);
                if (method is null)
                {
                    Throw.TypeError(_engine.Realm, "No dispose method found for the resource.");
                    return default;
                }
            }
        }

        return new DisposableResource(v, hint, method);
    }

    public Completion DisposeResources(Completion c)
    {
        var needsAwait = false;
        var hasAwaited = false;

        for (var i = _disposableResourceStack.Count - 1; i >= 0; i--)
        {
            var (value, hint, method) = _disposableResourceStack[i];

            if (hint == DisposeHint.Sync && needsAwait && !hasAwaited)
            {
                _engine.RunAvailableContinuations();
                needsAwait = false;
            }

            if (method is not null)
            {
                var result = JsValue.Undefined;
                JavaScriptException? exception = null;
                try
                {
                    result = method.Call(value);
                    if (hint == DisposeHint.Async)
                    {
                        hasAwaited = true;
                        try
                        {
                            result = result.UnwrapIfPromise(_engine.Options.Constraints.PromiseTimeout);
                        }
                        catch (PromiseRejectedException e)
                        {
                            exception = new JavaScriptException(e.RejectedValue);
                        }
                        catch (JavaScriptException e)
                        {
                            exception = e;
                        }
                    }
                }
                catch (JavaScriptException e)
                {
                    exception = e;
                }

                if (exception is not null)
                {
                    if (c.Type == CompletionType.Throw)
                    {
                        var error = _engine.Intrinsics.SuppressedError.Construct(_engine.Intrinsics.SuppressedError, "", exception.Error, c.Value);
                        c = new Completion(CompletionType.Throw, error, c._source);
                    }
                    else
                    {
                        c = new Completion(CompletionType.Throw, exception.Error, c._source);
                    }
                }
            }
            else
            {
                // Assert: hint is "async-dispose"
                needsAwait = true;
            }
        }

        if (needsAwait && !hasAwaited)
        {
            NeedsAsyncTick = true;
            _engine.RunAvailableContinuations();
        }

        _disposableResourceStack.Clear();
        return c;
    }

    private readonly record struct DisposableResource(JsValue ResourceValue, DisposeHint Hint, ICallable? DisposeMethod);
}

