#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.ArrayBuffer;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;

namespace Jint.WebApi.StructuredClone;

/// <summary>
/// StructuredSerializeWithTransfer / StructuredDeserializeWithTransfer, fused into a single walk.
/// <para>
/// https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializewithtransfer
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The specification serializes into an intermediate record graph and then deserializes that into the target
/// realm, because a real user agent has to hand the records to another agent. <c>structuredClone</c> has only
/// one realm, so this walks the graph once and builds the clone directly, with <see cref="_memory"/> keyed on
/// the <i>source</i> object rather than on a serialization record. Every observable of the two-phase algorithm
/// survives that fusion: the traversal order is identical, so getters run in the same order; a throw part-way
/// through discards a clone nothing can reach; and the transfer steps still run <i>after</i> the whole walk,
/// which is what lets a getter reached during the walk resize or write into a buffer the caller is
/// transferring and have the clone see the result (see <see cref="CompleteTransfers"/>).
/// </para>
/// <para>
/// <b>The walk is iterative, not recursive.</b> The specification's algorithm recurses once per graph edge and
/// bounds nothing, so a hostile <c>{a:{a:{a:…}}}</c> would take the native stack down with it — and a stack
/// overflow kills the process rather than raising something a host can catch. Container clones therefore push
/// a <see cref="CloneFrame"/> onto a heap <see cref="Stack{T}"/> and <see cref="Drain"/> runs them to
/// completion, so the only limit on nesting depth is available memory, and there is no arbitrary cutoff for a
/// legitimately deep document to trip over. Progress is charged against the engine's execution constraints
/// every <see cref="Engine.ConstraintCheckInterval"/> values, so a clone of a very large graph stays
/// interruptible by a timeout or a cancellation the same way a long loop is.
/// </para>
/// <para>
/// Everything the algorithm builds gets the <i>current</i> realm's intrinsic prototypes, per the
/// specification's "a new … object in targetRealm".
/// </para>
/// </remarks>
internal sealed class StructuredCloner
{
    private readonly Engine _engine;
    private readonly Realm _realm;

    /// <summary>
    /// The specification's <i>memory</i> map, keyed on object identity: what makes a cycle terminate and what
    /// makes two references to one object deserialize as two references to one clone.
    /// </summary>
    private readonly Dictionary<ObjectInstance, ObjectInstance> _memory = new(ReferenceEqualityComparer.Instance);

    private readonly Stack<CloneFrame> _pending = new();

    private int _visited;

