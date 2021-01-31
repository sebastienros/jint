using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Native.TypedArray
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-typedarray-prototype-objects
    /// </summary>
    public sealed class TypedArrayPrototype : ObjectInstance
    {
        private readonly TypedArrayConstructor _constructor;
        private readonly TypedArrayElementType _arrayElementType;

        internal TypedArrayPrototype(
            Engine engine,
            IntrinsicTypedArrayPrototype objectPrototype,
            TypedArrayConstructor constructor,
            TypedArrayElementType type) : base(engine)
        {
            _prototype = objectPrototype;
            _constructor = constructor;
            _arrayElementType = type;
        }

        protected override void Initialize()
        {
            var properties = new PropertyDictionary(2, false)
            {
                ["BYTES_PER_ELEMENT"] = new(JsNumber.Create(_arrayElementType.GetElementSize()), PropertyFlag.AllForbidden),
                ["constructor"] = new(_constructor, PropertyFlag.NonEnumerable)
            };
            SetProperties(properties);
        }
    }
}