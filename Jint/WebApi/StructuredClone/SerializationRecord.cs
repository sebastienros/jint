#if NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.TypedArray;

namespace Jint.WebApi.StructuredClone;

/// <summary>
/// The result of StructuredSerialize: a value the HTML Standard calls a <i>serialization record</i>,
/// expressed as a graph that belongs to no engine at all.
/// <para>
/// https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializewithtransfer
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing reachable from here is engine-affine.</b> Every node is a plain CLR object — numbers, strings,
/// <see cref="BigInteger"/>s, <c>byte[]</c>s, lists and the tagged records below — and there is deliberately
/// no <c>JsValue</c>, <c>Engine</c>, <c>Realm</c> or <c>ObjectInstance</c> anywhere in the graph. That is what
/// makes a record safe to build on one engine's thread and consume on another's, which is what
/// <c>MessagePort</c> does: <see cref="StructuredSerializer"/> runs entirely on the sender and
/// <see cref="StructuredDeserializer"/> entirely on the receiver, and the record is the only thing that
/// crosses. <c>Jint.Tests.Runtime.WebApi.SerializationRecordTests</c> pins the property both from the type
/// declarations and from a walk of an actual graph.
/// </para>
/// <para>
/// <b>A transferred <c>MessagePort</c> is the one deliberate exception, and it is an exception to the letter
/// of that rule rather than to its point.</b> <see cref="TransferredPorts"/> carries
/// <see cref="Messaging.MessagePortEndpoint"/>s, and a side does reach an <c>Engine</c> and the
/// <c>MessagePort</c> object currently bound to it. But that class <i>is</i> the sanctioned engine-crossing
/// handle — it is what a sender has always held for its peer, and every member of it a foreign thread may
/// touch is immutable, <see langword="volatile"/> or behind its own lock. Nothing else changes: no
/// <c>JsValue</c> is read or written across the boundary, and the receiving engine never dereferences the
/// sending engine. Re-pointing a channel is exactly the operation a transfer <i>is</i>, so the handle that
/// does it is what the record has to carry.
/// </para>
/// <para>
/// <b>A record is consumed once, unless the reader is told otherwise.</b> Deserializing takes ownership of the
/// byte arrays it finds — a transferred <c>ArrayBuffer</c>'s storage is moved rather than copied, which is the
/// whole point of transferring — so deserializing one record twice would hand two engines one mutable buffer.
/// <c>structuredClone</c> and <c>MessagePort</c> therefore hand each record to exactly one
/// <see cref="StructuredDeserializer"/>. <c>BroadcastChannel</c> is the one thing that cannot: its
/// <c>postMessage</c> serializes once and every destination deserializes that same record, which is what the
/// standard's steps say, so it asks the deserializer to copy the storage instead
/// (<see cref="StructuredDeserializer"/>'s <c>sharedRecord</c>). It has no transfer list, so there is never
/// storage there that had to be moved rather than copied.
/// </para>
/// <para>
/// Sharing and cycles are carried by CLR reference identity: two references to one source object serialize to
/// two references to one <see cref="SerializedObject"/>, and the deserializer's own memory map turns that back
/// into two references to one clone.
/// </para>
/// </remarks>
/// <param name="Root">The serialized form of the value that was handed to StructuredSerialize.</param>
/// <param name="TransferredPorts">
/// StructuredSerializeWithTransfer's <c>[[TransferDataHolders]]</c>, narrowed to the ports — in transfer-list
/// order, which is the order <c>MessageEvent.ports</c> has to preserve. <see langword="null"/> when no port
/// was transferred, which is every message <c>BroadcastChannel</c> ever produces and almost every other one.
/// <para>
/// A transferred <c>ArrayBuffer</c> is deliberately <i>not</i> listed here even though the specification puts
/// it in the same list: nothing consumes the transferred values of a buffer, and the walk already resolves one
/// through the serializer's memory map. Ports need the list because they are created <b>before</b> the graph
/// is walked — a port referenced from the message must be the very object <c>ports</c> hands over — and
/// because the event exposes them in order.
/// </para>
/// <para>
/// It also carries the channels a <i>transferred stream</i> created for itself, which are the results of the
/// nested serializations the stream transfer steps perform. Those are marked
/// <see cref="SerializedMessagePort.Nested"/> and never reach <c>event.ports</c>; they are here because
/// ending a side nobody will ever bind is exactly as necessary for them, and rather more so — a script can
/// close a port it was given, and can do nothing at all about a stream's private one.
/// </para>
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct SerializationRecord(SerializedValue Root, List<SerializedMessagePort>? TransferredPorts = null);

