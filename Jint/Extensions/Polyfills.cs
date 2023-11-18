namespace Jint;

internal static class Polyfills
{
#if NETFRAMEWORK || NETSTANDARD2_0
    internal static bool Contains(this string source, char c) => source.IndexOf(c) != -1;
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
    internal static bool StartsWith(this string source, char c) => source.Length > 0 && source[0] == c;
#endif
}
