// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// taken from and removed unused methods
// https://github.com/dotnet/roslyn/blob/8a7ca9af3360ef388b6dad61c95e2d0629d7a032/src/Compilers/Core/Portable/InternalUtilities/Hash.cs

using System.Runtime.CompilerServices;

namespace Jint.Extensions;

internal static class Hash
{
    /// <summary>
    /// The offset bias value used in the FNV-1a algorithm
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    private const int FnvOffsetBias = unchecked((int)2166136261);

    /// <summary>
    /// The generative factor used in the FNV-1a algorithm
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    private const int FnvPrime = 16777619;

    /// <summary>
    /// Compute the hashcode of a sub-string using FNV-1a
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// Note: FNV-1a was developed and tuned for 8-bit sequences. We're using it here
    /// for 16-bit Unicode chars on the understanding that the majority of chars will
    /// fit into 8-bits and, therefore, the algorithm will retain its desirable traits
    /// for generating hash codes.
    /// </summary>
    internal static int GetFNVHashCode(ReadOnlySpan<char> data) => CombineFNVHash(FnvOffsetBias, data);

    /// <summary>
    /// Compute the hashcode of a string using FNV-1a
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    /// <param name="text">The input string</param>
    /// <returns>The FNV-1a hash code of <paramref name="text"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetFNVHashCode(string text) => CombineFNVHash(FnvOffsetBias, text);

    /// <summary>
    /// Compute the hashcode of a string using FNV-1a
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    /// <param name="text">The input string</param>
    /// <returns>The FNV-1a hash code of <paramref name="text"/></returns>
    internal static int GetFNVHashCode(System.Text.StringBuilder text)
    {
        int hashCode = FnvOffsetBias;

#if NETCOREAPP3_1_OR_GREATER
            foreach (var chunk in text.GetChunks())
            {
                hashCode = CombineFNVHash(hashCode, chunk.Span);
            }
#else
        // StringBuilder.GetChunks is not available in this target framework. Since there is no other direct access
        // to the underlying storage spans of StringBuilder, we fall back to using slower per-character operations.
        int end = text.Length;

        for (int i = 0; i < end; i++)
        {
            hashCode = unchecked((hashCode ^ text[i]) * FnvPrime);
        }
#endif

        return hashCode;
    }

    /// <summary>
    /// Combine a string with an existing FNV-1a hash code
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    /// <param name="hashCode">The accumulated hash code</param>
    /// <param name="text">The string to combine</param>
    /// <returns>The result of combining <paramref name="hashCode"/> with <paramref name="text"/> using the FNV-1a algorithm</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    private static int CombineFNVHash(int hashCode, string text)
    {
        foreach (char ch in text)
        {
            hashCode = unchecked((hashCode ^ ch) * FnvPrime);
        }

        return hashCode;
    }

    /// <summary>
    /// Combine a string with an existing FNV-1a hash code
    /// See http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    /// <param name="hashCode">The accumulated hash code</param>
    /// <param name="data">The string to combine</param>
    /// <returns>The result of combining <paramref name="hashCode"/> with <paramref name="data"/> using the FNV-1a algorithm</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions) 512)]
    private static int CombineFNVHash(int hashCode, ReadOnlySpan<char> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            hashCode = unchecked((hashCode ^ data[i]) * FnvPrime);
        }

        return hashCode;
    }
}
