#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.TypedArray;

namespace Jint.WebApi.Encoding;

/// <summary>
/// Reads the bytes behind a WebIDL <c>AllowSharedBufferSource</c> — an <c>ArrayBuffer</c>, a
/// <c>SharedArrayBuffer</c>, or any view over one (a typed array or a <c>DataView</c>).
/// <para>
/// https://webidl.spec.whatwg.org/#AllowSharedBufferSource
/// </para>
/// </summary>
/// <remarks>
/// The span returned is a window onto the engine's own backing array, not a copy: the specification's
/// "get a copy of the buffer source" exists so that a later mutation cannot be observed, and every caller
/// here consumes the bytes synchronously before returning to script, so the copy would be pure cost.
/// <para>
/// A detached buffer yields the empty byte sequence rather than an error, which is what
/// https://webidl.spec.whatwg.org/#dfn-get-buffer-source-copy step 7 says. A view left hanging outside
/// its resized buffer is clamped to what is still in range, which is the same answer with the same
/// reasoning — the bytes are gone, so there are none to hand over.
/// </para>
/// </remarks>
internal static class BufferSource
{
    /// <summary>
    /// Whether <paramref name="value"/> is a buffer source at all — the only thing the WebIDL
    /// <c>AllowSharedBufferSource</c> conversion decides, and it decides it before the operation runs.
    /// </summary>
    /// <remarks>
    /// An operation whose <i>later</i> arguments can detach the buffer has to separate the two: the type
    /// check belongs to the argument conversion, the bytes to the operation that follows it. Everything else
    /// converts one buffer source and nothing that could run script, so it goes straight to
    /// <see cref="TryGetBytes"/>.
    /// </remarks>
    internal static bool IsBufferSource(JsValue value) => value is JsTypedArray or JsDataView or JsArrayBuffer;

    /// <summary>
    /// Yields the bytes of <paramref name="value"/>, or <see langword="false"/> when it is not a buffer
    /// source at all — which the caller reports as the <c>TypeError</c> the WebIDL conversion would raise.
    /// </summary>
    internal static bool TryGetBytes(JsValue value, out ReadOnlySpan<byte> bytes)
    {
        bytes = default;

        if (value is JsTypedArray typedArray)
        {
            // Length is the buffer-witness answer, so a length-tracking view follows a resize and an
            // out-of-bounds one reports zero.
            var elements = (int) typedArray.Length * typedArray._arrayElementType.GetElementSize();
            bytes = Window(typedArray._viewedArrayBuffer.ArrayBufferData, typedArray._byteOffset, elements);
            return true;
        }

        if (value is JsDataView dataView)
        {
            var data = dataView._viewedArrayBuffer?.ArrayBufferData;
            var offset = (int) dataView._byteOffset;
            var length = dataView._byteLength == JsTypedArray.LengthAuto
                ? (data?.Length ?? 0) - offset
                : (int) dataView._byteLength;

            bytes = Window(data, offset, length);
            return true;
        }

        // JsSharedArrayBuffer derives from JsArrayBuffer, which is what AllowShared means here.
        if (value is JsArrayBuffer arrayBuffer)
        {
            bytes = Window(arrayBuffer.ArrayBufferData, 0, arrayBuffer.ArrayBufferByteLength);
            return true;
        }

        return false;
    }

    private static ReadOnlySpan<byte> Window(byte[]? data, int offset, int length)
    {
        if (data is null || (uint) offset >= (uint) data.Length || length <= 0)
        {
            return default;
        }

        return data.AsSpan(offset, Math.Min(length, data.Length - offset));
    }
}
#endif
