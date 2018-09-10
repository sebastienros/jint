
namespace System
{
    internal static class ArrayExt
    {
        private static class EmptyArray<T>
        {
            public static readonly T[] Value;

            static EmptyArray()
            {
                EmptyArray<T>.Value = new T[0];
            }
        }

        public static T[] Empty<T>() => EmptyArray<T>.Value;
    }
}