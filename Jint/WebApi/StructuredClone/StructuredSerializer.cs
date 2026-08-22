#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.ArrayBuffer;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Files;
using Jint.WebApi.Messaging;
using Jint.WebApi.Streams;

namespace Jint.WebApi.StructuredClone;

/// <summary>
/// StructuredSerializeWithTransfer: walks a value graph on <i>this</i> engine and produces the engine-neutral
/// <see cref="SerializationRecord"/> that <see cref="StructuredDeserializer"/> turns back into objects.
/// <para>
/// https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializewithtransfer
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// This half runs entirely on the engine that owns the source graph: it invokes getters, reads internal slots
/// and detaches transferred buffers, all of which are things only that engine's thread may do. Everything it
/// produces belongs to nobody, which is what lets a <c>MessagePort</c> hand the record to another engine
/// altogether.
/// </para>
/// <para>
/// <b>The walk is iterative, not recursive.</b> The specification's algorithm recurses once per graph edge and
/// bounds nothing, so a hostile <c>{a:{a:{a:…}}}</c> would take the native stack down with it — and a stack
/// overflow kills the process rather than raising something a host can catch. Container records therefore push
/// a <see cref="SerializeFrame"/> onto a heap <see cref="Stack{T}"/> and <see cref="Drain"/> runs them to
/// completion, so the only limit on nesting depth is available memory. Progress is charged against the
/// engine's execution constraints every <see cref="Engine.ConstraintCheckInterval"/> values, so serializing a
/// very large graph stays interruptible by a timeout or a cancellation the same way a long loop is.
/// </para>
/// </remarks>
internal sealed class StructuredSerializer
{
    private readonly Engine _engine;
    private readonly Realm _realm;

    /// <summary>
    /// The specification's <i>memory</i> map, keyed on object identity: what makes a cycle terminate and what
    /// makes two references to one object serialize to two references to one record.
    /// </summary>
    private readonly Dictionary<ObjectInstance, SerializedObject> _memory = new(ReferenceEqualityComparer.Instance);

    private readonly Stack<SerializeFrame> _pending = new();

    /// <summary>
    /// The port half of <c>[[TransferDataHolders]]</c>, in transfer-list order; see
    /// <see cref="SerializationRecord.TransferredPorts"/>. Null until a port is actually named.
    /// </summary>
    private List<SerializedMessagePort>? _transferredPorts;

    private int _visited;

