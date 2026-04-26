using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.FinalizationRegistry;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-finalization-registry-prototype-object
/// </summary>
[JsObject]
internal sealed partial class FinalizationRegistryPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly FinalizationRegistryConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString FinalizationRegistryToStringTag = new("FinalizationRegistry");

    public FinalizationRegistryPrototype(
        Engine engine,
        Realm realm,
        FinalizationRegistryConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-finalization-registry.prototype.register
    /// </summary>
    [JsFunction(Length = 2)]
    private JsValue Register(JsValue thisObject, JsValue target, JsValue heldValue, JsValue unregisterToken)
    {
        var finalizationRegistry = AssertFinalizationRegistryInstance(thisObject);

        if (!target.CanBeHeldWeakly(_engine.GlobalSymbolRegistry))
        {
            Throw.TypeError(_realm, "target must be an object or symbol");
        }

        if (SameValue(target, heldValue))
        {
            Throw.TypeError(_realm, "target and holdings must not be same");
        }

        if (!unregisterToken.CanBeHeldWeakly(_engine.GlobalSymbolRegistry))
        {
            if (!unregisterToken.IsUndefined())
            {
                Throw.TypeError(_realm, unregisterToken + " must be an object");
            }
        }

        var cell = new Cell(target, heldValue, unregisterToken);
        finalizationRegistry.AddCell(cell);
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-finalization-registry.prototype.unregister
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Unregister(JsValue thisObject, JsValue unregisterToken)
    {
        var finalizationRegistry = AssertFinalizationRegistryInstance(thisObject);

        if (!unregisterToken.CanBeHeldWeakly(_engine.GlobalSymbolRegistry))
        {
            Throw.TypeError(_realm, unregisterToken + " must be an object or symbol");
        }

        return finalizationRegistry.Remove(unregisterToken);
    }

    private FinalizationRegistryInstance AssertFinalizationRegistryInstance(JsValue thisObject)
    {
        if (thisObject is FinalizationRegistryInstance finalizationRegistryInstance)
        {
            return finalizationRegistryInstance;
        }

        Throw.TypeError(_realm, "object must be a FinalizationRegistry");
        return default;
    }
}
