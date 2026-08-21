using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Jint;

internal static class Polyfills
{
#if NETFRAMEWORK || NETSTANDARD2_0
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(this string source, char c) => source.IndexOf(c) != -1;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool StartsWith(this string source, char c) => source.Length > 0 && source[0] == c;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool EndsWith(this string source, char c) => source.Length > 0 && source[source.Length - 1] == c;

    // The string.Join(char, ...) overloads were added in .NET Core 2.1 / netstandard2.1.
    // Backfill the IEnumerable<string> overload the codebase uses so call sites can pass a char
    // separator uniformly on every target framework, without a per-call-site #if.
    extension(string)
    {
        public static string Join(char separator, IEnumerable<string?> values) => string.Join(separator.ToString(), values);
    }
#endif

#if NETFRAMEWORK
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(this ReadOnlySpan<string> source, string c) => source.IndexOf(c) != -1;
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
    // The span overloads below arrived in .NET Core 2.1 / netstandard2.1. Each forwards to the
    // pointer-based overload that net462 and netstandard2.0 do have, so none of them allocates -- the
    // obvious `Append(value.ToString())` shape would, on paths that exist to avoid exactly that.

    // Dictionary<,>.TryAdd arrived in .NET Core 2.0 / netstandard2.1. Jint's own HybridDictionary and
    // StringDictionarySlim already expose TryAdd unconditionally, so without this the codebase
    // contradicts itself about whether the method exists.
    internal static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
    {
        if (dictionary.ContainsKey(key))
        {
            return false;
        }

        dictionary.Add(key, value);
        return true;
    }

    internal static unsafe StringBuilder Append(this StringBuilder builder, ReadOnlySpan<char> value)
    {
        // Pinning an empty span yields a null pointer, and Append(char*, int) rejects null even for a
        // zero count, where the real span overload simply does nothing. Short-circuit so the polyfill
        // cannot differ from it.
        if (value.IsEmpty)
        {
            return builder;
        }

        fixed (char* p = &MemoryMarshal.GetReference(value))
        {
            return builder.Append(p, value.Length);
        }
    }

    internal static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        fixed (byte* p = &MemoryMarshal.GetReference(bytes))
        {
            return encoding.GetString(p, bytes.Length);
        }
    }

    internal static unsafe void Convert(
        this Encoder encoder,
        ReadOnlySpan<char> chars,
        Span<byte> bytes,
        bool flush,
        out int charsUsed,
        out int bytesUsed,
        out bool completed)
    {
        // Encoder.Convert(char*, ...) rejects a null pointer, and pinning an empty span produces one.
        // Route an empty side through a one-element scratch buffer, still passing the real length of 0,
        // so the call behaves as the span overload does rather than throwing.
        Span<char> charScratch = stackalloc char[1];
        Span<byte> byteScratch = stackalloc byte[1];
        var charSource = chars.IsEmpty ? (ReadOnlySpan<char>) charScratch : chars;
        var byteSource = bytes.IsEmpty ? byteScratch : bytes;

        fixed (char* charsPtr = &MemoryMarshal.GetReference(charSource))
        fixed (byte* bytesPtr = &MemoryMarshal.GetReference(byteSource))
        {
            encoder.Convert(charsPtr, chars.Length, bytesPtr, bytes.Length, flush, out charsUsed, out bytesUsed, out completed);
        }
    }
#endif

