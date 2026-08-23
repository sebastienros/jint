using System.Globalization;
using System.Runtime.InteropServices;
using Jint.Native.Array;
using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.TypedArray;

/// <summary>
/// https://tc39.es/ecma262/#sec-typedarray-constructors
/// </summary>
[JsObject(UseShape = true)]
public abstract partial class TypedArrayConstructor : Constructor
{
    private readonly TypedArrayElementType _arrayElementType;

    [JsProperty(Name = "BYTES_PER_ELEMENT", Flags = PropertyFlag.AllForbidden)]
    private readonly JsNumber _bytesPerElement;

    internal TypedArrayConstructor(
        Engine engine,
        Realm realm,
        IntrinsicTypedArrayConstructor functionPrototype,
        IntrinsicTypedArrayPrototype objectPrototype,
        TypedArrayElementType type) : base(engine, realm, new JsString(type.GetTypedArrayName()))
    {
        _arrayElementType = type;
        _bytesPerElement = JsNumber.Create(type.GetElementSize());
        _prototype = functionPrototype;

        PrototypeObject = type == TypedArrayElementType.Uint8
            ? new Uint8ArrayPrototype(engine, objectPrototype, this)
            : new TypedArrayPrototype(engine, objectPrototype, this, type);

        _length = new PropertyDescriptor(JsNumber.PositiveThree, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private Prototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    public JsTypedArray Construct(JsArrayBuffer buffer, int? byteOffset = null, int? length = null)
    {
        var o = AllocateTypedArray(this);
        var offset = byteOffset ?? 0;
        ValidateByteOffsetAlignment(offset, o);
        InitializeTypedArrayFromArrayBuffer(o, buffer, offset, length);
        return o;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        var numberOfArgs = arguments.Length;
        if (numberOfArgs == 0)
        {
            return AllocateTypedArray(newTarget, 0);
        }

        var firstArgument = arguments[0];
        if (firstArgument.IsObject())
        {
            var o = AllocateTypedArray(newTarget);
            if (firstArgument is JsTypedArray typedArrayInstance)
            {
                InitializeTypedArrayFromTypedArray(o, typedArrayInstance);
            }
            else if (firstArgument is JsArrayBuffer arrayBuffer)
            {
                InitializeTypedArrayFromArrayBuffer(o, arrayBuffer, arguments.At(1), arguments.At(2));
            }
            else
            {
                var usingIterator = GetMethod(_realm, firstArgument, GlobalSymbolRegistry.Iterator);
                if (usingIterator is not null)
                {
                    var values = IterableToList(_realm, firstArgument, usingIterator);
                    InitializeTypedArrayFromList(o, values);
                }
                else
                {
                    InitializeTypedArrayFromArrayLike(o, (ObjectInstance) firstArgument);
                }
            }

            return o;
        }

        var elementLength = TypeConverter.ToIndexLong(_realm, firstArgument);
        if (elementLength > uint.MaxValue)
        {
            // AllocateTypedArrayBuffer would ask for elementLength * elementSize bytes, which cannot be
            // allocated; CreateByteDataBlock reports that as a RangeError, so report it here too rather
            // than truncating the length into something that happens to fit.
            Throw.RangeError(_realm, "Invalid typed array length");
        }

        return AllocateTypedArray(newTarget, (uint) elementLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterabletolist
    /// </summary>
    internal static List<JsValue> IterableToList(Realm realm, JsValue items, ICallable? method = null)
    {
        var iteratorRecord = items.GetIterator(realm, method: method);
        var values = new List<JsValue>();
        while (iteratorRecord.TryIteratorStep(out var nextItem))
        {
            values.Add(nextItem.Get(CommonProperties.Value));
        }

        return values;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-initializetypedarrayfromtypedarray
    /// </summary>
    private void InitializeTypedArrayFromTypedArray(JsTypedArray o, JsTypedArray srcArray)
    {
        var srcData = srcArray._viewedArrayBuffer;
        srcData.AssertNotDetached();

        var elementType = o._arrayElementType;
        var srcType = srcArray._arrayElementType;
        var srcElementSize = srcType.GetElementSize();
        var srcByteOffset = srcArray._byteOffset;

        var srcRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(srcArray, ArrayBufferOrder.SeqCst);
        if (srcRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(_realm, "Source TypedArray is out of bounds");
        }

        var elementLength = srcRecord.TypedArrayLength;
        var elementSize = elementType.GetElementSize();
        var byteLength = elementSize * elementLength;

        var arrayBuffer = _realm.Intrinsics.ArrayBuffer;
        JsArrayBuffer data;
        if (elementType == srcType)
        {
            data = srcData.CloneArrayBuffer(arrayBuffer, srcByteOffset, byteLength);
        }
        else
        {
            data = arrayBuffer.AllocateArrayBuffer(arrayBuffer, byteLength);
            srcData.AssertNotDetached();
            if (srcArray._contentType != o._contentType)
            {
                Throw.TypeError(_realm, "Content types differ");
            }

            var srcByteIndex = srcByteOffset;
            var targetByteIndex = 0;
            var count = elementLength;
            while (count > 0)
            {
                var value = srcData.GetValueFromBuffer(srcByteIndex, srcType, isTypedArray: true, ArrayBufferOrder.Unordered);
                data.SetValueInBuffer(targetByteIndex, elementType, value, isTypedArray: true, ArrayBufferOrder.Unordered);
                srcByteIndex += srcElementSize;
                targetByteIndex += elementSize;
                count--;
            }
        }

        o._viewedArrayBuffer = data;
        o._arrayLength = elementLength;
        o._byteLength = byteLength;
        o._byteOffset = 0;
    }

    /// <summary>
    /// Step 3 of https://tc39.es/ecma262/#sec-initializetypedarrayfromarraybuffer, split out because it
    /// sits between the two ToIndex coercions and therefore cannot live with the rest of the algorithm.
    /// </summary>
    private void ValidateByteOffsetAlignment(long offset, JsTypedArray o)
    {
        if (offset % o._arrayElementType.GetElementSize() != 0)
        {
            Throw.RangeError(_realm, "Invalid offset");
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-initializetypedarrayfromarraybuffer
    /// </summary>
    /// <remarks>
    /// The two coercions and the alignment check between them are observable and their order is
    /// normative: ToIndex(byteOffset) is step 2, the offset-modulo-elementSize RangeError is step 3, and
    /// ToIndex(length) only comes at step 5. Running both coercions up front made a call such as
    /// new Int32Array(buffer, 1, poisonedValue) report the poisoned value's own error where the spec
    /// requires the RangeError.
    /// </remarks>
    private void InitializeTypedArrayFromArrayBuffer(
        JsTypedArray o,
        JsArrayBuffer buffer,
        JsValue byteOffset,
        JsValue length)
    {
        // 2. Let offset be ? ToIndex(byteOffset).
        var offset = TypeConverter.ToIndexLong(_realm, byteOffset);

        // 3. If offset modulo elementSize is not 0, throw a RangeError exception.
        ValidateByteOffsetAlignment(offset, o);

        // 5. If length is not undefined, let newLength be ? ToIndex(length).
        long? newLength = length.IsUndefined() ? null : TypeConverter.ToIndexLong(_realm, length);

        InitializeTypedArrayFromArrayBuffer(o, buffer, offset, newLength);
    }

    /// <summary>
    /// Steps 4 and 6 onwards of https://tc39.es/ecma262/#sec-initializetypedarrayfromarraybuffer, for a
    /// byteOffset and length that are already coerced and whose alignment has already been checked.
    /// </summary>
    private void InitializeTypedArrayFromArrayBuffer(
        JsTypedArray o,
        JsArrayBuffer buffer,
        long offset,
        long? length)
    {
        // The arithmetic below stays in 64 bits. ToIndex admits anything up to 2^53-1, and narrowing an
        // offset or a length to Int32 first wrapped it into a value that then passed the bounds checks:
        // a byteOffset of 2^31+4 on an empty buffer produced a typed array with a negative byte offset
        // and a length of 536870911.
        var elementSize = o._arrayElementType.GetElementSize();

        // 4. Let bufferIsFixedLength be IsFixedLengthArrayBuffer(buffer).
        var bufferIsFixedLength = buffer.IsFixedLengthArrayBuffer;

        // 6. If IsDetachedBuffer(buffer) is true, throw a TypeError exception.
        buffer.AssertNotDetached();

        // 7. Let bufferByteLength be ArrayBufferByteLength(buffer, seq-cst).
        long bufferByteLength = IntrinsicTypedArrayPrototype.ArrayBufferByteLength(buffer, ArrayBufferOrder.SeqCst);

        // 8. If length is undefined and bufferIsFixedLength is false, then
        if (length is null && !bufferIsFixedLength)
        {
            if (offset > bufferByteLength)
            {
                Throw.RangeError(_realm, "Invalid offset");
            }

            o._arrayLength = JsTypedArray.LengthAuto;
            o._byteLength = JsTypedArray.LengthAuto;
        }
        else
        {
            long newByteLength;
            if (length is null)
            {
                if (bufferByteLength % elementSize != 0)
                {
                    Throw.RangeError(_realm, "Invalid buffer byte length");
                }

                newByteLength = bufferByteLength - offset;
                if (newByteLength < 0)
                {
                    Throw.RangeError(_realm, "Invalid buffer byte length");
                }
            }
            else
            {
                newByteLength = length.Value * elementSize;
                if (offset + newByteLength > bufferByteLength)
                {
                    Throw.RangeError(_realm, "Invalid buffer byte length");
                }
            }

            o._arrayLength = (uint) (newByteLength / elementSize);
            o._byteLength = (uint) newByteLength;
        }

        o._viewedArrayBuffer = buffer;
        o._byteOffset = (int) offset;
    }

    private static void InitializeTypedArrayFromList(JsTypedArray o, List<JsValue> values)
    {
        var len = values.Count;
        o.AllocateTypedArrayBuffer((uint) len);
        for (var k = 0; k < len; ++k)
        {
            o[k] = values[k];
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-initializetypedarrayfromarraylike
    /// </summary>
    private static void InitializeTypedArrayFromArrayLike(JsTypedArray o, ObjectInstance arrayLike)
    {
        var operations = ArrayOperations.For(arrayLike, forWrite: false);
        var len = operations.GetLongLength();
        o.AllocateTypedArrayBuffer(len);
        for (uint k = 0; k < len; ++k)
        {
            o[(int) k] = operations.Get(k);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-allocatetypedarray
    /// </summary>
    internal JsTypedArray AllocateTypedArray(JsValue newTarget, uint length = 0)
    {
        Func<Intrinsics, ObjectInstance> defaultProto = _arrayElementType switch
        {
            TypedArrayElementType.Float16 => static intrinsics => intrinsics.Float16Array.PrototypeObject,
            TypedArrayElementType.Float32 => static intrinsics => intrinsics.Float32Array.PrototypeObject,
            TypedArrayElementType.Float64 => static intrinsics => intrinsics.Float64Array.PrototypeObject,
            TypedArrayElementType.Int8 => static intrinsics => intrinsics.Int8Array.PrototypeObject,
            TypedArrayElementType.Int16 => static intrinsics => intrinsics.Int16Array.PrototypeObject,
            TypedArrayElementType.Int32 => static intrinsics => intrinsics.Int32Array.PrototypeObject,
            TypedArrayElementType.BigInt64 => static intrinsics => intrinsics.BigInt64Array.PrototypeObject,
            TypedArrayElementType.Uint8 => static intrinsics => intrinsics.Uint8Array.PrototypeObject,
            TypedArrayElementType.Uint8C => static intrinsics => intrinsics.Uint8ClampedArray.PrototypeObject,
            TypedArrayElementType.Uint16 => static intrinsics => intrinsics.Uint16Array.PrototypeObject,
            TypedArrayElementType.Uint32 => static intrinsics => intrinsics.Uint32Array.PrototypeObject,
            TypedArrayElementType.BigUint64 => static intrinsics => intrinsics.BigUint64Array.PrototypeObject,
            _ => null!
        };

        var proto = GetPrototypeFromConstructor(newTarget, defaultProto);
        var realm = GetFunctionRealm(newTarget);
        var obj = new JsTypedArray(_engine, realm.Intrinsics, _arrayElementType, length)
        {
            _prototype = proto
        };
        if (length > 0)
        {
            obj.AllocateTypedArrayBuffer(length);
        }

        return obj;
    }

    internal static void FillTypedArrayInstance<T>(JsTypedArray target, ReadOnlySpan<T> values) where T : struct
    {
        if (values.IsEmpty)
        {
            return;
        }

        // Each concrete constructor passes the CLR element type that exactly matches the target's element type, so on
        // little-endian platforms the source bytes already are the buffer's storage representation and can be copied
        // in bulk without any per-element conversion. Fall back to element-by-element conversion on big-endian.
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.AsBytes(values).CopyTo(target._viewedArrayBuffer._arrayBufferData!.AsSpan(target._byteOffset));
            return;
        }

        for (var i = 0; i < values.Length; ++i)
        {
            target.DoIntegerIndexedElementSet(i, Convert.ToDouble(values[i], CultureInfo.InvariantCulture));
        }
    }

    internal static void FillTypedArrayInstance(JsTypedArray target, ReadOnlySpan<ulong> values)
    {
        for (var i = 0; i < values.Length; ++i)
        {
            target.DoIntegerIndexedElementSet(i, values[i]);
        }
    }

    internal static void FillTypedArrayInstance(JsTypedArray target, ReadOnlySpan<long> values)
    {
        for (var i = 0; i < values.Length; ++i)
        {
            target.DoIntegerIndexedElementSet(i, values[i]);
        }
    }
}
