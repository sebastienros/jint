using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Disposable;

internal enum DisposableState
{
    Pending,
    Disposed,
}

internal sealed class DisposableStackInstance : ObjectInstance
{
    private readonly DisposeCapability _disposeCapability;

    public DisposableStackInstance(Engine engine) : base(engine)
    {
        State = DisposableState.Pending;
        _disposeCapability = new DisposeCapability(engine);
    }

    public DisposableState State { get; internal set; }

    public void AddDisposableResource(JsValue v, DisposeHint hint, ICallable? method = null)
    {
        AssertNotDisposed();
        _disposeCapability.AddDisposableResource(v, hint, method);
    }


    private void AssertNotDisposed()
    {
        if (State == DisposableState.Disposed)
        {
            ExceptionHelper.ThrowReferenceError(_engine.Realm, "Stack already disposed.");
        }
    }

    public JsValue Dispose()
    {
        if (State == DisposableState.Disposed)
        {
            return Undefined;
        }

        State = DisposableState.Disposed;
        return _disposeCapability.DisposeResources(Completion.Empty()).Value;
    }
}
