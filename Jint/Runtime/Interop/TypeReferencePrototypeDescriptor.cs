using Jint.Native.Object;

namespace Jint.Runtime.Interop
{
    internal sealed class TypeReferencePrototypeDescriptor : ObjectInstance
    {
        private readonly TypeReference _typeReference;

        public TypeReferencePrototypeDescriptor(Engine engine, TypeReference typeReference) : base(engine)
        {
            _typeReference = typeReference;
            _prototype = engine.Realm.Intrinsics.Object.PrototypeObject;
        }

        public TypeReference TypeReference => _typeReference;
    }

}
