using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
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
    internal static bool CanBeHeldWeakly(this JsValue value, GlobalSymbolRegistry symbolRegistry)
    {
        return value.IsObject() || (value.IsSymbol() && !symbolRegistry.ContainsCustom(value));
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsDate AsDate(this JsValue value)
    {
        if (!value.IsDate())
        {
            ExceptionHelper.ThrowArgumentException("The value is not a date");
        }

        return (JsDate) value;
    }

    [Pure]
    public static JsRegExp AsRegExp(this JsValue value)
    {
        if (!value.IsRegExp())
        {
            ExceptionHelper.ThrowArgumentException("The value is not a regex");
        }

        return (JsRegExp) value;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObjectInstance AsObject(this JsValue value)
    {
        if (!value.IsObject())
        {
            ExceptionHelper.ThrowArgumentException("The value is not an object");
        }

        return (ObjectInstance) value;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TInstance AsInstance<TInstance>(this JsValue value) where TInstance : class
    {
        if (!value.IsObject())
        {
            ExceptionHelper.ThrowArgumentException("The value is not an object");
        }

        return (value as TInstance)!;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsArray AsArray(this JsValue value)
    {
        if (!value.IsArray())
        {
            ExceptionHelper.ThrowArgumentException("The value is not an array");
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

        return ((JsNumber) value)._value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int AsInteger(this JsValue value)
    {
        return (int) ((JsNumber) value)._value;
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
    public static T? TryCast<T>(this JsValue value) where T : class
    {
        return value as T;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? TryCast<T>(this JsValue value, Action<JsValue> fail) where T : class
    {
        if (value is T o)
        {
            return o;
        }

        fail.Invoke(value);

        return null;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? As<T>(this JsValue value) where T : ObjectInstance
    {
        if (value.IsObject())
        {
            return value as T;
        }

        return null;
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
        ExceptionHelper.ThrowArgumentException(value + " is not object");
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
    /// <returns>inner value if Promise the value itself otherwise</returns>
    public static JsValue UnwrapIfPromise(this JsValue value)
    {
        if (value is JsPromise promise)
        {
            var engine = promise.Engine;
            var completedEvent = promise.CompletedEvent;
            engine.RunAvailableContinuations();
            completedEvent.Wait();
            switch (promise.State)
            {
                case PromiseState.Pending:
                    ExceptionHelper.ThrowInvalidOperationException("'UnwrapIfPromise' called before Promise was settled");
                    return null;
                case PromiseState.Fulfilled:
                    return promise.Value;
                case PromiseState.Rejected:
                    ExceptionHelper.ThrowPromiseRejectedException(promise.Value);
                    return null;
                default:
                    ExceptionHelper.ThrowArgumentOutOfRangeException();
                    return null;
            }
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowWrongTypeException(JsValue value, string expectedType)
    {
        ExceptionHelper.ThrowArgumentException($"Expected {expectedType} but got {value._type}");
    }

    internal static BigInteger ToBigInteger(this JsValue value, Engine engine)
    {
        try
        {
            return TypeConverter.ToBigInt(value);
        }
        catch (ParseErrorException ex)
        {
            ExceptionHelper.ThrowSyntaxError(engine.Realm, ex.Message);
            return default;
        }
    }

    internal static ICallable GetCallable(this JsValue source, Realm realm)
    {
        if (source is ICallable callable)
        {
            return callable;
        }

        ExceptionHelper.ThrowTypeError(realm, "Argument must be callable");
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
}