    internal StructuredSerializer(Engine engine, Realm realm)
    {
        _engine = engine;
        _realm = realm;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializewithtransfer
    /// </summary>
    /// <param name="value">The value to serialize.</param>
    /// <param name="transferList">
    /// The already-iterated <c>transfer</c> option, or <see langword="null"/> when the caller passed none.
    /// Its entries are objects, which is all the WebIDL <c>sequence&lt;object&gt;</c> conversion guarantees;
    /// deciding whether they are <i>transferable</i> is this algorithm's job.
    /// </param>
    internal SerializationRecord Serialize(JsValue value, List<JsValue>? transferList)
    {
        // Steps 2-4: every transferable is validated and given its (still empty) record BEFORE the walk, so a
        // transferred buffer or port reached from `value` resolves to that same record.
        var transfers = PrepareTransfers(transferList);

        try
        {
            var root = SerializeValue(value);
            Drain();

            // Step 5: only now is anything detached.
            CompleteTransfers(transfers);

            return new SerializationRecord(root, _transferredPorts);
        }
        catch
        {
            // A throw anywhere from the walk onwards discards the half-built record, and step 5 detaches in
            // list order, so a port earlier in the list may already have handed its side over with nothing
            // left that can ever bind it. Ending those is the same answer step 6's doomed case gets, for the
            // same reason: a side nobody can pick up must not leave its peer posting into a queue forever.
            StrandTransferredPorts(_transferredPorts);
            throw;
        }
    }

    /// <summary>
    /// Ends every channel side a record carries but nobody will ever bind — which is what a message that is
    /// serialized and then not delivered leaves behind. See <c>JsMessagePort.PostMessage</c>'s step 6.
    /// </summary>
    internal static void StrandTransferredPorts(in SerializationRecord record) => StrandTransferredPorts(record.TransferredPorts);

    private static void StrandTransferredPorts(List<SerializedMessagePort>? holders)
    {
        if (holders is null)
        {
            return;
        }

        foreach (var holder in holders)
        {
            // Null for a holder whose source was never detached, because the throw came before step 5 reached
            // it — there is nothing to end there.
            holder.Endpoint?.Close();
            holder.Endpoint = null;
        }
    }

    /// <summary>
    /// The spine of the walk: runs each pending container frame to exhaustion, depth first, so a value's own
    /// graph is finished before its next sibling is read — exactly the order the specification's recursion
    /// produces, which is what a getter observes.
    /// </summary>
    private void Drain()
    {
        while (_pending.Count > 0)
        {
            var frame = _pending.Peek();
            if (!frame.TryGetNextSource(out var next))
            {
                _pending.Pop();
                continue;
            }

            frame.Accept(SerializeValue(next));
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializeinternal
    /// </summary>
    private SerializedValue SerializeValue(JsValue value)
    {
        // A native walk over a script-controlled graph does not self-throttle through statement counting.
        if (++_visited % Engine.ConstraintCheckInterval == 0)
        {
            _engine.Constraints.Check();
        }

        if (value is not ObjectInstance source)
        {
            // Step 5: a Symbol is the one primitive that is not serializable.
            if (value.IsSymbol())
            {
                ThrowDataCloneError("A Symbol could not be cloned");
            }

            // Step 4: undefined, null, Boolean, Number, BigInt and String values are their own serialization.
            return SerializePrimitive(value);
        }

        // Step 2.
        if (_memory.TryGetValue(source, out var seen))
        {
            return SerializedValue.FromObject(seen);
        }

        SerializedObject record;
        switch (source)
        {
            // WebIDL declares QuotaExceededError [Serializable] too, and its steps run DOMException's and then
            // add [[Quota]] and [[Requested]] — https://webidl.spec.whatwg.org/#quotaexceedederror. Checked
            // before the DOMException arm it derives from, so the interface survives a clone rather than
            // flattening into a DOMException that merely wears the name.
            case JsQuotaExceededError quotaExceeded:
                record = new SerializedQuotaExceededError
                {
                    Name = quotaExceeded.Name.ToString(),
                    Message = quotaExceeded.Message.ToString(),
                    Stack = ReadStack(quotaExceeded),
                    Quota = quotaExceeded.Quota.IsNull() ? null : ((JsNumber) quotaExceeded.Quota)._value,
                    Requested = quotaExceeded.Requested.IsNull() ? null : ((JsNumber) quotaExceeded.Requested)._value,
                };
                break;

            // WebIDL declares DOMException [Serializable]; its serialization steps carry the name and the
            // message. https://webidl.spec.whatwg.org/#idl-DOMException — checked before the ErrorInstance arm
            // below, which it derives from.
            case JsDomException domException:
                record = new SerializedDomException
                {
                    Name = domException.Name.ToString(),
                    Message = domException.Message.ToString(),
                    Stack = ReadStack(domException),
                };
                break;

            // Step 19, "value is a platform object that is a serializable object", for the two the File API
            // declares [Serializable]. https://w3c.github.io/FileAPI/#file-section runs the Blob steps and
            // then adds the name and the last-modified time, so — as with QuotaExceededError above — the
            // derived interface is matched first, or a File would flatten into a Blob.
            case JsFile file:
                record = new SerializedFile
                {
                    Bytes = file.Data,
                    MediaType = file.MediaType,
                    Name = file.Name,
                    LastModified = file.LastModified,
                };
                break;

            case JsBlob blob:
                record = new SerializedBlob
                {
                    Bytes = blob.Data,
                    MediaType = blob.MediaType,
                };
                break;

            // Steps 7-10: the boxed primitives, each identified by its data slot.
            case Native.Boolean.BooleanInstance boolean:
                record = new SerializedBoxedPrimitive(SerializedValue.FromBoolean(boolean.BooleanData._value));
                break;

            case Native.Number.NumberInstance number:
                record = new SerializedBoxedPrimitive(SerializedValue.FromNumber(number.NumberData._value));
                break;

            case Native.BigInt.BigIntInstance bigInt:
                record = new SerializedBoxedPrimitive(SerializedValue.FromBigInt(bigInt.BigIntData._value));
                break;

            case Native.String.StringInstance stringInstance:
                record = new SerializedBoxedPrimitive(SerializedValue.FromString(stringInstance.StringData.ToString()));
                break;

            // Step 11: [[DateValue]] and nothing else, so an invalid Date serializes to an invalid Date.
            case JsDate date:
                record = new SerializedDate(date._dateValue);
                break;

            // Step 12.
            case JsRegExp regExp:
                record = new SerializedRegExp
                {
                    Matcher = regExp.Value,
                    Source = regExp.Source,
                    Flags = regExp.Flags,
                    ParseResult = regExp.ParseResult,
                    IsHostRegex = regExp.IsHostRegex,
                };
                break;

            // Step 13.
            case JsArrayBuffer buffer:
                record = SerializeArrayBuffer(buffer);
                break;

            // Step 14.
            case JsTypedArray typedArray:
                record = SerializeTypedArray(typedArray);
                break;

            case JsDataView dataView:
                record = SerializeDataView(dataView);
                break;

            // Steps 15-16: the container is registered before its contents are walked (step 25 runs before
            // step 26), which is what lets a Map or Set contain itself.
            case JsMap map:
                {
                    var target = new SerializedMap();
                    _memory[source] = target;
                    _pending.Push(new MapFrame(map, target));
                    return SerializedValue.FromObject(target);
                }

            case JsSet set:
                {
                    var target = new SerializedSet();
                    _memory[source] = target;
                    _pending.Push(new SetFrame(set, target));
                    return SerializedValue.FromObject(target);
                }

            // Step 17. The one deep arm that is not a container: `cause` is sub-serialized, so the record is
            // registered in the memory map first (step 25 before step 26) and an error that is its own cause
            // terminates.
            case ErrorInstance error:
                {
                    var errorName = ErrorNameFor(error.Get(CommonProperties.Name));
                    var errorMessage = ReadOwnDataMessage(error);
                    var hasCause = TryReadOwnDataCause(error, out var cause);

                    var target = new SerializedError
                    {
                        Name = errorName,
                        Message = errorMessage,
                        HasCause = hasCause,
                        Stack = ReadStack(error),
                    };

                    _memory[source] = target;
                    if (hasCause)
                    {
                        _pending.Push(new ErrorCauseFrame(cause, target));
                    }

                    return SerializedValue.FromObject(target);
                }

            // Step 18: an Array exotic object. A Proxy whose target is an array is not one — it is a Proxy
            // exotic object, and falls through to the refusal below, as the specification intends.
            case ArrayInstance array:
                {
                    var target = new SerializedArray(array.GetLength());
                    _memory[source] = target;
                    _pending.Push(new PropertyFrame(array, target.Properties, EnumerableOwnStringKeys(array)));
                    return SerializedValue.FromObject(target);
                }

            // Step 24: an ordinary object. Jint recognizes one by construction rather than by asking whether
            // it has "any internal slot other than [[Prototype]]": JsObject is the type every ordinary object
            // reaches — object literals, `new Foo()`, Object.create, JSON.parse output, and the host-facing
            // JsObject.Create / CreateFromEntries factories. See ThrowUncloneable for what that costs.
            //
            // ObjectPrototype is step 23's one carve-out, not a special case of Jint's: %Object.prototype% is
            // an immutable prototype exotic object, and step 23 refuses "an exotic object [that] is not the
            // %Object.prototype% intrinsic object associated with ANY realm" — so it reaches step 24 like any
            // ordinary object, and step 24's own note says the result "will be an empty object (not an
            // immutable prototype exotic object)". The type test IS that predicate rather than an identity
            // check standing in for it: ObjectPrototype is sealed and Intrinsics builds exactly one per realm,
            // so `is ObjectPrototype` is true of every realm's %Object.prototype% and of nothing else.
            case JsObject or ObjectPrototype:
                {
                    var target = new SerializedPlainObject();
                    _memory[source] = target;
                    _pending.Push(new PropertyFrame(source, target.Properties, EnumerableOwnStringKeys(source)));
                    return SerializedValue.FromObject(target);
                }

            // Steps 20-23.
            default:
                ThrowUncloneable(source);
                return SerializedValue.Undefined;
        }

        // Step 25, for everything that has no contents to walk.
        _memory[source] = record;
        return SerializedValue.FromObject(record);
    }

    /// <summary>
    /// Step 4: a primitive is carried as itself. Its JavaScript identity is not — a serialization record holds
    /// a <see cref="double"/>, a <see cref="string"/> or a <see cref="System.Numerics.BigInteger"/>, never the
    /// <c>JsValue</c> that held it — which costs nothing observable, since these are the values for which
    /// equality <i>is</i> identity.
    /// </summary>
    private static SerializedValue SerializePrimitive(JsValue value) => value.Type switch
    {
        Types.Undefined => SerializedValue.Undefined,
        Types.Null => SerializedValue.Null,
        Types.Boolean => SerializedValue.FromBoolean(((JsBoolean) value)._value),
        Types.Number => SerializedValue.FromNumber(((JsNumber) value)._value),
        Types.BigInt => SerializedValue.FromBigInt(((JsBigInt) value)._value),
        _ => SerializedValue.FromString(value.ToString()),
    };

    /// <summary>
    /// EnumerableOwnProperties(value, key) — the own <i>string</i> keys whose property is enumerable, snapshot
    /// once before any of them is read, per step 26.4.
    /// </summary>
    private JsValue[] EnumerableOwnStringKeys(ObjectInstance source)
    {
        var ownKeys = source.GetOwnPropertyKeys(Types.String);
        var keys = new List<JsValue>(ownKeys.Count);
        for (var i = 0; i < ownKeys.Count; i++)
        {
            if (i > 0 && i % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var key = ownKeys[i];
            if (key.IsString() && source.ProbeOwnPropertyChecked(key) == OwnPropertyProbe.Enumerable)
            {
                keys.Add(key);
            }
        }

        return keys.ToArray();
    }

    /// <summary>
    /// Step 13. The bytes are copied, and a resizable buffer stays resizable with the same
    /// <c>[[ArrayBufferMaxByteLength]]</c> (the specification's "ResizableArrayBuffer" serialization type).
    /// </summary>
    private SerializedArrayBuffer SerializeArrayBuffer(JsArrayBuffer source)
    {
        // Step 13.1: a SharedArrayBuffer is serializable only in a cross-origin-isolated agent cluster.
        // Jint has no such notion, so it is refused — which is what a browser without cross-origin isolation
        // does too.
        if (source.IsSharedArrayBuffer)
        {
            ThrowDataCloneError("A SharedArrayBuffer could not be cloned");
        }

        // Step 13.2.1.
        if (source.IsDetachedBuffer)
        {
            ThrowDataCloneError("A detached ArrayBuffer could not be cloned");
        }

        var data = source.ArrayBufferData!;
        var copy = new byte[data.Length];
        System.Array.Copy(data, copy, data.Length);

        var maxByteLength = source._arrayBufferMaxByteLength;
        return new SerializedArrayBuffer
        {
            Bytes = copy,
            MaxByteLength = maxByteLength is null ? null : (uint) maxByteLength.Value,
            Immutable = source.IsImmutableBuffer,
        };
    }

    /// <summary>
    /// Step 14, for a typed array. The viewed buffer goes through <see cref="SerializeValue"/> and therefore
    /// through the memory map, which is what makes two views over one buffer come out as two views over one
    /// record — and what makes a view of a <i>transferred</i> buffer land on the transferred record.
    /// </summary>
    private SerializedArrayBufferView SerializeTypedArray(JsTypedArray source)
    {
        // Step 14.1: IsArrayBufferViewOutOfBounds, which a detached buffer also fails. This has to come
        // before the buffer is looked up, because a buffer named in the transfer list is already in the
        // memory map and would resolve to its record rather than being examined.
        var record = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(source, ArrayBufferOrder.SeqCst);
        if (record.IsTypedArrayOutOfBounds)
        {
            ThrowDataCloneError("A TypedArray whose buffer is detached or no longer covers it could not be cloned");
        }

        return new SerializedArrayBufferView
        {
            Buffer = (SerializedArrayBuffer) SerializeValue(source._viewedArrayBuffer).AsObject(),
            ElementType = source._arrayElementType,
            ByteOffset = (uint) source._byteOffset,

            // A length-tracking view (over a resizable buffer) keeps tracking: the specification carries
            // [[ArrayLength]] across, and for such a view that value is "auto".
            Length = source._arrayLength == JsTypedArray.LengthAuto ? null : source._arrayLength,
        };
    }

    /// <summary>
    /// Step 14, for a <c>DataView</c> — which records <c>[[ByteLength]]</c> and <c>[[ByteOffset]]</c> but no
    /// <c>[[ArrayLength]]</c>.
    /// </summary>
    private SerializedArrayBufferView SerializeDataView(JsDataView source)
    {
        var sourceBuffer = source._viewedArrayBuffer;

        // Step 14.1 again, spelled out for a DataView: https://tc39.es/ecma262/#sec-isviewoutofbounds. As
        // above, this has to run before the buffer is looked up in the memory map.
        if (sourceBuffer is null || sourceBuffer.IsDetachedBuffer || IsOutOfBounds(source, sourceBuffer))
        {
            ThrowDataCloneError("A DataView whose buffer is detached or no longer covers it could not be cloned");
        }

        return new SerializedArrayBufferView
        {
            Buffer = (SerializedArrayBuffer) SerializeValue(sourceBuffer).AsObject(),
            ElementType = null,
            ByteOffset = source._byteOffset,
            Length = source._byteLength == JsTypedArray.LengthAuto ? null : source._byteLength,
        };
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-isviewoutofbounds, for a <c>DataView</c> whose buffer is not detached.
    /// </summary>
    private static bool IsOutOfBounds(JsDataView view, JsArrayBuffer buffer)
    {
        var bufferByteLength = (long) buffer.ArrayBufferByteLength;
        var byteOffsetEnd = view._byteLength == JsTypedArray.LengthAuto
            ? bufferByteLength
            : view._byteOffset + (long) view._byteLength;

        return view._byteOffset > bufferByteLength || byteOffsetEnd > bufferByteLength;
    }

    /// <summary>
    /// Step 17: the message is read as an own <i>data</i> property only, so an accessor is not invoked, and an
    /// error with no own message serializes without one at all.
    /// </summary>
    private static string? ReadOwnDataMessage(ErrorInstance source)
    {
        var messageDescriptor = source.GetOwnProperty(CommonProperties.Message);
        if (messageDescriptor == Runtime.Descriptors.PropertyDescriptor.Undefined || messageDescriptor.IsAccessorDescriptor())
        {
            return null;
        }

        return TypeConverter.ToString(messageDescriptor.Value);
    }

    /// <summary>
    /// The same read <see cref="ReadOwnDataMessage"/> performs, for <c>cause</c>: an own <i>data</i> property
    /// only, so an accessor is not invoked and an inherited <c>cause</c> is not one. See
    /// <see cref="SerializedError"/> for why <c>cause</c> is carried at all and why it is read this way
    /// rather than the way https://github.com/whatwg/html/pull/5749 proposes.
    /// </summary>
    /// <remarks>
    /// Unlike <c>message</c>, the value is not coerced: it is whatever the source held, and it is
    /// sub-serialized, so a <c>cause</c> that is itself an object is cloned and one that is uncloneable makes
    /// the whole clone fail with a <c>DataCloneError</c>.
    /// </remarks>
    private static bool TryReadOwnDataCause(ErrorInstance source, out JsValue cause)
    {
        var descriptor = source.GetOwnProperty(CommonProperties.Cause);
        if (descriptor == Runtime.Descriptors.PropertyDescriptor.Undefined || descriptor.IsAccessorDescriptor())
        {
            cause = JsValue.Undefined;
            return false;
        }

        cause = descriptor.Value;
        return true;
    }

    /// <summary>
    /// Neither <c>Error</c> nor <c>DOMException</c> has a specified <c>stack</c>; both specifications say only
    /// that a user agent "should attach a serialized representation of any interesting accompanying data …
    /// notably the stack property". Browsers carry it, and an error whose trace pointed at the clone site
    /// rather than at where it was raised would be actively misleading, so it is carried here too.
    /// </summary>
    private static string? ReadStack(ErrorInstance source)
    {
        return source.Get(CommonProperties.Stack) is JsString stack ? stack.ToString() : null;
    }

    /// <summary>
    /// Step 17: the name is read with <c>Get</c> (so it comes off the prototype for <c>new TypeError()</c>)
    /// and reduced to the seven names with a matching intrinsic; anything else — <c>AggregateError</c>, a
    /// subclass, a name the script made up — becomes <c>"Error"</c>.
    /// </summary>
    private static SerializedErrorName ErrorNameFor(JsValue name)
    {
        if (name is not JsString jsString)
        {
            return SerializedErrorName.Error;
        }

        return jsString.ToString() switch
        {
            "EvalError" => SerializedErrorName.EvalError,
            "RangeError" => SerializedErrorName.RangeError,
            "ReferenceError" => SerializedErrorName.ReferenceError,
            "SyntaxError" => SerializedErrorName.SyntaxError,
            "TypeError" => SerializedErrorName.TypeError,
            "URIError" => SerializedErrorName.UriError,
            _ => SerializedErrorName.Error,
        };
    }

    /// <summary>
    /// Steps 2-4 of StructuredSerializeWithTransfer: validate the whole transfer list and reserve a record for
    /// every entry, before a single byte of the graph is looked at.
    /// </summary>
    private List<TransferRecord>? PrepareTransfers(List<JsValue>? transferList)
    {
        if (transferList is null || transferList.Count == 0)
        {
            return null;
        }

        var records = new List<TransferRecord>(transferList.Count);
        foreach (var entry in transferList)
        {
            // Step 2.3, hoisted so it is asked once for both kinds: a duplicate is a DataCloneError, not a
            // silent second transfer. The entry is always an object — WebIDL's sequence<object> conversion
            // has already refused anything else — and nothing but this loop has put anything in the memory
            // map yet, so hoisting it above the type test cannot change which refusal a list earns.
            if (entry is ObjectInstance transferable && _memory.ContainsKey(transferable))
            {
                ThrowDataCloneError(entry is JsMessagePort
                    ? "A MessagePort appears more than once in the transfer list"
                    : "An ArrayBuffer appears more than once in the transfer list");
            }

            // Step 2.4 for a MessagePort: an empty data holder, which the walk resolves a reference to the
            // port itself to. It stays empty until CompleteTransfers takes the port's side, exactly as a
            // buffer's bytes do — the specification's uninitialized placeholder.
            if (entry is JsMessagePort port)
            {
                var holder = new SerializedMessagePort();
                _memory[port] = holder;
                (_transferredPorts ??= new List<SerializedMessagePort>()).Add(holder);
                records.Add(new TransferRecord(port, holder));
                continue;
            }

            // Step 2.4 for the three transferable stream interfaces, whose data holders are filled in by
            // their own transfer steps in CompleteTransfers for exactly the same reason a port's is: the
            // transfer has side effects — it locks the stream and starts a pipe — and the walk has to be able
            // to throw first.
            if (entry is JsReadableStream readable)
            {
                var holder = new SerializedReadableStream();
                _memory[readable] = holder;
                records.Add(new TransferRecord(readable, holder));
                continue;
            }

            if (entry is JsWritableStream writable)
            {
                var holder = new SerializedWritableStream();
                _memory[writable] = holder;
                records.Add(new TransferRecord(writable, holder));
                continue;
            }

            if (entry is JsTransformStream transform)
            {
                var holder = new SerializedTransformStream();
                _memory[transform] = holder;
                records.Add(new TransferRecord(transform, holder));
                continue;
            }

            // Step 2.1 / 2.2: an ArrayBuffer, a MessagePort and the three streams above are the transferables
            // Jint has. A SharedArrayBuffer is explicitly not one.
            if (entry is not JsArrayBuffer buffer || buffer.IsSharedArrayBuffer)
            {
                ThrowDataCloneError("Only ArrayBuffer, MessagePort, ReadableStream, WritableStream and TransformStream objects can be transferred");
                return null;
            }

            // Not in the specification, which predates the immutable-ArrayBuffer proposal: an immutable
            // buffer cannot hand its storage over, so it is not transferable.
            if (buffer.IsImmutableBuffer)
            {
                ThrowDataCloneError("An immutable ArrayBuffer cannot be transferred");
            }

            // Step 2.4: the record the walk resolves the source buffer to. Its bytes stay empty until
            // CompleteTransfers, which is precisely the specification's uninitialized placeholder — nothing
            // during the walk can observe them, because a view of this buffer records only its offset and
            // length and is rebuilt over the real storage on the other side.
            var maxByteLength = buffer._arrayBufferMaxByteLength;
            var target = new SerializedArrayBuffer
            {
                MaxByteLength = maxByteLength is null ? null : (uint) maxByteLength.Value,
            };

            _memory[buffer] = target;
            records.Add(new TransferRecord(buffer, target));
        }

        return records;
    }

    /// <summary>
    /// Step 5: the detach happens after the walk, so the transferred contents are whatever the source held
    /// when serialization finished — including anything a getter reached during the walk wrote into it. The
    /// storage moves rather than being copied, which is the entire point of transferring: the very
    /// <c>byte[]</c> the source held becomes the one the deserialized buffer holds, on whichever engine that
    /// turns out to be.
    /// </summary>
    private void CompleteTransfers(List<TransferRecord>? transfers)
    {
        if (transfers is null)
        {
            return;
        }

        foreach (var record in transfers)
        {
            if (record.Source is JsMessagePort port)
            {
                // Step 5.2: a port already detached — by close(), or by an earlier transfer — has no side
                // left to hand over. Asked here rather than in PrepareTransfers because the specification
                // asks it here, and the order is observable: an uncloneable message throws before this does.
                if (port.Detached)
                {
                    ThrowDataCloneError("A detached MessagePort cannot be transferred");
                }

                // Steps 5.5.4 and 5.5.5, which for a MessagePort are one act: the side — queue, peer and all
                // — leaves this port, and the port is detached.
                ((SerializedMessagePort) record.Target).Endpoint = port.DetachForTransfer();
                continue;
            }

            // Step 5.5.4 for the streams, whose transfer steps live in TransferableStreams. Each of them
            // performs a nested StructuredSerializeWithTransfer of a port, whose data holder is registered
            // here so that a record which is never delivered ends that channel too — see
            // SerializedMessagePort.Nested.
            if (record.Source is JsReadableStream readable)
            {
                ((SerializedReadableStream) record.Target).Port = TransferStreamPort(TransferableStreams.TransferReadable(readable));
                continue;
            }

            if (record.Source is JsWritableStream writable)
            {
                ((SerializedWritableStream) record.Target).Port = TransferStreamPort(TransferableStreams.TransferWritable(writable));
                continue;
            }

            if (record.Source is JsTransformStream transform)
            {
                var holder = (SerializedTransformStream) record.Target;

                // Steps 3 and 4 before either side moves: a transform stream with one side locked transfers
                // neither, so its writable is not left piping into a channel nothing will ever read.
                TransferableStreams.CheckTransformTransferable(transform);

                holder.Readable = new SerializedReadableStream
                {
                    Port = TransferStreamPort(TransferableStreams.TransferReadable(transform.Readable)),
                };

                holder.Writable = new SerializedWritableStream
                {
                    Port = TransferStreamPort(TransferableStreams.TransferWritable(transform.Writable)),
                };

                transform.Detached = true;
                continue;
            }

            var buffer = (JsArrayBuffer) record.Source;

            // Step 5.1: a buffer detached during the walk cannot be transferred any more.
            if (buffer.IsDetachedBuffer)
            {
                ThrowDataCloneError("An ArrayBuffer in the transfer list was detached while it was being cloned");
            }

            // Read now rather than in PrepareTransfers: a getter reached during the walk may have resized the
            // buffer, which replaces its data block outright.
            ((SerializedArrayBuffer) record.Target).Bytes = buffer.ArrayBufferData ?? [];
            buffer.DetachArrayBuffer();
        }
    }

    /// <summary>
    /// Wraps the channel side a stream's transfer steps produced in the data holder the nested
    /// StructuredSerializeWithTransfer would have returned, and registers it so that
    /// <see cref="StrandTransferredPorts(in SerializationRecord)"/> and a discarded port message queue both
    /// reach it.
    /// </summary>
    private SerializedMessagePort TransferStreamPort(Messaging.MessagePortEndpoint endpoint)
    {
        var holder = new SerializedMessagePort { Nested = true, Endpoint = endpoint };
        (_transferredPorts ??= new List<SerializedMessagePort>()).Add(holder);
        return holder;
    }

    [DoesNotReturn]
    private void ThrowUncloneable(ObjectInstance value)
    {
        // Step 21.
        if (value.IsCallable)
        {
            ThrowDataCloneError("A function could not be cloned");
        }

        // Steps 20, 22 and 23. Jint cannot enumerate an object's internal slots, so the refusal is decided
        // the other way round: everything the steps above recognize is serialized, and anything else is
        // refused. That is the conservative direction — it never produces a clone that has silently lost the
        // state its source carried, and it is what keeps a host's own ObjectInstance subclass and every
        // platform object Jint has yet to declare [Serializable] refused rather than silently flattened —
        // but it is stricter than a browser for two shapes with no internal slot of their own that Jint
        // nevertheless does not build as an ordinary object: a namespace object (Math, JSON, console) and an
        // unmapped arguments object. Cloning either is refused where a browser would answer with a plain
        // object. (%Object.prototype% used to be a third; the ordinary arm above now recognizes it, which is
        // what step 23's carve-out asks for.)
        var description = value switch
        {
            JsProxy => "A Proxy",
            JsPromise => "A Promise",

            // A MessagePort is transferable but not serializable, so reaching one during the walk means it
            // was in the message and not in the transfer list — which the specification refuses at its
            // "platform object that is not serializable" arm rather than at the transfer steps. The three
            // stream interfaces are transferable and not serializable in exactly the same way.
            JsMessagePort => "A MessagePort that was not in the transfer list",
            JsReadableStream => "A ReadableStream that was not in the transfer list",
            JsWritableStream => "A WritableStream that was not in the transfer list",
            JsTransformStream => "A TransformStream that was not in the transfer list",
            _ => "An object",
        };

        ThrowDataCloneError(description + " could not be cloned");
    }

    [DoesNotReturn]
    private void ThrowDataCloneError(string message) => ThrowDataCloneError(_realm, message);

    /// <summary>
    /// The one place a <c>DataCloneError</c> is raised, shared with the refusals
    /// <c>JsMessagePort.PostMessage</c> makes before it ever reaches serialization.
    /// </summary>
    [DoesNotReturn]
    internal static void ThrowDataCloneError(Realm realm, string message)
    {
        throw new JavaScriptException(realm.Intrinsics.DomException.CreateException(DomExceptionNames.DataClone, message));
    }

    /// <summary>
    /// One entry of <c>[[TransferDataHolders]]</c> before step 5 has filled it in: the source object and the
    /// record the walk resolves it to. Both are typed loosely because the list is in transfer-list order and
    /// mixes the two kinds — which is the order step 5 detaches in, and therefore the order a refusal
    /// part-way through leaves half the list transferred in.
    /// </summary>
    private readonly record struct TransferRecord(ObjectInstance Source, SerializedObject Target);

    /// <summary>
    /// One container whose contents are still to be serialized. The specification recurses here; this is the
    /// heap stand-in for the native frame that recursion would have used.
    /// </summary>
    private abstract class SerializeFrame
    {
        /// <summary>
        /// The next source value this container needs a record of, or <see langword="false"/> when it is done.
        /// </summary>
        internal abstract bool TryGetNextSource(out JsValue source);

        /// <summary>
        /// Receives the record of the value the last <see cref="TryGetNextSource"/> handed out.
        /// </summary>
        internal abstract void Accept(SerializedValue serialized);
    }

    /// <summary>
    /// Step 26.4, for an Array exotic object and for an ordinary object alike: the enumerable own string keys
    /// snapshot, each read with <c>[[Get]]</c> — so a getter <i>is</i> invoked, and its result is what the
    /// record carries. The <c>HasOwnProperty</c> re-check per key is the specification's, and it matters: an
    /// earlier getter is allowed to have deleted a later key.
    /// </summary>
    private sealed class PropertyFrame : SerializeFrame
    {
        private readonly ObjectInstance _source;
        private readonly List<SerializedProperty> _properties;
        private readonly JsValue[] _keys;
        private int _index;
        private string _currentKey = string.Empty;

        internal PropertyFrame(ObjectInstance source, List<SerializedProperty> properties, JsValue[] keys)
        {
            _source = source;
            _properties = properties;
            _keys = keys;
        }

        internal override bool TryGetNextSource(out JsValue source)
        {
            while (_index < _keys.Length)
            {
                var key = _keys[_index++];
                if (!_source.HasOwnProperty(key))
                {
                    continue;
                }

                _currentKey = key.ToString();
                source = _source.Get(key);
                return true;
            }

            source = JsValue.Undefined;
            return false;
        }

        internal override void Accept(SerializedValue serialized)
        {
            _properties.Add(new SerializedProperty(_currentKey, serialized));
        }
    }

    /// <summary>
    /// One error's <c>cause</c>: a frame with exactly one source, so that the sub-serialization happens after
    /// the error is in the memory map rather than during its own arm.
    /// </summary>
    private sealed class ErrorCauseFrame(JsValue cause, SerializedError target) : SerializeFrame
    {
        private bool _done;

        internal override bool TryGetNextSource(out JsValue source)
        {
            source = _done ? JsValue.Undefined : cause;
            var pending = !_done;
            _done = true;
            return pending;
        }

        internal override void Accept(SerializedValue serialized) => target.Cause = serialized;
    }

    /// <summary>
    /// Step 26.1: the entry list is copied before anything is serialized, and each entry's key is serialized
    /// before its value.
    /// </summary>
    private sealed class MapFrame : SerializeFrame
    {
        private readonly List<KeyValuePair<JsValue, JsValue>> _entries;
        private readonly SerializedMap _target;
        private int _index;
        private bool _expectingValue;
        private SerializedValue _pendingKey;

        internal MapFrame(JsMap source, SerializedMap target)
        {
            _entries = new List<KeyValuePair<JsValue, JsValue>>(source);
            _target = target;
        }

        internal override bool TryGetNextSource(out JsValue source)
        {
            if (_index >= _entries.Count)
            {
                source = JsValue.Undefined;
                return false;
            }

            var entry = _entries[_index];
            source = _expectingValue ? entry.Value : entry.Key;
            return true;
        }

        internal override void Accept(SerializedValue serialized)
        {
            if (!_expectingValue)
            {
                _pendingKey = serialized;
                _expectingValue = true;
                return;
            }

            _target.Entries.Add(new SerializedMapEntry(_pendingKey, serialized));
            _pendingKey = default;
            _expectingValue = false;
            _index++;
        }
    }

    /// <summary>
    /// Step 26.2, the Set counterpart of <see cref="MapFrame"/>.
    /// </summary>
    private sealed class SetFrame : SerializeFrame
    {
        private readonly List<JsValue> _entries;
        private readonly SerializedSet _target;
        private int _index;

        internal SetFrame(JsSet source, SerializedSet target)
        {
            _entries = new List<JsValue>(source);
            _target = target;
        }

        internal override bool TryGetNextSource(out JsValue source)
        {
            if (_index >= _entries.Count)
            {
                source = JsValue.Undefined;
                return false;
            }

            source = _entries[_index++];
            return true;
        }

        internal override void Accept(SerializedValue serialized)
        {
            _target.Entries.Add(serialized);
        }
    }
}
#endif
