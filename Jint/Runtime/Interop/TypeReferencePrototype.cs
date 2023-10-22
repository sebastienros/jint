using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop;

internal sealed class TypeReferencePrototype : ObjectInstance
{
    public TypeReferencePrototype(Engine engine, TypeReference typeReference) : base(engine)
    {
        TypeReference = typeReference;
        _prototype = engine.Realm.Intrinsics.Object.PrototypeObject;
    }

    public TypeReference TypeReference { get; }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        var descriptor = TypeReference.GetOwnProperty(property);
        if (descriptor != PropertyDescriptor.Undefined)
        {
            return descriptor;
        }
        return base.GetOwnProperty(property);
    }
}
