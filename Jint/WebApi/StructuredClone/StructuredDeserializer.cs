#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;
using Jint.WebApi.Files;
using Jint.WebApi.Messaging;
using Jint.WebApi.Streams;

namespace Jint.WebApi.StructuredClone;

/// <summary>
/// StructuredDeserializeWithTransfer's result: <c>[[Deserialized]]</c>, and <c>[[TransferredValues]]</c>
/// narrowed to the ports — which is all any caller needs, because a transferred <c>ArrayBuffer</c> is
/// reachable only from the message itself.
/// </summary>
/// <param name="Value">The clone of the message, in the deserializer's own realm.</param>
/// <param name="Ports">
/// The ports the transfer created, in transfer-list order, or <see langword="null"/> when none were
/// transferred — which is the overwhelmingly common case and the reason this is not an empty list.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct DeserializedMessage(JsValue Value, List<JsMessagePort>? Ports);

/// <summary>
/// StructuredDeserialize: turns an engine-neutral <see cref="SerializationRecord"/> back into objects, all of
/// them created in <i>this</i> deserializer's realm.
/// <para>
/// https://html.spec.whatwg.org/multipage/structured-data.html#structureddeserialize
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// This half runs entirely on the engine that will own the result, which is what the specification means by
/// "a new … object in targetRealm" and what makes a record posted through a <c>MessagePort</c> safe: nothing
/// here reads anything belonging to the engine that produced the record, and nothing there touched an object
/// belonging to this one.
/// </para>
/// <para>
/// <b>It takes ownership of the record's byte arrays.</b> A deserialized <c>ArrayBuffer</c> is built over the
/// very <c>byte[]</c> the record carries rather than over a copy — which is what makes a transfer a move
/// rather than a second copy — so a record must be deserialized exactly once. See
/// <see cref="SerializationRecord"/>.
/// </para>
/// <para>
/// The walk is iterative for the same reason the serializer's is: the specification recurses once per graph
/// edge, and a graph deep enough to overflow the native stack would kill the process rather than raise
/// something a host can catch. Progress is charged against this engine's execution constraints, so
/// deserializing a very large graph stays interruptible.
/// </para>
/// </remarks>
internal sealed class StructuredDeserializer
{
    private readonly Engine _engine;
    private readonly Realm _realm;

    /// <summary>
    /// The specification's <i>memory</i> map for the reverse direction, keyed on record identity: what makes
    /// two references to one record come out as two references to one object, and what terminates a cycle.
    /// </summary>
    private readonly Dictionary<SerializedObject, ObjectInstance> _memory = new(ReferenceEqualityComparer.Instance);

    private readonly Stack<DeserializeFrame> _pending = new();

    /// <summary>
    /// Whether the record being read is delivered to more than one destination; see the constructor.
    /// </summary>
    private readonly bool _sharedRecord;

    private int _visited;

    /// <param name="engine">The engine that will own the result.</param>
    /// <param name="realm">The realm the specification's "in targetRealm" names.</param>
    /// <param name="sharedRecord">
    /// Whether this record is deserialized more than once — which is <c>BroadcastChannel</c> and nothing else,
    /// because its <c>postMessage</c> serializes once (step 2) and every destination deserializes that one
    /// record (step 8.3). Set, the byte storage a record carries is <b>copied</b> rather than adopted, so two
    /// receivers do not come away with two <c>ArrayBuffer</c>s over one <c>byte[]</c> and see each other's
    /// writes. Left at its default the storage is adopted, which is the move a <i>transfer</i> promised and is
    /// what <see cref="SerializationRecord"/>'s "a record is consumed once" describes; a broadcast has no
    /// transfer list at all, so there is never storage that must be moved rather than copied.
    /// </param>
    internal StructuredDeserializer(Engine engine, Realm realm, bool sharedRecord = false)
    {
        _engine = engine;
        _realm = realm;
        _sharedRecord = sharedRecord;
    }

