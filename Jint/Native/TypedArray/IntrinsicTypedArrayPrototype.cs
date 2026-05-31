#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Jint.Native.Array;
using Jint.Native.ArrayBuffer;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.TypedArray;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-%typedarrayprototype%-object
/// </summary>
[JsObject(ExtraCapacity = 1)]
internal sealed partial class IntrinsicTypedArrayPrototype : Prototype
{
    private const int ConstraintCheckInterval = Engine.ConstraintCheckInterval;

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly IntrinsicTypedArrayConstructor _constructor;

    internal IntrinsicTypedArrayPrototype(
        Engine engine,
        ObjectInstance objectPrototype,
        IntrinsicTypedArrayConstructor constructor) : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Per spec (ECMA-262 23.2.3.32) the initial value of %TypedArray%.prototype.toString is the
        // SAME function object as %Array.prototype.toString%. Hand-write this entry — the generator
        // can't express "alias to another realm intrinsic" through [JsFunction]. AddDangerous skips
        // SetOwnProperty's validation; ExtraCapacity=1 on [JsObject] presizes the dict.
        _properties!.AddDangerous("toString", new LazyPropertyDescriptor<IntrinsicTypedArrayPrototype>(this, static prototype => prototype._realm.Intrinsics.Array.PrototypeObject.Get("toString"), PropertyFlags));