#if !NET8_0_OR_GREATER
    // string.GetHashCode(ReadOnlySpan<char>, StringComparison) arrived in .NET Core 2.1 but was not
    // exposed by netstandard2.1. The contract that matters here is only that a sliced string hash
    // equal the flat string's within one process, and StringComparer.Ordinal.GetHashCode(s) is
    // s.GetHashCode() on every target framework, so this is exact. It does materialize the slice,
    // which is the cost these targets already paid.
    extension(string)
    {
        public static int GetHashCode(ReadOnlySpan<char> value, StringComparison comparisonType)
        {
            // StringComparer.FromComparison is itself netstandard2.1+, so the mapping is spelled out.
            var s = value.ToString();
            switch (comparisonType)
            {
                case StringComparison.Ordinal: return StringComparer.Ordinal.GetHashCode(s);
                case StringComparison.OrdinalIgnoreCase: return StringComparer.OrdinalIgnoreCase.GetHashCode(s);
                case StringComparison.CurrentCulture: return StringComparer.CurrentCulture.GetHashCode(s);
                case StringComparison.CurrentCultureIgnoreCase: return StringComparer.CurrentCultureIgnoreCase.GetHashCode(s);
                case StringComparison.InvariantCulture: return StringComparer.InvariantCulture.GetHashCode(s);
                case StringComparison.InvariantCultureIgnoreCase: return StringComparer.InvariantCultureIgnoreCase.GetHashCode(s);
                default:
                    Jint.Runtime.Throw.ArgumentException("The string comparison type passed in is currently not supported.", nameof(comparisonType));
                    return 0;
            }
        }
    }
#endif

#if !NET8_0_OR_GREATER
    // The vectorized MemoryExtensions searches below arrived in .NET 8 and are not in netstandard2.1.
    // Each fallback is the character-at-a-time scan the call site used to spell out in its own #else,
    // so behaviour is identical everywhere and only the throughput differs -- which is exactly the
    // trade these call sites were already making, just written once instead of at every site.
    //
    // ContainsAny is the odd one out: MemoryExtensions.IndexOfAny(span, T, T) exists on every target
    // framework, so this one is vectorized downlevel too.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    internal static bool ContainsAny(this ReadOnlySpan<char> span, char value0, char value1)
        => span.IndexOfAny(value0, value1) >= 0;

    internal static int IndexOfAnyInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if ((uint) (span[i] - lowInclusive) <= (uint) (highInclusive - lowInclusive))
            {
                return i;
            }
        }

        return -1;
    }

    internal static int IndexOfAnyExceptInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if ((uint) (span[i] - lowInclusive) > (uint) (highInclusive - lowInclusive))
            {
                return i;
            }
        }

        return -1;
    }

    internal static bool ContainsAnyInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
        => span.IndexOfAnyInRange(lowInclusive, highInclusive) >= 0;

    internal static bool ContainsAnyExceptInRange(this ReadOnlySpan<char> span, char lowInclusive, char highInclusive)
        => span.IndexOfAnyExceptInRange(lowInclusive, highInclusive) >= 0;

    internal static bool ContainsAny(this ReadOnlySpan<char> span, SearchValues<char> values)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (values.Contains(span[i]))
            {
                return true;
            }
        }

        return false;
    }

    internal static int IndexOfAnyExcept(this ReadOnlySpan<char> span, SearchValues<char> values)
    {
        for (var i = 0; i < span.Length; i++)
        {
            if (!values.Contains(span[i]))
            {
                return i;
            }
        }

        return -1;
    }
#endif
}

internal static class MemoryMarshalPolyfills
{
    extension(MemoryMarshal)
    {
#if !NET8_0_OR_GREATER
        // MemoryMarshal.GetArrayDataReference arrived in .NET 5 and is not in netstandard2.1.
        //
        // GetReference over the array's span is the same address by construction. It is not the same
        // cost everywhere: netstandard2.1 gets the runtime's span and so gets the bounds-check and
        // covariance-check elision the real API is used for, while net462 and netstandard2.0 bind
        // System.Memory's slower span and land roughly where the plain indexer did. Correct on all of
        // them, which is what lets the call sites drop their #if.
        //
        // Deliberately not `ref array[0]`: ldelema on a reference-type array performs the array-type
        // check this helper exists to avoid.
        public static ref T GetArrayDataReference<T>(T[] array) => ref MemoryMarshal.GetReference(array.AsSpan());
#endif
    }
}

