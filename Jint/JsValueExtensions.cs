using System.Runtime.CompilerServices;
using Jint.Native;
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
            if (value._type != InternalTypes.String)
            {
                ThrowWrongTypeException(value, "string");
            }

            return AsStringWithoutTypeCheck(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string AsStringWithoutTypeCheck(this JsValue value)
        {
            return value.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string AsSymbol(this JsValue value)
        {
            if (value._type != InternalTypes.Symbol)
            {
                ThrowWrongTypeException(value, "symbol");
            }

            return ((JsSymbol) value)._value;
        }

        private static void ThrowWrongTypeException(JsValue value, string expectedType)
        {
            ExceptionHelper.ThrowArgumentException($"Expected {expectedType} but got {value._type}");
        }
    }
}