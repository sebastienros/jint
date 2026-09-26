using System.Diagnostics.CodeAnalysis;

namespace Jint.HtmlParser.InputValues.Dtoa;

// Same CLR exception kinds as the engine helper, without importing Jint.Runtime.
internal static class DtoaThrow
{
    [DoesNotReturn]
    internal static void ArgumentOutOfRangeException() => throw new ArgumentOutOfRangeException();
    [DoesNotReturn]
    internal static void InvalidOperationException() => throw new InvalidOperationException();
    [DoesNotReturn]
    internal static void NotImplementedException() => throw new NotImplementedException();
}
