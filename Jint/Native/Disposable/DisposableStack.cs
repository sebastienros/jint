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

    /// <summary>
    /// Exposes the internal dispose capability so async callers (AsyncDisposableStack)
    /// can drive the state machine via <see cref="DisposeResourcesHelper"/>.
    /// </summary>
    internal DisposeCapability DisposeCapability => _disposeCapability;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-disposablestack.prototype.dispose, run in <paramref name="realm"/>, the realm of
    /// the built-in that was called: a <c>SuppressedError</c> raised while disposing belongs to it, whichever realm
    /// called the built-in or created the stack.
    /// </summary>
    public JsValue Dispose(Realm realm)
    {
        if (State == DisposableState.Disposed)
        {
            return Undefined;
        }

        State = DisposableState.Disposed;
        var completion = _disposeCapability.DisposeResources(new Completion(CompletionType.Normal, Undefined, _engine.GetLastSyntaxElement()), realm);
        if (completion.Type == CompletionType.Throw)
        {
            Throw.JavaScriptException(_engine, completion.Value, in completion);
        }
        return completion.Value;
    }

    /// <summary>
    /// Marks the stack disposed and returns the dispose state machine's initial step
    /// for the async path. The caller (AsyncDisposableStack.prototype.disposeAsync)
    /// drives the state machine via <see cref="DisposeResourcesHelper"/> so that each
    /// spec-defined Await consumes a real microtask tick.
    /// Returns null if the stack was already disposed.
    /// </summary>
    internal bool TryMarkDisposed()
    {
        if (State == DisposableState.Disposed)
        {
            return false;
        }
        State = DisposableState.Disposed;
        return true;
    }

    // The methods below take the realm of the built-in that was called, the current Realm Record of its steps, for the
    // errors they raise: it is not the running realm when another realm's built-in is applied to this stack.

    public void Defer(JsValue onDispose, Realm realm)
    {
        AssertNotDisposed(realm);
        AddDisposableResource(Undefined, _hint, realm, onDispose.GetCallable(realm));
    }

    public JsValue Use(JsValue value, Realm realm)
    {
        AssertNotDisposed(realm);
        AddDisposableResource(value, _hint, realm);
        return value;
    }

    public JsValue Adopt(JsValue value, JsValue onDispose, Realm realm)
    {
        AssertNotDisposed(realm);

        var callable = onDispose.GetCallable(realm);
        JsCallDelegate closure = (_, _) =>
        {
            callable.Call(Undefined, value);
            return Undefined;
        };

        var f = new ClrFunction(_engine, string.Empty, closure);
        AddDisposableResource(Undefined, DisposeHint.Sync, realm, f);

        return value;
    }

    public JsValue Move(DisposableStack newDisposableStack, Realm realm)
    {
        AssertNotDisposed(realm);

        newDisposableStack.State = DisposableState.Pending;
        newDisposableStack._disposeCapability = this._disposeCapability;
        this._disposeCapability = new DisposeCapability(_engine);
        State = DisposableState.Disposed;
        return newDisposableStack;
    }

    private void AddDisposableResource(JsValue v, DisposeHint hint, Realm realm, ICallable? method = null)
    {
        _disposeCapability.AddDisposableResource(v, hint, realm, method);
    }

    private void AssertNotDisposed(Realm realm)
    {
        if (State == DisposableState.Disposed)
        {
            Throw.ReferenceError(realm, "Stack already disposed.");
        }
    }
}
