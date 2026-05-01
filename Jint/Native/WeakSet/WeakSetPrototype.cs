#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using FunctionInstance = Jint.Native.Function.Function;

namespace Jint.Native.WeakSet;

/// <summary>
/// https://tc39.es/ecma262/#sec-weakset-objects
/// </summary>
[JsObject]
internal sealed partial class WeakSetPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WeakSetConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString WeakSetToStringTag = new("WeakSet");

    // Captured once Initialize runs; the WeakSet constructor's array fast path
    // identity-compares against this snapshot to detect a user-overridden `add`.
    internal FunctionInstance OriginalAddFunction { get; private set; } = null!;

    internal WeakSetPrototype(
        Engine engine,
        Realm realm,
        WeakSetConstructor constructor,
        ObjectPrototype prototype) : base(engine, realm)
    {
        _prototype = prototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Snapshot the prototype's `add` function before any user code can replace it,
        // so the constructor's array fast path can detect overrides via ReferenceEquals.
        OriginalAddFunction = (FunctionInstance) GetOwnProperty("add").Value!;
    }

    [JsFunction(Length = 1)]
    private JsValue Add(JsValue thisObject, JsValue value)
    {
        var set = AssertWeakSetInstance(thisObject);
        set.WeakSetAdd(value);
        return thisObject;
    }

    [JsFunction(Length = 1)]
    private JsValue Delete(JsValue thisObject, JsValue value)
    {
        var set = AssertWeakSetInstance(thisObject);
        return set.WeakSetDelete(value) ? JsBoolean.True : JsBoolean.False;
    }

    [JsFunction(Length = 1)]
    private JsValue Has(JsValue thisObject, JsValue value)
    {
        var set = AssertWeakSetInstance(thisObject);
        return set.WeakSetHas(value) ? JsBoolean.True : JsBoolean.False;
    }

    private JsWeakSet AssertWeakSetInstance(JsValue thisObject)
    {
        if (thisObject is JsWeakSet set)
        {
            return set;
        }

        Throw.TypeError(_realm, "object must be a WeakSet");
        return default;
    }
}