/// <summary>
/// Which of the seven shapes a <see cref="SerializedValue"/> holds.
/// </summary>
internal enum SerializedValueKind : byte
{
    /// <summary>The <c>undefined</c> value.</summary>
    Undefined,

    /// <summary>The <c>null</c> value.</summary>
    Null,

    /// <summary>A Boolean.</summary>
    Boolean,

    /// <summary>A Number, including <c>NaN</c>, the infinities and negative zero.</summary>
    Number,

    /// <summary>A BigInt.</summary>
    BigInt,

    /// <summary>A String.</summary>
    String,

    /// <summary>A reference to a <see cref="SerializedObject"/>.</summary>
    Object,
}

/// <summary>
/// One node of a serialization record graph: either a serialized primitive, or a reference to a
/// <see cref="SerializedObject"/>.
/// </summary>
/// <remarks>
/// A tagged union rather than a class hierarchy, so a primitive costs no allocation of its own and an object
/// property list is one array of these. <see cref="SerializedValueKind.Boolean"/> is carried in the numeric
/// slot as one or zero; <see cref="SerializedValueKind.BigInt"/> boxes its <see cref="BigInteger"/>, which is
/// rare enough not to matter.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly struct SerializedValue
{
    private readonly object? _reference;
    private readonly double _number;

    private SerializedValue(SerializedValueKind kind, object? reference, double number)
    {
        Kind = kind;
        _reference = reference;
        _number = number;
    }

    internal SerializedValueKind Kind { get; }

    internal static SerializedValue Undefined => new(SerializedValueKind.Undefined, null, 0);

    internal static SerializedValue Null => new(SerializedValueKind.Null, null, 0);

    internal static SerializedValue FromBoolean(bool value) => new(SerializedValueKind.Boolean, null, value ? 1 : 0);

    internal static SerializedValue FromNumber(double value) => new(SerializedValueKind.Number, null, value);

    internal static SerializedValue FromBigInt(BigInteger value) => new(SerializedValueKind.BigInt, value, 0);

    internal static SerializedValue FromString(string value) => new(SerializedValueKind.String, value, 0);

    internal static SerializedValue FromObject(SerializedObject value) => new(SerializedValueKind.Object, value, 0);

    internal bool AsBoolean() => _number != 0;

    internal double AsNumber() => _number;

    internal BigInteger AsBigInt() => (BigInteger) _reference!;

    internal string AsString() => (string) _reference!;

    internal SerializedObject AsObject() => (SerializedObject) _reference!;
}

/// <summary>
/// The serialized form of one object, tagged by which of StructuredSerializeInternal's arms produced it.
/// Reference identity is the graph's identity: the same instance appearing twice means the same object
/// appeared twice.
/// </summary>
internal abstract class SerializedObject
{
}

/// <summary>
/// Steps 7-10: a boxed <c>Boolean</c>, <c>Number</c>, <c>BigInt</c> or <c>String</c>, identified by which data
/// slot it carries.
/// </summary>
internal sealed class SerializedBoxedPrimitive(SerializedValue value) : SerializedObject
{
    /// <summary>The primitive in the box, whose <see cref="SerializedValue.Kind"/> says which it is.</summary>
    internal SerializedValue Value { get; } = value;
}

