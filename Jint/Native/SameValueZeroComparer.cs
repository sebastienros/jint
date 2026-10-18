using System.Runtime.CompilerServices;

namespace Jint.Native;

internal sealed class SameValueZeroComparer : IEqualityComparer<JsValue>
{
    public static readonly SameValueZeroComparer Instance = new();

    bool IEqualityComparer<JsValue>.Equals(JsValue? x, JsValue? y)
    {
        return Equals(x, y);
    }

    public int GetHashCode(JsValue obj)
    {
        return obj.GetHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Equals(JsValue? x, JsValue? y)
    {
        return x == y || x is JsNumber xNum && y is JsNumber yNum && double.IsNaN(xNum._value) && double.IsNaN(yNum._value);
    }
}