    /// <summary>
    /// StructuredDeserialize for a record that transferred nothing but buffers, which is every message
    /// <c>BroadcastChannel</c> produces. Equivalent to <see cref="DeserializeWithTransfer"/> and its
    /// <c>Value</c>; a caller that has to expose <c>ports</c> wants the other one.
    /// </summary>
    internal JsValue Deserialize(in SerializationRecord record) => DeserializeWithTransfer(in record).Value;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/structured-data.html#structureddeserializewithtransfer
    /// </summary>
    /// <remarks>
    /// Step 3 runs <b>before</b> step 4, and the order is load-bearing rather than incidental: every
    /// transferred port exists, in this realm, before a single node of the message graph is looked at, so a
    /// reference to one from inside the message resolves — through the same memory map that carries sharing
    /// and cycles — to the very object <c>event.ports</c> hands over.
    /// </remarks>
    internal DeserializedMessage DeserializeWithTransfer(in SerializationRecord record)
    {
        // Step 3.
        var ports = DeserializeTransferredPorts(record.TransferredPorts);

        // Step 4.
        var result = DeserializeValue(record.Root);
        Drain();

        return new DeserializedMessage(result, ports);
    }

    /// <summary>
    /// Step 3, for the transfer data holders that are ports: one <c>MessagePort</c> per holder, created in
    /// this realm and bound to the side the sender detached, in transfer-list order.
    /// </summary>
    /// <remarks>
    /// The side is <i>taken</i> from the holder, so a record deserialized twice — which
    /// <see cref="SerializationRecord"/> forbids and only <c>BroadcastChannel</c> comes near, and it has no
    /// transfer list at all — cannot bind one channel side to two engines. The second read builds a port with
    /// a side of its own that is entangled with nothing, which is inert.
    /// <para>
    /// Step 3.3.2's "if the interface is not exposed in targetRealm, throw a DataCloneError" cannot fire here:
    /// the only way to hold a port is to have the messaging feature, and the only way a record reaches this
    /// engine is through a port of its own.
    /// </para>
    /// </remarks>
    private List<JsMessagePort>? DeserializeTransferredPorts(List<SerializedMessagePort>? holders)
    {
        if (holders is not { Count: > 0 })
        {
            return null;
        }

        List<JsMessagePort>? ports = null;
        foreach (var holder in holders)
        {
            // A stream's own channel is not one of this serialization's transfer data holders — it belongs to
            // the nested one its transfer steps performed — so it is neither created here nor exposed. Its
            // stream's transfer-receiving steps take it, at the point the walk reaches the stream.
            if (holder.Nested)
            {
                continue;
            }

            var endpoint = holder.Endpoint;
            holder.Endpoint = null;

            var port = new JsMessagePort(_engine, _realm, endpoint);
            _memory[holder] = port;
            (ports ??= new List<JsMessagePort>(holders.Count)).Add(port);
        }

        return ports;
    }

    /// <summary>
    /// Takes the channel side out of a data holder, so a record read twice cannot bind one side to two
    /// engines — the same rule <see cref="DeserializeTransferredPorts"/> follows, for the same reason.
    /// </summary>
    private static Messaging.MessagePortEndpoint? TakeEndpoint(SerializedMessagePort holder)
    {
        var endpoint = holder.Endpoint;
        holder.Endpoint = null;
        return endpoint;
    }

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

