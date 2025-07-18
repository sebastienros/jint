using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%iteratorprototype%-object
/// </summary>
internal class IteratorPrototype : Prototype
{
    internal IteratorPrototype(
        Engine engine,
        Realm realm,
        Prototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        var symbols = new SymbolDictionary(2)
        {
            [GlobalSymbolRegistry.Iterator] = new(new ClrFunction(Engine, "[Symbol.iterator]", ToIterator, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
            [GlobalSymbolRegistry.Dispose] = new(new ClrFunction(Engine, "[Symbol.dispose]", Dispose, 0, PropertyFlag.Configurable), PropertyFlag.NonEnumerable),
        };
        SetSymbols(symbols);
    }

    private static JsValue ToIterator(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }

    private static JsValue Dispose(JsValue thisObject, JsCallArguments arguments)
    {
        var method = thisObject.AsObject().GetMethod(CommonProperties.Return);
        if (method is not null)
        {
            method.Call(thisObject, arguments);
        }

        return Undefined;
    }

    internal JsValue Next(JsValue thisObject, JsCallArguments arguments)
    {
        var iterator = thisObject as IteratorInstance;
        if (iterator is null)
        {
            Throw.TypeError(_engine.Realm);
        }

        iterator.TryIteratorStep(out var result);
        return result;
    }
}
