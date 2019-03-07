
using System.Runtime.CompilerServices;

namespace System
{
    internal static class ArrayExt
    {
        private static class EmptyArray<T>
        {
            public static readonly T[] Value;

            static EmptyArray()
            {
                Value = new T[0];
            }
        }

        #if NETSTANDARD
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Empty<T>() => Array.Empty<T>();
        #else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Empty<T>() => EmptyArray<T>.Value;
        #endif
    }
}