    internal StructuredCloner(Engine engine, Realm realm)
    {
        _engine = engine;
        _realm = realm;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializewithtransfer
    /// </summary>
    /// <param name="value">The value to clone.</param>
    /// <param name="transferList">
    /// The already-iterated <c>transfer</c> option, or <see langword="null"/> when the caller passed none.
    /// Its entries are objects, which is all the WebIDL <c>sequence&lt;object&gt;</c> conversion guarantees;
    /// deciding whether they are <i>transferable</i> is this algorithm's job.
    /// </param>
    internal JsValue Clone(JsValue value, List<JsValue>? transferList)
    {
        // Steps 2-4: every transferable is validated and given its (still empty) result buffer BEFORE the
        // walk, so a transferred buffer reached from `value` resolves to that same result buffer.
        var transfers = PrepareTransfers(transferList);

        var result = CloneValue(value);
        Drain();

        // Step 5: only now is anything detached.
        CompleteTransfers(transfers);

        return result;
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

            frame.Accept(CloneValue(next));
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/structured-data.html#structuredserializeinternal — fused with
    /// StructuredDeserializeInternal, so what a step "sets serialized to" this method builds outright.
    /// </summary>
    private JsValue CloneValue(JsValue value)
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

            // Step 4: undefined, null, Boolean, Number, BigInt and String values are their own clone.
            return value;
        }

        // Step 2.
        if (_memory.TryGetValue(source, out var seen))
        {
            return seen;
        }

        ObjectInstance clone;
        switch (source)
        {
            // WebIDL declares DOMException [Serializable]; its serialization steps carry the name and the
            // message. https://webidl.spec.whatwg.org/#idl-DOMException — checked before the ErrorInstance arm
            // below, which it derives from.
            case JsDomException domException:
                clone = CloneDomException(domException);
                break;

            // Steps 7-10: the boxed primitives, each identified by its data slot.
            case Native.Boolean.BooleanInstance boolean:
                clone = new Native.Boolean.BooleanInstance(_engine, boolean.BooleanData)
                {
                    _prototype = _realm.Intrinsics.Boolean.PrototypeObject,
                };
                break;

            case Native.Number.NumberInstance number:
                clone = new Native.Number.NumberInstance(_engine, number.NumberData)
                {
                    _prototype = _realm.Intrinsics.Number.PrototypeObject,
                };
                break;

            case Native.BigInt.BigIntInstance bigInt:
                clone = new Native.BigInt.BigIntInstance(_engine, bigInt.BigIntData)
                {
                    _prototype = _realm.Intrinsics.BigInt.PrototypeObject,
                };
                break;

            case Native.String.StringInstance stringInstance:
                clone = new Native.String.StringInstance(_engine, stringInstance.StringData)
                {
                    _prototype = _realm.Intrinsics.String.PrototypeObject,
                };
                break;

            // Step 11: [[DateValue]] and nothing else, so an invalid Date clones to an invalid Date.
            case JsDate date:
                clone = new JsDate(_engine, date._dateValue)
                {
                    _prototype = _realm.Intrinsics.Date.PrototypeObject,
                };
                break;

            // Step 12.
            case JsRegExp regExp:
                clone = CloneRegExp(regExp);
                break;

            // Step 13.
            case JsArrayBuffer buffer:
                clone = CloneArrayBuffer(buffer);
                break;

            // Step 14.
            case JsTypedArray typedArray:
                clone = CloneTypedArray(typedArray);
                break;

            case JsDataView dataView:
                clone = CloneDataView(dataView);
                break;

            // Steps 15-16: the container is registered before its contents are walked (step 25 runs before
            // step 26), which is what lets a Map or Set contain itself.
            case JsMap map:
                {
                    var target = (JsMap) _realm.Intrinsics.Map.Construct(Arguments.Empty, _realm.Intrinsics.Map);
                    _memory[source] = target;
                    _pending.Push(new MapFrame(map, target));
                    return target;
                }

            case JsSet set:
                {
                    var target = (JsSet) _realm.Intrinsics.Set.Construct(Arguments.Empty, _realm.Intrinsics.Set);
                    _memory[source] = target;
                    _pending.Push(new SetFrame(set, target));
                    return target;
                }

            // Step 17.
            case ErrorInstance error:
                clone = CloneError(error);
                break;

            // Step 18: an Array exotic object. A Proxy whose target is an array is not one — it is a Proxy
            // exotic object, and falls through to the refusal below, as the specification intends.
            case ArrayInstance array:
                {
                    var target = _realm.Intrinsics.Array.ArrayCreateLazy(array.GetLength());
                    _memory[source] = target;
                    _pending.Push(new PropertyFrame(array, target, EnumerableOwnStringKeys(array)));
                    return target;
                }

            // Step 24: an ordinary object. Jint recognizes one by construction rather than by asking whether
            // it has "any internal slot other than [[Prototype]]": JsObject is the type every ordinary object
            // reaches — object literals, `new Foo()`, Object.create, JSON.parse output, and the host-facing
            // JsObject.Create / CreateFromEntries factories. See ThrowUncloneable for what that costs.
            case JsObject plain:
                {
                    var target = ObjectInstance.OrdinaryObjectCreate(_engine, _realm.Intrinsics.Object.PrototypeObject);
                    _memory[source] = target;
                    _pending.Push(new PropertyFrame(plain, target, EnumerableOwnStringKeys(plain)));
                    return target;
                }

            // Steps 20-23.
            default:
                ThrowUncloneable(source);
                return JsValue.Undefined;
        }

        // Step 25, for everything that has no contents to walk.
        _memory[source] = clone;
        return clone;
    }

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
    /// Step 12 and its deserialization counterpart: <c>[[OriginalSource]]</c>, <c>[[OriginalFlags]]</c> and
    /// the compiled <c>[[RegExpMatcher]]</c> carry over, and nothing else does — most visibly
    /// <c>lastIndex</c>, which a freshly created RegExp object starts at <c>0</c>. The matcher is shared
    /// rather than recompiled: it is immutable and engine-independent, and re-parsing the source would fail
    /// outright for a host-supplied .NET <c>Regex</c>, whose <c>Source</c> is not a JavaScript pattern.
    /// </summary>
    private JsRegExp CloneRegExp(JsRegExp source)
    {
        var clone = new JsRegExp(_engine)
        {
            _prototype = _realm.Intrinsics.RegExp.PrototypeObject,
            Value = source.Value,
            Source = source.Source,
            Flags = source.Flags,
            ParseResult = source.ParseResult,
            IsHostRegex = source.IsHostRegex,
        };

        clone.SetOwnProperty(JsRegExp.PropertyLastIndex, new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.OnlyWritable));
        return clone;
    }

    /// <summary>
    /// Step 13. The bytes are copied, and a resizable buffer stays resizable with the same
    /// <c>[[ArrayBufferMaxByteLength]]</c> (the specification's "ResizableArrayBuffer" serialization type).
    /// </summary>
    private JsArrayBuffer CloneArrayBuffer(JsArrayBuffer source)
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
        return new JsArrayBuffer(_engine, copy, maxByteLength is null ? null : (uint) maxByteLength.Value)
        {
            _prototype = _realm.Intrinsics.ArrayBuffer.PrototypeObject,

            // Not something the HTML Standard knows about yet: the immutable-ArrayBuffer proposal has no
            // structured-clone integration. Carrying the flag keeps the clone the same kind of buffer the
            // source was, which is the reading every other slot here follows.
            _isImmutable = source.IsImmutableBuffer,
        };
    }

    /// <summary>
    /// Step 14, for a typed array. The viewed buffer goes through <see cref="CloneValue"/> and therefore
    /// through the memory map, which is what makes two views over one buffer come out as two views over one
    /// cloned buffer — and what makes a view of a <i>transferred</i> buffer land on the transferred result.
    /// </summary>
    private JsTypedArray CloneTypedArray(JsTypedArray source)
    {
        // Step 14.1: IsArrayBufferViewOutOfBounds, which a detached buffer also fails. This has to come
        // before the buffer is looked up, because a buffer named in the transfer list is already in the
        // memory map and would resolve to its result buffer rather than being examined.
        var record = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(source, ArrayBufferOrder.SeqCst);
        if (record.IsTypedArrayOutOfBounds)
        {
            ThrowDataCloneError("A TypedArray whose buffer is detached or no longer covers it could not be cloned");
        }

        var buffer = (JsArrayBuffer) CloneValue(source._viewedArrayBuffer);
        var constructor = ConstructorFor(source._arrayElementType);

        // A length-tracking view (over a resizable buffer) keeps tracking: the specification carries
        // [[ArrayLength]] across, and for such a view that value is "auto".
        var length = source._arrayLength == JsTypedArray.LengthAuto ? (int?) null : (int) source._arrayLength;
        return constructor.Construct(buffer, source._byteOffset, length);
    }

    /// <summary>
    /// Step 14, for a <c>DataView</c> — which records <c>[[ByteLength]]</c> and <c>[[ByteOffset]]</c> but no
    /// <c>[[ArrayLength]]</c>.
    /// </summary>
    private ObjectInstance CloneDataView(JsDataView source)
    {
        var sourceBuffer = source._viewedArrayBuffer;

        // Step 14.1 again, spelled out for a DataView: https://tc39.es/ecma262/#sec-isviewoutofbounds. As
        // above, this has to run before the buffer is looked up in the memory map.
        if (sourceBuffer is null || sourceBuffer.IsDetachedBuffer || IsOutOfBounds(source, sourceBuffer))
        {
            ThrowDataCloneError("A DataView whose buffer is detached or no longer covers it could not be cloned");
        }

        var buffer = (JsArrayBuffer) CloneValue(sourceBuffer);
        var constructor = _realm.Intrinsics.DataView;

        JsCallArguments arguments = source._byteLength == JsTypedArray.LengthAuto
            ? [buffer, JsNumber.Create((int) source._byteOffset)]
            : [buffer, JsNumber.Create((int) source._byteOffset), JsNumber.Create((int) source._byteLength)];

        return constructor.Construct(arguments, constructor);
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
    /// Step 17 and its deserialization counterpart. The name is read with <c>Get</c> (so it comes off the
    /// prototype for <c>new TypeError()</c>) and reduced to the seven names with a matching intrinsic;
    /// anything else — <c>AggregateError</c>, a subclass, a name the script made up — becomes <c>"Error"</c>.
    /// The message is read as an own <i>data</i> property only, so an accessor is not invoked.
    /// </summary>
    private JsError CloneError(ErrorInstance source)
    {
        var prototype = ErrorPrototypeFor(source.Get(CommonProperties.Name));

        var clone = new JsError(_engine)
        {
            _prototype = prototype,
        };

        var messageDescriptor = source.GetOwnProperty(CommonProperties.Message);
        if (messageDescriptor != PropertyDescriptor.Undefined && !messageDescriptor.IsAccessorDescriptor())
        {
            // Deserialization step: writable, non-enumerable, configurable — which is what SetVirtualMessage
            // gives, without allocating property storage for the overwhelmingly common one-property error.
            clone.SetVirtualMessage(JsString.Create(TypeConverter.ToString(messageDescriptor.Value)));
        }

        CopyStack(source, clone);
        return clone;
    }

    /// <summary>
    /// The DOMException serialization/deserialization steps, https://webidl.spec.whatwg.org/#idl-DOMException:
    /// the clone carries the name and the message, and nothing else. <c>code</c> is derived from the name, as
    /// it is for any other DOMException.
    /// </summary>
    private JsDomException CloneDomException(JsDomException source)
    {
        var clone = _realm.Intrinsics.DomException.CreateException(source.Name.ToString(), source.Message.ToString());
        CopyStack(source, clone);
        return clone;
    }

    /// <summary>
    /// Neither <c>Error</c> nor <c>DOMException</c> has a specified <c>stack</c>; both specifications say only
    /// that a user agent "should attach a serialized representation of any interesting accompanying data …
    /// notably the stack property". Browsers carry it, and an error whose trace pointed at the clone site
    /// rather than at where it was raised would be actively misleading, so it is carried here too — as the
    /// same kind of property the source had it as.
    /// </summary>
    private static void CopyStack(ErrorInstance source, ObjectInstance clone)
    {
        if (source.Get(CommonProperties.Stack) is not JsString stack)
        {
            return;
        }

        if (clone is JsError error)
        {
            // Jint serves Error.prototype.stack from an internal field through the error-stack accessor
            // (https://tc39.es/proposal-error-stacks/), so the clone gets no own property and looks exactly
            // like an error the engine raised itself.
            error._stack = stack;
            return;
        }

        // A DOMException carries stack as an own non-enumerable property, so replace the one CreateException
        // captured at the clone site.
        clone.SetProperty(CommonProperties.Stack, new PropertyDescriptor(stack, PropertyFlag.NonEnumerable));
    }

    private ErrorPrototype ErrorPrototypeFor(JsValue name)
    {
        if (name is not JsString jsString)
        {
            return _realm.Intrinsics.Error.PrototypeObject;
        }

        return jsString.ToString() switch
        {
            "EvalError" => _realm.Intrinsics.EvalError.PrototypeObject,
            "RangeError" => _realm.Intrinsics.RangeError.PrototypeObject,
            "ReferenceError" => _realm.Intrinsics.ReferenceError.PrototypeObject,
            "SyntaxError" => _realm.Intrinsics.SyntaxError.PrototypeObject,
            "TypeError" => _realm.Intrinsics.TypeError.PrototypeObject,
            "URIError" => _realm.Intrinsics.UriError.PrototypeObject,
            _ => _realm.Intrinsics.Error.PrototypeObject,
        };
    }

    private Native.TypedArray.TypedArrayConstructor ConstructorFor(TypedArrayElementType type) => type switch
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
    /// Steps 2-4 of StructuredSerializeWithTransfer: validate the whole transfer list and reserve a result
    /// buffer for every entry, before a single byte of the graph is looked at.
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
            // Step 2.1 / 2.2: an ArrayBuffer is the only transferable Jint has. A SharedArrayBuffer is
            // explicitly not one.
            if (entry is not JsArrayBuffer buffer || buffer.IsSharedArrayBuffer)
            {
                ThrowDataCloneError("Only ArrayBuffer objects can be transferred");
                return null;
            }

            // Not in the specification, which predates the immutable-ArrayBuffer proposal: an immutable
            // buffer cannot hand its storage over, so it is not transferable.
            if (buffer.IsImmutableBuffer)
            {
                ThrowDataCloneError("An immutable ArrayBuffer cannot be transferred");
            }

            // Step 2.3: a duplicate is a DataCloneError, not a silent second transfer.
            if (_memory.ContainsKey(buffer))
            {
                ThrowDataCloneError("An ArrayBuffer appears more than once in the transfer list");
            }

            // Step 2.4: the result buffer the walk resolves the source buffer to. It takes the source's data
            // block straight away rather than waiting for CompleteTransfers, which is what the specification's
            // uninitialized placeholder would imply, for one reason: a view of this buffer met during the walk
            // is built over it, and a zero-length stand-in would make that view out of bounds. Sharing the
            // block is safe because nothing can reach this object until the clone is returned, by which point
            // the source is detached and this is the only holder — and because a write a getter makes through
            // the source during the walk is meant to be visible in the clone.
            var maxByteLength = buffer._arrayBufferMaxByteLength;
            var target = new JsArrayBuffer(_engine, buffer.ArrayBufferData ?? [], maxByteLength is null ? null : (uint) maxByteLength.Value)
            {
                _prototype = _realm.Intrinsics.ArrayBuffer.PrototypeObject,
            };

            _memory[buffer] = target;
            records.Add(new TransferRecord(buffer, target));
        }

        return records;
    }

    /// <summary>
    /// Step 5: the detach happens after the walk, so the transferred contents are whatever the source held
    /// when serialization finished — including anything a getter reached during the walk wrote into it. The
    /// storage moves rather than being copied, which is the entire point of transferring.
    /// </summary>
    private void CompleteTransfers(List<TransferRecord>? transfers)
    {
        if (transfers is null)
        {
            return;
        }

        foreach (var record in transfers)
        {
            // Step 5.1: a buffer detached during the walk cannot be transferred any more.
            if (record.Source.IsDetachedBuffer)
            {
                ThrowDataCloneError("An ArrayBuffer in the transfer list was detached while it was being cloned");
            }

            // Re-read rather than trusting what PrepareTransfers took: a getter reached during the walk may
            // have resized the buffer, which replaces its data block outright.
            record.Target._arrayBufferData = record.Source.ArrayBufferData;
            record.Source.DetachArrayBuffer();
        }
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
        // the other way round: everything the steps above recognize is cloned, and anything else is refused.
        // That is the conservative direction — it never produces a clone that has silently lost the state its
        // source carried — but it is stricter than a browser for three shapes with no internal slot of their
        // own that Jint nevertheless does not build as an ordinary object: a namespace object (Math, JSON,
        // console), an intrinsic prototype (%Object.prototype%, which step 23 explicitly permits), and an
        // arguments object. Cloning any of those is refused where a browser would answer with a plain object.
        var description = value switch
        {
            JsProxy => "A Proxy",
            JsPromise => "A Promise",
            _ => "An object",
        };

        ThrowDataCloneError(description + " could not be cloned");
    }

    [DoesNotReturn]
    private void ThrowDataCloneError(string message)
    {
        throw new JavaScriptException(_realm.Intrinsics.DomException.CreateException(DomExceptionNames.DataClone, message));
    }

    private readonly record struct TransferRecord(JsArrayBuffer Source, JsArrayBuffer Target);

    /// <summary>
    /// One container whose contents are still to be cloned. The specification recurses here; this is the heap
    /// stand-in for the native frame that recursion would have used.
    /// </summary>
    private abstract class CloneFrame
    {
        /// <summary>
        /// The next source value this container needs a clone of, or <see langword="false"/> when it is done.
        /// </summary>
        internal abstract bool TryGetNextSource(out JsValue source);

        /// <summary>
        /// Receives the clone of the value the last <see cref="TryGetNextSource"/> handed out.
        /// </summary>
        internal abstract void Accept(JsValue clone);
    }

    /// <summary>
    /// Step 26.4, for an Array exotic object and for an ordinary object alike: the enumerable own string keys
    /// snapshot, each read with <c>[[Get]]</c> — so a getter <i>is</i> invoked, and its result is what the
    /// clone carries, as a plain data property. The <c>HasOwnProperty</c> re-check per key is the
    /// specification's, and it matters: an earlier getter is allowed to have deleted a later key.
    /// </summary>
    private sealed class PropertyFrame : CloneFrame
    {
        private readonly ObjectInstance _source;
        private readonly ObjectInstance _target;
        private readonly JsValue[] _keys;
        private int _index;
        private JsValue _currentKey = JsValue.Undefined;

        internal PropertyFrame(ObjectInstance source, ObjectInstance target, JsValue[] keys)
        {
            _source = source;
            _target = target;
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

                _currentKey = key;
                source = _source.Get(key);
                return true;
            }

            source = JsValue.Undefined;
            return false;
        }

        internal override void Accept(JsValue clone)
        {
            _ = _target.CreateDataProperty(_currentKey, clone);
        }
    }

    /// <summary>
    /// Step 26.1: the entry list is copied before anything is cloned, and each entry's key is serialized
    /// before its value.
    /// </summary>
    private sealed class MapFrame : CloneFrame
    {
        private readonly List<KeyValuePair<JsValue, JsValue>> _entries;
        private readonly JsMap _target;
        private int _index;
        private bool _expectingValue;
        private JsValue _pendingKey = JsValue.Undefined;

        internal MapFrame(JsMap source, JsMap target)
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

        internal override void Accept(JsValue clone)
        {
            if (!_expectingValue)
            {
                _pendingKey = clone;
                _expectingValue = true;
                return;
            }

            _target.Set(_pendingKey, clone);
            _pendingKey = JsValue.Undefined;
            _expectingValue = false;
            _index++;
        }
    }

    /// <summary>
    /// Step 26.2, the Set counterpart of <see cref="MapFrame"/>.
    /// </summary>
    private sealed class SetFrame : CloneFrame
    {
        private readonly List<JsValue> _entries;
        private readonly JsSet _target;
        private int _index;

        internal SetFrame(JsSet source, JsSet target)
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

        internal override void Accept(JsValue clone)
        {
            _target.Add(clone);
        }
    }
}
#endif
