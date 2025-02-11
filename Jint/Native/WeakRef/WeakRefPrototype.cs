using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.WeakRef;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-weak-ref-prototype-object
/// </summary>
internal sealed class WeakRefPrototype : Prototype
{
    private readonly WeakRefConstructor _constructor;

    internal WeakRefPrototype(
        Engine engine,
        Realm realm,
        WeakRefConstructor constructor,
        ObjectPrototype prototype) : base(engine, realm)
    {
        _prototype = prototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(5, checkExistingKeys: false)
        {
            ["constructor"] = new(_constructor, PropertyFlag.NonEnumerable),
            ["deref"] = new(new ClrFunction(Engine, "deref", Deref, 0, PropertyFlag.Configurable), propertyFlags)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("WeakRef", false, false, true)
        };
        SetSymbols(symbols);
    }

    private JsValue Deref(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject is JsWeakRef weakRef)
        {
            return weakRef.WeakRefDeref();
        }

        ExceptionHelper.ThrowTypeError(_realm, "object must be a WeakRef");
        return default;
    }
}
