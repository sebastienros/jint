using System.Text;
using Jint.Extensions;
using Jint.Native.ArrayBuffer;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.TypedArray;

[JsObject]
internal sealed partial class Uint8ArrayPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TypedArrayConstructor _constructor;

    [JsProperty(Name = "BYTES_PER_ELEMENT", Flags = PropertyFlag.AllForbidden)]
    private static readonly JsNumber BytesPerElementValue = JsNumber.PositiveOne;

    public Uint8ArrayPrototype(
        Engine engine,
        IntrinsicTypedArrayPrototype objectPrototype,
        TypedArrayConstructor constructor)
        : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize() => CreateProperties_Generated();

    [JsFunction(Length = 1)]
    private JsObject SetFromBase64(JsValue thisObject, JsValue s, JsValue options)
    {
        var into = ValidateUint8Array(thisObject);

        if (!s.IsString())
        {
            Throw.TypeError(_realm, "setFromBase64 must be called with a string");
        }

        var opts = Uint8ArrayConstructor.GetOptionsObject(_engine, options);
        var alphabet = Uint8ArrayConstructor.GetAndValidateAlphabetOption(_engine, opts);
        var lastChunkHandling = Uint8ArrayConstructor.GetAndValidateLastChunkHandling(_engine, opts);

        var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(into, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(_realm, "TypedArray is out of bounds");
        }
        into._viewedArrayBuffer.AssertNotImmutable();

        var byteLength = taRecord.TypedArrayLength;
        var result = Uint8ArrayConstructor.FromBase64(_engine, s.ToString(), alphabet.ToString(), lastChunkHandling.ToString(), byteLength);
        SetUint8ArrayBytes(into, result.Bytes);

        if (result.Error is not null)
        {
            throw result.Error;
        }

        var resultObject = OrdinaryObjectCreate(_engine, _engine.Intrinsics.Object);
        resultObject.CreateDataPropertyOrThrow("read", result.Read);
        resultObject.CreateDataPropertyOrThrow("written", result.Bytes.Length);
        return resultObject;
    }

    private static void SetUint8ArrayBytes(JsTypedArray into, byte[] bytes)
    {
        var offset = into._byteOffset;
        var len = bytes.Length;
        var index = 0;
        while (index < len)
        {
            var b = bytes[index];
            var byteIndexInBuffer = index + offset;
            into._viewedArrayBuffer.SetValueInBuffer(byteIndexInBuffer, TypedArrayElementType.Uint8, b, isTypedArray: true, ArrayBufferOrder.Unordered);
            index++;
        }
    }

    [JsFunction]
    private JsObject SetFromHex(JsValue thisObject, JsValue s)
    {
        var into = ValidateUint8Array(thisObject);

        if (!s.IsString())
        {
            Throw.TypeError(_realm, "setFromHex must be called with a string");
        }

        var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(into, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(_realm, "TypedArray is out of bounds");
        }
        into._viewedArrayBuffer.AssertNotImmutable();

        var byteLength = taRecord.TypedArrayLength;
        var result = Uint8ArrayConstructor.FromHex(_engine, s.ToString(), byteLength);
        SetUint8ArrayBytes(into, result.Bytes);

        if (result.Error is not null)
        {
            throw result.Error;
        }

        var resultObject = OrdinaryObjectCreate(_engine, _engine.Intrinsics.Object);
        resultObject.CreateDataPropertyOrThrow("read", result.Read);
        resultObject.CreateDataPropertyOrThrow("written", result.Bytes.Length);
        return resultObject;
    }

    [JsFunction(Length = 0)]
    private JsValue ToBase64(JsValue thisObject, JsValue options)
    {
        var o = ValidateUint8Array(thisObject);

        var opts = Uint8ArrayConstructor.GetOptionsObject(_engine, options);
        var alphabet = Uint8ArrayConstructor.GetAndValidateAlphabetOption(_engine, opts);

        var omitPadding = TypeConverter.ToBoolean(opts.Get("omitPadding"));
#if NETCOREAPP
        var toEncode = GetUint8ArrayBytes(o);
#else
        var toEncode = GetUint8ArrayBytes(o).ToArray();
#endif

        string outAscii;
        if (alphabet == "base64")
        {
            outAscii = Convert.ToBase64String(toEncode);
            if (omitPadding)
            {
                outAscii = outAscii.TrimEnd('=');
            }
        }
        else
        {
            outAscii = WebEncoders.Base64UrlEncode(toEncode, omitPadding);
        }

        return outAscii;
    }

    [JsFunction]
    private JsValue ToHex(JsValue thisObject)
    {
        var o = ValidateUint8Array(thisObject);
        var toEncode = GetUint8ArrayBytes(o);

        using var outString = new ValueStringBuilder();
        foreach (var b in toEncode)
        {
            var b1 = (byte) (b >> 4);
            outString.Append((char) (b1 > 9 ? b1 - 10 + 'a' : b1 + '0'));

            var b2 = (byte) (b & 0x0F);
            outString.Append((char) (b2 > 9 ? b2 - 10 + 'a' : b2 + '0'));
        }

        return outString.ToString();
    }

    private ReadOnlySpan<byte> GetUint8ArrayBytes(JsTypedArray ta)
    {
        var buffer = ta._viewedArrayBuffer;
        var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(ta, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(_realm, "TypedArray is out of bounds");
        }

        return buffer._arrayBufferData!.AsSpan(0, (int) taRecord.TypedArrayLength);
    }

    private JsTypedArray ValidateUint8Array(JsValue ta)
    {
        if (ta is not JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8 } typedArray)
        {
            Throw.TypeError(_engine.Realm, "Not a Uint8Array");
            return default;
        }

        return typedArray;
    }
}
