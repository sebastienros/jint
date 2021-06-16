using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint
{
    public static class JsValueExtensions
    {
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
        public static string AsString(this JsValue value)
        {
            if (!value.IsString())
            {
                ThrowWrongTypeException(value, "string");
            }

            return value.ToString();
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
    }
}