// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace System.Text;

internal ref struct ValueStringBuilder
{
    private char[]? _arrayToReturnToPool;
    private Span<char> _chars;
    private int _pos;

    public ValueStringBuilder(Span<char> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _chars = initialBuffer;
        _pos = 0;
    }

    public ValueStringBuilder(int initialCapacity)
    {
        _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
        _chars = _arrayToReturnToPool;
        _pos = 0;
    }

    public int Length
    {
        get => _pos;
        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= _chars.Length);
            _pos = value;
        }
    }

    public int Capacity => _chars.Length;

    public void EnsureCapacity(int capacity)
    {
        // This is not expected to be called this with negative capacity
        Debug.Assert(capacity >= 0);

        // If the caller has a bug and calls this with negative capacity, make sure to call Grow to throw an exception.
        if ((uint) capacity > (uint) _chars.Length)
            Grow(capacity - _pos);
    }

    /// <summary>
    /// Get a pinnable reference to the builder.
    /// Does not ensure there is a null char after <see cref="Length"/>
    /// This overload is pattern matched in the C# 7.3+ compiler so you can omit
    /// the explicit method call, and write eg "fixed (char* c = builder)"
    /// </summary>
    public ref char GetPinnableReference()
    {
        return ref MemoryMarshal.GetReference(_chars);
    }

    /// <summary>
    /// Get a pinnable reference to the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    public ref char GetPinnableReference(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }
        return ref MemoryMarshal.GetReference(_chars);
    }

    public ref char this[int index]
    {
        get
        {
            Debug.Assert(index < _pos);
            return ref _chars[index];
        }
    }

    public override string ToString()
    {
        string s = _chars.Slice(0, _pos).ToString();
        Dispose();
        return s;
    }

    /// <summary>Returns the underlying storage of the builder.</summary>
    public Span<char> RawChars => _chars;

    /// <summary>
    /// Returns a span around the contents of the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    public ReadOnlySpan<char> AsSpan(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }
        return _chars.Slice(0, _pos);
    }

    public ReadOnlySpan<char> AsSpan() => _chars.Slice(0, _pos);
    public ReadOnlySpan<char> AsSpan(int start) => _chars.Slice(start, _pos - start);
    public ReadOnlySpan<char> AsSpan(int start, int length) => _chars.Slice(start, length);

    public void Reverse()
    {
        _chars.Slice(0, _pos).Reverse();
    }

    public bool TryCopyTo(Span<char> destination, out int charsWritten)
    {
        if (_chars.Slice(0, _pos).TryCopyTo(destination))
        {
            charsWritten = _pos;
            Dispose();
            return true;
        }
        else
        {
            charsWritten = 0;
            Dispose();
            return false;
        }
    }

    public void Insert(int index, char value, int count)
    {
        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
        _chars.Slice(index, count).Fill(value);
        _pos += count;
    }

    public void Insert(int index, string? s)
    {
        if (s == null)
        {
            return;
        }

        int count = s.Length;

        if (_pos > (_chars.Length - count))
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
        s
#if !NET8_0_OR_GREATER
            .AsSpan()
#endif
            .CopyTo(_chars.Slice(index));
        _pos += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        int pos = _pos;
        Span<char> chars = _chars;
        if ((uint) pos < (uint) chars.Length)
        {
            chars[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendHex(byte b)
    {
        const string Map = "0123456789ABCDEF";
        ReadOnlySpan<char> data =
        [
            Map[b / 16],
            Map[b % 16]
        ];
        Append(data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? s)
    {
        if (s == null)
        {
            return;
        }

        int pos = _pos;
        if (s.Length == 1 && (uint) pos < (uint) _chars.Length) // very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
        {
            _chars[pos] = s[0];
            _pos = pos + 1;
        }
        else
        {
            AppendSlow(s);
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Appends a formattable value using the invariant culture, which every caller of this builder
    /// wants: what it produces is machine-readable output — JSON, ISO 8601 dates, stack traces —
    /// not text for a user to read. A null provider would mean <see cref="NumberFormatInfo.CurrentInfo"/>,
    /// i.e. the host's ambient <see cref="CultureInfo.CurrentCulture"/>, whose <c>NegativeSign</c> is
    /// U+2212 MINUS SIGN in sv-SE, fi-FI, nb-NO and others, and carries a U+061C ARABIC LETTER MARK
    /// in ar-SA. Both routes are cultured, and both are taken: the <c>TryFormat</c> attempt fails on
    /// an empty or nearly full buffer, which is exactly the state of a freshly constructed builder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append<T>(T value) where T : ISpanFormattable
    {
        if (value.TryFormat(_chars.Slice(_pos), out var charsWritten, format: default, provider: CultureInfo.InvariantCulture))
        {
            _pos += charsWritten;
        }
        else
        {
            Append(value.ToString(format: null, CultureInfo.InvariantCulture));
        }
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(int value)
    {
        Append(value.ToString(CultureInfo.InvariantCulture));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(long value)
    {
        Append(value.ToString(CultureInfo.InvariantCulture));
    }
#endif

    private void AppendSlow(string s)
    {
        int pos = _pos;
        if (pos > _chars.Length - s.Length)
        {
            Grow(s.Length);
        }

        s
#if !NET8_0_OR_GREATER
            .AsSpan()
#endif
            .CopyTo(_chars.Slice(pos));
        _pos += s.Length;
    }

    public void Append(char c, int count)
    {
        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        Span<char> dst = _chars.Slice(_pos, count);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = c;
        }
        _pos += count;
    }

    public unsafe void Append(char* value, int length)
    {
        int pos = _pos;
        if (pos > _chars.Length - length)
        {
            Grow(length);
        }

        Span<char> dst = _chars.Slice(_pos, length);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = *value++;
        }
        _pos += length;
    }

    public void Append(scoped ReadOnlySpan<char> value)
    {
        int pos = _pos;
        if (pos > _chars.Length - value.Length)
        {
            Grow(value.Length);
        }

        value.CopyTo(_chars.Slice(_pos));
        _pos += value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> AppendSpan(int length)
    {
        int origPos = _pos;
        if (origPos > _chars.Length - length)
        {
            Grow(length);
        }

        _pos = origPos + length;
        return _chars.Slice(origPos, length);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        Append(c);
    }

    /// <summary>
    /// Resize the internal buffer either by doubling current buffer size or
    /// by adding <paramref name="additionalCapacityBeyondPos"/> to
    /// <see cref="_pos"/> whichever is greater.
    /// </summary>
    /// <param name="additionalCapacityBeyondPos">
    /// Number of chars requested beyond current position.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);
        Debug.Assert(_pos > _chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

        // The required size is computed as a long, because past int.MaxValue characters the int sum
        // wraps negative and the (uint) round-trip below then turns it into a Rent(int.MinValue) whose
        // ArgumentOutOfRangeException escapes the caller — for a script, past every catch block.
        // The cap in the doubling term below bounds only the doubling, never the required size.
        //
        // This level does not raise a JavaScript RangeError: the builder also backs the JSON
        // serializer, URI encoding/decoding and the Intl/Temporal formatters, none of which has a
        // realm to throw into. Its job is only to stop lying about the size, so a size no array could
        // ever hold reports as the overflow it is. Script-visible results are capped well below this
        // at JsString.MaxLength by the JavaScript-semantic callers, which do throw a RangeError a
        // script can catch.
        var required = (long) _pos + additionalCapacityBeyondPos;
        if (required > global::Jint.Runtime.ClrLimits.MaxArrayLength)
        {
            global::Jint.Runtime.Throw.OverflowException("The required string builder capacity exceeds the maximum supported array length.");
        }

        // Increase to at least the required size, but try to double the size if possible, bounding
        // the doubling to not go beyond the max array length.
        int newCapacity = (int) Math.Max(
            (uint) required,
            Math.Min((uint) _chars.Length * 2, global::Jint.Runtime.ClrLimits.MaxArrayLength));

        char[] poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

        _chars.Slice(0, _pos).CopyTo(poolArray);

        char[]? toReturn = _arrayToReturnToPool;
        _chars = _arrayToReturnToPool = poolArray;
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        char[]? toReturn = _arrayToReturnToPool;
        this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
        if (toReturn != null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }
}