        // Per spec, %TypedArray%.prototype[@@iterator] is the SAME function object as
        // %TypedArray%.prototype.values. Materialize the generated `values` slot and reuse it.
        // (Symbol additions stay as SetOwnProperty — they go to _symbols, not _properties.)
        var valuesValue = GetOwnProperty("values").Value;
        SetOwnProperty(GlobalSymbolRegistry.Iterator, new PropertyDescriptor(valuesValue, PropertyFlags));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.buffer
    /// </summary>
    [JsAccessor("buffer")]
    private JsValue Buffer(JsValue thisObject)
    {
        var o = thisObject as JsTypedArray;
        if (o is null)
        {
            Throw.TypeError(_realm, $"Method get TypedArray.prototype.buffer called on incompatible receiver {thisObject}");
        }

        return o._viewedArrayBuffer;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.bytelength
    /// </summary>
    [JsAccessor("byteLength")]
    private JsValue ByteLength(JsValue thisObject)
    {
        var o = thisObject as JsTypedArray;
        if (o is null)
        {
            Throw.TypeError(_realm, $"Method get TypedArray.prototype.byteLength called on incompatible receiver {thisObject}");
        }

        var taRecord = MakeTypedArrayWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
        return JsNumber.Create(taRecord.TypedArrayByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.byteoffset
    /// </summary>
    [JsAccessor("byteOffset")]
    private JsValue ByteOffset(JsValue thisObject)
    {
        var o = thisObject as JsTypedArray;
        if (o is null)
        {
            Throw.TypeError(_realm, $"Method get TypedArray.prototype.byteOffset called on incompatible receiver {thisObject}");
        }

        var taRecord = MakeTypedArrayWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            return JsNumber.PositiveZero;
        }

        return JsNumber.Create(o._byteOffset);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.length
    /// </summary>
    [JsAccessor("length")]
    private JsValue GetLength(JsValue thisObject)
    {
        var o = thisObject as JsTypedArray;
        if (o is null)
        {
            Throw.TypeError(_realm, $"Method get TypedArray.prototype.length called on incompatible receiver {thisObject}");
        }

        var taRecord = MakeTypedArrayWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            return JsNumber.PositiveZero;
        }

        return JsNumber.Create(taRecord.TypedArrayLength);
    }

    internal readonly record struct TypedArrayWithBufferWitnessRecord(JsTypedArray Object, int CachedBufferByteLength)
    {
        /// <summary>
        /// https://tc39.es/ecma262/#sec-istypedarrayoutofbounds
        /// </summary>
        public bool IsTypedArrayOutOfBounds
        {
            get
            {
                var o = Object;
                var bufferByteLength = CachedBufferByteLength;
                if (bufferByteLength == -1)
                {
                    return true;
                }

                var byteOffsetStart = o._byteOffset;
                long byteOffsetEnd;
                if (o._arrayLength == JsTypedArray.LengthAuto)
                {
                    byteOffsetEnd = bufferByteLength;
                }
                else
                {
                    var elementSize = o._arrayElementType.GetElementSize();
                    byteOffsetEnd = byteOffsetStart + o._arrayLength * elementSize;
                }

                if (byteOffsetStart > bufferByteLength || byteOffsetEnd > bufferByteLength)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-typedarraylength
        /// </summary>
        public uint TypedArrayLength
        {
            get
            {
                var o = Object;
                if (o._arrayLength != JsTypedArray.LengthAuto)
                {
                    return o._arrayLength;
                }

                var byteOffset = o._byteOffset;
                var elementSize = o._arrayElementType.GetElementSize();
                var byteLength = (double) CachedBufferByteLength;
                var floor = System.Math.Floor((byteLength - byteOffset) / elementSize);
                return floor < 0 ? 0 : (uint) floor;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-typedarraybytelength
        /// </summary>
        public uint TypedArrayByteLength
        {
            get
            {
                if (IsTypedArrayOutOfBounds)
                {
                    return 0;
                }

                var length = TypedArrayLength;
                if (length == 0)
                {
                    return 0;
                }

                var o = Object;
                if (o._byteLength != JsTypedArray.LengthAuto)
                {
                    return o._byteLength;
                }

                return length * o._arrayElementType.GetElementSize();
            }
        }
    }

    internal static TypedArrayWithBufferWitnessRecord MakeTypedArrayWithBufferWitnessRecord(JsTypedArray obj, ArrayBufferOrder order)
    {
        var buffer = obj._viewedArrayBuffer;
        var byteLength = buffer.IsDetachedBuffer
            ? -1
            : ArrayBufferByteLength(buffer, order);

        return new TypedArrayWithBufferWitnessRecord(obj, byteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraybufferbytelength
    /// </summary>
    internal static int ArrayBufferByteLength(JsArrayBuffer arrayBuffer, ArrayBufferOrder order)
    {
        if (arrayBuffer.IsSharedArrayBuffer && arrayBuffer.ArrayBufferByteLength > 0)
        {
            // a. Let bufferByteLengthBlock be arrayBuffer.[[ArrayBufferByteLengthData]].
            // b. Let rawLength be GetRawBytesFromSharedBlock(bufferByteLengthBlock, 0, BIGUINT64, true, order).
            // c. Let isLittleEndian be the value of the [[LittleEndian]] field of the surrounding agent's Agent Record.
            // d. Return ℝ(RawBytesToNumeric(BIGUINT64, rawLength, isLittleEndian)).
        }

        return arrayBuffer.ArrayBufferByteLength;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.copywithin
    /// </summary>
    [JsFunction(Length = 2)]
    private JsValue CopyWithin(JsValue thisObject, JsValue target, JsValue start, JsValue end)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var relativeTarget = TypeConverter.ToIntegerOrInfinity(target);

        long targetIndex;
        if (double.IsNegativeInfinity(relativeTarget))
        {
            targetIndex = 0;
        }
        else if (relativeTarget < 0)
        {
            targetIndex = (long) System.Math.Max(len + relativeTarget, 0);
        }
        else
        {
            targetIndex = (long) System.Math.Min(relativeTarget, len);
        }

        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);

        long startIndex;
        if (double.IsNegativeInfinity(relativeStart))
        {
            startIndex = 0;
        }
        else if (relativeStart < 0)
        {
            startIndex = (long) System.Math.Max(len + relativeStart, 0);
        }
        else
        {
            startIndex = (long) System.Math.Min(relativeStart, len);
        }

        var relativeEnd = end.IsUndefined()
            ? len
            : TypeConverter.ToIntegerOrInfinity(end);

        long endIndex;
        if (double.IsNegativeInfinity(relativeEnd))
        {
            endIndex = 0;
        }
        else if (relativeEnd < 0)
        {
            endIndex = (long) System.Math.Max(len + relativeEnd, 0);
        }
        else
        {
            endIndex = (long) System.Math.Min(relativeEnd, len);
        }

        var count = System.Math.Min(endIndex - startIndex, len - targetIndex);

        if (count > 0)
        {
            var buffer = o._viewedArrayBuffer;
            buffer.AssertNotDetached();

            taRecord = MakeTypedArrayWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
            if (taRecord.IsTypedArrayOutOfBounds)
            {
                Throw.TypeError(_realm, "TypedArray is out of bounds");
            }

            len = taRecord.TypedArrayLength;
            count = System.Math.Min(count, System.Math.Min(len - startIndex, len - targetIndex));

            var elementSize = o._arrayElementType.GetElementSize();
            var byteOffset = o._byteOffset;
            var toByteIndex = targetIndex * elementSize + byteOffset;
            var fromByteIndex = startIndex * elementSize + byteOffset;
            var countBytes = count * elementSize;

            // count is already clamped so the whole [fromByteIndex, +countBytes) and [toByteIndex, +countBytes)
            // ranges stay within the buffer. System.Array.Copy has memmove semantics, so it copies overlapping
            // regions correctly without the spec's explicit forward/backward direction handling.
            buffer.AssertNotImmutable();
            _engine.Constraints.Check();
            System.Array.Copy(buffer._arrayBufferData!, fromByteIndex, buffer._arrayBufferData!, toByteIndex, countBytes);
        }

        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.entries
    /// </summary>
    [JsFunction]
    private JsValue Entries(JsValue thisObject)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.KeyAndValue);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.every
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Every(JsValue thisObject, JsValue callbackFn, JsValue thisArg)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        if (len == 0)
        {
            return JsBoolean.True;
        }

        var predicate = GetCallable(callbackFn);

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o;
        for (var k = 0; k < len; k++)
        {
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            args[0] = o[k];
            args[1] = k;
            if (!TypeConverter.ToBoolean(predicate.Call(thisArg, args)))
            {
                return JsBoolean.False;
            }
        }

        _engine._jsValueArrayPool.ReturnArray(args);

        return JsBoolean.True;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.fill
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Fill(JsValue thisObject, JsValue jsValue, JsValue start, JsValue end)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        JsValue value;
        if (o._contentType == TypedArrayContentType.BigInt)
        {
            value = JsBigInt.Create(jsValue.ToBigInteger(_engine));
        }
        else
        {
            value = JsNumber.Create(jsValue);
        }

        int k;
        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);
        if (double.IsNegativeInfinity(relativeStart))
        {
            k = 0;
        }
        else if (relativeStart < 0)
        {
            k = (int) System.Math.Max(len + relativeStart, 0);
        }
        else
        {
            k = (int) System.Math.Min(relativeStart, len);
        }

        uint endIndex;
        var relativeEnd = end.IsUndefined() ? len : TypeConverter.ToIntegerOrInfinity(end);
        if (double.IsNegativeInfinity(relativeEnd))
        {
            endIndex = 0;
        }
        else if (relativeEnd < 0)
        {
            endIndex = (uint) System.Math.Max(len + relativeEnd, 0);
        }
        else
        {
            endIndex = (uint) System.Math.Min(relativeEnd, len);
        }

        taRecord = MakeTypedArrayWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
        if (taRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(_realm, "TypedArray is out of bounds");
        }

        len = taRecord.TypedArrayLength;
        endIndex = System.Math.Min(endIndex, len);

        var buffer = o._viewedArrayBuffer;
        buffer.AssertNotDetached();

        if (k < endIndex)
        {
            // Seed the first element through the normal indexer (this applies per-type clamping/rounding and the
            // immutable-buffer check), then replicate its on-buffer byte pattern across the remaining range with
            // exponential doubling. This is endian-agnostic (whole-element byte blocks are copied verbatim) and
            // avoids re-encoding the value for every element.
            o[k] = value;

            var elementSize = o._arrayElementType.GetElementSize();
            var data = buffer._arrayBufferData!;
            var startByte = k * elementSize + o._byteOffset;
            var count = (int) endIndex - k;

            _engine.Constraints.Check();

            var filled = 1;
            while (filled < count)
            {
                var copy = System.Math.Min(filled, count - filled);
                System.Array.Copy(data, startByte, data, startByte + filled * elementSize, copy * elementSize);
                filled += copy;
            }
        }

        return thisObject;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.filter
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Filter(JsValue thisObject, JsValue callbackFn, JsValue thisArg)
    {
        var callbackfn = GetCallable(callbackFn);

        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var kept = new List<JsValue>();
        var captured = 0;

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o;
        for (var k = 0; k < len; k++)
        {
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var kValue = o[k];
            args[0] = kValue;
            args[1] = k;
            var selected = callbackfn.Call(thisArg, args);
            if (TypeConverter.ToBoolean(selected))
            {
                kept.Add(kValue);
                captured++;
            }
        }

        _engine._jsValueArrayPool.ReturnArray(args);

        var a = _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, [captured]);
        for (var n = 0; n < captured; ++n)
        {
            a[n] = kept[n];
        }

        return a;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.find
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Find(JsValue thisObject, JsValue predicate, JsValue thisArg)
    {
        return DoFind(thisObject, predicate, thisArg).Value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.findindex
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue FindIndex(JsValue thisObject, JsValue predicate, JsValue thisArg)
    {
        return DoFind(thisObject, predicate, thisArg).Key;
    }

    [JsFunction(Length = 1)]
    private JsValue FindLast(JsValue thisObject, JsValue predicate, JsValue thisArg)
    {
        return DoFind(thisObject, predicate, thisArg, fromEnd: true).Value;
    }

    [JsFunction(Length = 1)]
    private JsValue FindLastIndex(JsValue thisObject, JsValue predicate, JsValue thisArg)
    {
        return DoFind(thisObject, predicate, thisArg, fromEnd: true).Key;
    }

    private KeyValuePair<JsValue, JsValue> DoFind(JsValue thisObject, JsValue predicateArg, JsValue thisArg, bool fromEnd = false)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var predicate = GetCallable(predicateArg);

        if (len == 0)
        {
            return new KeyValuePair<JsValue, JsValue>(JsNumber.IntegerNegativeOne, Undefined);
        }

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o;
        if (!fromEnd)
        {
            for (var k = 0; k < len; k++)
            {
                if (k > 0 && k % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                var kNumber = JsNumber.Create(k);
                var kValue = o[k];
                args[0] = kValue;
                args[1] = kNumber;
                if (TypeConverter.ToBoolean(predicate.Call(thisArg, args)))
                {
                    return new KeyValuePair<JsValue, JsValue>(kNumber, kValue);
                }
            }
        }
        else
        {
            for (var k = (int) (len - 1); k >= 0; k--)
            {
                if (k % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                var kNumber = JsNumber.Create(k);
                var kValue = o[k];
                args[0] = kValue;
                args[1] = kNumber;
                if (TypeConverter.ToBoolean(predicate.Call(thisArg, args)))
                {
                    return new KeyValuePair<JsValue, JsValue>(kNumber, kValue);
                }
            }
        }

        return new KeyValuePair<JsValue, JsValue>(JsNumber.IntegerNegativeOne, Undefined);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.foreach
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue ForEach(JsValue thisObject, JsValue callbackFn, JsValue thisArg)
    {
        var callbackfn = GetCallable(callbackFn);

        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o;
        for (var k = 0; k < len; k++)
        {
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var kValue = o[k];
            args[0] = kValue;
            args[1] = k;
            callbackfn.Call(thisArg, args);
        }

        _engine._jsValueArrayPool.ReturnArray(args);

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.includes
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Includes(JsValue thisObject, JsValue searchElement, JsValue fromIndex)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        if (len == 0)
        {
            return false;
        }

        // Per spec: when fromIndex is undefined treat n as 0 (skip ToIntegerOrInfinity coercion).
        var n = fromIndex.IsUndefined() ? 0 : TypeConverter.ToIntegerOrInfinity(fromIndex);
        if (double.IsPositiveInfinity(n))
        {
            return JsBoolean.False;
        }
        else if (double.IsNegativeInfinity(n))
        {
            n = 0;
        }

        long k;
        if (n >= 0)
        {
            k = (long) n;
        }
        else
        {
            k = (long) (len + n);
            if (k < 0)
            {
                k = 0;
            }
        }

        if (TryFastIntegerSearch(o, searchElement, (int) k, (int) len, last: false, out var fastIndex))
        {
            return fastIndex >= 0 ? JsBoolean.True : JsBoolean.False;
        }

        while (k < len)
        {
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var value = o[(int) k];
            if (SameValueZeroComparer.Equals(value, searchElement))
            {
                return JsBoolean.True;
            }

            k++;
        }

        return JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.indexof
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue IndexOf(JsValue thisObject, JsValue searchElement, JsValue fromIndex)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        if (len == 0)
        {
            return JsNumber.IntegerNegativeOne;
        }

        var n = TypeConverter.ToIntegerOrInfinity(fromIndex);
        if (double.IsPositiveInfinity(n))
        {
            return JsNumber.IntegerNegativeOne;
        }
        else if (double.IsNegativeInfinity(n))
        {
            n = 0;
        }

        long k;
        if (n >= 0)
        {
            k = (long) n;
        }
        else
        {
            k = (long) (len + n);
            if (k < 0)
            {
                k = 0;
            }
        }

        if (TryFastIntegerSearch(o, searchElement, (int) k, (int) len, last: false, out var fastIndex))
        {
            return fastIndex < 0 ? JsNumber.IntegerNegativeOne : JsNumber.Create(fastIndex);
        }

        for (; k < len; k++)
        {
            if (k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var kPresent = o.HasProperty(k);
            if (kPresent)
            {
                var elementK = o[(int) k];
                if (elementK == searchElement)
                {
                    return k;
                }
            }
        }

        return JsNumber.IntegerNegativeOne;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.join
    /// </summary>
    [JsFunction]
    private JsValue Join(JsValue thisObject, JsValue separator)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var sep = TypeConverter.ToString(separator.IsUndefined() ? JsString.CommaString : separator);
        // as per the spec, this has to be called after ToString(separator)
        if (len == 0)
        {
            return JsString.Empty;
        }

        static string StringFromJsValue(JsValue value)
        {
            return value.IsUndefined()
                ? ""
                : TypeConverter.ToString(value);
        }

        var s = StringFromJsValue(o[0]);
        if (len == 1)
        {
            return s;
        }

        using var result = new ValueStringBuilder();
        result.Append(s);
        for (var k = 1; k < len; k++)
        {
            if (k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            result.Append(sep);
            result.Append(StringFromJsValue(o[k]));
        }

        return result.ToString();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.keys
    /// </summary>
    [JsFunction]
    private JsValue Keys(JsValue thisObject)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.Key);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.lastindexof
    /// </summary>
    // Keeps JsCallArguments because the spec distinguishes "fromIndex is present" from
    // "fromIndex is absent": a present-but-undefined value coerces via ToIntegerOrInfinity (→ NaN),
    // an absent value defaults to len - 1. Only arguments.Length can tell them apart.
    [JsFunction(Length = 1)]
    private JsValue LastIndexOf(JsValue thisObject, JsCallArguments arguments)
    {
        var searchElement = arguments.At(0);

        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        if (len == 0)
        {
            return JsNumber.IntegerNegativeOne;
        }

        var fromIndex = arguments.At(1, len - 1);
        var n = TypeConverter.ToIntegerOrInfinity(fromIndex);

        if (double.IsNegativeInfinity(n))
        {
            return JsNumber.IntegerNegativeOne;
        }

        long k;
        if (n >= 0)
        {
            k = (long) System.Math.Min(n, len - 1);
        }
        else
        {
            k = (long) (len + n);
        }

        if (TryFastIntegerSearch(o, searchElement, (int) k, (int) len, last: true, out var fastIndex))
        {
            return fastIndex < 0 ? JsNumber.IntegerNegativeOne : JsNumber.Create(fastIndex);
        }

        for (; k >= 0; k--)
        {
            if (k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var kPresent = o.HasProperty(k);
            if (kPresent)
            {
                var elementK = o[(int) k];
                if (elementK == searchElement)
                {
                    return k;
                }
            }
        }

        return JsNumber.IntegerNegativeOne;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.map
    /// </summary>
    [JsFunction(Length = 1)]
    private ObjectInstance Map(JsValue thisObject, JsValue callbackFn, JsValue thisArg)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var callable = GetCallable(callbackFn);

        var a = _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, [len]);
        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o;
        for (var k = 0; k < len; k++)
        {
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            args[0] = o[k];
            args[1] = k;
            var mappedValue = callable.Call(thisArg, args);
            a[k] = mappedValue;
        }

        _engine._jsValueArrayPool.ReturnArray(args);
        return a;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.reduce
    /// </summary>
    // Keeps JsCallArguments because spec step 5 distinguishes "initialValue is present" from
    // "missing" — that distinction is only available via arguments.Length.
    [JsFunction(Length = 1)]
    private JsValue Reduce(JsValue thisObject, JsCallArguments arguments)
    {
        var callbackfn = GetCallable(arguments.At(0));
        var initialValue = arguments.At(1);

        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        if (len == 0 && arguments.Length < 2)
        {
            Throw.TypeError(_realm, "Reduce of empty array with no initial value");
        }

        var k = 0;
        var accumulator = Undefined;
        if (!initialValue.IsUndefined())
        {
            accumulator = initialValue;
        }
        else
        {
            accumulator = o[k];
            k++;
        }

        var args = _engine._jsValueArrayPool.RentArray(4);
        args[3] = o;
        while (k < len)
        {
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var kValue = o[k];
            args[0] = accumulator;
            args[1] = kValue;
            args[2] = k;
            accumulator = callbackfn.Call(Undefined, args);
            k++;
        }

        _engine._jsValueArrayPool.ReturnArray(args);

        return accumulator;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.reduceright
    /// </summary>
    // Keeps JsCallArguments — same arity-distinction reasoning as Reduce above.
    [JsFunction(Length = 1)]
    private JsValue ReduceRight(JsValue thisObject, JsCallArguments arguments)
    {
        var callbackfn = GetCallable(arguments.At(0));
        var initialValue = arguments.At(1);

        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        if (len == 0 && arguments.Length < 2)
        {
            Throw.TypeError(_realm, "Reduce of empty array with no initial value");
        }

        var k = (long) len - 1;
        JsValue accumulator;
        if (arguments.Length > 1)
        {
            accumulator = initialValue;
        }
        else
        {
            accumulator = o[k];
            k--;
        }

        var jsValues = _engine._jsValueArrayPool.RentArray(4);
        jsValues[3] = o;
        for (; k >= 0; k--)
        {
            if (k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            jsValues[0] = accumulator;
            jsValues[1] = o[(int) k];
            jsValues[2] = k;
            accumulator = callbackfn.Call(Undefined, jsValues);
        }

        _engine._jsValueArrayPool.ReturnArray(jsValues);
        return accumulator;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.reverse
    /// </summary>
    [JsFunction]
    private ObjectInstance Reverse(JsValue thisObject)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var buffer = o._viewedArrayBuffer;
        buffer.AssertNotImmutable();

        // Reverse the element order directly in the backing buffer. Reinterpreting the bytes as a span of the
        // element-sized integer and using the vectorized Span<T>.Reverse swaps whole element blocks by position,
        // which preserves each element's bit-level encoding (endian-agnostic) and avoids JsValue allocation.
        _engine.Constraints.Check();
        ReverseElements(buffer._arrayBufferData!, o._byteOffset, (int) len, o._arrayElementType.GetElementSize());

        return o;
    }

    /// <summary>
    /// Reverses <paramref name="len"/> element-sized blocks in place starting at <paramref name="byteOffset"/>.
    /// Endian-agnostic: whole element blocks are swapped by position, so each element's bytes are preserved.
    /// </summary>
    private static void ReverseElements(byte[] data, int byteOffset, int len, int elementSize)
    {
        switch (elementSize)
        {
            case 1:
                data.AsSpan(byteOffset, len).Reverse();
                break;
            case 2:
                MemoryMarshal.Cast<byte, short>(data.AsSpan(byteOffset, len * 2)).Reverse();
                break;
            case 4:
                MemoryMarshal.Cast<byte, int>(data.AsSpan(byteOffset, len * 4)).Reverse();
                break;
            case 8:
                MemoryMarshal.Cast<byte, long>(data.AsSpan(byteOffset, len * 8)).Reverse();
                break;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.set
    /// </summary>
    [JsFunction(Name = "set", Length = 1)]
    private JsValue SetTypedArray(JsValue thisObject, JsValue source, JsValue offset)
    {
        var target = thisObject as JsTypedArray;
        if (target is null)
        {
            Throw.TypeError(_realm, $"Method TypedArray.prototype.set called on incompatible receiver {thisObject}");
        }

        var targetOffset = TypeConverter.ToIntegerOrInfinity(offset);
        if (targetOffset < 0)
        {
            Throw.RangeError(_realm, "offset is out of bounds");
        }

        if (source is JsTypedArray typedArrayInstance)
        {
            SetTypedArrayFromTypedArray(target, targetOffset, typedArrayInstance);
        }
        else
        {
            SetTypedArrayFromArrayLike(target, (int) targetOffset, source);
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-settypedarrayfromtypedarray
    /// </summary>
    private void SetTypedArrayFromTypedArray(JsTypedArray target, double targetOffset, JsTypedArray source)
    {
        var targetBuffer = target._viewedArrayBuffer;
        var targetRecord = MakeTypedArrayWithBufferWitnessRecord(target, ArrayBufferOrder.SeqCst);
        if (targetRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(_realm, "Target TypedArray is out of bounds");
        }

        var targetLength = targetRecord.TypedArrayLength;

        var srcBuffer = source._viewedArrayBuffer;
        var srcRecord = MakeTypedArrayWithBufferWitnessRecord(source, ArrayBufferOrder.SeqCst);
        if (srcRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(_realm, "Source TypedArray is out of bounds");
        }

        var targetType = target._arrayElementType;
        var targetElementSize = targetType.GetElementSize();
        var targetByteOffset = target._byteOffset;

        var srcType = source._arrayElementType;
        var srcElementSize = srcType.GetElementSize();
        var srcLength = srcRecord.TypedArrayLength;
        var srcByteOffset = source._byteOffset;

        if (double.IsNegativeInfinity(targetOffset))
        {
            Throw.RangeError(_realm, "offset is out of bounds");
        }

        if (srcLength + targetOffset > targetLength)
        {
            Throw.RangeError(_realm, "offset is out of bounds");
        }

        if (target._contentType != source._contentType)
        {
            Throw.TypeError(_realm, "Cannot mix BigInt and other types, use explicit conversions");
        }

        var targetByteIndex = (int) (targetOffset * targetElementSize + targetByteOffset);

        if (srcType == targetType)
        {
            // NOTE: If srcType and targetType are the same, the transfer must be performed in a manner that
            // preserves the bit-level encoding of the source data, i.e. it is a plain byte copy.
            // System.Array.Copy has memmove semantics, so it copies correctly even when the source and target
            // share the same buffer (overlapping regions). The spec clones the source buffer first only to avoid
            // observable side-effects of a forward element-by-element copy; a byte move has no such side-effects,
            // so the clone is unnecessary here.
            targetBuffer.AssertNotImmutable();
            _engine.Constraints.Check();
            System.Array.Copy(srcBuffer._arrayBufferData!, srcByteOffset, targetBuffer._arrayBufferData!, targetByteIndex, targetElementSize * srcLength);
        }
        else
        {
            int srcByteIndex;
            if (SameValue(srcBuffer, targetBuffer))
            {
                // The source and target share a buffer but use different element sizes, so an in-place
                // overlapping read/write could corrupt not-yet-read source bytes. Clone the source region first.
                // %ArrayBuffer% is used to clone srcBuffer because it is known to not have any observable side-effects.
                var srcByteLength = srcRecord.TypedArrayByteLength;
                srcBuffer = srcBuffer.CloneArrayBuffer(_realm.Intrinsics.ArrayBuffer, srcByteOffset, srcByteLength);
                srcByteIndex = 0;
            }
            else
            {
                srcByteIndex = srcByteOffset;
            }

            var limit = targetByteIndex + targetElementSize * srcLength;
            var processed = 0;
            while (targetByteIndex < limit)
            {
                var value = srcBuffer.GetValueFromBuffer(srcByteIndex, srcType, isTypedArray: true, ArrayBufferOrder.Unordered);
                targetBuffer.SetValueInBuffer(targetByteIndex, targetType, value, isTypedArray: true, ArrayBufferOrder.Unordered);
                srcByteIndex += srcElementSize;
                targetByteIndex += targetElementSize;

                if (++processed % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-settypedarrayfromarraylike
    /// </summary>
    private void SetTypedArrayFromArrayLike(JsTypedArray target, int targetOffset, JsValue source)
    {
        var targetBuffer = target._viewedArrayBuffer;
        targetBuffer.AssertNotDetached();

        var targetRecord = MakeTypedArrayWithBufferWitnessRecord(target, ArrayBufferOrder.SeqCst);
        if (targetRecord.IsTypedArrayOutOfBounds)
        {
            Throw.TypeError(_realm, "Target TypedArray is out of bounds");
        }

        var targetLength = targetRecord.TypedArrayLength;
        var src = ArrayOperations.For(_realm, source, forWrite: false);
        var srcLength = src.GetLength();

        if (double.IsNegativeInfinity(targetOffset))
        {
            Throw.RangeError(_realm, "offset is out of bounds");
        }

        if (srcLength + targetOffset > targetLength)
        {
            Throw.RangeError(_realm, "offset is out of bounds");
        }

        var k = 0;
        while (k < srcLength)
        {
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var jsValue = src.Get((ulong) k);
            target.IntegerIndexedElementSet(targetOffset + k, jsValue);
            k++;
        }
    }

    /// <summary>
    /// https://tc39.es/proposal-relative-indexing-method/#sec-%typedarray.prototype%-additions
    /// </summary>
    [JsFunction]
    private JsValue At(JsValue thisObject, JsValue start)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var relativeStart = TypeConverter.ToInteger(start);
        int k;

        if (relativeStart < 0)
        {
            k = (int) (len + relativeStart);
        }
        else
        {
            k = (int) relativeStart;
        }

        if (k < 0 || k >= len)
        {
            return Undefined;
        }

        return o.Get(k);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.slice
    /// </summary>
    [JsFunction]
    private JsValue Slice(JsValue thisObject, JsValue start, JsValue end)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);
        int startIndex;
        if (double.IsNegativeInfinity(relativeStart))
        {
            startIndex = 0;
        }
        else if (relativeStart < 0)
        {
            startIndex = (int) System.Math.Max(len + relativeStart, 0);
        }
        else
        {
            startIndex = (int) System.Math.Min(relativeStart, len);
        }

        var relativeEnd = end.IsUndefined()
            ? len
            : TypeConverter.ToIntegerOrInfinity(end);

        long endIndex;
        if (double.IsNegativeInfinity(relativeEnd))
        {
            endIndex = 0;
        }
        else if (relativeEnd < 0)
        {
            endIndex = (long) System.Math.Max(len + relativeEnd, 0);
        }
        else
        {
            endIndex = (long) System.Math.Min(relativeEnd, len);
        }

        var countBytes = System.Math.Max(endIndex - startIndex, 0);
        var a = _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, [countBytes]);

        if (countBytes > 0)
        {
            taRecord = MakeTypedArrayWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);
            if (taRecord.IsTypedArrayOutOfBounds)
            {
                Throw.TypeError(_realm, "TypedArray is out of bounds");
            }

            endIndex = System.Math.Min(endIndex, taRecord.TypedArrayLength);
            countBytes = System.Math.Max(endIndex - startIndex, 0);
            var srcType = o._arrayElementType;
            var targetType = a._arrayElementType;
            if (srcType != targetType)
            {
                var n = 0;
                while (startIndex < endIndex)
                {
                    if (n > 0 && n % ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    var kValue = o[startIndex];
                    a[n] = kValue;
                    startIndex++;
                    n++;
                }
            }
            else if (countBytes > 0)
            {
                // Same element type: a plain byte copy preserving the bit-level encoding.
                var srcBuffer = o._viewedArrayBuffer;
                var targetBuffer = a._viewedArrayBuffer;
                var elementSize = srcType.GetElementSize();
                var srcByteIndex = startIndex * elementSize + o._byteOffset;
                var targetByteIndex = a._byteOffset;
                var byteCount = (int) countBytes * elementSize;

                _engine.Constraints.Check();

                if (SameValue(srcBuffer, targetBuffer))
                {
                    // A @@species constructor returned a target that aliases the source buffer. The spec performs a
                    // forward byte-by-byte copy, which "smears" the source when the target region overlaps ahead of
                    // it; System.Array.Copy (memmove) would not reproduce that, so copy forward explicitly here.
                    var data = srcBuffer._arrayBufferData!;
                    for (var i = 0; i < byteCount; i++)
                    {
                        data[targetByteIndex + i] = data[srcByteIndex + i];

                        if ((i + 1) % ConstraintCheckInterval == 0)
                        {
                            _engine.Constraints.Check();
                        }
                    }
                }
                else
                {
                    System.Array.Copy(srcBuffer._arrayBufferData!, srcByteIndex, targetBuffer._arrayBufferData!, targetByteIndex, byteCount);
                }
            }
        }

        return a;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.some
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Some(JsValue thisObject, JsValue callbackFn, JsValue thisArg)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var callbackfn = GetCallable(callbackFn);

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = o;
        for (var k = 0; k < len; k++)
        {
            if (k > 0 && k % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            args[0] = o[k];
            args[1] = k;
            if (TypeConverter.ToBoolean(callbackfn.Call(thisArg, args)))
            {
                return JsBoolean.True;
            }
        }

        _engine._jsValueArrayPool.ReturnArray(args);
        return JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.sort
    /// </summary>
    [JsFunction]
    private JsValue Sort(JsValue thisObject, JsValue compareFnArg)
    {
        /*
         * %TypedArray%.prototype.sort is a distinct function that, except as described below,
         * implements the same requirements as those of Array.prototype.sort as defined in 23.1.3.27.
         * The implementation of the %TypedArray%.prototype.sort specification may be optimized with the knowledge that the this value is
         * an object that has a fixed length and whose integer-indexed properties are not sparse.
         */

        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var buffer = o._viewedArrayBuffer;

        var compareFn = GetCompareFunction(compareFnArg);

        if (len <= 1)
        {
            return o;
        }

        var array = SortArray(buffer, compareFn, o);

        for (var i = 0; i < (uint) array.Length; ++i)
        {
            if (i > 0 && i % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            o[i] = array[i];
        }

        return o;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.subarray
    /// </summary>
    [JsFunction]
    private JsValue Subarray(JsValue thisObject, JsValue start, JsValue end)
    {
        var o = thisObject as JsTypedArray;
        if (o is null)
        {
            Throw.TypeError(_realm, $"Method TypedArray.prototype.subarray called on incompatible receiver {thisObject}");
        }

        var buffer = o._viewedArrayBuffer;
        var srcRecord = MakeTypedArrayWithBufferWitnessRecord(o, ArrayBufferOrder.SeqCst);

        uint srcLength = 0;
        if (!srcRecord.IsTypedArrayOutOfBounds)
        {
            srcLength = srcRecord.TypedArrayLength;
        }

        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);

        double startIndex;
        if (double.IsNegativeInfinity(relativeStart))
        {
            startIndex = 0;
        }
        else if (relativeStart < 0)
        {
            startIndex = System.Math.Max(srcLength + relativeStart, 0);
        }
        else
        {
            startIndex = System.Math.Min(relativeStart, srcLength);
        }

        var elementSize = o._arrayElementType.GetElementSize();
        var srcByteOffset = o._byteOffset;
        var beginByteOffset = srcByteOffset + startIndex * elementSize;

        JsCallArguments argumentsList;
        if (o._arrayLength == JsTypedArray.LengthAuto && end.IsUndefined())
        {
            argumentsList = [buffer, beginByteOffset];
        }
        else
        {
            double relativeEnd;
            if (end.IsUndefined())
            {
                relativeEnd = srcLength;
            }
            else
            {
                relativeEnd = TypeConverter.ToIntegerOrInfinity(end);
            }

            double endIndex;
            if (double.IsNegativeInfinity(relativeEnd))
            {
                endIndex = 0;
            }
            else if (relativeEnd < 0)
            {
                endIndex = System.Math.Max(srcLength + relativeEnd, 0);
            }
            else
            {
                endIndex = System.Math.Min(relativeEnd, srcLength);
            }

            var newLength = System.Math.Max(endIndex - startIndex, 0);
            argumentsList = [buffer, beginByteOffset, newLength];
        }

        return _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, argumentsList);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.tolocalestring
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue ToLocaleString(JsValue thisObject, JsValue locales, JsValue options)
    {
        /*
         * %TypedArray%.prototype.toLocaleString is a distinct function that implements the same algorithm as Array.prototype.toLocaleString
         * as defined in 23.1.3.29 except that the this value's [[ArrayLength]] internal slot is accessed in place of performing
         * a [[Get]] of "length". The implementation of the algorithm may be optimized with the knowledge that the this value is an object
         * that has a fixed length and whose integer-indexed properties are not sparse. However, such optimization must not introduce
         * any observable changes in the specified behaviour of the algorithm.
         */

        const string Separator = ",";

        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var array = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        if (len == 0)
        {
            return JsString.Empty;
        }

        // Per ECMA-402, pass locales and options to element's toLocaleString
        var invokeArgs = !locales.IsUndefined() || !options.IsUndefined()
            ? new[] { locales, options }
            : System.Array.Empty<JsValue>();

        using var r = new ValueStringBuilder();
        for (uint k = 0; k < len; k++)
        {
            if (k > 0)
            {
                if (k % ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                r.Append(Separator);
            }
            if (array.TryGetValue(k, out var nextElement) && !nextElement.IsNullOrUndefined())
            {
                var s = TypeConverter.ToString(Invoke(nextElement, "toLocaleString", invokeArgs));
                r.Append(s);
            }
        }

        return r.ToString();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.values
    /// </summary>
    [JsFunction]
    private JsValue Values(JsValue thisObject)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.Value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype-@@tostringtag
    /// </summary>
    [JsSymbolAccessor("ToStringTag")]
    private static JsValue ToStringTag(JsValue thisObject)
    {
        if (thisObject is not JsTypedArray o)
        {
            return Undefined;
        }

        return o._arrayElementType.GetTypedArrayName();
    }

    [JsFunction]
    private JsValue ToReversed(JsValue thisObject)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var a = TypedArrayCreateSameType(o, [JsNumber.Create(len)]);

        // The result has the same element type, so bulk-copy the source bytes into the fresh target and then
        // reverse the element order in place, avoiding any per-element JsValue decode/encode.
        var elementSize = o._arrayElementType.GetElementSize();
        var dstData = a._viewedArrayBuffer._arrayBufferData!;
        var dstByteOffset = a._byteOffset;

        _engine.Constraints.Check();
        System.Array.Copy(o._viewedArrayBuffer._arrayBufferData!, o._byteOffset, dstData, dstByteOffset, (long) len * elementSize);
        ReverseElements(dstData, dstByteOffset, (int) len, elementSize);

        return a;
    }

    [JsFunction]
    private JsValue ToSorted(JsValue thisObject, JsValue compareFnArg)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var compareFn = GetCompareFunction(compareFnArg);

        var buffer = o._viewedArrayBuffer;

        var a = TypedArrayCreateSameType(o, [JsNumber.Create(len)]);

        var array = SortArray(buffer, compareFn, o);
        for (var i = 0; (uint) i < (uint) array.Length; ++i)
        {
            if (i > 0 && i % ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            a[i] = array[i];
        }

        return a;
    }

    [JsFunction]
    private ObjectInstance With(JsValue thisObject, JsValue indexArg, JsValue value)
    {
        var taRecord = thisObject.ValidateTypedArray(_realm, ArrayBufferOrder.SeqCst);
        var o = taRecord.Object;
        var len = taRecord.TypedArrayLength;

        var relativeIndex = TypeConverter.ToIntegerOrInfinity(indexArg);

        long actualIndex;
        if (relativeIndex >= 0)
        {
            actualIndex = (long) relativeIndex;
        }
        else
        {
            actualIndex = (long) (len + relativeIndex);
        }

        value = o._contentType == TypedArrayContentType.BigInt
            ? TypeConverter.ToJsBigInt(value)
            : TypeConverter.ToJsNumber(value);

        if (!o.IsValidIntegerIndex(actualIndex))
        {
            Throw.RangeError(_realm, "Invalid typed array index");
        }

        var a = TypedArrayCreateSameType(o, [JsNumber.Create(len)]);

        // The result has the same element type, so bulk-copy the source bytes into the freshly created target
        // and then overwrite the single replaced element (whose value is already coerced above).
        var elementSize = o._arrayElementType.GetElementSize();
        _engine.Constraints.Check();
        System.Array.Copy(o._viewedArrayBuffer._arrayBufferData!, o._byteOffset, a._viewedArrayBuffer._arrayBufferData!, a._byteOffset, len * elementSize);
        a[(int) actualIndex] = value;

        return a;
    }

    private JsTypedArray TypedArrayCreateSameType(JsTypedArray exemplar, JsValue[] argumentList)
    {
        var constructor = exemplar._arrayElementType.GetConstructor(_realm.Intrinsics);
        var result = IntrinsicTypedArrayConstructor.TypedArrayCreate(_realm, constructor, argumentList);
        return result;
    }

    /// <summary>
    /// Vectorized search fast path used by indexOf/lastIndexOf/includes. It applies only when the element type is a
    /// non-BigInt integer type, the platform is little-endian, and <paramref name="searchElement"/> is a Number that
    /// is exactly representable in that element type. Under those conditions strict equality and SameValueZero both
    /// reduce to plain integer equality (no NaN/-0 distinctions), so the vectorized span IndexOf/LastIndexOf
    /// yields the correct result. A search value that is not exactly representable cannot equal any
    /// stored element, so the result is "not found". Returns <c>false</c> to signal the caller to fall back to the
    /// generic element loop (floats, BigInt, big-endian, or a non-Number search element).
    /// </summary>
    private static bool TryFastIntegerSearch(JsTypedArray o, JsValue searchElement, int fromInclusive, int len, bool last, out int index)
    {
        index = -1;

        var type = o._arrayElementType;
        if (!BitConverter.IsLittleEndian
            || searchElement is not JsNumber number
            || type.IsBigIntElementType()
            || type is TypedArrayElementType.Float16 or TypedArrayElementType.Float32 or TypedArrayElementType.Float64)
        {
            return false;
        }

        var d = number._value;
        var byteOffset = o._byteOffset;
        var elementSize = type.GetElementSize();
        var data = o._viewedArrayBuffer._arrayBufferData;

        // The caller coerces fromIndex/searchElement before reaching here, which can run user code that detaches or
        // shrinks the buffer. If the buffer is gone or the captured length no longer fits, fall back to the generic
        // per-element loop, which validates each index against the array's current length.
        if (data is null || (long) byteOffset + (long) len * elementSize > data.Length)
        {
            return false;
        }

        switch (type)
        {
            case TypedArrayElementType.Int8:
                if ((double) (sbyte) d == d)
                {
                    index = SearchSpan(MemoryMarshal.Cast<byte, sbyte>(data.AsSpan(byteOffset, len)), (sbyte) d, fromInclusive, last);
                }
                return true;
            case TypedArrayElementType.Uint8:
            case TypedArrayElementType.Uint8C:
                if ((double) (byte) d == d)
                {
                    index = SearchSpan(data.AsSpan(byteOffset, len), (byte) d, fromInclusive, last);
                }
                return true;
            case TypedArrayElementType.Int16:
                if ((double) (short) d == d)
                {
                    index = SearchSpan(MemoryMarshal.Cast<byte, short>(data.AsSpan(byteOffset, len * 2)), (short) d, fromInclusive, last);
                }
                return true;
            case TypedArrayElementType.Uint16:
                if ((double) (ushort) d == d)
                {
                    index = SearchSpan(MemoryMarshal.Cast<byte, ushort>(data.AsSpan(byteOffset, len * 2)), (ushort) d, fromInclusive, last);
                }
                return true;
            case TypedArrayElementType.Int32:
                if ((double) (int) d == d)
                {
                    index = SearchSpan(MemoryMarshal.Cast<byte, int>(data.AsSpan(byteOffset, len * 4)), (int) d, fromInclusive, last);
                }
                return true;
            case TypedArrayElementType.Uint32:
                if ((double) (uint) d == d)
                {
                    index = SearchSpan(MemoryMarshal.Cast<byte, uint>(data.AsSpan(byteOffset, len * 4)), (uint) d, fromInclusive, last);
                }
                return true;
            default:
                return false;
        }
    }

    private static int SearchSpan<T>(Span<T> span, T value, int fromInclusive, bool last) where T : IEquatable<T>
    {
        if (last)
        {
            // backward search over [0, fromInclusive]
            if (fromInclusive < 0)
            {
                return -1;
            }

            if (fromInclusive >= span.Length)
            {
                fromInclusive = span.Length - 1;
            }

            return span.Slice(0, fromInclusive + 1).LastIndexOf(value);
        }

        // forward search over [fromInclusive, len)
        if (fromInclusive < 0)
        {
            fromInclusive = 0;
        }

        if (fromInclusive >= span.Length)
        {
            return -1;
        }

        var relative = span.Slice(fromInclusive).IndexOf(value);
        return relative < 0 ? -1 : fromInclusive + relative;
    }

    private ICallable? GetCompareFunction(JsValue compareArg)
    {
        ICallable? compareFn = null;
        if (!compareArg.IsUndefined())
        {
            if (compareArg is not ICallable callable)
            {
                Throw.TypeError(_realm, "The comparison function must be either a function or undefined");
                return null;
            }
            compareFn = callable;
        }

        return compareFn;
    }

    private static JsValue[] SortArray(JsArrayBuffer buffer, ICallable? compareFn, JsTypedArray obj)
    {
        var comparer = TypedArrayComparer.WithFunction(buffer, compareFn);
        var operations = ArrayOperations.For(obj, forWrite: false);
        try
        {
            return operations.OrderBy(x => x, comparer).ToArray();
        }
        catch (InvalidOperationException e)
        {
            throw e.InnerException ?? e;
        }
    }

    private sealed class TypedArrayComparer : IComparer<JsValue>
    {
        public static TypedArrayComparer WithFunction(JsArrayBuffer buffer, ICallable? compare)
        {
            return new TypedArrayComparer(buffer, compare);
        }

        private readonly JsArrayBuffer _buffer;
        private readonly ICallable? _compare;
        private readonly JsValue[] _comparableArray = new JsValue[2];

        private TypedArrayComparer(JsArrayBuffer buffer, ICallable? compare)
        {
            _buffer = buffer;
            _compare = compare;
        }

        public int Compare(JsValue? x, JsValue? y)
        {
            if (x is null && y is null)
            {
                return 0;
            }

            if (x is not null && y is null)
            {
                return 1;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            if (_compare is not null)
            {
                _comparableArray[0] = x;
                _comparableArray[1] = y;

                var v = TypeConverter.ToNumber(_compare.Call(Undefined, _comparableArray));

                if (double.IsNaN(v))
                {
                    return 0;
                }

                return (int) v;
            }

            if (x.Type == Types.BigInt || y.Type == Types.BigInt)
            {
                var xBigInt = TypeConverter.ToBigInt(x);
                var yBigInt = TypeConverter.ToBigInt(y);
                return xBigInt.CompareTo(yBigInt);
            }

            var xValue = x.AsNumber();
            var yValue = y.AsNumber();

            if (double.IsNaN(xValue) && double.IsNaN(yValue))
            {
                return 0;
            }

            if (double.IsNaN(xValue))
            {
                return 1;
            }

            if (double.IsNaN(yValue))
            {
                return -1;
            }

            if (xValue < yValue)
            {
                return -1;
            }

            if (xValue > yValue)
            {
                return 1;
            }

            if (NumberInstance.IsNegativeZero(xValue) && yValue == 0)
            {
                return -1;
            }

            if (xValue == 0 && NumberInstance.IsNegativeZero(yValue))
            {
                return 1;
            }

            return 0;
        }
    }
}
