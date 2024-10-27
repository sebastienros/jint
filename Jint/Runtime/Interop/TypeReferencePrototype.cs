using Jint.Collections;
using Jint.Native;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop;

internal sealed class TypeReferencePrototype : Prototype
{
    public TypeReferencePrototype(Engine engine, TypeReference typeReference) : base(engine, engine.Realm)
    {
        TypeReference = typeReference;
        _prototype = engine.Realm.Intrinsics.Object.PrototypeObject;

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor(typeReference.ReferenceType.Name, writable: false, enumerable: false, configurable: true),
        };
        SetSymbols(symbols);
    }

    public TypeReference TypeReference { get; }
}
