using Jint.Collections;
using Jint.Runtime.Descriptors;

namespace Jint.Native.TypedArray;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-typedarray-prototype-objects
/// </summary>
internal sealed class TypedArrayPrototype : Prototype
{
    private readonly TypedArrayConstructor _constructor;
    private readonly TypedArrayElementType _arrayElementType;

    internal TypedArrayPrototype(
        Engine engine,
        IntrinsicTypedArrayPrototype objectPrototype,
        TypedArrayConstructor constructor,
        TypedArrayElementType type) : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
        _arrayElementType = type;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["BYTES_PER_ELEMENT"] = new(JsNumber.Create(_arrayElementType.GetElementSize()), PropertyFlag.AllForbidden),
            ["constructor"] = new(_constructor, PropertyFlag.NonEnumerable),
        };
        SetProperties(properties);
    }
}
