using System.Diagnostics;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif
using Jint.Native;

namespace Jint.Runtime;

public static class Arguments
{
    public static JsCallArguments Empty => [];

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
        return At(args, index, JsValue.Undefined);
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

#if NET8_0_OR_GREATER
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index) = value;
#else
        array[index] = value;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsCallArguments Skip(this JsValue[] args, int count)
    {
        var newLength = args.Length - count;
        if (newLength <= 0)
        {
            return [];
        }

        var array = new JsValue[newLength];
        Array.Copy(args, count, array, 0, newLength);
        return array;
    }
}