/// <summary>
/// Step 11: <c>[[DateValue]]</c> and nothing else, so an invalid <c>Date</c> serializes to an invalid
/// <c>Date</c>.
/// </summary>
/// <remarks>
/// Carried as Jint's own <see cref="DatePresentation"/> rather than as a <see cref="double"/>, because that
/// struct distinguishes NaN from an out-of-range infinity where a <see cref="double"/> round trip would not.
/// It is a <c>long</c> and a flags enum and belongs to no engine, so it is as neutral as the number would have
/// been.
/// </remarks>
internal sealed class SerializedDate(DatePresentation dateValue) : SerializedObject
{
    internal DatePresentation DateValue { get; } = dateValue;
}

/// <summary>
/// Step 12: <c>[[OriginalSource]]</c>, <c>[[OriginalFlags]]</c> and the compiled <c>[[RegExpMatcher]]</c>.
/// </summary>
/// <remarks>
/// The matcher is shared rather than recompiled — it is immutable and belongs to no engine, and re-parsing the
/// source would fail outright for a host-supplied .NET <see cref="Regex"/> whose pattern is not a JavaScript
/// one. A <see cref="Regex"/> and the parse result behind it are documented as safe for concurrent matching,
/// so sharing one across two engines on two threads is sound.
/// </remarks>
internal sealed class SerializedRegExp : SerializedObject
{
    internal Regex Matcher { get; init; } = null!;

    internal string Source { get; init; } = null!;

    internal string Flags { get; init; } = null!;

    internal RegExpParseResult ParseResult { get; init; }

    internal bool IsHostRegex { get; init; }
}

/// <summary>
/// Step 13: an <c>ArrayBuffer</c>, either copied or — when it was named in the transfer list — moved.
/// </summary>
/// <remarks>
/// <see cref="Bytes"/> is settable because a transferred buffer's storage is only taken at the very end of
/// StructuredSerializeWithTransfer, after the whole walk: a getter reached during the walk is allowed to
/// resize the buffer, which replaces its data block outright, and the transfer must carry whatever the source
/// held when serialization finished.
/// </remarks>
internal sealed class SerializedArrayBuffer : SerializedObject
{
    internal byte[] Bytes { get; set; } = [];

    /// <summary><c>[[ArrayBufferMaxByteLength]]</c>, or <see langword="null"/> for a fixed-length buffer.</summary>
    internal uint? MaxByteLength { get; init; }

    /// <summary>
    /// Whether the source was an immutable <c>ArrayBuffer</c>. Not something the HTML Standard knows about —
    /// the immutable-ArrayBuffer proposal has no structured-clone integration — but carrying it keeps the
    /// clone the same kind of buffer the source was.
    /// </summary>
    internal bool Immutable { get; init; }
}

/// <summary>
/// Step 14: a typed array or a <c>DataView</c> over a <see cref="SerializedArrayBuffer"/>.
/// </summary>
/// <remarks>
/// The buffer is referenced rather than embedded, and it is the very record the buffer itself serialized to.
/// That is what makes two views over one buffer come out as two views over one clone, and what makes a view of
/// a <i>transferred</i> buffer land on the transferred storage.
/// </remarks>
internal sealed class SerializedArrayBufferView : SerializedObject
{
    internal SerializedArrayBuffer Buffer { get; init; } = null!;

    /// <summary>
    /// The element type, or <see langword="null"/> for a <c>DataView</c> — which records
    /// <c>[[ByteLength]]</c> where a typed array records <c>[[ArrayLength]]</c>.
    /// </summary>
    internal TypedArrayElementType? ElementType { get; init; }

    internal uint ByteOffset { get; init; }

    /// <summary>
    /// <c>[[ArrayLength]]</c> for a typed array and <c>[[ByteLength]]</c> for a <c>DataView</c>, or
    /// <see langword="null"/> for a length-tracking view over a resizable buffer.
    /// </summary>
    internal uint? Length { get; init; }
}

