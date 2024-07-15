// modified from
// https://github.com/dotnet/aspnetcore/blob/fd060ce8c36ffe195b9e9a69a1bbd8fb53cc6d7c/src/Shared/WebEncoders/WebEncoders.cs

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NETCOREAPP
using System.Buffers;
#endif
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Jint.Extensions;

/// <summary>
/// Contains utility APIs to assist with common encoding and decoding operations.
/// </summary>
[SuppressMessage("Maintainability", "CA1510:Use ArgumentNullException throw helper")]
[SuppressMessage("Maintainability", "CA1512:Use ArgumentOutOfRangeException throw helper")]
internal static class WebEncoders
{
    private static readonly byte[] EmptyBytes = [];

    /// <summary>
    /// Decodes a base64url-encoded string.
    /// </summary>
    /// <param name="input">The base64url-encoded input to decode.</param>
    /// <returns>The base64url-decoded form of the input.</returns>
    /// <remarks>
    /// The input must not contain any whitespace or padding characters.
    /// Throws <see cref="FormatException"/> if the input is malformed.
    /// </remarks>
    public static byte[] Base64UrlDecode(ReadOnlySpan<char> input)
    {
        // Special-case empty input
        if (input.Length == 0)
        {
            return EmptyBytes;
        }

        // Create array large enough for the Base64 characters, not just shorter Base64-URL-encoded form.
        var buffer = new char[GetArraySizeRequiredToDecode(input.Length)];

        return Base64UrlDecode(input, buffer);
    }

    /// <summary>
    /// Decodes a base64url-encoded <paramref name="input"/> into a <c>byte[]</c>.
    /// </summary>
    public static byte[] Base64UrlDecode(ReadOnlySpan<char> input, char[] buffer)
    {
        if (input.Length == 0)
        {
            return EmptyBytes;
        }

        // Assumption: input is base64url encoded without padding and contains no whitespace.

        var paddingCharsToAdd = GetNumBase64PaddingCharsToAddForDecode(input.Length);
        var arraySizeRequired = checked(input.Length + paddingCharsToAdd);
        Debug.Assert(arraySizeRequired % 4 == 0, "Invariant: Array length must be a multiple of 4.");

        // Copy input into buffer, fixing up '-' -> '+' and '_' -> '/'.
        var i = 0;
        for (var j = 0; i < input.Length; i++, j++)
        {
            var ch = input[j];
            if (ch == '-')
            {
                buffer[i] = '+';
            }
            else if (ch == '_')
            {
                buffer[i] = '/';
            }
            else
            {
                buffer[i] = ch;
            }
        }

        // Add the padding characters back.
        for (; paddingCharsToAdd > 0; i++, paddingCharsToAdd--)
        {
            buffer[i] = '=';
        }

        // Decode.
        // If the caller provided invalid base64 chars, they'll be caught here.
        return Convert.FromBase64CharArray(buffer, 0, arraySizeRequired);
    }

    private static int GetArraySizeRequiredToDecode(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var numPaddingCharsToAdd = GetNumBase64PaddingCharsToAddForDecode(count);

        return checked(count + numPaddingCharsToAdd);
    }

    /// <summary>
    /// Encodes <paramref name="input"/> using base64url encoding.
    /// </summary>
    /// <param name="input">The binary input to encode.</param>
    /// <param name="omitPadding"></param>
    /// <returns>The base64url-encoded form of <paramref name="input"/>.</returns>
    public static string Base64UrlEncode(byte[] input, bool omitPadding)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        return Base64UrlEncode(input, offset: 0, count: input.Length, omitPadding);
    }

    /// <summary>
    /// Encodes <paramref name="input"/> using base64url encoding.
    /// </summary>
    /// <param name="input">The binary input to encode.</param>
    /// <param name="offset">The offset into <paramref name="input"/> at which to begin encoding.</param>
    /// <param name="count">The number of bytes from <paramref name="input"/> to encode.</param>
    /// <param name="omitPadding"></param>
    /// <returns>The base64url-encoded form of <paramref name="input"/>.</returns>
    public static string Base64UrlEncode(byte[] input, int offset, int count, bool omitPadding)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

#if NETCOREAPP
        return Base64UrlEncode(input.AsSpan(offset, count), omitPadding);
#else
            // Special-case empty input
            if (count == 0)
            {
                return string.Empty;
            }

            var buffer = new char[GetArraySizeRequiredToEncode(count)];
            var numBase64Chars = Base64UrlEncode(input, offset, buffer, outputOffset: 0, count: count, omitPadding);

            return new string(buffer, startIndex: 0, length: numBase64Chars);
