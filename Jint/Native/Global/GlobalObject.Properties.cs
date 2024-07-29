using Jint.Collections;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Global;

public partial class GlobalObject
{
    private static readonly Key propertyAggregateError = "AggregateError";
    private static readonly Key propertyArray = "Array";
    private static readonly Key propertyArrayBuffer = "ArrayBuffer";
    private static readonly Key propertyAtomics = "Atomics";
    private static readonly Key propertyBigInt = "BigInt";
    private static readonly Key propertyBigInt64Array = "BigInt64Array";
    private static readonly Key propertyBigUint64Array = "BigUint64Array";
    private static readonly Key propertyBoolean = "Boolean";
    private static readonly Key propertyDataView = "DataView";
    private static readonly Key propertyDate = "Date";
    private static readonly Key propertyError = "Error";
    private static readonly Key propertyEvalError = "EvalError";
    private static readonly Key propertyFinalizationRegistry = "FinalizationRegistry";
    private static readonly Key propertyFloat16Array = "Float16Array";
    private static readonly Key propertyFloat32Array = "Float32Array";
    private static readonly Key propertyFloat64Array = "Float64Array";
    private static readonly Key propertyFunction = "Function";
    private static readonly Key propertyGeneratorFunction = "Generator";
    private static readonly Key propertyInt16Array = "Int16Array";
    private static readonly Key propertyInt32Array = "Int32Array";
    private static readonly Key propertyInt8Array = "Int8Array";
    //private static readonly Key propertyIntl = "Intl";
    private static readonly Key propertyJSON = "JSON";
    private static readonly Key propertyMap = "Map";
    private static readonly Key propertyMath = "Math";
    private static readonly Key propertyNumber = "Number";
    private static readonly Key propertyObject = "Object";
    private static readonly Key propertyPromise = "Promise";
    private static readonly Key propertyProxy = "Proxy";
    private static readonly Key propertyRangeError = "RangeError";
    private static readonly Key propertyReferenceError = "ReferenceError";
    private static readonly Key propertyReflect = "Reflect";
    private static readonly Key propertyRegExp = "RegExp";
    private static readonly Key propertySet = "Set";
    private static readonly Key propertyShadowRealm = "ShadowRealm";
    private static readonly Key propertySharedArrayBuffer = "SharedArrayBuffer";
    private static readonly Key propertyString = "String";
    private static readonly Key propertySymbol = "Symbol";
    private static readonly Key propertySyntaxError = "SyntaxError";
    private static readonly Key propertyTypeError = "TypeError";
    private static readonly Key propertyTypedArray = "TypedArray";
    private static readonly Key propertyURIError = "URIError";
    private static readonly Key propertyUint16Array = "Uint16Array";
    private static readonly Key propertyUint32Array = "Uint32Array";
    private static readonly Key propertyUint8Array = "Uint8Array";
    private static readonly Key propertyUint8ClampedArray = "Uint8ClampedArray";
    private static readonly Key propertyWeakMap = "WeakMap";
    private static readonly Key propertyWeakRef = "WeakRef";
    private static readonly Key propertyWeakSet = "WeakSet";
    private static readonly Key propertyNaN = "NaN";
    private static readonly Key propertyInfinity = "Infinity";
    private static readonly Key propertyUndefined = "undefined";
    private static readonly Key propertyParseInt = "parseInt";
    private static readonly Key propertyParseFloat = "parseFloat";
    private static readonly Key propertyIsNaN = "isNaN";
    private static readonly Key propertyIsFinite = "isFinite";
    private static readonly Key propertyDecodeURI = "decodeURI";
    private static readonly Key propertyDecodeURIComponent = "decodeURIComponent";
    private static readonly Key propertyEncodeURI = "encodeURI";
    private static readonly Key propertyEncodeURIComponent = "encodeURIComponent";
    private static readonly Key propertyEscape = "escape";
    private static readonly Key propertyUnescape = "unescape";
    private static readonly Key propertyGlobalThis = "globalThis";
    private static readonly Key propertyEval = "eval";
    private static readonly Key propertyToString = "toString";

    private static readonly PropertyDescriptor _propertyDescriptorNan = new(JsNumber.DoubleNaN, PropertyFlag.AllForbidden);
    private static readonly PropertyDescriptor _propertyDescriptorPositiveInfinity = new(JsNumber.DoublePositiveInfinity, PropertyFlag.AllForbidden);
    private static readonly PropertyDescriptor _propertyDescriptorUndefined = new(Undefined, PropertyFlag.AllForbidden);

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;

