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

    /// <summary>
    /// Returns a form of <paramref name="value"/> that is safe to <em>store</em> as a key in a
    /// collection hashed by this comparer.
    /// </summary>
    /// <remarks>
    /// <see cref="GetHashCode"/> is content-based for strings, and both <c>HashSet&lt;T&gt;</c> and the
    /// dictionary behind Map cache the hash an entry was inserted with. A concatenated string is the
    /// one <see cref="JsValue"/> whose content can change afterwards: the interpreter's <c>+=</c> lane
    /// appends into its buffer in place, so an instance handed over as a key and then appended to
    /// changes its own hash under the table. The entry is then stranded — it answers to neither its old
    /// nor its new content, and cannot even be deleted through the instance that created it. Storing a
    /// flattened copy costs nothing observable: strings are primitives, and the copy is the value as of
    /// insertion, which is precisely what the collection is defined to hold. Lookups need no such
    /// treatment, since they hash the probe's current content and never retain it.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JsValue ToStableKey(JsValue value)
    {
        return value is JsString.ConcatenatedString concatenated ? concatenated.DoClone() : value;
    }
}
