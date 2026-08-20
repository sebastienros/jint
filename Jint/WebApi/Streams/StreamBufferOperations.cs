#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ArrayBuffer</c> plumbing a readable byte stream is built on: the standard's
/// <c>TransferArrayBuffer</c>, <c>CloneAsUint8Array</c> and the data-block copy behind
/// <c>ReadableByteStreamControllerFillPullIntoDescriptorFromQueue</c>, plus the WebIDL
/// <c>ArrayBufferView</c> conversion every byte-stream entry point starts with.
/// <para>
/// https://streams.spec.whatwg.org/#misc-abstract-ops
/// </para>
/// </summary>
internal static class StreamBufferOperations
{
    /// <summary>
    /// What a byte stream needs to know about a view it was handed:
    /// https://webidl.spec.whatwg.org/#ArrayBufferView.
    /// </summary>
    /// <remarks>
    /// <paramref name="ElementType"/> is <see langword="null"/> for a <c>DataView</c>, which is exactly the
    /// standard's "if view has a [[TypedArrayName]] internal slot" test, and it is also what a pull-into
    /// descriptor records as its view constructor: the specification takes the constructor from the typed
    /// array constructors table rather than from the view's own <c>constructor</c> property, so a subclass
    /// instance is filled and handed back as a plain view of its element type.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct ArrayBufferViewInfo(
        JsArrayBuffer Buffer,
        int ByteOffset,
        int ByteLength,
        int ArrayLength,
        int ElementSize,
        TypedArrayElementType? ElementType);

    /// <summary>
    /// The WebIDL <c>ArrayBufferView</c> conversion. A value that is not a view — or is a view onto a
    /// <c>SharedArrayBuffer</c>, which only an <c>[AllowShared]</c> declaration would accept and none of the
    /// stream declarations carries — raises a <c>TypeError</c>.
    /// </summary>
    internal static ArrayBufferViewInfo ReadArrayBufferView(Realm realm, JsValue value, string what)
    {
        if (value is JsTypedArray typedArray)
        {
            var buffer = typedArray._viewedArrayBuffer;
            AssertNotShared(realm, buffer, what);

            var elementSize = typedArray._arrayElementType.GetElementSize();

            // Length is the buffer-witness answer, so a length-tracking view over a resizable buffer
            // reports what it currently covers and a detached one reports zero — which is what
            // [[ArrayLength]] and [[ByteLength]] evaluate to in the same situations.
            var arrayLength = (int) typedArray.Length;

            return new ArrayBufferViewInfo(
                buffer,
                typedArray._byteOffset,
                arrayLength * elementSize,
                arrayLength,
                elementSize,
                typedArray._arrayElementType);
        }

        if (value is JsDataView dataView)
        {
            var buffer = dataView._viewedArrayBuffer;
            if (buffer is null)
            {
                Throw.TypeError(realm, $"{what} is not an ArrayBufferView");
                return default;
            }

            AssertNotShared(realm, buffer, what);

            var byteOffset = (int) dataView._byteOffset;
            var byteLength = dataView._byteLength == JsTypedArray.LengthAuto
                ? System.Math.Max(buffer.ArrayBufferByteLength - byteOffset, 0)
                : (int) dataView._byteLength;

            // A DataView's "number of elements" is its byte length: the element size the standard gives a
            // DataView pull-into descriptor is 1.
            return new ArrayBufferViewInfo(buffer, byteOffset, byteLength, byteLength, 1, ElementType: null);
        }

        Throw.TypeError(realm, $"{what} is not an ArrayBufferView");
        return default;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transfer-array-buffer — a move, not a copy: the new buffer takes the
    /// very same data block and the old one is detached, which is what makes every buffer a byte stream
    /// touches unusable by the code that handed it over.
    /// </summary>
    internal static JsArrayBuffer TransferArrayBuffer(Realm realm, JsArrayBuffer buffer)
    {
        // "Perform ? DetachArrayBuffer(O). This will throw an exception if O has an [[ArrayBufferDetachKey]]
        // that is not undefined" — and, per https://tc39.es/proposal-immutable-arraybuffer/, an immutable
        // buffer cannot be detached at all. The Streams Standard predates that proposal and so does not
        // mention it; refusing is the only answer that keeps an immutable buffer immutable.
        buffer.AssertNotImmutable();

        var data = buffer.ArrayBufferData!;
        buffer.DetachArrayBuffer();

        return new JsArrayBuffer(buffer.Engine, data)
        {
            _prototype = realm.Intrinsics.ArrayBuffer.PrototypeObject,
        };
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-cloneasuint8array, generalized to the region a
    /// caller names — which is also the <c>CloneArrayBuffer</c> behind
    /// <c>ReadableByteStreamControllerEnqueueClonedChunkToQueue</c>.
    /// </summary>
    internal static JsArrayBuffer CloneArrayBufferRegion(Realm realm, JsArrayBuffer buffer, int byteOffset, int byteLength)
        => buffer.CloneArrayBuffer(realm.Intrinsics.ArrayBuffer, byteOffset, (uint) byteLength);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-copydatablockbytes, on the two buffers' data blocks.
    /// </summary>
    internal static void CopyDataBlockBytes(JsArrayBuffer to, int toIndex, JsArrayBuffer from, int fromIndex, int count)
        => System.Array.Copy(from.ArrayBufferData!, fromIndex, to.ArrayBufferData!, toIndex, count);

    /// <summary>
    /// Builds the view a filled pull-into descriptor is handed back as, or the empty one a closed stream
    /// answers a BYOB read with: <c>Construct(ctor, « buffer, byteOffset, length »)</c> where <c>ctor</c>
    /// comes from the typed array constructors table, or is <c>%DataView%</c>.
    /// </summary>
    internal static ObjectInstance ConstructView(Realm realm, TypedArrayElementType? elementType, JsArrayBuffer buffer, int byteOffset, int length)
    {
        if (elementType is not { } type)
        {
            var dataView = realm.Intrinsics.DataView;
            return dataView.Construct([buffer, JsNumber.Create(byteOffset), JsNumber.Create(length)], dataView);
        }

        return ConstructorFor(realm, type).Construct(buffer, byteOffset, length);
    }

    /// <summary>
    /// <c>Construct(%Uint8Array%, « buffer, byteOffset, byteLength »)</c> — the view every chunk a default
    /// reader takes out of a byte stream's queue is handed over as.
    /// </summary>
    internal static JsTypedArray ConstructUint8Array(Realm realm, JsArrayBuffer buffer, int byteOffset, int byteLength)
        => realm.Intrinsics.Uint8Array.Construct(buffer, byteOffset, byteLength);

    private static void AssertNotShared(Realm realm, JsArrayBuffer buffer, string what)
    {
        if (buffer.IsSharedArrayBuffer)
        {
            // Only an [AllowShared] declaration accepts a view onto shared memory, and no member of the
            // Streams Standard carries one — https://webidl.spec.whatwg.org/#AllowSharedBufferSource.
            Throw.TypeError(realm, $"{what} is backed by a SharedArrayBuffer");
        }
    }

    private static Native.TypedArray.TypedArrayConstructor ConstructorFor(Realm realm, TypedArrayElementType type) => type switch
    {
        TypedArrayElementType.Int8 => realm.Intrinsics.Int8Array,
        TypedArrayElementType.Uint8 => realm.Intrinsics.Uint8Array,
        TypedArrayElementType.Uint8C => realm.Intrinsics.Uint8ClampedArray,
        TypedArrayElementType.Int16 => realm.Intrinsics.Int16Array,
        TypedArrayElementType.Uint16 => realm.Intrinsics.Uint16Array,
        TypedArrayElementType.Int32 => realm.Intrinsics.Int32Array,
        TypedArrayElementType.Uint32 => realm.Intrinsics.Uint32Array,
        TypedArrayElementType.BigInt64 => realm.Intrinsics.BigInt64Array,
        TypedArrayElementType.BigUint64 => realm.Intrinsics.BigUint64Array,
        TypedArrayElementType.Float16 => realm.Intrinsics.Float16Array,
        TypedArrayElementType.Float32 => realm.Intrinsics.Float32Array,
        TypedArrayElementType.Float64 => realm.Intrinsics.Float64Array,
        _ => throw new InvalidOperationException("Unknown typed array element type " + type),
    };
}
#endif
