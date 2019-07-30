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
            if (value._type != Types.Boolean)
            {
                ExceptionHelper.ThrowArgumentException($"Expected boolean but got {value._type}");
            }

            return ((JsBoolean) value)._value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double AsNumber(this JsValue value)
        {
            if (value._type != Types.Number)
            {
                ExceptionHelper.ThrowArgumentException($"Expected number but got {value._type}");
            }

            return ((JsNumber) value)._value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string AsString(this JsValue value)
        {
            if (value._type != Types.String)
            {
                ExceptionHelper.ThrowArgumentException($"Expected string but got {value._type}");
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
            if (value._type != Types.Symbol)
            {
                ExceptionHelper.ThrowArgumentException($"Expected symbol but got {value._type}");
            }

            return ((JsSymbol) value)._value;
        }
    }
}