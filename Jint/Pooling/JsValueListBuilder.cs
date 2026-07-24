using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Pooling;

/// <summary>
/// Pooled, growable accumulator for building array contents of unknown final size.
/// Hand the result off via <see cref="ToArray"/> (exact-size copy that <see cref="JsArray"/>
/// can take ownership of) and always call <see cref="Dispose"/> from a finally block so the
/// rented buffer returns to the pool. Holes use the dense-array convention: null entries.
/// </summary>
internal ref struct JsValueListBuilder
{
    private JsValue?[] _array;
    private int _pos;

    public JsValueListBuilder(int initialCapacity)
    {
        _array = ArrayPool<JsValue?>.Shared.Rent(System.Math.Max(initialCapacity, 4));
        _pos = 0;
    }

    public readonly int Length => _pos;

    public readonly JsValue? this[int index]
    {
        get
        {
            Debug.Assert((uint) index < (uint) _pos);
            return _array[index];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(JsValue value)
    {
        var pos = _pos;
        var array = _array;
        if ((uint) pos < (uint) array.Length)
        {
            WriteUnchecked(array, pos, value);
            _pos = pos + 1;
        }
        else
        {
            GrowAndAdd(value);
        }
    }

    /// <summary>
    /// Stores into the backing buffer without the CLR array-covariance check that a plain
    /// <c>array[index] = value</c> pays on every write (<c>stelem.ref</c> → <c>CastHelpers.StelemRef</c>)
    /// because <see cref="JsValue"/> is not sealed. Sound because <c>_array</c> is always an exact
    /// <see cref="JsValue"/>[]: <see cref="ArrayPool{T}"/>.Rent never hands back a covariant subtype array.
    /// Callers have already bounds-checked <paramref name="index"/> (both invariants asserted in debug builds).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUnchecked(JsValue?[] array, int index, JsValue? value)
    {
        Debug.Assert(array.GetType() == typeof(JsValue[]), "backing buffer must be an exact JsValue[]");
        Debug.Assert((uint) index < (uint) array.Length, "index must be within the backing buffer");

#if NET8_0_OR_GREATER
        ref var slot = ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array);
        Unsafe.Add(ref slot, (nint) index) = value;
#else
        array[index] = value;
#endif
    }

    /// <summary>
    /// Appends a hole; materialized arrays keep the index absent (null dense entry).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddHole()
    {
        var pos = _pos;
        var array = _array;
        if ((uint) pos < (uint) array.Length)
        {
            WriteUnchecked(array, pos, null);
            _pos = pos + 1;
        }
        else
        {
            GrowAndAdd(null);
        }
    }

    public void AddRange(ReadOnlySpan<JsValue?> values)
    {
        var pos = _pos;
        if (pos > _array.Length - values.Length)
        {
            Grow(values.Length);
        }

        values.CopyTo(_array.AsSpan(pos));
        _pos = pos + values.Length;
    }

    /// <summary>
    /// Returns an exact-size copy of the accumulated values; null entries (holes) are
    /// preserved. Does not dispose, so a caller's finally-block <see cref="Dispose"/>
    /// stays valid on every exit path.
    /// </summary>
    public readonly JsValue[] ToArray()
    {
        var pos = _pos;
        if (pos == 0)
        {
            return [];
        }

        var result = new JsValue[pos];
        System.Array.Copy(_array, result, pos);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAdd(JsValue? value)
    {
        Grow(1);
        WriteUnchecked(_array, _pos, value);
        _pos++;
    }

    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);

        var newCapacity = (int) System.Math.Max(
            (uint) (_pos + additionalCapacityBeyondPos),
            System.Math.Min((uint) _array.Length * 2, ClrLimits.MaxArrayLength));

        var newArray = ArrayPool<JsValue?>.Shared.Rent(newCapacity);
        System.Array.Copy(_array, newArray, _pos);

        var toReturn = _array;
        _array = newArray;

        // The used range must be cleared before returning: retained JsValue references
        // in a pooled buffer would root arbitrarily large engine object graphs.
        System.Array.Clear(toReturn, 0, _pos);
        ArrayPool<JsValue?>.Shared.Return(toReturn);
    }

    public void Dispose()
    {
        var toReturn = _array;
        var pos = _pos;
        this = default; // safe double-dispose; further Adds fail fast instead of corrupting the pool
        if (toReturn is not null)
        {
            System.Array.Clear(toReturn, 0, pos);
            ArrayPool<JsValue?>.Shared.Return(toReturn);
        }
    }
}
