using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.Runtime;

public static class Arguments
{
    public static JsCallArguments Empty => [];

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
        return At(args, index, JsValue.Undefined);
    }

    /// <summary>
    /// The span form of <see cref="At(JsValue[], int)"/>, used by the generated variadic fast-call
    /// dispatchers so their bodies stay character-for-character the same question as the array ones.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JsValue At(this ReadOnlySpan<JsValue> args, int index)
    {
        return (uint) index < (uint) args.Length ? args[index] : JsValue.Undefined;
    }

    /// <summary>
    /// Writes <paramref name="value"/> to <paramref name="array"/> without the covariant array store
    /// type check (stelemref). Because <see cref="JsValue"/> is not sealed, a plain <c>array[index] = value</c>
    /// must verify at runtime that the array's actual element type accepts the value; that check is pure
    /// overhead when the array is known to be exactly <see cref="JsValue"/>[]. Bounds are checked explicitly.
    /// </summary>
    /// <remarks>
    /// Callers MUST guarantee that <paramref name="array"/> is exactly of type <see cref="JsValue"/>[] —
    /// freshly allocated via <c>new JsValue[n]</c> or rented from <see cref="Jint.Pooling.JsValueArrayPool"/>
    /// (whose factories only ever create such arrays). Writing through this helper into an array whose
    /// element type is a subtype of <see cref="JsValue"/> would break array type safety. In particular,
    /// never use it on caller-supplied <see cref="JsCallArguments"/> whose provenance is unknown.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteNoTypeCheck(JsValue[] array, int index, JsValue value)
    {
        Debug.Assert(array.GetType() == typeof(JsValue[]), "array must be exactly JsValue[]");

        if ((uint) index >= (uint) array.Length)
        {
            Throw.ArgumentOutOfRangeException(nameof(index), "Index was outside the bounds of the array.");
        }

        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index) = value;
    }

    /// <summary>
    /// Copies the arguments from <paramref name="start"/> onward into a fresh array, or returns an empty one
    /// when there are none. Deliberately <b>not</b> an extension method: as <c>args.Skip(1)</c> it was a more
    /// specific overload than <see cref="System.Linq.Enumerable.Skip{TSource}"/>, so with
    /// <c>using Jint.Runtime;</c> in scope the identical source text bound to this eager, array-allocating
    /// copy instead of LINQ's lazy one, with nothing to warn either a caller or a reader.
    /// </summary>
    /// <remarks>
    /// A copy rather than a slice because every caller needs an array that outlives the call: the arguments it
    /// is handed are pooled and are recycled as soon as the host function returns.
    /// </remarks>
    internal static JsCallArguments Slice(JsCallArguments args, int start)
    {
        var newLength = args.Length - start;
        if (newLength <= 0)
        {
            return [];
        }

        var array = new JsValue[newLength];
        Array.Copy(args, start, array, 0, newLength);
        return array;
    }
}
