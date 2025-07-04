using Jint.Runtime;

namespace Jint.Native.Disposable;

internal sealed record DisposeCapability
{
    private readonly Engine _engine;

    public DisposeCapability(Engine engine)
    {
        _engine = engine;
    }

    private List<DisposableResource> DisposableResourceStack { get; } = [];

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

        DisposableResourceStack.Add(resource);
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
                    ExceptionHelper.ThrowTypeError(_engine.Realm, "Expected an object for disposable resource.");
                    return null!;
                }
                method = v.AsObject().GetDisposeMethod(hint);
                if (method is null)
                {
                    ExceptionHelper.ThrowTypeError(_engine.Realm, "No dispose method found for the resource.");
                    return null!;
                }
            }
        }

        return new DisposableResource(v, hint, method);
    }

    public Completion DisposeResources(Completion c)
    {
        var needsAwait = false;
        var hasAwaited = false;

        for (var i = DisposableResourceStack.Count - 1; i >= 0; i--)
        {
            var (value, hint, method) = DisposableResourceStack[i];

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
                            result = result.UnwrapIfPromise();
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
            _engine.RunAvailableContinuations();
        }

        DisposableResourceStack.Clear();
        return c;
    }
}
