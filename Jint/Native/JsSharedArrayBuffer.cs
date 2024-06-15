using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-sharedarraybuffer-objects
/// </summary>
internal sealed class JsSharedArrayBuffer : JsArrayBuffer
{
    private readonly int _arrayBufferByteLengthData;

    internal JsSharedArrayBuffer(
        Engine engine,
        byte[] data,
        uint? arrayBufferMaxByteLength,
        uint arrayBufferByteLengthData) : base(engine, data, arrayBufferMaxByteLength)
    {
        if (arrayBufferByteLengthData > int.MaxValue)
        {
            ExceptionHelper.ThrowRangeError(engine.Realm, "arrayBufferByteLengthData cannot be larger than int32.MaxValue");
        }
        this._arrayBufferByteLengthData = (int) arrayBufferByteLengthData;
    }

    internal override int ArrayBufferByteLength => _arrayBufferByteLengthData;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createsharedbytedatablock
    /// </summary>
    internal static byte[] CreateSharedByteDataBlock(Realm realm, ulong byteLength)
    {
        if (byteLength > int.MaxValue)
        {
            ExceptionHelper.ThrowRangeError(realm, "Array buffer allocation failed");
        }

        return new byte[byteLength];
    }

    internal override bool IsSharedArrayBuffer => true;
}
