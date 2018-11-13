namespace Jint.Collections
{
    internal static class HashHelpers
    {
        internal static readonly int[] DictionarySlimSizeOneIntArray = new int[1];

        internal static int PowerOf2(int v)
        {
            if ((v & (v - 1)) == 0) return v;
            int i = 2;
            while (i < v) i <<= 1;
            return i;
        }
    }
}