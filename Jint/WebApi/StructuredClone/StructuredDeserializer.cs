#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;

namespace Jint.WebApi.StructuredClone;

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

    private int _visited;

    internal StructuredDeserializer(Engine engine, Realm realm)
    {
        _engine = engine;
        _realm = realm;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/structured-data.html#structureddeserializewithtransfer
    /// </summary>
    internal JsValue Deserialize(in SerializationRecord record)
    {
        var result = DeserializeValue(record.Root);
        Drain();
        return result;
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

            case SerializedError error:
                result = DeserializeError(error);
                break;

            case SerializedDomException domException:
                result = DeserializeDomException(domException);
                break;

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
    /// happened during serialization.
    /// </summary>
    private JsArrayBuffer DeserializeArrayBuffer(SerializedArrayBuffer record)
    {
        return new JsArrayBuffer(_engine, record.Bytes, record.MaxByteLength)
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

    private JsDomException DeserializeDomException(SerializedDomException record)
    {
        var result = _realm.Intrinsics.DomException.CreateException(record.Name, record.Message);

        if (record.Stack is { } stack)
        {
            // A DOMException carries stack as an own non-enumerable property, so replace the one
            // CreateException captured here.
            result.SetProperty(CommonProperties.Stack, new PropertyDescriptor(JsString.Create(stack), PropertyFlag.NonEnumerable));
        }

        return result;
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
