#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using Jint.Native;
using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The <c>crypto</c> object — an instance of the <c>Crypto</c> interface.
/// <para>
/// https://w3c.github.io/webcrypto/#crypto-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Both operations take their randomness from <see cref="RandomNumberGenerator"/>, which is the BCL's
/// cryptographically secure generator — the only source that can satisfy "fill bytes with cryptographically
/// secure random bytes", which both algorithms say. <c>Random</c> and <c>Math.random</c> are deliberately
/// nowhere near this file.
/// </para>
/// <para>
/// <c>crypto.subtle</c> exists and carries <b>ten of the twelve operations</b>: <c>digest</c>,
/// <c>sign</c>, <c>verify</c>, <c>encrypt</c>, <c>decrypt</c>, <c>generateKey</c>, <c>importKey</c>,
/// <c>exportKey</c>, <c>deriveBits</c> and <c>deriveKey</c>. <c>wrapKey</c> and <c>unwrapKey</c> are absent
/// rather than present-and-throwing, so the feature detection a library performing cryptography starts with
/// gets the truthful answer about each operation it means to use. See <see cref="SubtleCryptoInstance"/>.
/// </para>
/// <para>
/// Three documented simplifications against WebIDL, the first pair of which <c>console</c> carries too. There
/// is no <c>Crypto</c> interface object and no <c>Crypto.prototype</c>, so the two operations, the
/// <c>subtle</c> attribute and the <c>@@toStringTag</c> are own properties of this object with the attributes
/// an ECMAScript built-in member has — non-enumerable and configurable, the two operations writable as well —
/// rather than those of a WebIDL interface prototype's members. What a script can actually observe of that is unchanged: <c>Object.keys(crypto)</c>
/// answers the empty array in a browser too, because there the members live one level up on the prototype.
/// All three still brand-check their receiver, so extracting one and calling it on something else raises a
/// <c>TypeError</c> exactly as a browser does. The object is installed as an ordinary enumerable data
/// property of the global rather than through the <c>[Replaceable]</c> accessor pair WebIDL gives it. And
/// <c>[SecureContext]</c>, which gates <c>subtle</c> and <c>randomUUID</c> in a browser, has no meaning for
/// an embedded engine — there is no origin and no transport to be secure — so both are exposed
/// unconditionally.
/// </para>
/// </remarks>
[JsObject]
internal sealed partial class CryptoInstance : BuiltinShapeObject
{
    /// <summary>
    /// The quota step 3 enforces, https://w3c.github.io/webcrypto/#Crypto-method-getRandomValues: "If
    /// byteLength is greater than 65536, throw a QuotaExceededError".
    /// </summary>
    private const uint MaxRandomBytes = 65536;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString CryptoToStringTag = new("Crypto");

    private readonly Realm _realm;

