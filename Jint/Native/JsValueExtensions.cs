using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;
using FunctionInstance = Jint.Native.Function.Function;

namespace Jint.Native;

/// <summary>
/// The specialised half of <see cref="JsValue"/>'s vocabulary: reading a binary buffer, narrowing to one of
/// Jint's own runtime types, and calling a value.
/// </summary>
/// <remarks>
/// <para>
/// What a value <em>is</em> — <see cref="JsValue.IsString"/>, <see cref="JsValue.IsObject"/> and their
/// eleven siblings — and what is in it — <see cref="JsValue.AsString"/>, <see cref="JsValue.TryGetNumber"/>,
/// <see cref="JsValue.UnwrapIfPromise()"/> — are members of <see cref="JsValue"/> itself. What stays here
/// decodes a typed array, narrows to a type only Jint declares, or invokes something.
/// </para>
/// <para>
/// This class is in <see cref="JsValue"/>'s own namespace, so one <c>using Jint.Native;</c> reaches both
/// halves.
/// </para>
/// </remarks>
public static class JsValueExtensions
{
    /// <summary>
    /// Returns whether this value is a primitive, meaning anything that is not an object.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrimitive(this JsValue value)
    {
        return (value._type & (InternalTypes.Primitive | InternalTypes.Undefined | InternalTypes.Null)) != InternalTypes.Empty;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNullOrUndefined(this JsValue value)
    {
        return value._type < InternalTypes.Boolean;
    }

    /// <summary>
    /// Returns whether this value is a private class member name, which only class bodies produce.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrivateName(this JsValue value) => value._type == InternalTypes.PrivateName;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsInteger(this JsValue value)
    {
        return value._type == InternalTypes.Integer;
    }

    /// <summary>
    /// Returns whether this value can be used with <c>new</c>, which an arrow function and a method cannot.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsConstructor(this JsValue value)
    {
        return value.IsConstructor;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-canbeheldweakly
    /// Every Object, and every Symbol that is not a registered symbol -- one appended to the
    /// GlobalSymbolRegistry by Symbol.for, which lives as long as the agent does and so can never
    /// be collected. Sharing a description with a registered symbol does not make one.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanBeHeldWeakly(this JsValue value)
    {
        return value.IsObject() || (value.IsSymbol() && GlobalSymbolRegistry.KeyForSymbol(value).IsUndefined());
    }

    /// <summary>
    /// Returns this value as a <see cref="JsDate"/>, throwing when it is not one.
    /// </summary>
    /// <param name="value">The value to narrow.</param>
    /// <exception cref="ArgumentException">The value is not a <c>Date</c>.</exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsDate AsDate(this JsValue value)
    {
        if (!value.IsDate())
        {
            Throw.ArgumentException("The value is not a date");
        }

        return (JsDate) value;
    }

    /// <summary>
    /// Returns this value as a <see cref="JsRegExp"/>, throwing when it is not a regular expression.
    /// </summary>
    /// <param name="value">The value to narrow.</param>
    /// <exception cref="ArgumentException">The value is not a regular expression.</exception>
    [Pure]
    public static JsRegExp AsRegExp(this JsValue value)
    {
        if (!value.IsRegExp())
        {
            Throw.ArgumentException("The value is not a regex");
        }

        return (JsRegExp) value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AsInteger(this JsValue value)
    {
        return (int) ((JsNumber) value)._value;
    }

    /// <summary>
    /// Reinterprets a value the caller has already proven to be an object with <see cref="JsValue.IsObject"/>.
    /// <see cref="InternalTypes.Object"/> is set by <see cref="ObjectInstance"/>'s constructors and
    /// nowhere else — the same invariant <see cref="JsValue.IsObject"/> and <see cref="JsValue.AsObject"/>
    /// already rely on — so this is exactly as sound as `is ObjectInstance` but costs a flag test instead of
    /// a <c>CastHelpers.IsInstanceOfClass</c> hierarchy walk. Mirrors <see cref="JsValue.AsNumber"/>'s shape.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ObjectInstance AsObjectNoTypeCheck(this JsValue value)
    {
        Debug.Assert(value.IsObject() && value is ObjectInstance);
        return Unsafe.As<ObjectInstance>(value);
    }

    /// <summary>
    /// Reinterprets a value the caller has already proven to be a string with <see cref="JsValue.IsString"/>.
    /// <see cref="InternalTypes.String"/> is set only by <see cref="JsString"/> and its two nested
    /// subclasses (ConcatenatedString, SlicedString), all of which are <see cref="JsString"/>.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JsString AsStringNoTypeCheck(this JsValue value)
    {
        Debug.Assert(value.IsString() && value is JsString);
        return Unsafe.As<JsString>(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BigInteger AsBigInt(this JsValue value)
    {
        return ((JsBigInt) value)._value;
    }

    /// <summary>
    /// Returns whether this value is an <c>ArrayBuffer</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsArrayBuffer(this JsValue value)
    {
        return value is JsArrayBuffer;
    }

    /// <summary>
    /// Returns the bytes an <c>ArrayBuffer</c> holds, or <see langword="null"/> when it has been detached.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not an <c>ArrayBuffer</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[]? AsArrayBuffer(this JsValue value)
    {
        if (!value.IsArrayBuffer())
        {
            ThrowWrongTypeException(value, "ArrayBuffer");
        }

        return ((JsArrayBuffer) value)._arrayBufferData;
    }

    /// <summary>
    /// Returns whether this value is a <c>DataView</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDataView(this JsValue value)
    {
        return value is JsDataView;
    }

    /// <summary>
    /// Returns a copy of the bytes a <c>DataView</c> sees, or <see langword="null"/> when its buffer is gone.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>DataView</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[]? AsDataView(this JsValue value)
    {
        if (!value.IsDataView())
        {
            ThrowWrongTypeException(value, "DataView");
        }

        var dataView = (JsDataView) value;

        if (dataView._viewedArrayBuffer?._arrayBufferData == null)
        {
            return null; // should not happen
        }

        // create view
        var res = new byte[dataView._byteLength];
        System.Array.Copy(dataView._viewedArrayBuffer._arrayBufferData!, dataView._byteOffset, res, 0, dataView._byteLength);
        return res;
    }

    /// <summary>
    /// Returns whether this value is a <c>Uint8Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUint8Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8 };
    }

    /// <summary>
    /// Returns a copy of a <c>Uint8Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>Uint8Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] AsUint8Array(this JsValue value)
    {
        if (!value.IsUint8Array())
        {
            ThrowWrongTypeException(value, "Uint8Array");
        }

        return ((JsTypedArray) value).ToNativeArray<byte>();
    }

    /// <summary>
    /// Returns whether this value is a <c>Uint8ClampedArray</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUint8ClampedArray(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8C };
    }

    /// <summary>
    /// Returns a copy of a <c>Uint8ClampedArray</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>Uint8ClampedArray</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] AsUint8ClampedArray(this JsValue value)
    {
        if (!value.IsUint8ClampedArray())
        {
            ThrowWrongTypeException(value, "Uint8ClampedArray");
        }

        return ((JsTypedArray) value).ToNativeArray<byte>();
    }

    /// <summary>
    /// Returns whether this value is an <c>Int8Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInt8Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Int8 };
    }

    /// <summary>
    /// Returns a copy of an <c>Int8Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not an <c>Int8Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte[] AsInt8Array(this JsValue value)
    {
        if (!value.IsInt8Array())
        {
            ThrowWrongTypeException(value, "Int8Array");
        }

        return ((JsTypedArray) value).ToNativeArray<sbyte>();
    }

    /// <summary>
    /// Returns whether this value is an <c>Int16Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInt16Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Int16 };
    }

    /// <summary>
    /// Returns a copy of an <c>Int16Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not an <c>Int16Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short[] AsInt16Array(this JsValue value)
    {
        if (!value.IsInt16Array())
        {
            ThrowWrongTypeException(value, "Int16Array");
        }

        return ((JsTypedArray) value).ToNativeArray<short>();
    }

    /// <summary>
    /// Returns whether this value is a <c>Uint16Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUint16Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Uint16 };
    }

