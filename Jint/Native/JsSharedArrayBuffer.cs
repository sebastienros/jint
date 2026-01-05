using System.Threading;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-sharedarraybuffer-objects
/// </summary>
internal sealed class JsSharedArrayBuffer : JsArrayBuffer
{
    // Using volatile for thread-safe reads/writes
    private volatile int _arrayBufferByteLengthData;

    internal JsSharedArrayBuffer(
        Engine engine,
        byte[] data,
        uint? arrayBufferMaxByteLength,
        uint arrayBufferByteLengthData) : base(engine, data, arrayBufferMaxByteLength)
    {
        if (arrayBufferByteLengthData > int.MaxValue)
        {
            Throw.RangeError(engine.Realm, "arrayBufferByteLengthData cannot be larger than int32.MaxValue");
        }
        this._arrayBufferByteLengthData = (int) arrayBufferByteLengthData;
    }

    internal override int ArrayBufferByteLength => _arrayBufferByteLengthData;

    /// <summary>
    /// Override Resize for SharedArrayBuffer to properly update the byte length.
    /// https://tc39.es/ecma262/#sec-sharedarraybuffer.prototype.grow
    /// </summary>
    internal new void Resize(uint newByteLength)
    {
        if (_arrayBufferMaxByteLength is null)
        {
            Throw.TypeError(_engine.Realm);
        }

        if (newByteLength > _arrayBufferMaxByteLength)
        {
            Throw.RangeError(_engine.Realm);
        }

        var currentByteLength = _arrayBufferByteLengthData;
        if (newByteLength < currentByteLength)
        {
            Throw.RangeError(_engine.Realm);
        }

        if (newByteLength == currentByteLength)
        {
            return;
        }

        // For growable SharedArrayBuffers, the underlying data block is pre-allocated
        // to maxByteLength, so we just need to update the byte length atomically
        // Zero-fill the new bytes (they should already be zero in a new array, but be safe)
        if (_arrayBufferData is not null)
        {
            for (var i = currentByteLength; i < (int) newByteLength; i++)
            {
                _arrayBufferData[i] = 0;
            }
        }

        // Atomically update the byte length
        Interlocked.Exchange(ref _arrayBufferByteLengthData, (int) newByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createsharedbytedatablock
    /// </summary>
    internal static byte[] CreateSharedByteDataBlock(Realm realm, ulong byteLength)
    {
        if (byteLength > int.MaxValue)
        {
            Throw.RangeError(realm, "Array buffer allocation failed");
        }

        return new byte[byteLength];
    }

    internal override bool IsSharedArrayBuffer => true;
}
