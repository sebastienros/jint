using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using Esprima;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Date;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.RegExp;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint
{
    public static class JsValueExtensions
    {
        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPrimitive(this JsValue value)
        {
            return (value._type & (InternalTypes.Primitive | InternalTypes.Undefined | InternalTypes.Null)) != 0;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUndefined(this JsValue value)
        {
            return value._type == InternalTypes.Undefined;
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
            return value is DateInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPromise(this JsValue value)
        {
            return value is PromiseInstance;
        }

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

            return value is RegExpInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsObject(this JsValue value)
        {
            return (value._type & InternalTypes.Object) != 0;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsString(this JsValue value)
        {
            return (value._type & InternalTypes.String) != 0;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNumber(this JsValue value)
        {
            return (value._type & (InternalTypes.Number | InternalTypes.Integer)) != 0;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBigInt(this JsValue value)
        {
            return (value._type & InternalTypes.BigInt) != 0;
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
        public static DateInstance AsDate(this JsValue value)
        {
            if (!value.IsDate())
            {
                ExceptionHelper.ThrowArgumentException("The value is not a date");
            }

            return (DateInstance) value;
        }

        [Pure]
        public static RegExpInstance AsRegExp(this JsValue value)
        {
            if (!value.IsRegExp())
            {
                ExceptionHelper.ThrowArgumentException("The value is not a regex");
            }

            return (RegExpInstance) value;
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
        public static ArrayInstance AsArray(this JsValue value)
        {
            if (!value.IsArray())
            {
                ExceptionHelper.ThrowArgumentException("The value is not an array");
            }

            return (ArrayInstance) value;
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
        public static bool IsUint8Array(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.Uint8 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] AsUint8Array(this JsValue value)
        {
            if (!value.IsUint8Array())
            {
                ThrowWrongTypeException(value, "Uint8Array");
            }

            return ((TypedArrayInstance) value).ToNativeArray<byte>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUint8ClampedArray(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.Uint8C };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] AsUint8ClampedArray(this JsValue value)
        {
            if (!value.IsUint8ClampedArray())
            {
                ThrowWrongTypeException(value, "Uint8ClampedArray");
            }

            return ((TypedArrayInstance) value).ToNativeArray<byte>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInt8Array(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.Int8 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static sbyte[] AsInt8Array(this JsValue value)
        {
            if (!value.IsInt8Array())
            {
                ThrowWrongTypeException(value, "Int8Array");
            }

            return ((TypedArrayInstance) value).ToNativeArray<sbyte>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInt16Array(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.Int16 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short[] AsInt16Array(this JsValue value)
        {
            if (!value.IsInt16Array())
            {
                ThrowWrongTypeException(value, "Int16Array");
            }

            return ((TypedArrayInstance) value).ToNativeArray<short>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUint16Array(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.Uint16 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort[] AsUint16Array(this JsValue value)
        {
            if (!value.IsUint16Array())
            {
                ThrowWrongTypeException(value, "Uint16Array");
            }

            return ((TypedArrayInstance) value).ToNativeArray<ushort>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInt32Array(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.Int32 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int[] AsInt32Array(this JsValue value)
        {
            if (!value.IsInt32Array())
            {
                ThrowWrongTypeException(value, "Int32Array");
            }

            return ((TypedArrayInstance) value).ToNativeArray<int>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUint32Array(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.Uint32 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint[] AsUint32Array(this JsValue value)
        {
            if (!value.IsUint32Array())
            {
                ThrowWrongTypeException(value, "Uint32Array");
            }

            return ((TypedArrayInstance) value).ToNativeArray<uint>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBigInt64Array(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.BigInt64 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long[] AsBigInt64Array(this JsValue value)
        {
            if (!value.IsBigInt64Array())
            {
                ThrowWrongTypeException(value, "BigInt64Array");
            }

            return ((TypedArrayInstance) value).ToNativeArray<long>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBigUint64Array(this JsValue value)
        {
            return value is TypedArrayInstance { _arrayElementType: TypedArrayElementType.BigUint64 };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong[] AsBigUint64Array(this JsValue value)
        {
            if (!value.IsBigUint64Array())
            {
                ThrowWrongTypeException(value, "BigUint64Array");
            }

            return ((TypedArrayInstance) value).ToNativeArray<ulong>();
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
        public static FunctionInstance AsFunctionInstance(this JsValue value)
        {
            if (value is not FunctionInstance instance)
            {
                ThrowWrongTypeException(value, "FunctionInstance");
                return null!;
            }

            return instance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsValue Call(this JsValue value, params JsValue[] arguments)
        {
            if (value is not ObjectInstance objectInstance)
            {
                ExceptionHelper.ThrowArgumentException(value + " is not object");
                return null;
            }

            return objectInstance.Engine.Call(value, arguments);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsValue Call(this JsValue value, JsValue thisObj, params JsValue[] arguments)
        {
            if (value is not ObjectInstance objectInstance)
            {
                ExceptionHelper.ThrowArgumentException(value + " is not object");
                return null;
            }

            return objectInstance.Engine.Call(value, thisObj, arguments);
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
            if (value is PromiseInstance promise)
            {
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
            catch (ParserException ex)
            {
                ExceptionHelper.ThrowSyntaxError(engine.Realm, ex.Message);
                return default;
            }
        }
    }
}