/// <summary>
/// A transferred <c>MessagePort</c>: the specification's data holder, whose
/// <c>[[PortMessageQueue]]</c> and <c>[[RemotePort]]</c> are both reached through the one
/// <see cref="Messaging.MessagePortEndpoint"/> the transfer detached from the source port.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#messageport — "their transfer steps".
/// </para>
/// </summary>
/// <remarks>
/// <see cref="Endpoint"/> is settable for the same reason <see cref="SerializedArrayBuffer.Bytes"/> is: a
/// transferable is given its (still empty) record before the walk so that a reference to it inside the message
/// resolves here, and the source is only detached at the very end of StructuredSerializeWithTransfer. It is
/// also <i>taken</i> — set back to <see langword="null"/> — by the deserialization that binds it, so a record
/// read a second time cannot hand two engines one side of a channel; see <see cref="SerializationRecord"/> on
/// a record being consumed once.
/// </remarks>
internal sealed class SerializedMessagePort : SerializedObject
{
    internal Messaging.MessagePortEndpoint? Endpoint { get; set; }

    /// <summary>
    /// Whether this holder belongs to a <i>nested</i> StructuredSerializeWithTransfer that a transferable's
    /// own transfer steps performed — which today means the channel a transferred stream travels over — rather
    /// than to the transfer list the caller wrote.
    /// </summary>
    /// <remarks>
    /// It decides one thing: <c>event.ports</c> exposes the caller's ports and not a stream's private one. The
    /// two kinds share <see cref="SerializationRecord.TransferredPorts"/> anyway, because the other thing that
    /// list is for — ending a side nobody can ever bind — has to reach both, and a stream's channel is
    /// precisely the side no script could ever end by hand.
    /// </remarks>
    internal bool Nested { get; init; }
}

/// <summary>
/// A transferred <c>ReadableStream</c>: the standard's data holder, whose <c>[[port]]</c> is the receiving end
/// of the channel the sender is piping the original stream into.
/// <para>
/// https://streams.spec.whatwg.org/#rs-transfer
/// </para>
/// </summary>
/// <remarks>
/// The standard's <c>[[port]]</c> is the <i>result of a nested StructuredSerializeWithTransfer</i> of one
/// port; <see cref="SerializedMessagePort"/> is what that result amounts to here, so the field holds one
/// directly — see <c>TransferableStreams</c>. Settable for the reason every transfer data holder's payload is:
/// the holder is created before the graph is walked and filled in by step 5.
/// </remarks>
internal sealed class SerializedReadableStream : SerializedObject
{
    internal SerializedMessagePort Port { get; set; } = null!;
}

/// <summary>
/// A transferred <c>WritableStream</c>, the mirror image of <see cref="SerializedReadableStream"/>: the sender
/// is piping a readable of its own <i>into</i> the original stream, and the port is how chunks reach it.
/// <para>
/// https://streams.spec.whatwg.org/#ws-transfer
/// </para>
/// </summary>
internal sealed class SerializedWritableStream : SerializedObject
{
    internal SerializedMessagePort Port { get; set; } = null!;
}

/// <summary>
/// A transferred <c>TransformStream</c>: its two sides, each transferred in its own right.
/// <para>
/// https://streams.spec.whatwg.org/#ts-transfer
/// </para>
/// </summary>
/// <remarks>
/// <c>[[backpressure]]</c>, <c>[[backpressureChangePromise]]</c> and <c>[[controller]]</c> are deliberately
/// absent: the standard's transfer-receiving steps set all three to undefined, because what arrives is a
/// readable and a writable with two channels between them and no transformer of its own.
/// </remarks>
internal sealed class SerializedTransformStream : SerializedObject
{
    internal SerializedReadableStream Readable { get; set; } = null!;

    internal SerializedWritableStream Writable { get; set; } = null!;
}

