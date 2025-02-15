using System.Globalization;
using Jint.Collections;
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
public abstract class TypedArrayConstructor : Constructor
{
    private readonly TypedArrayElementType _arrayElementType;

    internal TypedArrayConstructor(
        Engine engine,
        Realm realm,
        IntrinsicTypedArrayConstructor functionPrototype,
        IntrinsicTypedArrayPrototype objectPrototype,
        TypedArrayElementType type) : base(engine, realm, new JsString(type.GetTypedArrayName()))
    {
        _arrayElementType = type;
        _prototype = functionPrototype;

        PrototypeObject = type == TypedArrayElementType.Uint8
            ? new Uint8ArrayPrototype(engine, objectPrototype, this)
            : new TypedArrayPrototype(engine, objectPrototype, this, type);

        _length = new PropertyDescriptor(JsNumber.PositiveThree, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private Prototype PrototypeObject { get; }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, false)
        {
            ["BYTES_PER_ELEMENT"] = new(new PropertyDescriptor(JsNumber.Create(_arrayElementType.GetElementSize()), PropertyFlag.AllForbidden))
        };
        SetProperties(properties);
    }

    public JsTypedArray Construct(JsArrayBuffer buffer, int? byteOffset = null, int? length = null)
    {
        var o = AllocateTypedArray(this);
        InitializeTypedArrayFromArrayBuffer(o, buffer, byteOffset, length);
        return o;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
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
                int? byteOffset = !arguments.At(1).IsUndefined() ? (int) TypeConverter.ToIndex(_realm, arguments[1]) : null;
                int? length = !arguments.At(2).IsUndefined() ? (int) TypeConverter.ToIndex(_realm, arguments[2]) : null;
                InitializeTypedArrayFromArrayBuffer(o, arrayBuffer, byteOffset, length);
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

        var elementLength = TypeConverter.ToIndex(_realm, firstArgument);
        return AllocateTypedArray(newTarget, elementLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterabletolist
    /// </summary>
    internal static List<JsValue> IterableToList(Realm realm, JsValue items, ICallable? method = null)
    {
        var iteratorRecord = items.GetIterator(realm);
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
            ExceptionHelper.ThrowTypeError(_realm);
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
                ExceptionHelper.ThrowTypeError(_realm, "Content types differ");
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
    /// https://tc39.es/ecma262/#sec-initializetypedarrayfromarraybuffer
    /// </summary>
    private void InitializeTypedArrayFromArrayBuffer(
        JsTypedArray o,
        JsArrayBuffer buffer,
        int? byteOffset,
        int? length)
    {
        var elementSize = o._arrayElementType.GetElementSize();
        var offset = byteOffset ?? 0;
        if (offset % elementSize != 0)
        {
            ExceptionHelper.ThrowRangeError(_realm, "Invalid offset");
        }

        int newByteLength;
        var newLength = length ?? 0;

        var bufferIsFixedLength = buffer.IsFixedLengthArrayBuffer;

        buffer.AssertNotDetached();

        var bufferByteLength = IntrinsicTypedArrayPrototype.ArrayBufferByteLength(buffer, ArrayBufferOrder.SeqCst);
        if (length == null && !bufferIsFixedLength)
        {
            if (offset > bufferByteLength)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid offset");
            }

            o._arrayLength = JsTypedArray.LengthAuto;
            o._byteLength = JsTypedArray.LengthAuto;
        }
        else
        {
            if (length == null)
            {
                if (bufferByteLength % elementSize != 0)
                {
                    ExceptionHelper.ThrowRangeError(_realm, "Invalid buffer byte length");
                }

                newByteLength = bufferByteLength - offset;
                if (newByteLength < 0)
                {
                    ExceptionHelper.ThrowRangeError(_realm, "Invalid buffer byte length");
                }
            }
            else
            {
                newByteLength = newLength * elementSize;
                if (offset + newByteLength > bufferByteLength)
                {
                    ExceptionHelper.ThrowRangeError(_realm, "Invalid buffer byte length");
                }
            }

            o._arrayLength = (uint) (newByteLength / elementSize);
            o._byteLength = (uint) newByteLength;
        }

        o._viewedArrayBuffer = buffer;
        o._byteOffset = offset;
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

    internal static void FillTypedArrayInstance<T>(JsTypedArray target, ReadOnlySpan<T>values)
    {
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
