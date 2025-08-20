using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Disposable;

internal enum DisposableState
{
    Pending,
    Disposed,
}

internal sealed class DisposableStack : ObjectInstance
{
    internal readonly DisposeHint _hint;
    private DisposeCapability _disposeCapability;

    public DisposableStack(Engine engine, DisposeHint hint) : base(engine)
    {
        _hint = hint;
        State = DisposableState.Pending;
        _disposeCapability = new DisposeCapability(engine);
    }

    public DisposableState State { get; private set; }

    public JsValue Dispose()
    {
        if (State == DisposableState.Disposed)
        {
            return Undefined;
        }

        State = DisposableState.Disposed;
        var completion = _disposeCapability.DisposeResources(new Completion(CompletionType.Normal, Undefined, _engine.GetLastSyntaxElement()));
        if (completion.Type == CompletionType.Throw)
        {
            Throw.JavaScriptException(_engine, completion.Value, completion);
        }
        return completion.Value;
    }

    public void Defer(JsValue onDispose)
    {
        AddDisposableResource(Undefined, _hint, onDispose.GetCallable(_engine.Realm));
    }

    public JsValue Use(JsValue value)
    {
        AddDisposableResource(value, _hint);
        return value;
    }

    public JsValue Adopt(JsValue value, JsValue onDispose)
    {
        AssertNotDisposed();

        var callable = onDispose.GetCallable(_engine.Realm);
        JsCallDelegate closure = (_, _) =>
        {
            callable.Call(Undefined, value);
            return Undefined;
        };

        var f = new ClrFunction(_engine, string.Empty, closure);
        AddDisposableResource(Undefined, DisposeHint.Sync, f);

        return value;
    }

    public JsValue Move(DisposableStack newDisposableStack)
    {
        AssertNotDisposed();

        newDisposableStack.State = DisposableState.Pending;
        newDisposableStack._disposeCapability = this._disposeCapability;
        this._disposeCapability = new DisposeCapability(_engine);
        State = DisposableState.Disposed;
        return newDisposableStack;
    }

    private void AddDisposableResource(JsValue v, DisposeHint hint, ICallable? method = null)
    {
        AssertNotDisposed();
        _disposeCapability.AddDisposableResource(v, hint, method);
    }

    private void AssertNotDisposed()
    {
        if (State == DisposableState.Disposed)
        {
            Throw.ReferenceError(_engine.Realm, "Stack already disposed.");
        }
    }
}
