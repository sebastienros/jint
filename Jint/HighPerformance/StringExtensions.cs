// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jint.HighPerformance;

/// <summary>
/// Helpers for working with the <see cref="string"/> type.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Returns a reference to the first element within a given <see cref="string"/>, with no bounds checks.
    /// </summary>
    /// <param name="text">The input <see cref="string"/> instance.</param>
    /// <returns>A reference to the first element within <paramref name="text"/>, or the location it would have used, if <paramref name="text"/> is empty.</returns>
    /// <remarks>This method doesn't do any bounds checks, therefore it is responsibility of the caller to perform checks in case the returned value is dereferenced.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref char DangerousGetReference(this string text)
    {
#if NETCOREAPP3_1_OR_GREATER
        return ref Unsafe.AsRef(in text.GetPinnableReference());
#else
        return ref MemoryMarshal.GetReference(text.AsSpan());
#endif
    }

    /// <summary>
    /// Gets a content hash from the input <see cref="string"/> instance using the Djb2 algorithm.
    /// For more info, see the documentation for <see cref="SpanHelper.GetDjb2HashCode{T}"/>.
    /// </summary>
    /// <param name="text">The source <see cref="string"/> to enumerate.</param>
    /// <returns>The Djb2 value for the input <see cref="string"/> instance.</returns>
    /// <remarks>The Djb2 hash is fully deterministic and with no random components.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int GetDjb2HashCode(this string text)
    {
        ref char r0 = ref text.DangerousGetReference();
        nint length = (nint)(uint)text.Length;

        return SpanHelper.GetDjb2HashCode(ref r0, length);
    }
}
