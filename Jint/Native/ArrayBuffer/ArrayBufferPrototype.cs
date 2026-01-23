#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.ArrayBuffer;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-arraybuffer-prototype-object
/// </summary>
internal sealed class ArrayBufferPrototype : Prototype
{
    private readonly ArrayBufferConstructor _constructor;

    internal ArrayBufferPrototype(
        Engine engine,
        ArrayBufferConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, engine.Realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["byteLength"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get byteLength", ByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            [KnownKeys.Constructor] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["detached"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get detached", Detached, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["immutable"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get immutable", Immutable, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["maxByteLength"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get maxByteLength", MaxByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["resizable"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get resizable", Resizable, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["resize"] = new PropertyDescriptor(new ClrFunction(_engine, "resize", Resize, 1, lengthFlags), PropertyFlag.NonEnumerable),
            ["slice"] = new PropertyDescriptor(new ClrFunction(_engine, "slice", Slice, 2, lengthFlags), PropertyFlag.NonEnumerable),
            ["sliceToImmutable"] = new PropertyDescriptor(new ClrFunction(_engine, "sliceToImmutable", SliceToImmutable, 2, lengthFlags), PropertyFlag.NonEnumerable),
            ["transfer"] = new PropertyDescriptor(new ClrFunction(_engine, "transfer", Transfer, 0, lengthFlags), PropertyFlag.NonEnumerable),
            ["transferToFixedLength"] = new PropertyDescriptor(new ClrFunction(_engine, "transferToFixedLength", TransferToFixedLength, 0, lengthFlags), PropertyFlag.NonEnumerable),
            ["transferToImmutable"] = new PropertyDescriptor(new ClrFunction(_engine, "transferToImmutable", TransferToImmutable, 0, lengthFlags), PropertyFlag.NonEnumerable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1) { [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("ArrayBuffer", PropertyFlag.Configurable) };
        SetSymbols(symbols);
    }

    private JsValue Detached(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.detached called on incompatible receiver " + thisObject);
        }

        return o.IsDetachedBuffer;
    }

    /// <summary>
    /// https://tc39.es/proposal-immutable-arraybuffer/#sec-get-arraybuffer.prototype.immutable
    /// </summary>
    private JsValue Immutable(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.immutable called on incompatible receiver " + thisObject);
        }

        return o.IsImmutableBuffer;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-arraybuffer.prototype.maxbytelength
    /// </summary>
    private JsValue MaxByteLength(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.maxByteLength called on incompatible receiver " + thisObject);
        }

        if (o.IsDetachedBuffer)
        {
            return JsNumber.PositiveZero;
        }

        long length = o.IsFixedLengthArrayBuffer
            ? o.ArrayBufferByteLength
            : o._arrayBufferMaxByteLength.GetValueOrDefault();

        return length;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-arraybuffer.prototype.resizable
    /// </summary>
    private JsValue Resizable(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.resizable called on incompatible receiver " + thisObject);
        }

        return !o.IsFixedLengthArrayBuffer;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraybuffer.prototype.resize
    /// https://tc39.es/proposal-immutable-arraybuffer/#sec-arraybuffer.prototype.resize
    /// </summary>
    private JsValue Resize(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.resize called on incompatible receiver " + thisObject);
        }

        // Step 2: Perform ? RequireInternalSlot(O, [[ArrayBufferMaxByteLength]]).
        // This check must happen before reading newLength
        if (o.IsFixedLengthArrayBuffer)
        {
            Throw.TypeError(_realm, "Cannot resize a fixed-length ArrayBuffer");
        }

        var newLength = arguments.At(0);
        var newByteLength = TypeConverter.ToIndex(_realm, newLength);

        o.AssertNotDetached();

        o.Resize(newByteLength);

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-arraybuffer.prototype.bytelength
    /// </summary>
    private JsValue ByteLength(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.byteLength called on incompatible receiver " + thisObject);
        }

        if (o.IsDetachedBuffer)
        {
            return JsNumber.PositiveZero;
        }

        return JsNumber.Create(o.ArrayBufferByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraybuffer.prototype.slice
    /// </summary>
    private JsValue Slice(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.slice called on incompatible receiver " + thisObject);
        }

        o.AssertNotDetached();

        var start = arguments.At(0);
        var end = arguments.At(1);

        var len = o.ArrayBufferByteLength;
        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);
        var first = relativeStart switch
        {
            double.NegativeInfinity => 0,
            < 0 => (int) System.Math.Max(len + relativeStart, 0),
            _ => (int) System.Math.Min(relativeStart, len)
        };

        double relativeEnd;
        if (end.IsUndefined())
        {
            relativeEnd = len;
        }
        else
        {
            relativeEnd = TypeConverter.ToIntegerOrInfinity(end);
        }

        var final = relativeEnd switch
        {
            double.NegativeInfinity => 0,
            < 0 => (int) System.Math.Max(len + relativeEnd, 0),
            _ => (int) System.Math.Min(relativeEnd, len)
        };

        var newLen = System.Math.Max(final - first, 0);
        var ctor = SpeciesConstructor(o, _realm.Intrinsics.ArrayBuffer);
        var bufferInstance = Construct(ctor, [JsNumber.Create(newLen)]) as JsArrayBuffer;

        if (bufferInstance is null)
        {
            Throw.TypeError(_realm);
        }

        if (bufferInstance.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm);
        }

        if (bufferInstance.IsDetachedBuffer)
        {
            Throw.TypeError(_realm);
        }

        if (ReferenceEquals(bufferInstance, o))
        {
            Throw.TypeError(_realm);
        }

        if (bufferInstance.ArrayBufferByteLength < newLen)
        {
            Throw.TypeError(_realm);
        }

        // https://tc39.es/proposal-immutable-arraybuffer/#sec-arraybuffer.prototype.slice
        // If IsImmutableBuffer(new) is true, throw a TypeError exception.
        if (bufferInstance.IsImmutableBuffer)
        {
            Throw.TypeError(_realm, "Cannot use an immutable ArrayBuffer as species constructor result");
        }

        // NOTE: Side-effects of the above steps may have detached O.

        if (o.IsDetachedBuffer)
        {
            Throw.TypeError(_realm);
        }

        var fromBuf = o.ArrayBufferData;
        var toBuf = bufferInstance.ArrayBufferData;
        System.Array.Copy(fromBuf!, first, toBuf!, 0, newLen);
        return bufferInstance;
    }

    /// <summary>
    /// https://tc39.es/proposal-arraybuffer-transfer/#sec-arraybuffer.prototype.transfer
    /// </summary>
    private JsValue Transfer(JsValue thisObject, JsCallArguments arguments)
    {
        return ArrayBufferCopyAndDetach(thisObject, arguments.At(0), PreserveResizability.PreserveResizability);
    }

    /// <summary>
    /// https://tc39.es/proposal-arraybuffer-transfer/#sec-arraybuffer.prototype.transfertofixedlength
    /// </summary>
    private JsValue TransferToFixedLength(JsValue thisObject, JsCallArguments arguments)
    {
        return ArrayBufferCopyAndDetach(thisObject, arguments.At(0), PreserveResizability.FixedLength);
    }

    /// <summary>
    /// https://tc39.es/proposal-immutable-arraybuffer/#sec-arraybuffer.prototype.transfertoimmutable
    /// </summary>
    private JsValue TransferToImmutable(JsValue thisObject, JsCallArguments arguments)
    {
        // 1. Let O be the this value.
        // 2. Return ? ArrayBufferCopyAndDetach(O, newLength, immutable).
        return ArrayBufferCopyAndDetach(thisObject, arguments.At(0), PreserveResizability.Immutable);
    }

    /// <summary>
    /// https://tc39.es/proposal-immutable-arraybuffer/#sec-arraybuffer.prototype.slicetoimmutable
    /// </summary>
    private JsValue SliceToImmutable(JsValue thisObject, JsCallArguments arguments)
    {
        // 1. Let O be the this value.
        var o = thisObject as JsArrayBuffer;
        if (o is null || o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.sliceToImmutable called on incompatible receiver " + thisObject);
        }

        // 2. Perform ? RequireInternalSlot(O, [[ArrayBufferData]]).
        // 3. If IsSharedArrayBuffer(O) is true, throw a TypeError exception.
        // (already checked above)

        // 4. If IsDetachedBuffer(O) is true, throw a TypeError exception.
        o.AssertNotDetached();

        var start = arguments.At(0);
        var end = arguments.At(1);

        // 5. Let len be O.[[ArrayBufferByteLength]].
        var len = o.ArrayBufferByteLength;

        // 6. Let relativeStart be ? ToIntegerOrInfinity(start).
        var relativeStart = TypeConverter.ToIntegerOrInfinity(start);

        // 7-8. Set first based on relativeStart
        var first = relativeStart switch
        {
            double.NegativeInfinity => 0,
            < 0 => (int) System.Math.Max(len + relativeStart, 0),
            _ => (int) System.Math.Min(relativeStart, len)
        };

        // 9-10. Set relativeEnd based on end
        double relativeEnd;
        if (end.IsUndefined())
        {
            relativeEnd = len;
        }
        else
        {
            relativeEnd = TypeConverter.ToIntegerOrInfinity(end);
        }

        // 11-12. Set final based on relativeEnd
        var final = relativeEnd switch
        {
            double.NegativeInfinity => 0,
            < 0 => (int) System.Math.Max(len + relativeEnd, 0),
            _ => (int) System.Math.Min(relativeEnd, len)
        };

        // 13. Let newLen be max(final - first, 0).
        var newLen = (uint) System.Math.Max(final - first, 0);

        // 14. Let new be ? AllocateArrayBuffer(%ArrayBuffer%, newLen).
        var newBuffer = _engine.Realm.Intrinsics.ArrayBuffer.AllocateArrayBuffer(_engine.Realm.Intrinsics.ArrayBuffer, newLen);

        // 15. If IsDetachedBuffer(O) is true, throw a TypeError exception.
        o.AssertNotDetached();

        // 16. Let fromBuf be O.[[ArrayBufferData]].
        var fromBuf = o.ArrayBufferData!;

        // 17. Let toBuf be new.[[ArrayBufferData]].
        var toBuf = newBuffer.ArrayBufferData!;

        // 18. Perform CopyDataBlockBytes(toBuf, 0, fromBuf, first, newLen).
        System.Array.Copy(fromBuf, first, toBuf, 0, newLen);

        // 19. Set new.[[ArrayBufferImmutable]] to true.
        newBuffer._isImmutable = true;

        // 20. Return new.
        return newBuffer;
    }

    private JsValue ArrayBufferCopyAndDetach(JsValue o, JsValue newLength, PreserveResizability preserveResizability)
    {
        if (o is not JsArrayBuffer arrayBuffer || arrayBuffer.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method ArrayBuffer.prototype.ArrayBufferCopyAndDetach called on incompatible receiver " + o);
            return Undefined;
        }

        uint newByteLength;
        if (newLength.IsUndefined())
        {
            newByteLength = (uint) arrayBuffer.ArrayBufferByteLength;
        }
        else
        {
            newByteLength = TypeConverter.ToIndex(_realm, newLength);
        }

        arrayBuffer.AssertNotDetached();

        // https://tc39.es/proposal-immutable-arraybuffer/#sec-arraybuffercopyanddetach
        // If IsImmutableBuffer(arrayBuffer) is true, throw a TypeError exception.
        arrayBuffer.AssertNotImmutable();

        uint? newMaxByteLength = null;
        if (preserveResizability == PreserveResizability.PreserveResizability && arrayBuffer._arrayBufferMaxByteLength != null)
        {
            newMaxByteLength = (uint) arrayBuffer._arrayBufferMaxByteLength.Value;
        }

        if (!arrayBuffer._arrayBufferDetachKey.IsUndefined())
        {
            Throw.TypeError(_realm);
        }

        var newBuffer = _engine.Realm.Intrinsics.ArrayBuffer.AllocateArrayBuffer(_engine.Realm.Intrinsics.ArrayBuffer, newByteLength, newMaxByteLength);
        var copyLength = System.Math.Min(newByteLength, arrayBuffer.ArrayBufferByteLength);
        var fromBlock = arrayBuffer.ArrayBufferData!;
        var toBlock = newBuffer.ArrayBufferData!;

        System.Array.Copy(fromBlock, 0, toBlock, 0, copyLength);

        // NOTE: Neither creation of the new Data Block nor copying from the old Data Block are observable. Implementations may implement this method as a zero-copy move or a realloc.

        // If preserveResizability is immutable, set new buffer's immutable flag
        if (preserveResizability == PreserveResizability.Immutable)
        {
            newBuffer._isImmutable = true;
        }

        arrayBuffer.DetachArrayBuffer();

        return newBuffer;
    }

    private enum PreserveResizability
    {
        PreserveResizability,
        FixedLength,
        Immutable
    }
}
