#nullable enable
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Jint.Runtime.RegExp.GregsEx.Text;

/// <summary>
/// A low-allocation, mutable value-type character buffer backed by <see cref="ArrayPool{T}.Shared"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Design &amp; Ownership:</b> Unlike a <c>ref struct</c> builder, <see cref="ValueStringBuilder"/> is a standard
/// <c>struct</c> so it can be stored as a field on state machines or preserved across incremental steps.
/// Never copy a non-empty instance by value, as doing so duplicates the reference to the underlying pooled array
/// and risks double-returning it to <see cref="ArrayPool{T}.Shared"/>.
/// </para>
/// <para>
/// <b>Best Practices &amp; Usage Patterns:</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Local scoped usage (<c>using var</c>):</b> When the builder is used within a single method and not passed
/// by <c>ref</c> to helper methods, prefer <c>using var</c> so the pooled buffer is returned even on exceptions:
/// <code>
/// using var builder = new ValueStringBuilder(initialCapacity: 64);
/// builder.Append("prefix-");
/// builder.Append(id, provider: CultureInfo.InvariantCulture);
/// return builder.ToString();
/// </code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Passing to helpers by <c>ref</c> (<c>try / finally</c>):</b> In C#, <c>using var</c> variables are implicitly
/// read-only and cannot be passed as <c>ref</c> arguments (compiler error <c>CS1657</c>). When delegating to helper
/// methods that mutate the builder via <c>ref ValueStringBuilder</c>, declare a standard local variable and dispose
/// it in a <c>finally</c> block:
/// <code>
/// var builder = new ValueStringBuilder();
/// try
/// {
///     AppendHeader(ref builder);
///     AppendEscaped(ref builder, input);
///     return builder.ToString();
/// }
/// finally
/// {
///     builder.Dispose();
/// }
/// </code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Reusable field across tokens (<c>Clear</c> / <c>ToStringAndClear</c>):</b> When stored as a field on a parser
/// or tokenizer, initialize with <c>new()</c> (which starts with <see cref="Array.Empty{T}"/> and performs zero pool
/// rentals until first write). Use <see cref="ToStringAndClear"/> or <see cref="Clear"/> between tokens to reuse the
/// same rented array across the entire parse, and call <see cref="Dispose"/> once when the owner completes or faults:
/// <code>
/// // During tokenization:
/// attrValue.Append(ch);
/// string materialized = attrValue.ToStringAndClear(); // Keeps pooled array for next attribute
///
/// // On completion or fault:
/// attrValue.Dispose(); // Returns pooled array once
/// </code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Zero-allocation inspection (<c>AsSpan</c>):</b> Use <see cref="AsSpan()"/> to inspect, compare, or look up
/// interned strings in an <c>AlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;</c> dictionary without allocating a
/// <see cref="string"/> first.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Direct span writing (<see cref="IBufferWriter{T}"/>):</b> Use <see cref="GetSpan"/> and <see cref="Advance"/>
/// when feeding APIs that write directly into a destination <see cref="Span{T}"/> (such as <c>Encoding.GetChars</c>
/// or custom transcoders) to avoid intermediate scratch buffers. Ensure the struct is not boxed into an
/// <see cref="IBufferWriter{T}"/> interface reference; pass it to generic methods constrained by
/// <c>where TWriter : IBufferWriter&lt;char&gt;</c> (ideally by <c>ref</c>) or call <see cref="GetSpan"/>/<see cref="Advance"/> directly.
/// </description>
/// </item>
/// </list>
/// </remarks>
internal struct ValueStringBuilder : IBufferWriter<char>, IDisposable
{
    // Initialized to Array.Empty<char>() so hot Append paths can perform a single unsigned bounds check
    // ((uint)_pos < (uint)chars.Length) without a separate null check, while avoiding pool rentals when unused.
    private char[] _arrayFromPool = [];
    private int _pos;

    /// <summary>
    /// Initializes an empty builder that rents from <see cref="ArrayPool{T}.Shared"/> lazily on first write.
    /// </summary>
    public ValueStringBuilder()
    {
    }