#endif
    }

    /// <summary>
    /// Encodes <paramref name="input"/> using base64url encoding.
    /// </summary>
    /// <param name="input">The binary input to encode.</param>
    /// <param name="offset">The offset into <paramref name="input"/> at which to begin encoding.</param>
    /// <param name="output">
    /// Buffer to receive the base64url-encoded form of <paramref name="input"/>. Array must be large enough to
    /// hold <paramref name="outputOffset"/> characters and the full base64-encoded form of
    /// <paramref name="input"/>, including padding characters.
    /// </param>
    /// <param name="outputOffset">
    /// The offset into <paramref name="output"/> at which to begin writing the base64url-encoded form of
    /// <paramref name="input"/>.
    /// </param>
    /// <param name="count">The number of <c>byte</c>s from <paramref name="input"/> to encode.</param>
    /// <param name="omitPadding"></param>
    /// <returns>
    /// The number of characters written to <paramref name="output"/>, less any padding characters.
    /// </returns>
    public static int Base64UrlEncode(byte[] input, int offset, char[] output, int outputOffset, int count, bool omitPadding)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }
        if (output == null)
        {
            throw new ArgumentNullException(nameof(output));
        }

        if (outputOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputOffset));
        }

        var arraySizeRequired = GetArraySizeRequiredToEncode(count);
        if (output.Length - outputOffset < arraySizeRequired)
        {
            throw new ArgumentException("invalid", nameof(count));
        }

#if NETCOREAPP
        return Base64UrlEncode(input.AsSpan(offset, count), output.AsSpan(outputOffset), omitPadding);
#else
            // Special-case empty input.
            if (count == 0)
            {
                return 0;
            }

            // Use base64url encoding with no padding characters. See RFC 4648, Sec. 5.

            // Start with default Base64 encoding.
            var numBase64Chars = Convert.ToBase64CharArray(input, offset, count, output, outputOffset);

            // Fix up '+' -> '-' and '/' -> '_'. Drop padding characters.
            for (var i = outputOffset; i - outputOffset < numBase64Chars; i++)
            {
                var ch = output[i];
                if (ch == '+')
                {
                    output[i] = '-';
                }
                else if (ch == '/')
                {
                    output[i] = '_';
                }
                else if (omitPadding && ch == '=')
                {
                    // We've reached a padding character; truncate the remainder.
                    return i - outputOffset;
                }
            }

            return numBase64Chars;
#endif
    }

    /// <summary>
    /// Get the minimum output <c>char[]</c> size required for encoding <paramref name="count"/>
    /// <see cref="byte"/>s with the <see cref="Base64UrlEncode(byte[], int, char[], int, int, bool)"/> method.
    /// </summary>
    /// <param name="count">The number of characters to encode.</param>
    /// <returns>
    /// The minimum output <c>char[]</c> size required for encoding <paramref name="count"/> <see cref="byte"/>s.
    /// </returns>
    public static int GetArraySizeRequiredToEncode(int count)
    {
        var numWholeOrPartialInputBlocks = checked(count + 2) / 3;
        return checked(numWholeOrPartialInputBlocks * 4);
    }

#if NETCOREAPP
    /// <summary>
    /// Encodes <paramref name="input"/> using base64url encoding.
    /// </summary>
    /// <param name="input">The binary input to encode.</param>
    /// <param name="omitPadding"></param>
    /// <returns>The base64url-encoded form of <paramref name="input"/>.</returns>
    public static string Base64UrlEncode(ReadOnlySpan<byte> input, bool omitPadding)
    {
        if (input.IsEmpty)
        {
            return string.Empty;
        }

        int bufferSize = GetArraySizeRequiredToEncode(input.Length);

        char[]? bufferToReturnToPool = null;
        Span<char> buffer = bufferSize <= 128
            ? stackalloc char[bufferSize]
            : bufferToReturnToPool = ArrayPool<char>.Shared.Rent(bufferSize);

        var numBase64Chars = Base64UrlEncode(input, buffer, omitPadding);
        var base64Url = new string(buffer.Slice(0, numBase64Chars));

        if (bufferToReturnToPool != null)
        {
            ArrayPool<char>.Shared.Return(bufferToReturnToPool);
        }

        return base64Url;
    }

    private static int Base64UrlEncode(ReadOnlySpan<byte> input, Span<char> output, bool omitPadding)
    {
        Debug.Assert(output.Length >= GetArraySizeRequiredToEncode(input.Length));

        if (input.IsEmpty)
        {
            return 0;
        }

        // Use base64url encoding with no padding characters. See RFC 4648, Sec. 5.

        Convert.TryToBase64Chars(input, output, out int charsWritten);

        // Fix up '+' -> '-' and '/' -> '_'. Drop padding characters.
        for (var i = 0; i < charsWritten; i++)
        {
            var ch = output[i];
            if (ch == '+')
            {
                output[i] = '-';
            }
            else if (ch == '/')
            {
                output[i] = '_';
            }
            else if (omitPadding && ch == '=')
            {
                // We've reached a padding character; truncate the remainder.
                return i;
            }
        }

        return charsWritten;
    }
#endif

    private static int GetNumBase64PaddingCharsInString(string str)
    {
        // Assumption: input contains a well-formed base64 string with no whitespace.

        // base64 guaranteed have 0 - 2 padding characters.
        if (str[str.Length - 1] == '=')
        {
            if (str[str.Length - 2] == '=')
            {
                return 2;
            }
            return 1;
        }
        return 0;
    }

    private static int GetNumBase64PaddingCharsToAddForDecode(int inputLength)
    {
        switch (inputLength % 4)
        {
            case 0:
                return 0;
            case 2:
                return 2;
            case 3:
                return 1;
            default:
                throw new FormatException("invalid length");
        }
    }
}
