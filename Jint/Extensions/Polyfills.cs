using System.Globalization;

namespace Jint;

internal static class Polyfills
{
#if NETFRAMEWORK || NETSTANDARD2_0
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(this string source, char c) => source.IndexOf(c) != -1;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool StartsWith(this string source, char c) => source.Length > 0 && source[0] == c;
#endif

#if NETFRAMEWORK
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(this ReadOnlySpan<string> source, string c) => source.IndexOf(c) != -1;
#endif
}

public static class Int32Extensions
{
    extension(int)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static bool TryParse(ReadOnlySpan<char> span, NumberStyles style, IFormatProvider provider, out int value)
        {
            return int.TryParse(span.ToString(), style, provider, out value);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD
        public static int Parse(ReadOnlySpan<char> span, IFormatProvider? provider = null)
        {
            return int.Parse(span.ToString(), NumberStyles.Integer, provider);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
        public static int Parse(ReadOnlySpan<char> span, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return int.Parse(span.ToString(), style, provider);
        }
#endif
    }
}

public static class Int64Extensions
{
    extension(long)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static bool TryParse(ReadOnlySpan<char> span, NumberStyles style, IFormatProvider formatProvider, out long value)
        {
            return long.TryParse(span.ToString(), style, formatProvider, out value);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD
        public static long Parse(ReadOnlySpan<char> span, IFormatProvider? provider = null)
        {
            return long.Parse(span.ToString(), NumberStyles.Integer, provider);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
        public static long Parse(ReadOnlySpan<char> span, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return long.Parse(span.ToString(), style, provider);
        }
#endif
    }
}

public static class DoubleExtensions
{
    extension(double)
    {
#if NETFRAMEWORK || NETSTANDARD
        public static double Parse(ReadOnlySpan<char> span, IFormatProvider? provider = null)
        {
            return double.Parse(span.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
        public static double Parse(ReadOnlySpan<char> span, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider? provider = null)
        {
            return double.Parse(span.ToString(), style, provider);
        }
#endif
    }
}