    /// <summary>
    /// Initializes a builder with at least <paramref name="initialCapacity"/> characters rented from <see cref="ArrayPool{T}.Shared"/>.
    /// </summary>
    /// <param name="initialCapacity">The minimum initial buffer capacity to rent when positive; zero or negative defers rental.</param>
    public ValueStringBuilder(int initialCapacity)
    {
        _arrayFromPool = initialCapacity > 0 ? ArrayPool<char>.Shared.Rent(initialCapacity) : [];
        _pos = 0;
    }

    /// <summary>
    /// Gets or sets the number of characters written to the builder.
    /// </summary>
    public int Length
    {
        readonly get => _pos;
        set => _pos = value;
    }

    /// <summary>
    /// Gets the character at the specified zero-based index.
    /// </summary>
    /// <param name="index">The zero-based index into the written portion of the buffer.</param>
    public readonly char this[int index] => _arrayFromPool[index];

    /// <summary>
    /// Returns a read-only span over the characters written so far.
    /// </summary>
    /// <returns>A borrowed view of the active buffer contents, valid until the next buffer mutation or <see cref="Dispose"/>.</returns>
    public readonly ReadOnlySpan<char> AsSpan() => _arrayFromPool.AsSpan(0, _pos);

    /// <summary>
    /// Returns a read-only span over a slice of the buffer.
    /// </summary>
    /// <param name="start">The zero-based start index of the slice.</param>
    /// <param name="length">The number of characters in the slice.</param>
    /// <returns>A borrowed view of the requested slice, valid until the next buffer mutation or <see cref="Dispose"/>.</returns>
    public readonly ReadOnlySpan<char> AsSpan(int start, int length) => _arrayFromPool.AsSpan(start, length);

    /// <summary>
    /// Resets the write position to zero while retaining any currently rented buffer for reuse.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _pos = 0;

    /// <summary>
    /// Appends a single UTF-16 character to the builder.
    /// </summary>
    /// <param name="c">The character to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        var pos = _pos;
        var chars = _arrayFromPool;
        // Single unsigned comparison checks both non-empty capacity and available space.
        if ((uint)pos < (uint)chars.Length)
        {
            chars[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    // Kept out-of-line so the fast path in Append(char) stays small and inlineable in tight loops.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        _arrayFromPool[_pos++] = c;
    }

    /// <summary>
    /// Appends a character repeated a specified number of times.
    /// </summary>
    /// <param name="c">The character to repeat.</param>
    /// <param name="count">The number of times to append <paramref name="c"/>; non-positive values are ignored.</param>
    public void Append(char c, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var pos = _pos;
        if (pos > _arrayFromPool.Length - count)
        {
            Grow(count);
        }

        _arrayFromPool.AsSpan(pos, count).Fill(c);
        _pos = pos + count;
    }

    /// <summary>
    /// Appends a string to the builder if it is neither <see langword="null"/> nor empty.
    /// </summary>
    /// <param name="s">The string to append, or <see langword="null"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? s)
    {
        if (s is null || s.Length == 0)
        {
            return;
        }

        var pos = _pos;
        var chars = _arrayFromPool;
        // Single-character strings (common in tokenizers and entity tables) bypass span copy overhead.
        if (s!.Length == 1 && (uint)pos < (uint)chars.Length)
        {
            chars[pos] = s[0];
            _pos = pos + 1;
            return;
        }

        Append(s.AsSpan());
    }

    /// <summary>
    /// Appends a span of characters to the builder.
    /// </summary>
    /// <param name="value">The characters to copy into the builder.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        var pos = _pos;
        var chars = _arrayFromPool;
        if (pos > chars.Length - value.Length)
        {
            Grow(value.Length);
            chars = _arrayFromPool;
        }

        value.CopyTo(chars.AsSpan(pos));
        _pos = pos + value.Length;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Formats a value directly into the underlying buffer using its <see cref="ISpanFormattable"/> implementation without intermediate string allocations.
    /// </summary>
    /// <typeparam name="T">The span-formattable value type.</typeparam>
    /// <param name="value">The value to format and append.</param>
    /// <param name="format">An optional format specifier span.</param>
    /// <param name="provider">An optional culture or format provider.</param>
    public void Append<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) where T : ISpanFormattable
    {
        int charsWritten;
        while (!value.TryFormat(_arrayFromPool.AsSpan(_pos), out charsWritten, format, provider))
        {
            Grow(Math.Max(_arrayFromPool.Length - _pos + 1, 16));
        }

        _pos += charsWritten;
    }
#endif

