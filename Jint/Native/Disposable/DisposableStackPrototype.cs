using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Disposable;

internal sealed class DisposableStackPrototype : Prototype
{
    private readonly DisposableStackConstructor _constructor;

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
        var disposeFunction = new ClrFunction(Engine, "dispose", Dispose, 0, PropertyFlag.Configurable);

        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(8, checkExistingKeys: false)
        {
            ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["adopt"] = new PropertyDescriptor(new ClrFunction(Engine, "adopt", Adopt, 2, PropertyFlag.Configurable), PropertyFlags),
            ["defer"] = new PropertyDescriptor(new ClrFunction(Engine, "defer", Defer, 1, PropertyFlag.Configurable), PropertyFlags),
            ["dispose"] = new PropertyDescriptor(disposeFunction, PropertyFlags),
            ["disposed"] = new GetSetPropertyDescriptor(get: new ClrFunction(Engine, "get disposed", Disposed, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlags),
            ["move"] = new PropertyDescriptor(new ClrFunction(Engine, "move", Move, 0, PropertyFlag.Configurable), PropertyFlags),
            ["use"] = new PropertyDescriptor(new ClrFunction(Engine, "use", Use, 1, PropertyFlag.Configurable), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(2)
        {
            [GlobalSymbolRegistry.Dispose] = new PropertyDescriptor(disposeFunction, PropertyFlags),
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("DisposableStack", PropertyFlag.Configurable),
        };
        SetSymbols(symbols);
    }


    private JsValue Adopt(JsValue thisObject, JsCallArguments arguments)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.Adopt(arguments.At(0), arguments.At(1));
    }

    private JsValue Defer(JsValue thisObject, JsCallArguments arguments)
    {
        var stack = AssertDisposableStack(thisObject);
        stack.Defer(arguments.At(0));
        return Undefined;
    }

    private JsValue Dispose(JsValue thisObject, JsCallArguments arguments)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.Dispose();
    }

    private JsValue Disposed(JsValue thisObject, JsCallArguments arguments)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.State == DisposableState.Disposed;
    }

    private JsValue Move(JsValue thisObject, JsCallArguments arguments)
    {
        var stack = AssertDisposableStack(thisObject);
        var newDisposableStack = _engine.Intrinsics.Function.OrdinaryCreateFromConstructor(
            _engine.Intrinsics.DisposableStack,
            static intrinsics => intrinsics.DisposableStack.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new DisposableStack(engine, DisposeHint.Sync));

        return stack.Move(newDisposableStack);
    }

    private JsValue Use(JsValue thisObject, JsCallArguments arguments)
    {
        var stack = AssertDisposableStack(thisObject);
        return stack.Use(arguments.At(0));
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
