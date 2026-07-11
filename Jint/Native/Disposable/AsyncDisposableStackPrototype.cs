using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Disposable;

// Spec requires AsyncDisposableStack.prototype[@@asyncDispose] to be the same function object as
// AsyncDisposableStack.prototype.disposeAsync; [JsSymbolAlias] shares the materialized function.
[JsSymbolAlias("AsyncDispose", "disposeAsync")]
[JsObject(UseShape = true)]
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
    }

    [JsFunction]
    private JsValue Adopt(JsValue thisObject, JsValue value, JsValue onDispose)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.Adopt(value, onDispose);
    }

    [JsFunction]
    private JsValue Defer(JsValue thisObject, JsValue onDispose)
    {
        var stack = AssertDisposableStack(thisObject);
        stack.Defer(onDispose);
        return Undefined;
    }

    [JsFunction(Name = "disposeAsync")]
    private JsValue Dispose(JsValue thisObject)
    {
        // Per spec: create promise capability first, then validate receiver.
        // If validation fails, reject the promise instead of throwing synchronously.
        var capability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        try
        {
            var stack = AssertDisposableStack(thisObject);
            if (!stack.TryMarkDisposed())
            {
                // Already disposed — resolve with undefined.
                capability.Resolve.Call(Undefined, Undefined);
                return capability.PromiseInstance;
            }

            // Drive the dispose state machine via Promise.then chains so each
            // spec-defined Await(...) consumes a real microtask tick. Settlement
            // of the returned promise is deferred until the dispose chain finishes.
            DisposeResourcesHelper.DisposeAndThen(
                _engine,
                stack.DisposeCapability,
                new Completion(CompletionType.Normal, Undefined, _engine.GetLastSyntaxElement()),
                final =>
                {
                    if (final.Type == CompletionType.Throw)
                    {
                        capability.Reject.Call(Undefined, final.Value);
                    }
                    else
                    {
                        capability.Resolve.Call(Undefined, Undefined);
                    }
                });
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

    [JsFunction]
    private JsValue Move(JsValue thisObject)
    {
        var stack = AssertDisposableStack(thisObject);
        var newDisposableStack = _engine.Intrinsics.Function.OrdinaryCreateFromConstructor(
            _engine.Intrinsics.AsyncDisposableStack,
            static intrinsics => intrinsics.AsyncDisposableStack.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new DisposableStack(engine, DisposeHint.Async));

        return stack.Move(newDisposableStack);
    }

    [JsFunction]
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
