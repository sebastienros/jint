#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Collections;
using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.DataView;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-dataview-prototype-object
/// </summary>
internal sealed class DataViewPrototype : Prototype
{
    private readonly DataViewConstructor _constructor;

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
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(26, checkExistingKeys: false)
        {
            ["buffer"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get buffer", Buffer, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["byteLength"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get byteLength", ByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["byteOffset"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get byteOffset", ByteOffset, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["getBigInt64"] = new PropertyDescriptor(new ClrFunction(Engine, "getBigInt64", GetBigInt64, length: 1, lengthFlags), propertyFlags),
            ["getBigUint64"] = new PropertyDescriptor(new ClrFunction(Engine, "getBigUint64", GetBigUint64, length: 1, lengthFlags), propertyFlags),
            ["getFloat16"] = new PropertyDescriptor(new ClrFunction(Engine, "getFloat16", GetFloat16, length: 1, lengthFlags), propertyFlags),
            ["getFloat32"] = new PropertyDescriptor(new ClrFunction(Engine, "getFloat32", GetFloat32, length: 1, lengthFlags), propertyFlags),
            ["getFloat64"] = new PropertyDescriptor(new ClrFunction(Engine, "getFloat64", GetFloat64, length: 1, lengthFlags), propertyFlags),
            ["getInt8"] = new PropertyDescriptor(new ClrFunction(Engine, "getInt8", GetInt8, length: 1, lengthFlags), propertyFlags),
            ["getInt16"] = new PropertyDescriptor(new ClrFunction(Engine, "getInt16", GetInt16, length: 1, lengthFlags), propertyFlags),
            ["getInt32"] = new PropertyDescriptor(new ClrFunction(Engine, "getInt32", GetInt32, length: 1, lengthFlags), propertyFlags),
            ["getUint8"] = new PropertyDescriptor(new ClrFunction(Engine, "getUint8", GetUint8, length: 1, lengthFlags), propertyFlags),
            ["getUint16"] = new PropertyDescriptor(new ClrFunction(Engine, "getUint16", GetUint16, length: 1, lengthFlags), propertyFlags),
            ["getUint32"] = new PropertyDescriptor(new ClrFunction(Engine, "getUint32", GetUint32, length: 1, lengthFlags), propertyFlags),
            ["setBigInt64"] = new PropertyDescriptor(new ClrFunction(Engine, "setBigInt64", SetBigInt64, length: 2, lengthFlags), propertyFlags),
            ["setBigUint64"] = new PropertyDescriptor(new ClrFunction(Engine, "setBigUint64", SetBigUint64, length: 2, lengthFlags), propertyFlags),
            ["setFloat16"] = new PropertyDescriptor(new ClrFunction(Engine, "setFloat16", SetFloat16, length: 2, lengthFlags), propertyFlags),
            ["setFloat32"] = new PropertyDescriptor(new ClrFunction(Engine, "setFloat32", SetFloat32, length: 2, lengthFlags), propertyFlags),
            ["setFloat64"] = new PropertyDescriptor(new ClrFunction(Engine, "setFloat64", SetFloat64, length: 2, lengthFlags), propertyFlags),
            ["setInt8"] = new PropertyDescriptor(new ClrFunction(Engine, "setInt8", SetInt8, length: 2, lengthFlags), propertyFlags),
            ["setInt16"] = new PropertyDescriptor(new ClrFunction(Engine, "setInt16", SetInt16, length: 2, lengthFlags), propertyFlags),
            ["setInt32"] = new PropertyDescriptor(new ClrFunction(Engine, "setInt32", SetInt32, length: 2, lengthFlags), propertyFlags),
            ["setUint8"] = new PropertyDescriptor(new ClrFunction(Engine, "setUint8", SetUint8, length: 2, lengthFlags), propertyFlags),
            ["setUint16"] = new PropertyDescriptor(new ClrFunction(Engine, "setUint16", SetUint16, length: 2, lengthFlags), propertyFlags),
            ["setUint32"] = new PropertyDescriptor(new ClrFunction(Engine, "setUint32", SetUint32, length: 2, lengthFlags), propertyFlags)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1) { [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("DataView", PropertyFlag.Configurable) };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-dataview.prototype.buffer
    /// </summary>
    private JsValue Buffer(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsDataView;
        if (o is null)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method get DataView.prototype.buffer called on incompatible receiver " + thisObject);
        }

        return o._viewedArrayBuffer!;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-dataview.prototype.bytelength
    /// </summary>
    private JsValue ByteLength(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsDataView;
        if (o is null)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method get DataView.prototype.byteLength called on incompatible receiver " + thisObject);
        }

        var viewRecord = MakeDataViewWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
        if (viewRecord.IsViewOutOfBounds)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var buffer = o._viewedArrayBuffer!;
        buffer.AssertNotDetached();

        return JsNumber.Create(viewRecord.ViewByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-dataview.prototype.byteoffset
    /// </summary>
    private JsValue ByteOffset(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsDataView;
        if (o is null)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method get DataView.prototype.byteOffset called on incompatible receiver " + thisObject);
        }

        var viewRecord = MakeDataViewWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
        if (viewRecord.IsViewOutOfBounds)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var buffer = o._viewedArrayBuffer!;
        buffer.AssertNotDetached();

        return JsNumber.Create(o._byteOffset);
    }

    private JsValue GetBigInt64(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1), TypedArrayElementType.BigInt64);
    }

    private JsValue GetBigUint64(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1), TypedArrayElementType.BigUint64);
    }

    private JsValue GetFloat16(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1, JsBoolean.False), TypedArrayElementType.Float16);
    }

    private JsValue GetFloat32(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1, JsBoolean.False), TypedArrayElementType.Float32);
    }

    private JsValue GetFloat64(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1, JsBoolean.False), TypedArrayElementType.Float64);
    }

    private JsValue GetInt8(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), JsBoolean.True, TypedArrayElementType.Int8);
    }

    private JsValue GetInt16(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1, JsBoolean.False), TypedArrayElementType.Int16);
    }

    private JsValue GetInt32(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1, JsBoolean.False), TypedArrayElementType.Int32);
    }

    private JsValue GetUint8(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), JsBoolean.True, TypedArrayElementType.Uint8);
    }

    private JsValue GetUint16(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1, JsBoolean.False), TypedArrayElementType.Uint16);
    }

    private JsValue GetUint32(JsValue thisObject, JsCallArguments arguments)
    {
        return GetViewValue(thisObject, arguments.At(0), arguments.At(1, JsBoolean.False), TypedArrayElementType.Uint32);
    }

    private JsValue SetBigInt64(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2), TypedArrayElementType.BigInt64, arguments.At(1));
    }

    private JsValue SetBigUint64(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2), TypedArrayElementType.BigUint64, arguments.At(1));
    }

    private JsValue SetFloat16(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2, JsBoolean.False), TypedArrayElementType.Float16, arguments.At(1));
    }

    private JsValue SetFloat32(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2, JsBoolean.False), TypedArrayElementType.Float32, arguments.At(1));
    }

    private JsValue SetFloat64(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2, JsBoolean.False), TypedArrayElementType.Float64, arguments.At(1));
    }

    private JsValue SetInt8(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), JsBoolean.True, TypedArrayElementType.Int8, arguments.At(1));
    }

    private JsValue SetInt16(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2, JsBoolean.False), TypedArrayElementType.Int16, arguments.At(1));
    }

    private JsValue SetInt32(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2, JsBoolean.False), TypedArrayElementType.Int32, arguments.At(1));
    }

    private JsValue SetUint8(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), JsBoolean.True, TypedArrayElementType.Uint8, arguments.At(1));
    }

    private JsValue SetUint16(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2, JsBoolean.False), TypedArrayElementType.Uint16, arguments.At(1));
    }

    private JsValue SetUint32(JsValue thisObject, JsCallArguments arguments)
    {
        return SetViewValue(thisObject, arguments.At(0), arguments.At(2, JsBoolean.False), TypedArrayElementType.Uint32, arguments.At(1));
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
            ExceptionHelper.ThrowTypeError(_realm, "Method called on incompatible receiver " + view);
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
            ExceptionHelper.ThrowTypeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var viewSize = viewRecord.ViewByteLength;
        var elementSize = type.GetElementSize();
        if (getIndex + elementSize > viewSize)
        {
            ExceptionHelper.ThrowRangeError(_realm, "Offset is outside the bounds of the DataView");
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
            ExceptionHelper.ThrowTypeError(_realm, "Method called on incompatible receiver " + view);
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
        var buffer = dataView._viewedArrayBuffer!;
        buffer.AssertNotDetached();

        var viewOffset = dataView._byteOffset;
        var viewRecord = MakeDataViewWithBufferWitnessRecord(dataView, ArrayBufferOrder.Unordered);
        if (viewRecord.IsViewOutOfBounds)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var viewSize = viewRecord.ViewByteLength;
        var elementSize = type.GetElementSize();
        if (getIndex + elementSize > viewSize)
        {
            ExceptionHelper.ThrowRangeError(_realm, "Offset is outside the bounds of the DataView");
        }

        var bufferIndex = (int) (getIndex + viewOffset);
        buffer.SetValueInBuffer(bufferIndex, type, numberValue, false, ArrayBufferOrder.Unordered, isLittleEndianBoolean);
        return Undefined;
    }
}
