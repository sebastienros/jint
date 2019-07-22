using System;
using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime
{
    public static class Arguments
    {
        public static readonly JsValue[] Empty = ArrayExt.Empty<JsValue>();

        public static JsValue[] From(params JsValue[] o)
        {
            return o;
        }

        /// <summary>
        /// Returns the arguments at the provided position or Undefined if not present
        /// </summary>
        /// <param name="args"></param>
        /// <param name="index">The index of the parameter to return</param>
        /// <param name="undefinedValue">The value to return is the parameter is not provided</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsValue At(this JsValue[] args, int index, JsValue undefinedValue)
        {
            return (uint) index < (uint) args.Length ? args[index] : undefinedValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsValue At(this JsValue[] args, int index)
        {
            return At(args, index, Undefined.Instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsValue[] Skip(this JsValue[] args, int count)
        {
            var newLength = args.Length - count;
            if (newLength <= 0)
            {
                return ArrayExt.Empty<JsValue>();
            }

            var array = new JsValue[newLength];
            Array.Copy(args, count, array, 0, newLength);
            return array;
        }
    }
}