    /// <summary>
    /// Returns a copy of a <c>Uint16Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>Uint16Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort[] AsUint16Array(this JsValue value)
    {
        if (!value.IsUint16Array())
        {
            ThrowWrongTypeException(value, "Uint16Array");
        }

        return ((JsTypedArray) value).ToNativeArray<ushort>();
    }

    /// <summary>
    /// Returns whether this value is an <c>Int32Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInt32Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Int32 };
    }

    /// <summary>
    /// Returns a copy of an <c>Int32Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not an <c>Int32Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int[] AsInt32Array(this JsValue value)
    {
        if (!value.IsInt32Array())
        {
            ThrowWrongTypeException(value, "Int32Array");
        }

        return ((JsTypedArray) value).ToNativeArray<int>();
    }

    /// <summary>
    /// Returns whether this value is a <c>Uint32Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUint32Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Uint32 };
    }

    /// <summary>
    /// Returns a copy of a <c>Uint32Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>Uint32Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint[] AsUint32Array(this JsValue value)
    {
        if (!value.IsUint32Array())
        {
            ThrowWrongTypeException(value, "Uint32Array");
        }

        return ((JsTypedArray) value).ToNativeArray<uint>();
    }

    /// <summary>
    /// Returns whether this value is a <c>BigInt64Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBigInt64Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.BigInt64 };
    }

    /// <summary>
    /// Returns a copy of a <c>BigInt64Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>BigInt64Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long[] AsBigInt64Array(this JsValue value)
    {
        if (!value.IsBigInt64Array())
        {
            ThrowWrongTypeException(value, "BigInt64Array");
        }

        return ((JsTypedArray) value).ToNativeArray<long>();
    }

    /// <summary>
    /// Returns whether this value is a <c>BigUint64Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBigUint64Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.BigUint64 };
    }

    /// <summary>
    /// Returns a copy of a <c>BigUint64Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>BigUint64Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong[] AsBigUint64Array(this JsValue value)
    {
        if (!value.IsBigUint64Array())
        {
            ThrowWrongTypeException(value, "BigUint64Array");
        }

        return ((JsTypedArray) value).ToNativeArray<ulong>();
    }

    /// <summary>
    /// Returns whether this value is a <c>Float16Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFloat16Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Float16 };
    }

