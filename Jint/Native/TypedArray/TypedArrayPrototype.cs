using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.TypedArray;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-typedarray-prototype-objects
/// </summary>
[JsObject]
internal sealed partial class TypedArrayPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TypedArrayConstructor _constructor;

    [JsProperty(Name = "BYTES_PER_ELEMENT", Flags = PropertyFlag.AllForbidden)]
    private readonly JsNumber _bytesPerElement;

    internal TypedArrayPrototype(
        Engine engine,
        IntrinsicTypedArrayPrototype objectPrototype,
        TypedArrayConstructor constructor,
        TypedArrayElementType type) : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
        _bytesPerElement = JsNumber.Create(type.GetElementSize());
    }

    protected override void Initialize() => CreateProperties_Generated();
}