    internal CryptoInstance(Engine engine, Realm realm, ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#Crypto-attribute-subtle — <c>[SecureContext] readonly attribute
    /// SubtleCrypto subtle</c>.
    /// </summary>
    /// <remarks>
    /// One object per realm, returned by reference, so <c>crypto.subtle === crypto.subtle</c> holds and a
    /// script may keep the reference. It is built on the first read and never before: a script that never
    /// mentions <c>subtle</c> has not paid for one.
    /// </remarks>
    [JsAccessor("subtle")]
    private SubtleCryptoInstance SubtleGet(JsValue thisObject)
    {
        Brand(thisObject, "Failed to read the 'subtle' property from 'Crypto'");
        return _realm.Intrinsics.SubtleCrypto;
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#Crypto-method-getRandomValues
    /// </summary>
    /// <remarks>
    /// <para>
    /// The IDL is <c>ArrayBufferView getRandomValues(ArrayBufferView array)</c>, so the argument goes through
    /// two separate gates and they fail differently. WebIDL's own <c>ArrayBufferView</c> conversion runs
    /// first and raises a <c>TypeError</c> for anything that is not a view at all — and for a view onto a
    /// <c>SharedArrayBuffer</c>, which only an operation declaring <c>[AllowShared]</c> accepts and this one
    /// does not (https://webidl.spec.whatwg.org/#es-arraybufferview). Step 1 then raises a
    /// <c>TypeMismatchError</c> <c>DOMException</c> for a view that <i>is</i> one but holds floats — a
    /// <c>Float32Array</c> or a <c>DataView</c>.
    /// </para>
    /// <para>
    /// A detached view, and a length-tracking view whose resizable buffer has shrunk past it, both have a
    /// byte length of zero. Step 4's byte sequence is then empty and step 6 writes it into nothing, so the
    /// array comes back untouched rather than raising: the quota in step 3 is a maximum, not a minimum.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "getRandomValues", Length = 1)]
    private JsValue GetRandomValues(JsValue thisObject, JsValue array)
    {
        Brand(thisObject, "Failed to execute 'getRandomValues' on 'Crypto'");

        if (array is JsDataView)
        {
            // A DataView passes WebIDL's ArrayBufferView conversion and fails step 1, which is why it is a
            // DOMException here and a TypeError two lines below.
            ThrowDomException(
                DomExceptionNames.TypeMismatch,
                "Failed to execute 'getRandomValues' on 'Crypto': the provided ArrayBufferView is a DataView, which is not an integer array type.");
        }

        if (array is not JsTypedArray typedArray)
        {
            Throw.TypeError(_realm, "Failed to execute 'getRandomValues' on 'Crypto': parameter 1 is not of type 'ArrayBufferView'.");
            return Undefined;
        }

        var buffer = typedArray._viewedArrayBuffer;
        if (buffer.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Failed to execute 'getRandomValues' on 'Crypto': parameter 1 is a view onto a SharedArrayBuffer, which this operation does not accept.");
            return Undefined;
        }

        var elementType = typedArray._arrayElementType;
        if (!IsIntegerElementType(elementType))
        {
            ThrowDomException(
                DomExceptionNames.TypeMismatch,
                $"Failed to execute 'getRandomValues' on 'Crypto': the provided ArrayBufferView is a {elementType.GetTypedArrayName()}, which is not an integer array type.");
        }

        // "Let byteLength be the byte length of array" — from the buffer witness, so a detached view and a
        // length-tracking view that has fallen out of bounds both answer zero, exactly as the typed array's
        // own byteLength accessor does.
        var record = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(typedArray, ArrayBufferOrder.SeqCst);
        var byteLength = record.TypedArrayByteLength;

        if (byteLength > MaxRandomBytes)
        {
            // WebIDL has since given QuotaExceededError an interface of its own carrying `quota` and
            // `requested`; Jint exposes the name on a plain DOMException, which is what every browser did
            // until that change and what the algorithm's own wording asks for.
            ThrowDomException(
                DomExceptionNames.QuotaExceeded,
                $"Failed to execute 'getRandomValues' on 'Crypto': the ArrayBufferView's byte length ({byteLength}) exceeds the {MaxRandomBytes} bytes of entropy this operation provides.");
        }

        var data = buffer.ArrayBufferData;
        if (byteLength == 0 || data is null)
        {
            return array;
        }

        // https://tc39.es/proposal-immutable-arraybuffer/#sec-isimmutablebuffer — writing into an immutable
        // buffer is a TypeError, the same one an ordinary element assignment raises, and for the same reason.
        // It is asked only once there is something to write, which is also how IntegerIndexedElementSet
        // orders it: a write that does not happen cannot fail.
        buffer.AssertNotImmutable();

        RandomNumberGenerator.Fill(data.AsSpan(typedArray._byteOffset, (int) byteLength));

        // Step 7: the very same object, never a copy.
        return array;
    }

    /// <summary>
    /// https://w3c.github.io/webcrypto/#Crypto-method-randomUUID
    /// </summary>
    /// <remarks>
    /// The algorithm verbatim: sixteen cryptographically secure random bytes, the four most significant bits
    /// of byte 6 set to <c>0100</c> (the version) and the two most significant bits of byte 8 set to
    /// <c>10</c> (the variant), rendered as the lowercase hyphenated form. <c>Guid.NewGuid()</c> would be one
    /// line, and does produce a version 4 UUID, but which generator backs it is a platform-specific
    /// implementation detail rather than a documented guarantee — and "cryptographically secure random bytes"
    /// is the one thing this operation exists to promise. <see cref="RandomNumberGenerator"/> promises it in
    /// writing, so the bytes come from there and <see cref="Guid"/> is used only to format them: the
    /// big-endian constructor and <c>ToString("D")</c> are exactly the hex-and-hyphens concatenation the last
    /// step spells out.
    /// </remarks>
    [JsFunction(Name = "randomUUID", Length = 0)]
    private JsString RandomUuid(JsValue thisObject)
    {
        Brand(thisObject, "Failed to execute 'randomUUID' on 'Crypto'");

        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);

        bytes[6] = (byte) ((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte) ((bytes[8] & 0x3F) | 0x80);

        return JsString.Create(new Guid(bytes, bigEndian: true).ToString("D", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Step 1's list of accepted views: every integer typed array, which is every typed array except the
    /// three floating-point ones. Written as an explicit list rather than as "not a float", so that a typed
    /// array type added to the engine later is refused until someone has read this algorithm again.
    /// </summary>
    private static bool IsIntegerElementType(TypedArrayElementType type) => type
        is TypedArrayElementType.Int8
        or TypedArrayElementType.Uint8
        or TypedArrayElementType.Uint8C
        or TypedArrayElementType.Int16
        or TypedArrayElementType.Uint16
        or TypedArrayElementType.Int32
        or TypedArrayElementType.Uint32
        or TypedArrayElementType.BigInt64
        or TypedArrayElementType.BigUint64;

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>, so an extracted <c>getRandomValues</c> — or the <c>subtle</c>
    /// getter — cannot be called on anything else.
    /// </summary>
    private void Brand(JsValue thisObject, string what)
    {
        if (thisObject is not CryptoInstance)
        {
            Throw.TypeError(_realm, what + ": illegal invocation, receiver is not a Crypto object.");
        }
    }

    [DoesNotReturn]
    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }
}
#endif
