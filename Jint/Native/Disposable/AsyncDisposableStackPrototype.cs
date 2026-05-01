using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Disposable;

[JsObject]
internal sealed partial class AsyncDisposableStackPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly AsyncDisposableStackConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString AsyncDisposableStackToStringTag = new("AsyncDisposableStack");

    internal AsyncDisposableStackPrototype(
        Engine engine,
        Realm realm,
        AsyncDisposableStackConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Spec requires AsyncDisposableStack.prototype[@@asyncDispose] to be the same function object
        // as AsyncDisposableStack.prototype.disposeAsync. Alias the descriptor here so the
        // @@asyncDispose slot shares the same materialized function as `disposeAsync`.
        SetProperty(GlobalSymbolRegistry.AsyncDispose, GetOwnProperty("disposeAsync"));
    }

    [JsFunction(Length = 2)]
    private JsValue Adopt(JsValue thisObject, JsValue value, JsValue onDispose)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.Adopt(value, onDispose);
    }

    [JsFunction(Length = 1)]
    private JsValue Defer(JsValue thisObject, JsValue onDispose)
    {
        var stack = AssertDisposableStack(thisObject);
        stack.Defer(onDispose);
        return Undefined;
    }

    [JsFunction(Length = 0, Name = "disposeAsync")]
    private JsValue Dispose(JsValue thisObject)
    {
        // Per spec: create promise capability first, then validate receiver.
        // If validation fails, reject the promise instead of throwing synchronously.
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        try
        {
            var stack = AssertDisposableStack(thisObject);
            var result = stack.Dispose();
            capability.Resolve.Call(Undefined, result);
        }
        catch (JavaScriptException e)
        {
            capability.Reject.Call(Undefined, e.Error);
        }
        return capability.PromiseInstance;
    }

    [JsAccessor("disposed")]
    private JsBoolean Disposed(JsValue thisObject)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.State == DisposableState.Disposed ? JsBoolean.True : JsBoolean.False;
    }

    [JsFunction(Length = 0)]
    private JsValue Move(JsValue thisObject)
    {
        var stack = AssertDisposableStack(thisObject);
        var newDisposableStack = _engine.Intrinsics.Function.OrdinaryCreateFromConstructor(
            _engine.Intrinsics.AsyncDisposableStack,
            static intrinsics => intrinsics.AsyncDisposableStack.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new DisposableStack(engine, DisposeHint.Async));

        return stack.Move(newDisposableStack);
    }

    [JsFunction(Length = 1)]
    private JsValue Use(JsValue thisObject, JsValue value)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.Use(value);
    }

    private DisposableStack AssertDisposableStack(JsValue thisObject)
    {
        if (thisObject is not DisposableStack { _hint: DisposeHint.Async } stack)
        {
            Throw.TypeError(_engine.Realm, "This is not a AsyncDisposableStack instance.");
            return null!;
        }

        return stack;
    }
}