    /// <summary>
    /// Encodes and appends a Unicode scalar value (code point) as one or two UTF-16 code units without allocating a temporary string.
    /// </summary>
    /// <param name="c">A valid Unicode scalar value (<c>0x0000</c>..&#x200B;<c>0x10FFFF</c>, excluding surrogates unless handled by caller).</param>
    public void AppendScalar(int c)
    {
        if (c <= 0xFFFF)
        {
            Append((char)c);
        }
        else
        {
            // Supplementary plane scalar: encode directly into high + low UTF-16 surrogate pair.
            c -= 0x10000;
            var pos = _pos;
            var chars = _arrayFromPool;
            if (pos > chars.Length - 2)
            {
                Grow(2);
                chars = _arrayFromPool;
            }
            chars[pos] = (char)(0xD800 + (c >> 10));
            chars[pos + 1] = (char)(0xDC00 + (c & 1023));
            _pos = pos + 2;
        }
    }

    /// <inheritdoc />
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (_pos > _arrayFromPool.Length - count)
        {
            throw new InvalidOperationException("Cannot advance past the end of the buffer.");
        }

        _pos += count;
    }

    /// <inheritdoc />
    public Memory<char> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _arrayFromPool.AsMemory(_pos);
    }

    /// <inheritdoc />
    public Span<char> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _arrayFromPool.AsSpan(_pos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        var needed = sizeHint == 0 ? 1 : sizeHint;
        if (_pos > _arrayFromPool.Length - needed)
        {
            Grow(needed);
        }
    }

    // Rents a larger array (at least doubling current capacity, minimum 64 chars, capped at Array.MaxLength),
    // copies the initialized prefix, and returns any previously rented array to ArrayPool<char>.Shared.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacityBeyondPos)
    {
        var currentLength = _arrayFromPool.Length;
        var nextCapacity = (int)Math.Max((uint)(_pos + additionalCapacityBeyondPos), Math.Min((uint)currentLength * 2, 0x7FFFFFC7u));
        if (nextCapacity < 64)
        {
            nextCapacity = 64;
        }

        var poolArray = ArrayPool<char>.Shared.Rent(nextCapacity);
        if (_pos > 0)
        {
            _arrayFromPool.AsSpan(0, _pos).CopyTo(poolArray);
        }

        if (currentLength > 0)
        {
            ArrayPool<char>.Shared.Return(_arrayFromPool);
        }

        _arrayFromPool = poolArray;
    }

    /// <summary>
    /// Materializes the written characters into a new <see cref="string"/> without clearing or disposing the buffer.
    /// </summary>
    /// <returns><see cref="string.Empty"/> when empty; otherwise a newly allocated string containing the written characters.</returns>
    public readonly override string ToString()
    {
        if (_pos == 0)
        {
            return "";
        }

#if NETFRAMEWORK || NETSTANDARD2_0
        return new string(_arrayFromPool, 0, _pos);
#else
        return new string(_arrayFromPool.AsSpan(0, _pos));
#endif
    }

    /// <summary>
    /// Materializes the written characters into a new <see cref="string"/> and resets <see cref="Length"/> to zero while keeping the rented buffer for reuse.
    /// </summary>
    /// <returns><see cref="string.Empty"/> when empty; otherwise a newly allocated string containing the written characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToStringAndClear()
    {
        if (_pos == 0)
        {
            return "";
        }

#if NETFRAMEWORK || NETSTANDARD2_0
        var s = new string(_arrayFromPool, 0, _pos);
#else
        var s = new string(_arrayFromPool.AsSpan(0, _pos));
#endif
        _pos = 0;
        return s;
    }

    /// <summary>
    /// Materializes the written characters into a new <see cref="string"/> and returns any rented buffer to <see cref="ArrayPool{T}.Shared"/>.
    /// </summary>
    /// <returns><see cref="string.Empty"/> when empty; otherwise a newly allocated string containing the written characters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToStringAndDispose()
    {
        var s = ToString();
        Dispose();
        return s;
    }

    /// <summary>
    /// Returns any rented array to <see cref="ArrayPool{T}.Shared"/> and resets the builder to its empty state.
    /// Safe to call multiple times.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var toReturn = _arrayFromPool;
        _arrayFromPool = [];
        _pos = 0;
        if (toReturn is { Length: > 0 })
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }
}
