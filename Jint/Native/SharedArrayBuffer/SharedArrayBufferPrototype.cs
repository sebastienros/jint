using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.SharedArrayBuffer;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-sharedarraybuffer-prototype-object
/// </summary>
[JsObject]
internal sealed partial class SharedArrayBufferPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly SharedArrayBufferConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString SharedArrayBufferToStringTag = new("SharedArrayBuffer");

    internal SharedArrayBufferPrototype(
        Engine engine,
        SharedArrayBufferConstructor constructor,
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
    /// https://tc39.es/ecma262/#sec-get-sharedarraybuffer.prototype.bytelength
    /// </summary>
    [JsAccessor("byteLength")]
    private JsNumber ByteLength(JsValue thisObject)
    {
        var o = thisObject as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method prototype.byteLength called on incompatible receiver " + thisObject);
        }

        return JsNumber.Create(o.ArrayBufferByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-sharedarraybuffer.prototype.slice
    /// </summary>
    [JsFunction(Length = 2)]
    private JsSharedArrayBuffer Slice(JsValue thisObject, JsValue start, JsValue end)
    {
        var o = thisObject as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method prototype.slice called on incompatible receiver " + thisObject);
        }

        o.AssertNotDetached();

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
        var ctor = SpeciesConstructor(o, _realm.Intrinsics.SharedArrayBuffer);
        var bufferInstance = Construct(ctor, [JsNumber.Create(newLen)]) as JsSharedArrayBuffer;

        if (bufferInstance is null)
        {
            Throw.TypeError(_realm, "Species constructor did not return a SharedArrayBuffer");
        }

        if (!bufferInstance.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "SharedArrayBuffer.prototype.slice: result is not a SharedArrayBuffer");
        }

        if (bufferInstance.IsDetachedBuffer)
        {
            Throw.TypeError(_realm, "Cannot perform SharedArrayBuffer.prototype.slice on a detached buffer");
        }

        if (ReferenceEquals(bufferInstance, o))
        {
            Throw.TypeError(_realm, "SharedArrayBuffer.prototype.slice returned the same buffer");
        }

        if (bufferInstance.ArrayBufferByteLength < newLen)
        {
            Throw.TypeError(_realm, "SharedArrayBuffer.prototype.slice: constructed buffer is too small");
        }

        // NOTE: Side-effects of the above steps may have detached O.

        if (bufferInstance.IsDetachedBuffer)
        {
            Throw.TypeError(_realm, "Cannot perform SharedArrayBuffer.prototype.slice on a detached buffer");
        }

        var fromBuf = o.ArrayBufferData!;
        var toBuf = bufferInstance.ArrayBufferData!;
        System.Array.Copy(fromBuf, first, toBuf, 0, newLen);
        return bufferInstance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-sharedarraybuffer.prototype.growable
    /// </summary>
    [JsAccessor("growable")]
    private JsValue Growable(JsValue thisObject)
    {
        var o = thisObject as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method SharedArrayBuffer.prototype.growable called on incompatible receiver " + thisObject);
        }

        return !o.IsFixedLengthArrayBuffer;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-sharedarraybuffer.prototype.grow
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Grow(JsValue thisObject, JsValue newLength)
    {
        var o = thisObject as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method SharedArrayBuffer.prototype.grow called on incompatible receiver " + thisObject);
        }

        var newByteLength = TypeConverter.ToIndex(_realm, newLength);

        o.AssertNotDetached();

        o.Resize(newByteLength);

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-sharedarraybuffer.prototype.maxbytelength
    /// </summary>
    [JsAccessor("maxByteLength")]
    private JsValue MaxByteLength(JsValue thisObject)
    {
        var o = thisObject as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            Throw.TypeError(_realm, "Method SharedArrayBuffer.prototype.maxByteLength called on incompatible receiver " + thisObject);
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
}
