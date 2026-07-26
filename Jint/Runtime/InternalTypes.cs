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
    // a shape-mode JsObject that is still being built up incrementally (a hot constructor's `this`), so a
    // brand-new property grows the shape via a transition. Plain shaped objects (object literals) lack this
    // flag and deopt to a dictionary when they gain a key, since they aren't a reused allocation site.
    ShapeBuilding = 131072,
    // the object overrides [[Get]] / [[GetOwnProperty]] with non-ordinary semantics (Proxy, TypedArray
    // integer-index access, IteratorResult). The prototype-method inline cache skips such receivers and
    // prototypes so it never bypasses their custom property resolution.
    ExoticGet = 262144,
    // a built-in whose string-keyed own properties live in a shared BuiltinShape + a per-realm descriptor
    // array reached via IBuiltinShaped, not _properties. Set when the shape is installed, cleared on deopt
    // to dictionary mode. Mutually exclusive with ShapeMode; lets ObjectInstance's property virtuals
    // discriminate built-in-shape vs dictionary storage with a single flag test on the already-loaded _type.
    BuiltinShapeMode = 524288,
    // the value implements ICallable. Set by every ICallable root (Function, BindFunction,
    // IsHTMLDDA, JsProxy) so a call site can decide callability with a flag test on the
    // already-loaded _type plus an Unsafe.As, instead of an `is ICallable` interface-map scan —
    // measured at 1.2% of dromaeo-object-string-modern, all of it from JintCallExpression, which
    // tests it twice per call. Note this is strictly "implements ICallable", NOT "is callable":
    // a JsProxy over a non-callable target carries the flag and reports IsCallable == false,
    // matching what `is ICallable` answers today.
    Callable = 1048576,
    // the value is a Function. Implies Callable, and narrows it: the other ICallable roots
    // (BindFunction, IsHTMLDDA, JsProxy, NamespaceReference) do not carry it. Function is an
    // abstract class with many subclasses, so `is Function` costs a CastHelpers.IsInstanceOfClass
    // hierarchy walk — the last such walk left on the call-dispatch path, where it decides whether
    // the callee gets a call-stack frame.
    Function = 2097152,
    // the object promises ORDINARY [[Get]] semantics even though it is not a PlainObject: Get(p, receiver)
    // returns exactly UnwrapJsValue(GetOwnProperty(p), receiver) for an existing own property and otherwise
    // walks the prototype chain. Declared by a host-defined subclass through
    // ObjectInstance.SetPropertyAccessSemantics; never inferred. Unlike PlainObject (a *storage* claim, which
    // lets the engine read _properties / the shape directly and so cannot be honoured by an object that
    // projects properties lazily from native state) this is purely a *semantics* claim, so the interpreter
    // may resolve a read from a single GetOwnProperty probe. Mutually exclusive with ExoticGet.
    OrdinaryGet = 4194304,

    Primitive = Boolean | String | Number | Integer | BigInt | Symbol,
    InternalFlags = ObjectEnvironmentRecord | RequiresCloning | PlainObject | Array | Module | IsHTMLDDA | ShapeMode | ShapeBuilding | ExoticGet | BuiltinShapeMode | Callable | Function | OrdinaryGet
}
