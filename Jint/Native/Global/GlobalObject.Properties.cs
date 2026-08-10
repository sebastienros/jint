using Jint.Runtime.Descriptors;

namespace Jint.Native.Global;

// 58 lazy realm-intrinsic properties on globalThis. Each emits a LazyPropertyDescriptor whose factory
// body is `host => host._realm.Intrinsics.<IntrinsicMember>` — the object allocates only on first
// read. Sorted by JsName to match the generator's emit order. Casing overrides via IntrinsicMember:
//   "JSON"     → Intrinsics.Json
//   "URIError" → Intrinsics.UriError
//   "Generator" → Intrinsics.GeneratorFunction
//   "eval"     → Intrinsics.Eval
//   "parseInt" / "parseFloat" → Intrinsics.ParseInt / ParseFloat, the same two function objects
//   NumberConstructor installs (see NumberParseFunction)
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
[JsIntrinsicReference("parseFloat", IntrinsicMember = "ParseFloat")]
[JsIntrinsicReference("parseInt", IntrinsicMember = "ParseInt")]
[JsInstanceSlot("globalThis")]
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

        CreateProperties_Generated();

        // The one entry that can't be expressed declaratively fills the reserved [JsInstanceSlot]:
        // globalThis is a self-reference (the GlobalObject instance itself), which is neither a
        // static [JsProperty] field nor an intrinsic.
        SetBuiltinSlotByName("globalThis", new PropertyDescriptor(this, PropertyFlags));
    }
}
