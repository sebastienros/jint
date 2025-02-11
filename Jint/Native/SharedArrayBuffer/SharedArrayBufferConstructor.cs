using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.SharedArrayBuffer;

/// <summary>
/// https://tc39.es/ecma262/#sec-sharedarraybuffer-constructor
/// </summary>
internal sealed class SharedArrayBufferConstructor : Constructor
{
    private static readonly JsString _functionName = new("SharedArrayBuffer");

    internal SharedArrayBufferConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new SharedArrayBufferPrototype(engine, this, objectPrototype);
        _length = new PropertyDescriptor(1, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private SharedArrayBufferPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["isView"] = new PropertyDescriptor(new PropertyDescriptor(new ClrFunction(Engine, "isView", IsView, 1, lengthFlags), PropertyFlag.Configurable | PropertyFlag.Writable)),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunction(Engine, "get [Symbol.species]", Species, 0, lengthFlags), set: Undefined,PropertyFlag.Configurable),
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraybuffer.isview
    /// </summary>
    private static JsValue IsView(JsValue thisObject, JsCallArguments arguments)
    {
        var arg = arguments.At(0);
        return arg is JsDataView or JsTypedArray;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-arraybuffer-@@species
    /// </summary>
    private static JsValue Species(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        ExceptionHelper.ThrowTypeError(_realm, "Constructor SharedArrayBuffer requires 'new'");
        return Undefined;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var length = arguments.At(0);
        var options = arguments.At(1);

        var byteLength = TypeConverter.ToIndex(_realm, length);
        var requestedMaxByteLength = options.GetArrayBufferMaxByteLengthOption();

        return AllocateSharedArrayBuffer(newTarget, byteLength, requestedMaxByteLength);
    }

    private JsSharedArrayBuffer AllocateSharedArrayBuffer(JsValue constructor, uint byteLength, uint? maxByteLength  = null)
    {
        var allocatingGrowableBuffer = maxByteLength != null;

        if (allocatingGrowableBuffer && byteLength > maxByteLength)
        {
            ExceptionHelper.ThrowRangeError(_realm);
        }

        var allocLength = maxByteLength.GetValueOrDefault(byteLength);

        var obj = OrdinaryCreateFromConstructor(
            constructor,
            static intrinsics => intrinsics.SharedArrayBuffer.PrototypeObject,
            static (engine, _, state) =>
            {
                var buffer = new JsSharedArrayBuffer(engine, [], state.MaxByteLength, state.ArrayBufferByteLengthData)
                {
                    _arrayBufferData = state.Block ?? (state.ByteLength > 0 ? JsSharedArrayBuffer.CreateSharedByteDataBlock(engine.Realm, state.ByteLength) : []),
                };
                return buffer;
            },
            new ConstructState(Block: null, allocLength, maxByteLength, byteLength));

        return obj;
    }

    private readonly record struct ConstructState(byte[]? Block, uint ByteLength, uint? MaxByteLength, uint ArrayBufferByteLengthData);
}