            frame.Accept(DeserializeValue(next));
        }
    }

    private JsValue DeserializeValue(SerializedValue value)
    {
        if (++_visited % Engine.ConstraintCheckInterval == 0)
        {
            _engine.Constraints.Check();
        }

        return value.Kind switch
        {
            SerializedValueKind.Undefined => JsValue.Undefined,
            SerializedValueKind.Null => JsValue.Null,
            SerializedValueKind.Boolean => JsBoolean.Create(value.AsBoolean()),
            SerializedValueKind.Number => JsNumber.Create(value.AsNumber()),
            SerializedValueKind.BigInt => JsBigInt.Create(value.AsBigInt()),
            SerializedValueKind.String => JsString.Create(value.AsString()),
            _ => DeserializeObject(value.AsObject()),
        };
    }

    private ObjectInstance DeserializeObject(SerializedObject record)
    {
        if (_memory.TryGetValue(record, out var seen))
        {
            return seen;
        }

        ObjectInstance result;
        switch (record)
        {
            case SerializedBoxedPrimitive boxed:
                result = DeserializeBoxedPrimitive(boxed);
                break;

            case SerializedDate date:
                result = new JsDate(_engine, date.DateValue)
                {
                    _prototype = _realm.Intrinsics.Date.PrototypeObject,
                };
                break;

            case SerializedRegExp regExp:
                result = DeserializeRegExp(regExp);
                break;

            case SerializedArrayBuffer buffer:
                result = DeserializeArrayBuffer(buffer);
                break;

            case SerializedArrayBufferView view:
                result = DeserializeView(view);
                break;

            // Deep, and for one field only: the error is registered before its `cause` is deserialized, so an
            // error that is its own cause comes back as one rather than recursing forever.
            case SerializedError error:
                {
                    var target = DeserializeError(error);
                    _memory[record] = target;

                    if (error.HasCause)
                    {
                        _pending.Push(new ErrorCauseFrame(error.Cause, target));
                    }

                    return target;
                }

            // Before the DOMException arm it derives from — https://webidl.spec.whatwg.org/#quotaexceedederror
            // deserializes into the interface, not into a DOMException wearing the name.
            case SerializedQuotaExceededError quotaExceeded:
                result = DeserializeQuotaExceededError(quotaExceeded);
                break;

            case SerializedDomException domException:
                result = DeserializeDomException(domException);
                break;

            // The File API's deserialization steps, and — as in the serializer — the derived interface first,
            // or a File would come back as a Blob. https://w3c.github.io/FileAPI/#file-section
            case SerializedFile file:
                result = DeserializeFile(file);
                break;

            case SerializedBlob blob:
                result = DeserializeBlob(blob);
                break;

            // The three streams' transfer-receiving steps. Each builds its half of a cross-realm transform
            // over the channel side the sender detached, in this realm.
            case SerializedReadableStream readableStream:
                result = TransferableStreams.ReceiveReadable(_engine, _realm, TakeEndpoint(readableStream.Port));
                break;

            case SerializedWritableStream writableStream:
                result = TransferableStreams.ReceiveWritable(_engine, _realm, TakeEndpoint(writableStream.Port));
                break;

            case SerializedTransformStream transformStream:
                {
                    // Registered before its two sides are built, exactly as the containers below are: the
                    // sides are separate records, so a graph that names the transform stream twice must come
                    // out as one object either way.
                    var target = new JsTransformStream(_engine, _realm)
                    {
                        _prototype = _realm.Intrinsics.TransformStream.PrototypeObject,
                    };

                    _memory[record] = target;

                    target.Readable = (JsReadableStream) DeserializeObject(transformStream.Readable);
                    target.Writable = (JsWritableStream) DeserializeObject(transformStream.Writable);
                    return target;
                }

            // The four containers are registered before their contents are filled in, which is what lets a
            // cycle terminate.
            case SerializedMap map:
                {
                    var target = (JsMap) _realm.Intrinsics.Map.Construct(Arguments.Empty, _realm.Intrinsics.Map);
                    _memory[record] = target;
                    _pending.Push(new MapFrame(map, target));
                    return target;
                }

            case SerializedSet set:
                {
                    var target = (JsSet) _realm.Intrinsics.Set.Construct(Arguments.Empty, _realm.Intrinsics.Set);
                    _memory[record] = target;
                    _pending.Push(new SetFrame(set, target));
                    return target;
                }

            case SerializedArray array:
                {
                    var target = _realm.Intrinsics.Array.ArrayCreateLazy(array.Length);
                    _memory[record] = target;
                    _pending.Push(new PropertyFrame(array.Properties, target));
                    return target;
                }

            case SerializedPlainObject plain:
                {
                    var target = ObjectInstance.OrdinaryObjectCreate(_engine, _realm.Intrinsics.Object.PrototypeObject);
                    _memory[record] = target;
                    _pending.Push(new PropertyFrame(plain.Properties, target));
                    return target;
                }

            default:
                Throw.InvalidOperationException("Unknown structured serialization record " + record.GetType().Name);
                return null!;
        }

        _memory[record] = result;
        return result;
    }

    /// <summary>
    /// The deserialization counterpart of steps 7-10: a fresh box of the same kind, carrying the same data
    /// slot and this realm's prototype.
    /// </summary>
    private ObjectInstance DeserializeBoxedPrimitive(SerializedBoxedPrimitive record)
    {
        var value = record.Value;
        switch (value.Kind)
        {
            case SerializedValueKind.Boolean:
                return new Native.Boolean.BooleanInstance(_engine, JsBoolean.Create(value.AsBoolean()))
                {
                    _prototype = _realm.Intrinsics.Boolean.PrototypeObject,
                };

            case SerializedValueKind.Number:
                return new Native.Number.NumberInstance(_engine, JsNumber.Create(value.AsNumber()))
                {
                    _prototype = _realm.Intrinsics.Number.PrototypeObject,
                };

            case SerializedValueKind.BigInt:
                return new Native.BigInt.BigIntInstance(_engine, JsBigInt.Create(value.AsBigInt()))
                {
                    _prototype = _realm.Intrinsics.BigInt.PrototypeObject,
                };

            default:
                return new Native.String.StringInstance(_engine, JsString.Create(value.AsString()))
                {
                    _prototype = _realm.Intrinsics.String.PrototypeObject,
                };
        }
    }

    /// <summary>
    /// Step 12's deserialization counterpart: the source, the flags and the compiled matcher carry over, and
    /// nothing else does — most visibly <c>lastIndex</c>, which a freshly created RegExp object starts at
    /// <c>0</c>.
    /// </summary>
    private JsRegExp DeserializeRegExp(SerializedRegExp record)
    {
        var result = new JsRegExp(_engine)
        {
            _prototype = _realm.Intrinsics.RegExp.PrototypeObject,
            Value = record.Matcher,
            Source = record.Source,
            Flags = record.Flags,
            ParseResult = record.ParseResult,
            IsHostRegex = record.IsHostRegex,
        };

        result.SetOwnProperty(JsRegExp.PropertyLastIndex, new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.OnlyWritable));
        return result;
    }

    /// <summary>
    /// Step 13's deserialization counterpart. The record's storage is adopted rather than copied — for a
    /// transferred buffer that is the move the transfer promised, and for a copied one the copy already
    /// happened during serialization. The one exception is a record with several destinations, where adopting
    /// would hand every one of them the same mutable array; see the constructor's <c>sharedRecord</c>.
    /// </summary>
    private JsArrayBuffer DeserializeArrayBuffer(SerializedArrayBuffer record)
    {
        var bytes = _sharedRecord ? record.Bytes.AsSpan().ToArray() : record.Bytes;

        return new JsArrayBuffer(_engine, bytes, record.MaxByteLength)
        {
            _prototype = _realm.Intrinsics.ArrayBuffer.PrototypeObject,
            _isImmutable = record.Immutable,
        };
    }

    /// <summary>
    /// Step 14's deserialization counterpart, for both a typed array and a <c>DataView</c>. The buffer is
    /// resolved through the memory map, which is what makes two views over one buffer come out as two views
    /// over one buffer here too.
    /// </summary>
    private ObjectInstance DeserializeView(SerializedArrayBufferView record)
    {
        var buffer = (JsArrayBuffer) DeserializeObject(record.Buffer);

        if (record.ElementType is { } elementType)
        {
            var constructor = ConstructorFor(elementType);
            return constructor.Construct(buffer, (int) record.ByteOffset, (int?) record.Length);
        }

        var dataView = _realm.Intrinsics.DataView;
        JsCallArguments arguments = record.Length is { } byteLength
            ? [buffer, JsNumber.Create((int) record.ByteOffset), JsNumber.Create((int) byteLength)]
            : [buffer, JsNumber.Create((int) record.ByteOffset)];

        return dataView.Construct(arguments, dataView);
    }

    private JsError DeserializeError(SerializedError record)
    {
        var result = new JsError(_engine)
        {
            _prototype = ErrorPrototypeFor(record.Name),
        };

        if (record.Message is { } message)
        {
            // Writable, non-enumerable, configurable — which is what SetVirtualMessage gives, without
            // allocating property storage for the overwhelmingly common one-property error.
            result.SetVirtualMessage(JsString.Create(message));
        }

        if (record.Stack is { } stack)
        {
            // Jint serves Error.prototype.stack from an internal field through the error-stack accessor
            // (https://tc39.es/proposal-error-stacks/), so the result gets no own property and looks exactly
            // like an error the engine raised itself.
            result._stack = JsString.Create(stack);
        }

        return result;
    }

    /// <summary>
    /// The <c>Blob</c> deserialization steps, https://w3c.github.io/FileAPI/#dfn-Blob: the byte sequence and
    /// the snapshot state (which Jint has nothing to represent — see <see cref="SerializedBlob"/>), plus the
    /// media type the steps omit but every engine carries.
    /// </summary>
    /// <remarks>
    /// The byte sequence is adopted rather than copied even for a <c>sharedRecord</c>, unlike an
    /// <c>ArrayBuffer</c>'s: a blob's bytes are immutable and never handed out, so several receivers holding
    /// one array cannot observe one another.
    /// </remarks>
    private JsBlob DeserializeBlob(SerializedBlob record)
    {
        RequireFileApiExposed("Blob");

        return new JsBlob(_engine, record.Bytes, record.MediaType)
        {
            _prototype = _realm.Intrinsics.Blob.PrototypeObject,
        };
    }

    /// <summary>
    /// The <c>File</c> deserialization steps, https://w3c.github.io/FileAPI/#file-section. The result is a
    /// plain <c>File</c> in this realm whatever the source's prototype was, which is what makes a subclass
    /// instance "deserialize as its closest serializable superclass": only the primary interface takes part.
    /// </summary>
    private JsFile DeserializeFile(SerializedFile record)
    {
        RequireFileApiExposed("File");

        return new JsFile(_engine, record.Bytes, record.MediaType, record.Name, record.LastModified)
        {
            _prototype = _realm.Intrinsics.File.PrototypeObject,
        };
    }

    /// <summary>
    /// "If the interface identified by interfaceName is not exposed in targetRealm, then throw a
    /// DataCloneError" — https://html.spec.whatwg.org/multipage/structured-data.html#structureddeserialize.
    /// Reachable because a record crosses engines: a <c>MessagePort</c>, a <c>BroadcastChannel</c> or a
    /// <c>Worker</c> can carry a blob from an engine that enabled <see cref="WebApiFeatures.Files"/> to one
    /// that did not.
    /// </summary>
    /// <remarks>
    /// The feature set is asked, not the global object: <c>Intrinsics.Blob</c> exists on any engine that
    /// reaches for it, and the global property can be deleted by script — which is exactly the case the
    /// battery's "an object whose interface is deleted from the global must still deserialize" pins.
    /// </remarks>
    private void RequireFileApiExposed(string interfaceName)
    {
        if ((_engine._webApiFeatures & WebApiFeatures.Files) == WebApiFeatures.None)
        {
            StructuredSerializer.ThrowDataCloneError(_realm, interfaceName + " is not exposed in the target realm");
        }
    }

    private JsQuotaExceededError DeserializeQuotaExceededError(SerializedQuotaExceededError record)
    {
        var result = _realm.Intrinsics.QuotaExceededError.CreateException(record.Message, record.Quota, record.Requested);
        AttachStack(result, record.Stack);
        return result;
    }

    private JsDomException DeserializeDomException(SerializedDomException record)
    {
        var result = _realm.Intrinsics.DomException.CreateException(record.Name, record.Message);
        AttachStack(result, record.Stack);
        return result;
    }

    /// <summary>
    /// Replaces the trace <c>CreateException</c> captured at the deserialization site with the one the source
    /// exception carried, so a clone's <c>stack</c> still points where the error was raised.
    /// </summary>
    private static void AttachStack(JsDomException result, string? recordStack)
    {
        if (recordStack is { } stack)
        {
            // A DOMException carries stack as an own non-enumerable property, so replace the one
            // CreateException captured here.
            result.SetProperty(CommonProperties.Stack, new PropertyDescriptor(JsString.Create(stack), PropertyFlag.NonEnumerable));
        }
    }

    private ErrorPrototype ErrorPrototypeFor(SerializedErrorName name) => name switch
    {
        SerializedErrorName.EvalError => _realm.Intrinsics.EvalError.PrototypeObject,
        SerializedErrorName.RangeError => _realm.Intrinsics.RangeError.PrototypeObject,
        SerializedErrorName.ReferenceError => _realm.Intrinsics.ReferenceError.PrototypeObject,
        SerializedErrorName.SyntaxError => _realm.Intrinsics.SyntaxError.PrototypeObject,
        SerializedErrorName.TypeError => _realm.Intrinsics.TypeError.PrototypeObject,
        SerializedErrorName.UriError => _realm.Intrinsics.UriError.PrototypeObject,
        _ => _realm.Intrinsics.Error.PrototypeObject,
    };

    private TypedArrayConstructor ConstructorFor(TypedArrayElementType type) => type switch
    {
        TypedArrayElementType.Int8 => _realm.Intrinsics.Int8Array,
        TypedArrayElementType.Uint8 => _realm.Intrinsics.Uint8Array,
        TypedArrayElementType.Uint8C => _realm.Intrinsics.Uint8ClampedArray,
        TypedArrayElementType.Int16 => _realm.Intrinsics.Int16Array,
        TypedArrayElementType.Uint16 => _realm.Intrinsics.Uint16Array,
        TypedArrayElementType.Int32 => _realm.Intrinsics.Int32Array,
        TypedArrayElementType.Uint32 => _realm.Intrinsics.Uint32Array,
        TypedArrayElementType.BigInt64 => _realm.Intrinsics.BigInt64Array,
        TypedArrayElementType.BigUint64 => _realm.Intrinsics.BigUint64Array,
        TypedArrayElementType.Float16 => _realm.Intrinsics.Float16Array,
        TypedArrayElementType.Float32 => _realm.Intrinsics.Float32Array,
        TypedArrayElementType.Float64 => _realm.Intrinsics.Float64Array,
        _ => throw new InvalidOperationException("Unknown typed array element type " + type),
    };

    /// <summary>
    /// One container whose contents are still to be deserialized — the heap stand-in for the native frame the
    /// specification's recursion would have used.
    /// </summary>
    private abstract class DeserializeFrame
    {
        internal abstract bool TryGetNextSource(out SerializedValue source);

        internal abstract void Accept(JsValue value);
    }

    /// <summary>
    /// The properties of a serialized array or ordinary object, each created as a plain data property — which
    /// is what the specification's CreateDataProperty amounts to and why a getter never survives a clone.
    /// </summary>
    private sealed class PropertyFrame : DeserializeFrame
    {
        private readonly List<SerializedProperty> _properties;
        private readonly ObjectInstance _target;
        private int _index;
        private JsString _currentKey = JsString.Empty;

        internal PropertyFrame(List<SerializedProperty> properties, ObjectInstance target)
        {
            _properties = properties;
            _target = target;
        }

        internal override bool TryGetNextSource(out SerializedValue source)
        {
            if (_index >= _properties.Count)
            {
                source = SerializedValue.Undefined;
                return false;
            }

            var property = _properties[_index++];
            _currentKey = JsString.Create(property.Key);
            source = property.Value;
            return true;
        }

        internal override void Accept(JsValue value)
        {
            _ = _target.CreateDataProperty(_currentKey, value);
        }
    }

    /// <summary>
    /// One error's <c>cause</c>, installed the way the language installs it: CreateNonEnumerableDataProperty,
    /// so the clone's descriptor is writable, non-enumerable and configurable — exactly what
    /// https://tc39.es/ecma262/#sec-installerrorcause gives an error the engine constructed itself.
    /// </summary>
    /// <remarks>
    /// It runs after the error's own arm, so <c>message</c> is already installed and <c>cause</c> lands
    /// second, which is the own-key order <c>new Error(m, { cause: c })</c> produces.
    /// </remarks>
    private sealed class ErrorCauseFrame(SerializedValue cause, JsError target) : DeserializeFrame
    {
        private bool _done;

        internal override bool TryGetNextSource(out SerializedValue source)
        {
            source = _done ? SerializedValue.Undefined : cause;
            var pending = !_done;
            _done = true;
            return pending;
        }

        internal override void Accept(JsValue value)
            => target.CreateNonEnumerableDataPropertyOrThrow(CommonProperties.Cause, value);
    }

    private sealed class MapFrame : DeserializeFrame
    {
        private readonly SerializedMap _record;
        private readonly JsMap _target;
        private int _index;
        private bool _expectingValue;
        private JsValue _pendingKey = JsValue.Undefined;

        internal MapFrame(SerializedMap record, JsMap target)
        {
            _record = record;
            _target = target;
        }

        internal override bool TryGetNextSource(out SerializedValue source)
        {
            if (_index >= _record.Entries.Count)
            {
                source = SerializedValue.Undefined;
                return false;
            }

            var entry = _record.Entries[_index];
            source = _expectingValue ? entry.Value : entry.Key;
            return true;
        }

        internal override void Accept(JsValue value)
        {
            if (!_expectingValue)
            {
                _pendingKey = value;
                _expectingValue = true;
                return;
            }

            _target.Set(_pendingKey, value);
            _pendingKey = JsValue.Undefined;
            _expectingValue = false;
            _index++;
        }
    }

    private sealed class SetFrame : DeserializeFrame
    {
        private readonly SerializedSet _record;
        private readonly JsSet _target;
        private int _index;

        internal SetFrame(SerializedSet record, JsSet target)
        {
            _record = record;
            _target = target;
        }

        internal override bool TryGetNextSource(out SerializedValue source)
        {
            if (_index >= _record.Entries.Count)
            {
                source = SerializedValue.Undefined;
                return false;
            }

            source = _record.Entries[_index++];
            return true;
        }

        internal override void Accept(JsValue value)
        {
            _target.Add(value);
        }
    }
}
#endif
