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

    Primitive = Boolean | String | Number | Integer | BigInt | Symbol,
    InternalFlags = ObjectEnvironmentRecord | RequiresCloning | PlainObject | Array | Module
}
