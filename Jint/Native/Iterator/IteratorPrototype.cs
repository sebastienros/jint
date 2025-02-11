using Jint.Collections;
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
        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Iterator] = new(new ClrFunction(Engine, "[Symbol.iterator]", ToIterator, 0, PropertyFlag.Configurable), true, false, true),
        };
        SetSymbols(symbols);
    }

    private static JsValue ToIterator(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }

    internal JsValue Next(JsValue thisObject, JsCallArguments arguments)
    {
        var iterator = thisObject as IteratorInstance;
        if (iterator is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        iterator.TryIteratorStep(out var result);
        return result;
    }
}
