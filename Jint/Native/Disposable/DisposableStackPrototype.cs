using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Disposable;

[JsObject]
internal sealed partial class DisposableStackPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly DisposableStackConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString DisposableStackToStringTag = new("DisposableStack");

    internal DisposableStackPrototype(
        Engine engine,
        Realm realm,
        DisposableStackConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Spec requires DisposableStack.prototype[@@dispose] to be the same function object as
        // DisposableStack.prototype.dispose. Alias the descriptor here so the @@dispose slot shares
        // the same materialized function as `dispose` rather than emitting a separate dispatcher.
        SetProperty(GlobalSymbolRegistry.Dispose, GetOwnProperty("dispose"));
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

    [JsFunction(Length = 0)]
    private JsValue Dispose(JsValue thisObject)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.Dispose();
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
            _engine.Intrinsics.DisposableStack,
            static intrinsics => intrinsics.DisposableStack.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new DisposableStack(engine, DisposeHint.Sync));

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
        if (thisObject is not DisposableStack { _hint: DisposeHint.Sync } stack)
        {
            Throw.TypeError(_engine.Realm, "This is not a DisposableStack instance.");
            return null!;
        }

        return stack;
    }
}