// One container per receiver type is required, not stylistic. A static extension member lowers into
// its containing class with the receiver type erased from the signature, so int.Parse, long.Parse and
// double.Parse -- identical parameters, differing only in return type -- collide with CS0111 if they
// share one class. Same reason applies to any future static polyfill whose signature is shared across
// receivers.
//
// Each member mirrors its BCL counterpart exactly, defaults included: an extra optional parameter the
// real API does not have would change overload resolution on the frameworks that do have it, which is
// how a polyfill silently stops being a polyfill. The span-taking numeric overloads arrived in two
// waves -- the NumberStyles ones in .NET Core 2.1 / netstandard2.1, the IFormatProvider ones with
// IParsable<T> in .NET 7 -- hence the two different guards.
internal static class Int32Polyfills
{
    extension(int)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out int result)
        {
            return int.TryParse(s.ToString(), style, provider, out result);
        }

        public static int Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return int.Parse(s.ToString(), style, provider);
        }
#endif

#if !NET8_0_OR_GREATER
        public static int Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return int.Parse(s.ToString(), NumberStyles.Integer, provider);
        }
#endif
    }
}

internal static class Int64Polyfills
{
    extension(long)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out long result)
        {
            return long.TryParse(s.ToString(), style, provider, out result);
        }

        public static long Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return long.Parse(s.ToString(), style, provider);
        }
#endif

#if !NET8_0_OR_GREATER
        public static long Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return long.Parse(s.ToString(), NumberStyles.Integer, provider);
        }
#endif
    }
}

internal static class DoublePolyfills
{
    extension(double)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // double.IsFinite arrived in .NET Core 3.0 / netstandard2.1. Backfill it so spec steps
        // phrased as "if x is finite" read the same on every target framework.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static double Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Float | NumberStyles.AllowThousands, IFormatProvider? provider = null)
        {
            return double.Parse(s.ToString(), style, provider);
        }

        public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out double result)
        {
            return double.TryParse(s.ToString(), style, provider, out result);
        }
#endif

#if !NET8_0_OR_GREATER
        public static double Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return double.Parse(s.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, provider);
        }
#endif
    }
}

internal static class BytePolyfills
{
    extension(byte)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // byte.Parse(ReadOnlySpan<char>, ...) arrived in .NET Core 2.1 / netstandard2.1.
        public static byte Parse(ReadOnlySpan<char> s, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return byte.Parse(s.ToString(), style, provider);
        }
#endif
    }
}

internal static class BigIntegerPolyfills
{
    extension(BigInteger)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // BigInteger.TryParse(ReadOnlySpan<char>, ...) arrived in .NET Core 2.1 / netstandard2.1.
        public static bool TryParse(ReadOnlySpan<char> value, NumberStyles style, IFormatProvider? provider, out BigInteger result)
        {
            return BigInteger.TryParse(value.ToString(), style, provider, out result);
        }
#endif
    }
}

internal static class RuntimeHelpersPolyfills
{
    extension(RuntimeHelpers)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // RuntimeHelpers.TryEnsureSufficientExecutionStack arrived in .NET Core 2.0 / netstandard2.1. The
        // older frameworks only have the throwing form, so this is the try/catch the call site was
        // writing itself -- an exception per failed probe, which is why the real API exists.
        public static bool TryEnsureSufficientExecutionStack()
        {
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
                return true;
            }
            catch (InsufficientExecutionStackException)
            {
                return false;
            }
        }
#endif
    }
}

internal static class MathPolyfills
{
    extension(Math)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // Math.Clamp arrived in .NET Core 2.0 / netstandard2.1.
        //
        // The bounds check is not decoration: the real Math.Clamp throws for min > max rather than
        // silently preferring one of them, and the message it throws with is reproduced verbatim. Without
        // it, net462 and netstandard2.0 would answer an inverted-bounds call with a number while
        // netstandard2.1 and up threw -- and no target framework this repository executes tests on could
        // ever see the split, since netstandard2.1 binds the real member.
        public static int Clamp(int value, int min, int max)
        {
            if (min > max)
            {
                Jint.Runtime.Throw.ArgumentException($"'{min}' cannot be greater than {max}.");
            }

            return value < min ? min : value > max ? max : value;
        }
#endif
    }
}

