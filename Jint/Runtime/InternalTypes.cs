namespace Jint.Runtime;

[Flags]
internal enum InternalTypes
{
    // should not be used, used for empty match
    Empty = 0,

    Undefined = 1,
    Null = 2,

    // primitive  types range start
    Boolean = 4,
    String = 8,
    Number = 16,
    Integer = 32,
    Symbol = 64,
    BigInt = 128,

    // primitive  types range end
    Object = 256,

    PrivateName = 512,

    // internal usage
    ObjectEnvironmentRecord = 1024,
    RequiresCloning = 2048,
    Module = 4096,

    // the object doesn't override important GetOwnProperty etc which change behavior
    PlainObject = 8192,
    // our native array
    Array = 16384,
    // IsHTMLDDA internal slot
    IsHTMLDDA = 32768,
    // the object is a JsObject currently in hidden-class shape mode (string-keyed own properties live in a
    // Shape + slot array, not _properties). Set when a shape store is installed, cleared on deopt to
    // dictionary mode. Lets the hot property paths discriminate shape vs dictionary storage with a single
    // flag test on the already-loaded _type instead of a `this is JsObject` type-check plus a field read.
    ShapeMode = 65536,

    Primitive = Boolean | String | Number | Integer | BigInt | Symbol,
    InternalFlags = ObjectEnvironmentRecord | RequiresCloning | PlainObject | Array | Module | IsHTMLDDA | ShapeMode
}
