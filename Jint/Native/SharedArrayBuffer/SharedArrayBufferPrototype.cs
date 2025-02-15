using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.SharedArrayBuffer;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-sharedarraybuffer-prototype-object
/// </summary>
internal sealed class SharedArrayBufferPrototype : Prototype
{
    private readonly SharedArrayBufferConstructor _constructor;

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
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(3, checkExistingKeys: false)
        {
            ["byteLength"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get byteLength", ByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            [KnownKeys.Constructor] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["growable"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get growable", Growable, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["grow"] = new PropertyDescriptor(new ClrFunction(_engine, "grow", Grow, 1, lengthFlags), PropertyFlag.NonEnumerable),
            ["maxByteLength"] = new GetSetPropertyDescriptor(new ClrFunction(_engine, "get maxByteLength", MaxByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
            ["slice"] = new PropertyDescriptor(new ClrFunction(Engine, "slice", Slice, 2, lengthFlags), PropertyFlag.Configurable | PropertyFlag.Writable)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1) { [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("SharedArrayBuffer", PropertyFlag.Configurable) };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-sharedarraybuffer.prototype.bytelength
    /// </summary>
    private JsNumber ByteLength(JsValue thisObj, JsCallArguments arguments)
    {
        var o = thisObj as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method prototype.byteLength called on incompatible receiver " + thisObj);
        }

        return JsNumber.Create(o.ArrayBufferByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-sharedarraybuffer.prototype.slice
    /// </summary>
    private JsSharedArrayBuffer Slice(JsValue thisObj, JsCallArguments arguments)
    {
        var o = thisObj as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method prototype.slice called on incompatible receiver " + thisObj);
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
        var ctor = SpeciesConstructor(o, _realm.Intrinsics.SharedArrayBuffer);
        var bufferInstance = Construct(ctor, [JsNumber.Create(newLen)]) as JsSharedArrayBuffer;

        if (bufferInstance is null)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (!bufferInstance.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (bufferInstance.IsDetachedBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (ReferenceEquals(bufferInstance, o))
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (bufferInstance.ArrayBufferByteLength < newLen)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        // NOTE: Side-effects of the above steps may have detached O.

        if (bufferInstance.IsDetachedBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var fromBuf = o.ArrayBufferData!;
        var toBuf = bufferInstance.ArrayBufferData!;
        System.Array.Copy(fromBuf, first, toBuf, 0, newLen);
        return bufferInstance;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-sharedarraybuffer.prototype.growable
    /// </summary>
    private JsValue Growable(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method SharedArrayBuffer.prototype.growable called on incompatible receiver " + thisObject);
        }

        return !o.IsFixedLengthArrayBuffer;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-sharedarraybuffer.prototype.grow
    /// </summary>
    private JsValue Grow(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method SharedArrayBuffer.prototype.grow called on incompatible receiver " + thisObject);
        }

        var newLength = arguments.At(0);
        var newByteLength = TypeConverter.ToIndex(_realm, newLength);

        o.AssertNotDetached();

        o.Resize(newByteLength);

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-sharedarraybuffer.prototype.maxbytelength
    /// </summary>
    private JsValue MaxByteLength(JsValue thisObject, JsCallArguments arguments)
    {
        var o = thisObject as JsSharedArrayBuffer;
        if (o is null || !o.IsSharedArrayBuffer)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Method SharedArrayBuffer.prototype.maxByteLength called on incompatible receiver " + thisObject);
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