internal static class GCPolyfills
{
    internal static bool AllocatedBytesForCurrentThreadIsSupported
    {
        get
        {
#if NETFRAMEWORK || NETSTANDARD2_0
            return AllocatedBytesForCurrentThread is not null;
#else
            return true;
#endif
        }
    }

    extension(GC)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // GC.GetAllocatedBytesForCurrentThread is absent from the net462 and netstandard2.0 reference
        // assemblies, so it cannot be called directly here. It is a *public* method of System.GC on
        // every runtime that has one -- .NET Framework since 4.6, .NET Core since 2.0 -- which is what
        // makes the lookup below work at all: it uses the default binding flags, and those see public
        // members only. So this reaches the real method wherever one exists, and answers null where none
        // does: a runtime older than that, or one whose linker removed it. A caller left with null
        // cannot measure allocation at all, and says so.
        public static long GetAllocatedBytesForCurrentThread()
        {
            var getter = AllocatedBytesForCurrentThread;
            if (getter is null)
            {
                Jint.Runtime.Throw.PlatformNotSupportedException("The current platform doesn't support MemoryLimit.");
            }

            return getter();
        }
#endif
    }

    /// <summary>
    /// The total form of the above, for a caller that must not throw -- a Reset() invoked from a finally,
    /// where an exception would unwind in place of the one already in flight. Deliberately not an
    /// extension member on <see cref="GC"/>: no BCL ever had a TryGet form, and a polyfill that invents
    /// API has stopped being a polyfill.
    /// </summary>
    internal static bool TryGetAllocatedBytesForCurrentThread(out long allocatedBytes)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        var getter = AllocatedBytesForCurrentThread;
        if (getter is null)
        {
            allocatedBytes = 0;
            return false;
        }

        allocatedBytes = getter();
#else
        allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
#endif
        return true;
    }

#if NETFRAMEWORK || NETSTANDARD2_0
    private static readonly Func<long>? AllocatedBytesForCurrentThread = ResolveAllocatedBytesForCurrentThread();

    private static Func<long>? ResolveAllocatedBytesForCurrentThread()
    {
        var methodInfo = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");
        return methodInfo is null ? null : (Func<long>) Delegate.CreateDelegate(typeof(Func<long>), null, methodInfo);
    }
#endif
}

internal static class CharPolyfills
{
    extension(char)
    {
#if !NET8_0_OR_GREATER
        // The char.IsAscii* family arrived in .NET 7 and is not in netstandard2.1, so net462,
        // netstandard2.0 and netstandard2.1 all need it. Each body is the BCL's own shape: a single
        // unsigned-cast range test, and for the letter predicates an OR with 0x20 that folds the two
        // cases together before the one comparison.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiDigit(char c) => (uint) (c - '0') <= '9' - '0';

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiLetter(char c) => (uint) ((c | 0x20) - 'a') <= 'z' - 'a';

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiLetterLower(char c) => (uint) (c - 'a') <= 'z' - 'a';

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiLetterUpper(char c) => (uint) (c - 'A') <= 'Z' - 'A';

        // Non-short-circuiting `|` on purpose, as the BCL has it: both operands are branchless, so
        // evaluating them unconditionally is cheaper than the branch a `||` would introduce.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool IsAsciiLetterOrDigit(char c) => char.IsAsciiLetter(c) | ((uint) (c - '0') <= '9' - '0');
#endif
    }
}

internal static class CharUnicodeInfoPolyfills
{
    extension(CharUnicodeInfo)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // CharUnicodeInfo.GetUnicodeCategory(int) arrived in .NET Core 3.0 / netstandard2.1. Below that a
        // supplementary code point has to be materialized as a surrogate pair first, which is the
        // allocation this overload exists to avoid; BMP code points still avoid it.
        public static UnicodeCategory GetUnicodeCategory(int codePoint)
            => codePoint <= 0xFFFF
                ? CharUnicodeInfo.GetUnicodeCategory((char) codePoint)
                : CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
#endif
    }
}