/// <summary>
/// Step 18: an Array exotic object — its length, plus the enumerable own string-keyed properties step 26.4
/// collected.
/// </summary>
internal sealed class SerializedArray(uint length) : SerializedObject
{
    internal uint Length { get; } = length;

    internal List<SerializedProperty> Properties { get; } = [];
}

/// <summary>
/// Step 24: an ordinary object, which is nothing but its enumerable own string-keyed properties.
/// </summary>
internal sealed class SerializedPlainObject : SerializedObject
{
    internal List<SerializedProperty> Properties { get; } = [];
}

/// <summary>
/// One entry of step 26.4's property list: the key as a plain string, and the serialized form of what
/// <c>[[Get]]</c> answered for it.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct SerializedProperty(string Key, SerializedValue Value);

/// <summary>
/// Steps 15 and 26.1: a <c>Map</c>, whose entries were snapshot before any of them was serialized.
/// </summary>
internal sealed class SerializedMap : SerializedObject
{
    internal List<SerializedMapEntry> Entries { get; } = [];
}

/// <summary>One <c>Map</c> entry, key serialized before value as step 26.1 requires.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct SerializedMapEntry(SerializedValue Key, SerializedValue Value);

/// <summary>
/// Steps 16 and 26.2: a <c>Set</c>.
/// </summary>
internal sealed class SerializedSet : SerializedObject
{
    internal List<SerializedValue> Entries { get; } = [];
}

/// <summary>
/// Which of the error intrinsics an error's <c>name</c> reduced to. Step 17 keeps the seven names that have an
/// intrinsic of their own and folds everything else — <c>AggregateError</c>, a subclass, a name the script made
/// up — into <see cref="Error"/>.
/// </summary>
internal enum SerializedErrorName : byte
{
    Error,
    EvalError,
    RangeError,
    ReferenceError,
    SyntaxError,
    TypeError,
    UriError,
}

/// <summary>
/// Step 17: an <c>Error</c>. The message is <see langword="null"/> when the source had none as an own data
/// property, which is the difference between a clone that has no own <c>message</c> and one whose
/// <c>message</c> is the empty string.
/// </summary>
internal sealed class SerializedError : SerializedObject
{
    internal SerializedErrorName Name { get; init; }

    internal string? Message { get; init; }

    /// <summary>
    /// The <c>stack</c>, which neither specification requires but both say a user agent "should attach". An
    /// error whose trace pointed at the clone site rather than at where it was raised would be actively
    /// misleading.
    /// </summary>
    internal string? Stack { get; init; }
}

/// <summary>
/// The <c>DOMException</c> serialization steps, https://webidl.spec.whatwg.org/#idl-DOMException: the name and
/// the message, and nothing else. <c>code</c> is derived from the name on the way back out.
/// </summary>
internal class SerializedDomException : SerializedObject
{
    internal string Name { get; init; } = null!;

    internal string Message { get; init; } = null!;

    internal string? Stack { get; init; }
}

/// <summary>
/// The <c>QuotaExceededError</c> serialization steps, https://webidl.spec.whatwg.org/#quotaexceedederror:
/// "Run the DOMException serialization steps", then <c>[[Quota]]</c> and <c>[[Requested]]</c>.
/// </summary>
/// <remarks>
/// A record of its own rather than two nullable fields on the base, because the discriminator cannot be the
/// name: <c>new DOMException('x', 'QuotaExceededError')</c> is a plain <c>DOMException</c> wearing the name and
/// has to come back as one. Deriving it is what makes "run the base steps" literal — the two switches that
/// consume these records match it before <see cref="SerializedDomException"/>, and the compiler refuses the
/// other order.
/// </remarks>
internal sealed class SerializedQuotaExceededError : SerializedDomException
{
    /// <summary>The <c>[[Quota]]</c>: a number, or <see langword="null"/>.</summary>
    internal double? Quota { get; init; }

    /// <summary>The <c>[[Requested]]</c>: a number, or <see langword="null"/>.</summary>
    internal double? Requested { get; init; }
}
#endif