#if SUPPORTS_HALF
    /// <summary>
    /// Returns a copy of a <c>Float16Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>Float16Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half[] AsFloat16Array(this JsValue value)
    {
        if (!value.IsFloat16Array())
        {
            ThrowWrongTypeException(value, "Float16Array");
        }

        return ((JsTypedArray) value).ToNativeArray<Half>();
    }
#endif

    /// <summary>
    /// Returns whether this value is a <c>Float32Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFloat32Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Float32 };
    }

    /// <summary>
    /// Returns a copy of a <c>Float32Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>Float32Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float[] AsFloat32Array(this JsValue value)
    {
        if (!value.IsFloat32Array())
        {
            ThrowWrongTypeException(value, "Float32Array");
        }

        return ((JsTypedArray) value).ToNativeArray<float>();
    }

    /// <summary>
    /// Returns whether this value is a <c>Float64Array</c>.
    /// </summary>
    /// <param name="value">The value to test.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFloat64Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Float64 };
    }

    /// <summary>
    /// Returns a copy of a <c>Float64Array</c>'s elements as a CLR array.
    /// </summary>
    /// <param name="value">The value to read.</param>
    /// <exception cref="ArgumentException">The value is not a <c>Float64Array</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double[] AsFloat64Array(this JsValue value)
    {
        if (!value.IsFloat64Array())
        {
            ThrowWrongTypeException(value, "Float64Array");
        }

        return ((JsTypedArray) value).ToNativeArray<double>();
    }

    /// <summary>
    /// Returns this value as the <see cref="Jint.Native.Function.Function"/> it already is, throwing when it
    /// is not one.
    /// </summary>
    /// <param name="value">The value to narrow.</param>
    /// <exception cref="ArgumentException">The value is not a function object.</exception>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FunctionInstance AsFunctionInstance(this JsValue value)
    {
        if (value is not FunctionInstance instance)
        {
            ThrowWrongTypeException(value, "FunctionInstance");
            return null!;
        }

        return instance;
    }

    /// <summary>
    /// Calls this value with no arguments and <c>undefined</c> as <c>this</c>.
    /// </summary>
    /// <param name="value">The function object to call.</param>
    /// <returns>What the call returned.</returns>
    /// <exception cref="ArgumentException">The value is not an object.</exception>
    [Pure]
    public static JsValue Call(this JsValue value)
    {
        if (value is ObjectInstance objectInstance)
        {
            var engine = objectInstance.Engine;
            return engine.Call(value, System.Array.Empty<JsValue>());
        }

        return ThrowNotObject(value);
    }

    /// <summary>
    /// Calls this value with one argument and <c>undefined</c> as <c>this</c>.
    /// </summary>
    /// <param name="value">The function object to call.</param>
    /// <param name="arg1">The first argument.</param>
    /// <returns>What the call returned.</returns>
    /// <exception cref="ArgumentException">The value is not an object.</exception>
    [Pure]
    public static JsValue Call(this JsValue value, JsValue arg1)
    {
        if (value is ObjectInstance objectInstance)
        {
            var engine = objectInstance.Engine;
            var arguments = engine._jsValueArrayPool.RentArray(1);
            arguments[0] = arg1;
            var result = engine.Call(value, arguments);
            engine._jsValueArrayPool.ReturnArray(arguments);
            return result;
        }

        return ThrowNotObject(value);
    }

    /// <summary>
    /// Calls this value with two arguments and <c>undefined</c> as <c>this</c>.
    /// </summary>
    /// <param name="value">The function object to call.</param>
    /// <param name="arg1">The first argument.</param>
    /// <param name="arg2">The second argument.</param>
    /// <returns>What the call returned.</returns>
    /// <exception cref="ArgumentException">The value is not an object.</exception>
    [Pure]
    public static JsValue Call(this JsValue value, JsValue arg1, JsValue arg2)
    {
        if (value is ObjectInstance objectInstance)
        {
            var engine = objectInstance.Engine;
            var arguments = engine._jsValueArrayPool.RentArray(2);
            arguments[0] = arg1;
            arguments[1] = arg2;
            var result = engine.Call(value, arguments);
            engine._jsValueArrayPool.ReturnArray(arguments);
            return result;
        }

        return ThrowNotObject(value);
    }

    /// <summary>
    /// Calls this value with three arguments and <c>undefined</c> as <c>this</c>.
    /// </summary>
    /// <param name="value">The function object to call.</param>
    /// <param name="arg1">The first argument.</param>
    /// <param name="arg2">The second argument.</param>
    /// <param name="arg3">The third argument.</param>
    /// <returns>What the call returned.</returns>
    /// <exception cref="ArgumentException">The value is not an object.</exception>
    [Pure]
    public static JsValue Call(this JsValue value, JsValue arg1, JsValue arg2, JsValue arg3)
    {
        if (value is ObjectInstance objectInstance)
        {
            var engine = objectInstance.Engine;
            var arguments = engine._jsValueArrayPool.RentArray(3);
            arguments[0] = arg1;
            arguments[1] = arg2;
            arguments[2] = arg3;
            var result = engine.Call(value, arguments);
            engine._jsValueArrayPool.ReturnArray(arguments);
            return result;
        }

        return ThrowNotObject(value);
    }

    /// <summary>
    /// Calls this value with the given arguments and <c>undefined</c> as <c>this</c>.
    /// </summary>
    /// <param name="value">The function object to call.</param>
    /// <param name="arguments">The arguments to pass.</param>
    /// <returns>What the call returned.</returns>
    /// <exception cref="ArgumentException">The value is not an object.</exception>
    [Pure]
    public static JsValue Call(this JsValue value, params JsCallArguments arguments)
    {
        if (value is ObjectInstance objectInstance)
        {
            return objectInstance.Engine.Call(value, arguments);
        }

        return ThrowNotObject(value);
    }

    /// <summary>
    /// Calls this value with the given <c>this</c> and arguments.
    /// </summary>
    /// <param name="value">The function object to call.</param>
    /// <param name="thisObj">The value to use as <c>this</c>.</param>
    /// <param name="arguments">The arguments to pass.</param>
    /// <returns>What the call returned.</returns>
    /// <exception cref="ArgumentException">The value is not an object.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsValue Call(this JsValue value, JsValue thisObj, JsCallArguments arguments)
    {
        if (value is ObjectInstance objectInstance)
        {
            return objectInstance.Engine.Call(value, thisObj, arguments);
        }

        return ThrowNotObject(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JsValue ThrowNotObject(JsValue value)
    {
        // Every caller reaches here from a failed `value is ObjectInstance`, so value is a primitive and
        // rendering it runs nothing; the safe renderer states that rather than relying on the reader to
        // re-derive it. Which value it was is the whole point of a host-facing ArgumentException.
        Throw.ArgumentException($"{Throw.SafeToDisplayString(value)} is not object");
        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowWrongTypeException(JsValue value, string expectedType)
    {
        Throw.ArgumentException($"Expected {expectedType} but got {value._type}");
    }

    internal static BigInteger ToBigInteger(this JsValue value, Engine engine)
    {
        try
        {
            return TypeConverter.ToBigInt(value);
        }
        catch (ParseErrorException ex)
        {
            Throw.SyntaxError(engine.Realm, ex.Message);
            return default;
        }
    }

    internal static ICallable GetCallable(this JsValue source, Realm realm)
    {
        if (source is ICallable callable)
        {
            return callable;
        }

        Throw.TypeError(realm, "Argument must be callable");
        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getarraybuffermaxbytelengthoption
    /// </summary>
    internal static uint? GetArrayBufferMaxByteLengthOption(this JsValue options)
    {
        if (options is not JsObject oi)
        {
            return null;
        }

        var maxByteLength = options.Get("maxByteLength");
        if (maxByteLength.IsUndefined())
        {
            return null;
        }

        return TypeConverter.ToIndex(oi.Engine.Realm, maxByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-canonicalize-keyed-collection-key
    /// </summary>
    internal static JsValue CanonicalizeKeyedCollectionKey(this JsValue key)
    {
        return key is JsNumber number && number.IsNegativeZero() ? JsNumber.PositiveZero : key;
    }
}
