using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Global;

// 56 lazy realm-intrinsic constructor properties on globalThis. Each emits a LazyPropertyDescriptor
// whose factory body is `host => host._realm.Intrinsics.<IntrinsicMember>` — the constructor object
// allocates only on first read. Sorted by JsName to match the generator's emit order. Casing
// overrides via IntrinsicMember:
//   "JSON"     → Intrinsics.Json
//   "URIError" → Intrinsics.UriError
//   "Generator" → Intrinsics.GeneratorFunction
//   "eval"     → Intrinsics.Eval
[JsIntrinsicReference("AggregateError")]
[JsIntrinsicReference("Array")]
[JsIntrinsicReference("ArrayBuffer")]
[JsIntrinsicReference("AsyncDisposableStack")]
[JsIntrinsicReference("AsyncIterator")]
[JsIntrinsicReference("Atomics")]
[JsIntrinsicReference("BigInt")]
[JsIntrinsicReference("BigInt64Array")]
[JsIntrinsicReference("BigUint64Array")]
[JsIntrinsicReference("Boolean")]
[JsIntrinsicReference("DataView")]
[JsIntrinsicReference("Date")]
[JsIntrinsicReference("DisposableStack")]
[JsIntrinsicReference("Error")]
[JsIntrinsicReference("EvalError")]
[JsIntrinsicReference("FinalizationRegistry")]
[JsIntrinsicReference("Float16Array")]
[JsIntrinsicReference("Float32Array")]
[JsIntrinsicReference("Float64Array")]
[JsIntrinsicReference("Function")]
[JsIntrinsicReference("Generator", IntrinsicMember = "GeneratorFunction")]
[JsIntrinsicReference("Int16Array")]
[JsIntrinsicReference("Int32Array")]
[JsIntrinsicReference("Int8Array")]
[JsIntrinsicReference("Intl")]
[JsIntrinsicReference("Iterator")]
[JsIntrinsicReference("JSON", IntrinsicMember = "Json")]
[JsIntrinsicReference("Map")]
[JsIntrinsicReference("Math")]
[JsIntrinsicReference("Number")]
[JsIntrinsicReference("Object")]
[JsIntrinsicReference("Promise")]
[JsIntrinsicReference("Proxy")]
[JsIntrinsicReference("RangeError")]
[JsIntrinsicReference("ReferenceError")]
[JsIntrinsicReference("Reflect")]
[JsIntrinsicReference("RegExp")]
[JsIntrinsicReference("Set")]
[JsIntrinsicReference("ShadowRealm")]
[JsIntrinsicReference("SharedArrayBuffer")]
[JsIntrinsicReference("String")]
[JsIntrinsicReference("SuppressedError")]
[JsIntrinsicReference("Symbol")]
[JsIntrinsicReference("SyntaxError")]
[JsIntrinsicReference("Temporal")]
[JsIntrinsicReference("TypeError")]
[JsIntrinsicReference("TypedArray")]
[JsIntrinsicReference("URIError", IntrinsicMember = "UriError")]
[JsIntrinsicReference("Uint16Array")]
[JsIntrinsicReference("Uint32Array")]
[JsIntrinsicReference("Uint8Array")]
[JsIntrinsicReference("Uint8ClampedArray")]
[JsIntrinsicReference("WeakMap")]
[JsIntrinsicReference("WeakRef")]
[JsIntrinsicReference("WeakSet")]
[JsIntrinsicReference("eval", IntrinsicMember = "Eval")]
public partial class GlobalObject
{
    [JsProperty(Name = "NaN", Flags = PropertyFlag.AllForbidden)]
    private static readonly JsNumber NaNValue = JsNumber.DoubleNaN;

    [JsProperty(Name = "Infinity", Flags = PropertyFlag.AllForbidden)]
    private static readonly JsNumber InfinityValue = JsNumber.DoublePositiveInfinity;

    [JsProperty(Name = "undefined", Flags = PropertyFlag.AllForbidden)]
    private static readonly JsValue UndefinedValue = JsValue.Undefined;

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        CreateProperties_Generated();

        // After CreateProperties_Generated, _properties is the HybridDictionary wrapper around the
        // generator-built StringDictionarySlim. AddDangerous skips both duplicate-key lookup and
        // SetOwnProperty's validation path. These 3 entries are new keys (not overwriting), so the
        // direct path is correct. [JsObject(ExtraCapacity = 3)] presizes the underlying slim dict
        // so the adds don't trigger a resize.
        // parseInt / parseFloat are kept hand-rolled because spec requires
        // Number.parseInt === parseInt (and Number.parseFloat === parseFloat). Pre-source-gen Jint
        // satisfied that via ClrFunction.Equals comparing the underlying delegate; both globalThis
        // and NumberConstructor wrap the same GlobalObject.ParseInt/ParseFloat static method.
        // Migrating to [JsFunction] would emit a distinct __GlobalObjectFunction dispatcher and
        // break the strict-equality test. NumberConstructor.cs:50 documents the reciprocal half.
        // globalThis is a self-reference (the GlobalObject instance itself), which can't be
        // expressed as a static [JsProperty] field or as an intrinsic.
        _properties!.AddDangerous("parseInt", new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "parseInt", ParseInt, 2, LengthFlags), PropertyFlags));
        _properties.AddDangerous("parseFloat", new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "parseFloat", ParseFloat, 1, LengthFlags), PropertyFlags));
        _properties.AddDangerous("globalThis", new PropertyDescriptor(this, PropertyFlags));
    }
}
