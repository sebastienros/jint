using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint;

public static class JsValueExtensions
{
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrimitive(this JsValue value)
    {
        return (value._type & (InternalTypes.Primitive | InternalTypes.Undefined | InternalTypes.Null)) != InternalTypes.Empty;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUndefined(this JsValue value)
    {
        return value._type == InternalTypes.Undefined;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsArray(this JsValue value)
    {
        return value is JsArray;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsNullOrUndefined(this JsValue value)
    {
        return value._type < InternalTypes.Boolean;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDate(this JsValue value)
    {
        return value is JsDate;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPromise(this JsValue value)
    {
        return value is JsPromise;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrivateName(this JsValue value) => value._type == InternalTypes.PrivateName;

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRegExp(this JsValue value)
    {
        if (value is not ObjectInstance oi)
        {
            return false;
        }

        var matcher = oi.Get(GlobalSymbolRegistry.Match);
        if (!matcher.IsUndefined())
        {
            return TypeConverter.ToBoolean(matcher);
        }

        return value is JsRegExp;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsObject(this JsValue value)
    {
        return (value._type & InternalTypes.Object) != InternalTypes.Empty;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsString(this JsValue value)
    {
        return (value._type & InternalTypes.String) != InternalTypes.Empty;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNumber(this JsValue value)
    {
        return (value._type & (InternalTypes.Number | InternalTypes.Integer)) != InternalTypes.Empty;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBigInt(this JsValue value)
    {
        return (value._type & InternalTypes.BigInt) != InternalTypes.Empty;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsInteger(this JsValue value)
    {
        return value._type == InternalTypes.Integer;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBoolean(this JsValue value)
    {
        return value._type == InternalTypes.Boolean;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNull(this JsValue value)
    {
        return value._type == InternalTypes.Null;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSymbol(this JsValue value)
    {
        return value._type == InternalTypes.Symbol;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCallable(this JsValue value)
    {
        return value.IsCallable;
    }

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

    [Pure]
    public static JsRegExp AsRegExp(this JsValue value)
    {
        if (!value.IsRegExp())
        {
            Throw.ArgumentException("The value is not a regex");
        }

        return (JsRegExp) value;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObjectInstance AsObject(this JsValue value)
    {
        if (!value.IsObject())
        {
            Throw.ArgumentException("The value is not an object");
        }

        return (ObjectInstance) value;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsArray AsArray(this JsValue value)
    {
        if (!value.IsArray())
        {
            Throw.ArgumentException("The value is not an array");
        }

        return (JsArray) value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AsBoolean(this JsValue value)
    {
        if (value._type != InternalTypes.Boolean)
        {
            ThrowWrongTypeException(value, "boolean");
        }

        return ((JsBoolean) value)._value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AsNumber(this JsValue value)
    {
        if (!value.IsNumber())
        {
            ThrowWrongTypeException(value, "number");
        }

        Debug.Assert(value is JsNumber);
        return Unsafe.As<JsNumber>(value)._value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AsInteger(this JsValue value)
    {
        return (int) ((JsNumber) value)._value;
    }

    /// <summary>
    /// Reinterprets a value the caller has already proven to be an object with <see cref="IsObject"/>.
    /// <see cref="InternalTypes.Object"/> is set by <see cref="ObjectInstance"/>'s constructors and
    /// nowhere else — the same invariant <see cref="IsObject"/> and <see cref="AsObject"/> already rely
    /// on — so this is exactly as sound as `is ObjectInstance` but costs a flag test instead of a
    /// <c>CastHelpers.IsInstanceOfClass</c> hierarchy walk. Mirrors <see cref="AsNumber"/>'s shape.
    /// </summary>
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ObjectInstance AsObjectNoTypeCheck(this JsValue value)
    {
        Debug.Assert(value.IsObject() && value is ObjectInstance);
        return Unsafe.As<ObjectInstance>(value);
    }

    /// <summary>
    /// Reinterprets a value the caller has already proven to be a string with <see cref="IsString"/>.
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string AsString(this JsValue value)
    {
        if (!value.IsString())
        {
            ThrowWrongTypeException(value, "string");
        }

        return value.ToString();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsArrayBuffer(this JsValue value)
    {
        return value is JsArrayBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[]? AsArrayBuffer(this JsValue value)
    {
        if (!value.IsArrayBuffer())
        {
            ThrowWrongTypeException(value, "ArrayBuffer");
        }

        return ((JsArrayBuffer) value)._arrayBufferData;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDataView(this JsValue value)
    {
        return value is JsDataView;
    }

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
        Array.Copy(dataView._viewedArrayBuffer._arrayBufferData!, dataView._byteOffset, res, 0, dataView._byteLength);
        return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUint8Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] AsUint8Array(this JsValue value)
    {
        if (!value.IsUint8Array())
        {
            ThrowWrongTypeException(value, "Uint8Array");
        }

        return ((JsTypedArray) value).ToNativeArray<byte>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUint8ClampedArray(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8C };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] AsUint8ClampedArray(this JsValue value)
    {
        if (!value.IsUint8ClampedArray())
        {
            ThrowWrongTypeException(value, "Uint8ClampedArray");
        }

        return ((JsTypedArray) value).ToNativeArray<byte>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInt8Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Int8 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte[] AsInt8Array(this JsValue value)
    {
        if (!value.IsInt8Array())
        {
            ThrowWrongTypeException(value, "Int8Array");
        }

        return ((JsTypedArray) value).ToNativeArray<sbyte>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInt16Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Int16 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short[] AsInt16Array(this JsValue value)
    {
        if (!value.IsInt16Array())
        {
            ThrowWrongTypeException(value, "Int16Array");
        }

        return ((JsTypedArray) value).ToNativeArray<short>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUint16Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Uint16 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort[] AsUint16Array(this JsValue value)
    {
        if (!value.IsUint16Array())
        {
            ThrowWrongTypeException(value, "Uint16Array");
        }

        return ((JsTypedArray) value).ToNativeArray<ushort>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInt32Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Int32 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int[] AsInt32Array(this JsValue value)
    {
        if (!value.IsInt32Array())
        {
            ThrowWrongTypeException(value, "Int32Array");
        }

        return ((JsTypedArray) value).ToNativeArray<int>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUint32Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Uint32 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint[] AsUint32Array(this JsValue value)
    {
        if (!value.IsUint32Array())
        {
            ThrowWrongTypeException(value, "Uint32Array");
        }

        return ((JsTypedArray) value).ToNativeArray<uint>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBigInt64Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.BigInt64 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long[] AsBigInt64Array(this JsValue value)
    {
        if (!value.IsBigInt64Array())
        {
            ThrowWrongTypeException(value, "BigInt64Array");
        }

        return ((JsTypedArray) value).ToNativeArray<long>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBigUint64Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.BigUint64 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong[] AsBigUint64Array(this JsValue value)
    {
        if (!value.IsBigUint64Array())
        {
            ThrowWrongTypeException(value, "BigUint64Array");
        }

        return ((JsTypedArray) value).ToNativeArray<ulong>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFloat16Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Float16 };
    }

#if SUPPORTS_HALF
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFloat32Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Float32 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float[] AsFloat32Array(this JsValue value)
    {
        if (!value.IsFloat32Array())
        {
            ThrowWrongTypeException(value, "Float32Array");
        }

        return ((JsTypedArray) value).ToNativeArray<float>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFloat64Array(this JsValue value)
    {
        return value is JsTypedArray { _arrayElementType: TypedArrayElementType.Float64 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double[] AsFloat64Array(this JsValue value)
    {
        if (!value.IsFloat64Array())
        {
            ThrowWrongTypeException(value, "Float64Array");
        }

        return ((JsTypedArray) value).ToNativeArray<double>();
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Function AsFunctionInstance(this JsValue value)
    {
        if (value is not Function instance)
        {
            ThrowWrongTypeException(value, "FunctionInstance");
            return null!;
        }

        return instance;
    }

    [Pure]
    public static JsValue Call(this JsValue value)
    {
        if (value is ObjectInstance objectInstance)
        {
            var engine = objectInstance.Engine;
            return engine.Call(value, Array.Empty<JsValue>());
        }

        return ThrowNotObject(value);
    }

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

    [Pure]
    public static JsValue Call(this JsValue value, params JsCallArguments arguments)
    {
        if (value is ObjectInstance objectInstance)
        {
            return objectInstance.Engine.Call(value, arguments);
        }

        return ThrowNotObject(value);
    }

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

    /// <summary>
    /// If the value is a Promise
    ///     1. If "Fulfilled" returns the value it was fulfilled with
    ///     2. If "Rejected" throws "PromiseRejectedException" with the rejection reason
    ///     3. If "Pending" throws "InvalidOperationException". Should be called only in "Settled" state
    /// Else
    ///     returns the value intact
    /// </summary>
    /// <param name="value">value to unwrap</param>
    /// <remarks>
    /// The wait is bounded by the promise's own engine — <c>Options.Constraints.PromiseTimeout</c>, which
    /// defaults to ten seconds. Use the <see cref="UnwrapIfPromise(JsValue, TimeSpan)"/> overload for a
    /// bound that differs from the one the engine is configured with.
    /// </remarks>
    /// <returns>inner value if Promise the value itself otherwise</returns>
    public static JsValue UnwrapIfPromise(this JsValue value) => UnwrapIfPromiseCore(value, timeout: null, CancellationToken.None);

    /// <summary>
    /// If the value is a Promise
    ///     1. If "Fulfilled" returns the value it was fulfilled with
    ///     2. If "Rejected" throws "PromiseRejectedException" with the rejection reason
    ///     3. If "Pending" throws "InvalidOperationException". Should be called only in "Settled" state
    /// Else
    ///     returns the value intact
    /// </summary>
    /// <param name="value">value to unwrap</param>
    /// <param name="timeout">timeout to wait</param>
    /// <returns>inner value if Promise the value itself otherwise</returns>
    public static JsValue UnwrapIfPromise(this JsValue value, TimeSpan timeout)
        => UnwrapIfPromiseCore(value, timeout, CancellationToken.None);

    /// <summary>
    /// If the value is a Promise
    ///     1. If "Fulfilled" returns the value it was fulfilled with
    ///     2. If "Rejected" throws "PromiseRejectedException" with the rejection reason
    ///     3. If "Pending" throws "OperationCanceledException" if cancellation is requested
    /// Else
    ///     returns the value intact
    /// </summary>
    /// <param name="value">value to unwrap</param>
    /// <param name="cancellationToken">cancellation token to observe</param>
    /// <returns>inner value if Promise the value itself otherwise</returns>
    public static JsValue UnwrapIfPromise(this JsValue value, CancellationToken cancellationToken)
        => UnwrapIfPromiseCore(value, Timeout.InfiniteTimeSpan, cancellationToken);

    /// <summary>
    /// Asynchronously unwraps a <see cref="JsPromise"/> without blocking the calling thread.
    /// If the value is a Promise:
    ///     1. If "Fulfilled" returns the value it was fulfilled with
    ///     2. If "Rejected" throws <see cref="PromiseRejectedException"/> with the rejection reason
    ///     3. If "Pending" awaits settlement asynchronously
    /// Else
    ///     returns the value intact immediately.
    /// </summary>
    /// <param name="value">value to unwrap</param>
    /// <param name="cancellationToken">cancellation token to observe</param>
    /// <returns>A task that resolves to the inner value if the value is a Promise, or the value itself otherwise</returns>
    public static Task<JsValue> UnwrapIfPromiseAsync(this JsValue value, CancellationToken cancellationToken = default)
    {
        if (value is JsPromise promise)
        {
            return promise.Engine.UnwrapResultAsync(value, cancellationToken);
        }

        return Task.FromResult(value);
    }

    // A null timeout means "take the promise's own engine's configured Options.Constraints.PromiseTimeout";
    // a caller that named a bound gets exactly that bound, including Timeout.InfiniteTimeSpan. The engine is
    // only reachable once the value is known to be a promise, which is also the only case a bound applies to.
    private static JsValue UnwrapIfPromiseCore(JsValue value, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (value is JsPromise promise)
        {
            var effectiveTimeout = timeout ?? promise.Engine.Options.Constraints.PromiseTimeout;

            // Delegate to the engine's own drain rather than polling here. This used to be a
            // near-duplicate of it that predated EventLoop's work-arrived signal: it woke only on the
            // promise's own completion event, which a settle enqueued from a background thread never
            // sets, so every hop of an asynchronous chain idled out the full poll slice before this
            // thread ran the continuation that could advance it. DrainEventLoopUntilSettled waits on the
            // enqueue signal too - running that work on this thread is the only way the promise can
            // settle - and already carries the _waitingThreadId save/restore, its nesting, and the
            // engine's cancellation constraint.
            if (!promise.Engine.DrainEventLoopUntilSettled(promise, effectiveTimeout, cancellationToken))
            {
                Throw.PromiseRejectedException($"Timeout of {effectiveTimeout} reached");
            }

            switch (promise.State)
            {
                case PromiseState.Pending:
                    Throw.InvalidOperationException("'UnwrapIfPromise' called before Promise was settled");
                    return null;
                case PromiseState.Fulfilled:
                    return promise.Value;
                case PromiseState.Rejected:
                    Throw.PromiseRejectedException(promise.Value);
                    return null;
                default:
                    Throw.ArgumentOutOfRangeException();
                    return null;
            }
        }

        return value;
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
