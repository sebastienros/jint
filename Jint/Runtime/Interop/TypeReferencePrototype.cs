using Jint.Native.Object;

namespace Jint.Runtime.Interop;

internal sealed class TypeReferencePrototype : ObjectInstance
{
    public TypeReferencePrototype(Engine engine, TypeReference typeReference) : base(engine)
    {
        TypeReference = typeReference;
        _prototype = engine.Realm.Intrinsics.Object.PrototypeObject;
    }

    public TypeReference TypeReference { get; }
}