        var properties = new StringDictionarySlim<PropertyDescriptor>(65);
        properties.AddDangerous(propertyAggregateError, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.AggregateError, PropertyFlags));
        properties.AddDangerous(propertyArray, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Array, PropertyFlags));
        properties.AddDangerous(propertyArrayBuffer, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.ArrayBuffer, PropertyFlags));
        properties.AddDangerous(propertyAtomics, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Atomics, PropertyFlags));
        properties.AddDangerous(propertyBigInt, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.BigInt, PropertyFlags));
        properties.AddDangerous(propertyBigInt64Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.BigInt64Array, PropertyFlags));
        properties.AddDangerous(propertyBigUint64Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.BigUint64Array, PropertyFlags));
        properties.AddDangerous(propertyBoolean, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Boolean, PropertyFlags));
        properties.AddDangerous(propertyDataView, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.DataView, PropertyFlags));
        properties.AddDangerous(propertyDate, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Date, PropertyFlags));
        properties.AddDangerous(propertyError, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Error, PropertyFlags));
        properties.AddDangerous(propertyEvalError, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.EvalError, PropertyFlags));
        properties.AddDangerous(propertyFinalizationRegistry, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.FinalizationRegistry, PropertyFlags));
        properties.AddDangerous(propertyFloat16Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Float16Array, PropertyFlags));
        properties.AddDangerous(propertyFloat32Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Float32Array, PropertyFlags));
        properties.AddDangerous(propertyFloat64Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Float64Array, PropertyFlags));
        properties.AddDangerous(propertyFunction, new PropertyDescriptor(_realm.Intrinsics.Function, PropertyFlags));
        properties.AddDangerous(propertyGeneratorFunction, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.GeneratorFunction, PropertyFlags));
        properties.AddDangerous(propertyInt16Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Int16Array, PropertyFlags));
        properties.AddDangerous(propertyInt32Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Int32Array, PropertyFlags));
        properties.AddDangerous(propertyInt8Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Int8Array, PropertyFlags));
        // TODO properties.AddDapropertygerous(propertyIntl, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Intl, propertyFlags));
        properties.AddDangerous(propertyJSON, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Json, PropertyFlags));
        properties.AddDangerous(propertyMap, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Map, PropertyFlags));
        properties.AddDangerous(propertyMath, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Math, PropertyFlags));
        properties.AddDangerous(propertyNumber, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Number, PropertyFlags));
        properties.AddDangerous(propertyObject, new PropertyDescriptor(_realm.Intrinsics.Object, PropertyFlags));
        properties.AddDangerous(propertyPromise, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Promise, PropertyFlags));
        properties.AddDangerous(propertyProxy, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Proxy, PropertyFlags));
        properties.AddDangerous(propertyRangeError, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.RangeError, PropertyFlags));
        properties.AddDangerous(propertyReferenceError, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.ReferenceError, PropertyFlags));
        properties.AddDangerous(propertyReflect, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Reflect, PropertyFlags));
        properties.AddDangerous(propertyRegExp, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.RegExp, PropertyFlags));
        properties.AddDangerous(propertySet, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Set, PropertyFlags));
        properties.AddDangerous(propertyShadowRealm, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.ShadowRealm, PropertyFlags));
        properties.AddDangerous(propertySharedArrayBuffer, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.SharedArrayBuffer, PropertyFlags));
        properties.AddDangerous(propertyString, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.String, PropertyFlags));
        properties.AddDangerous(propertySymbol, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Symbol, PropertyFlags));
        properties.AddDangerous(propertySyntaxError, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.SyntaxError, PropertyFlags));
        properties.AddDangerous(propertyTypeError, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.TypeError, PropertyFlags));
        properties.AddDangerous(propertyTypedArray, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.TypedArray, PropertyFlags));
        properties.AddDangerous(propertyURIError, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.UriError, PropertyFlags));
        properties.AddDangerous(propertyUint16Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Uint16Array, PropertyFlags));
        properties.AddDangerous(propertyUint32Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Uint32Array, PropertyFlags));
        properties.AddDangerous(propertyUint8Array, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Uint8Array, PropertyFlags));
        properties.AddDangerous(propertyUint8ClampedArray, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Uint8ClampedArray, PropertyFlags));
        properties.AddDangerous(propertyWeakMap, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.WeakMap, PropertyFlags));
        properties.AddDangerous(propertyWeakRef, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.WeakRef, PropertyFlags));
        properties.AddDangerous(propertyWeakSet, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.WeakSet, PropertyFlags));

        properties.AddDangerous(propertyNaN, _propertyDescriptorNan);
        properties.AddDangerous(propertyInfinity, _propertyDescriptorPositiveInfinity);
        properties.AddDangerous(propertyUndefined, _propertyDescriptorUndefined);
        properties.AddDangerous(propertyParseInt, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "parseInt", ParseInt, 2, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyParseFloat, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "parseFloat", ParseFloat, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyIsNaN, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "isNaN", IsNaN, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyIsFinite, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "isFinite", IsFinite, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyDecodeURI, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "decodeURI", global.DecodeUri, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyDecodeURIComponent, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "decodeURIComponent", global.DecodeUriComponent, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyEncodeURI, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "encodeURI", global.EncodeUri, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyEncodeURIComponent, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "encodeURIComponent", global.EncodeUriComponent, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyEscape, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "escape", global.Escape, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyUnescape, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "unescape", global.Unescape, 1, LengthFlags), PropertyFlags));
        properties.AddDangerous(propertyGlobalThis, new PropertyDescriptor(this, PropertyFlags));
        properties.AddDangerous(propertyEval, new LazyPropertyDescriptor<GlobalObject>(this, static global => global._realm.Intrinsics.Eval, PropertyFlag.Configurable | PropertyFlag.Writable));

        // toString is not mentioned or actually required in spec, but some tests rely on it
        properties.AddDangerous(propertyToString, new LazyPropertyDescriptor<GlobalObject>(this, static global => new ClrFunction(global._engine, "toString", global.ToStringString, 1), PropertyFlags));

        SetProperties(properties);
    }
}
