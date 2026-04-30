#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.DataView;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-dataview-prototype-object
/// </summary>
[JsObject]
internal sealed partial class DataViewPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly DataViewConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString DataViewToStringTag = new("DataView");

    internal DataViewPrototype(
        Engine engine,
        DataViewConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-dataview.prototype.buffer
    /// </summary>
    [JsAccessor("buffer")]
    private JsValue Buffer(JsValue thisObject)
    {
        var o = thisObject as JsDataView;
        if (o is null)
        {
            Throw.TypeError(_realm, "Method get DataView.prototype.buffer called on incompatible receiver " + thisObject);
        }

        return o._viewedArrayBuffer!;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-dataview.prototype.bytelength
    /// </summary>
    [JsAccessor("byteLength")]
    private JsValue ByteLength(JsValue thisObject)
    {
        var o = thisObject as JsDataView;
        if (o is null)
        {
            Throw.TypeError(_realm, "Method get DataView.prototype.byteLength called on incompatible receiver " + thisObject);
        }

        var viewRecord = MakeDataViewWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
        if (viewRecord.IsViewOutOfBounds)
        {
            Throw.TypeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var buffer = o._viewedArrayBuffer!;
        buffer.AssertNotDetached();

        return JsNumber.Create(viewRecord.ViewByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-dataview.prototype.byteoffset
    /// </summary>
    [JsAccessor("byteOffset")]
    private JsValue ByteOffset(JsValue thisObject)
    {
        var o = thisObject as JsDataView;
        if (o is null)
        {
            Throw.TypeError(_realm, "Method get DataView.prototype.byteOffset called on incompatible receiver " + thisObject);
        }

        var viewRecord = MakeDataViewWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
        if (viewRecord.IsViewOutOfBounds)
        {
            Throw.TypeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var buffer = o._viewedArrayBuffer!;
        buffer.AssertNotDetached();

        return JsNumber.Create(o._byteOffset);
    }

    [JsFunction(Length = 1)]
    private JsValue GetBigInt64(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.BigInt64);
    }

    [JsFunction(Length = 1)]
    private JsValue GetBigUint64(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.BigUint64);
    }

    [JsFunction(Length = 1)]
    private JsValue GetFloat16(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Float16);
    }

    [JsFunction(Length = 1)]
    private JsValue GetFloat32(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Float32);
    }

    [JsFunction(Length = 1)]
    private JsValue GetFloat64(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Float64);
    }

    // 1-byte reads have no endianness; spec passes byteOffset only and Length is 1.
    [JsFunction(Length = 1)]
    private JsValue GetInt8(JsValue thisObject, JsValue byteOffset)
    {
        return GetViewValue(thisObject, byteOffset, JsBoolean.True, TypedArrayElementType.Int8);
    }

    [JsFunction(Length = 1)]
    private JsValue GetInt16(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Int16);
    }

    [JsFunction(Length = 1)]
    private JsValue GetInt32(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Int32);
    }

    [JsFunction(Length = 1)]
    private JsValue GetUint8(JsValue thisObject, JsValue byteOffset)
    {
        return GetViewValue(thisObject, byteOffset, JsBoolean.True, TypedArrayElementType.Uint8);
    }

    [JsFunction(Length = 1)]
    private JsValue GetUint16(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Uint16);
    }

    [JsFunction(Length = 1)]
    private JsValue GetUint32(JsValue thisObject, JsValue byteOffset, JsValue littleEndian)
    {
        return GetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Uint32);
    }

    [JsFunction(Length = 2)]
    private JsValue SetBigInt64(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.BigInt64, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetBigUint64(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.BigUint64, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetFloat16(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Float16, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetFloat32(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Float32, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetFloat64(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Float64, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetInt8(JsValue thisObject, JsValue byteOffset, JsValue value)
    {
        return SetViewValue(thisObject, byteOffset, JsBoolean.True, TypedArrayElementType.Int8, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetInt16(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Int16, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetInt32(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Int32, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetUint8(JsValue thisObject, JsValue byteOffset, JsValue value)
    {
        return SetViewValue(thisObject, byteOffset, JsBoolean.True, TypedArrayElementType.Uint8, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetUint16(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Uint16, value);
    }

    [JsFunction(Length = 2)]
    private JsValue SetUint32(JsValue thisObject, JsValue byteOffset, JsValue value, JsValue littleEndian)
    {
        return SetViewValue(thisObject, byteOffset, littleEndian, TypedArrayElementType.Uint32, value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getviewvalue
    /// </summary>
    private JsValue GetViewValue(
        JsValue view,
        JsValue requestIndex,
        JsValue isLittleEndian,
        TypedArrayElementType type)
    {
        if (view is not JsDataView dataView)
        {
            Throw.TypeError(_realm, "Method called on incompatible receiver " + view);
            return Undefined;
        }

        var getIndex = (int) TypeConverter.ToIndex(_realm, requestIndex);
        var isLittleEndianBoolean = TypeConverter.ToBoolean(isLittleEndian);
        var buffer = dataView._viewedArrayBuffer!;

        buffer.AssertNotDetached();

        var viewOffset = dataView._byteOffset;
        var viewRecord = MakeDataViewWithBufferWitnessRecord(dataView, ArrayBufferOrder.Unordered);
        if (viewRecord.IsViewOutOfBounds)
        {
            Throw.TypeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var viewSize = viewRecord.ViewByteLength;
        var elementSize = type.GetElementSize();
        if (getIndex + elementSize > viewSize)
        {
            Throw.RangeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var bufferIndex = (int) (getIndex + viewOffset);
        return buffer.GetValueFromBuffer(bufferIndex, type, isTypedArray: false, ArrayBufferOrder.Unordered, isLittleEndianBoolean).ToJsValue();
    }

    internal readonly record struct DataViewWithBufferWitnessRecord(JsDataView Object, int CachedBufferByteLength)
    {
        /// <summary>
        /// https://tc39.es/ecma262/#sec-isviewoutofbounds
        /// </summary>
        public bool IsViewOutOfBounds
        {
            get
            {
                var view = Object;
                var bufferByteLength = CachedBufferByteLength;
                if (bufferByteLength == -1)
                {
                    return true;
                }

                var byteOffsetStart = view._byteOffset;
                long byteOffsetEnd;
                if (view._byteLength == JsTypedArray.LengthAuto)
                {
                    byteOffsetEnd = bufferByteLength;
                }
                else
                {
                    byteOffsetEnd = byteOffsetStart + view._byteLength;
                }

                if (byteOffsetStart > bufferByteLength || byteOffsetEnd > bufferByteLength)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getviewbytelength
        /// </summary>
        public long ViewByteLength
        {
            get
            {
                var view = Object;
                if (view._byteLength != JsTypedArray.LengthAuto)
                {
                    return view._byteLength;
                }

                var byteOffset = view._byteOffset;
                var byteLength = CachedBufferByteLength;
                return byteLength - byteOffset;
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-makedataviewwithbufferwitnessrecord
    /// </summary>
    private static DataViewWithBufferWitnessRecord MakeDataViewWithBufferWitnessRecord(JsDataView obj, ArrayBufferOrder order)
    {
        var buffer = obj._viewedArrayBuffer;
        int byteLength;
        if (buffer?.IsDetachedBuffer == true)
        {
            byteLength = -1;
        }
        else
        {
            byteLength = IntrinsicTypedArrayPrototype.ArrayBufferByteLength(buffer!, order);
        }

        return new DataViewWithBufferWitnessRecord(obj, byteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-setviewvalue
    /// https://tc39.es/proposal-immutable-arraybuffer/#sec-setviewvalue
    /// </summary>
    private JsValue SetViewValue(
        JsValue view,
        JsValue requestIndex,
        JsValue isLittleEndian,
        TypedArrayElementType type,
        JsValue value)
    {
        var dataView = view as JsDataView;
        if (dataView is null)
        {
            Throw.TypeError(_realm, "Method called on incompatible receiver " + view);
        }

        var buffer = dataView._viewedArrayBuffer!;

        // https://tc39.es/proposal-immutable-arraybuffer/#sec-setviewvalue
        // Check immutability BEFORE processing arguments
        if (buffer.IsImmutableBuffer)
        {
            Throw.TypeError(_realm, "Cannot modify an immutable ArrayBuffer");
        }

        var getIndex = TypeConverter.ToIndex(_realm, requestIndex);

        TypedArrayValue numberValue;
        if (type.IsBigIntElementType())
        {
            numberValue = TypeConverter.ToBigInt(value);
        }
        else
        {
            numberValue = TypeConverter.ToNumber(value);
        }

        var isLittleEndianBoolean = TypeConverter.ToBoolean(isLittleEndian);
        buffer.AssertNotDetached();

        var viewOffset = dataView._byteOffset;
        var viewRecord = MakeDataViewWithBufferWitnessRecord(dataView, ArrayBufferOrder.Unordered);
        if (viewRecord.IsViewOutOfBounds)
        {
            Throw.TypeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var viewSize = viewRecord.ViewByteLength;
        var elementSize = type.GetElementSize();
        if (getIndex + elementSize > viewSize)
        {
            Throw.RangeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var bufferIndex = (int) (getIndex + viewOffset);
        buffer.SetValueInBuffer(bufferIndex, type, numberValue, false, ArrayBufferOrder.Unordered, isLittleEndianBoolean);
        return Undefined;
    }
}
