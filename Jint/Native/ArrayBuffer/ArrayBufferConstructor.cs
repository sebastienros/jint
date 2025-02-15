using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.ArrayBuffer;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-arraybuffer-constructor
/// </summary>
public sealed class ArrayBufferConstructor : Constructor
{
    private static readonly JsString _functionName = new("ArrayBuffer");

    internal ArrayBufferConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ArrayBufferPrototype(engine, this, objectPrototype);
        _length = new PropertyDescriptor(1, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ArrayBufferPrototype PrototypeObject { get; }

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
    /// Constructs a new JsArrayBuffer instance and takes ownership of the given byte array and uses it as backing store.
    /// </summary>
    public JsArrayBuffer Construct(byte[] data)
    {
        return CreateJsArrayBuffer(this, data, byteLength: (ulong) data.Length, maxByteLength: null);
    }

    /// <summary>
    /// Constructs a new JsArrayBuffer with given byte length and optional max byte length.
    /// </summary>
    public JsArrayBuffer Construct(ulong byteLength, uint? maxByteLength = null)
    {
        return AllocateArrayBuffer(this, byteLength, maxByteLength);
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
        return AllocateArrayBuffer(newTarget, byteLength, requestedMaxByteLength);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-arraybuffer-@@species
    /// </summary>
    private static JsValue Species(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
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
    /// https://tc39.es/ecma262/#sec-allocatearraybuffer
    /// </summary>
    internal JsArrayBuffer AllocateArrayBuffer(JsValue constructor, ulong byteLength, uint? maxByteLength = null)
    {
        var allocatingResizableBuffer = maxByteLength != null;

        if (allocatingResizableBuffer && byteLength > maxByteLength)
        {
            ExceptionHelper.ThrowRangeError(_realm);
        }

        return CreateJsArrayBuffer(constructor, block: null, byteLength, maxByteLength);
    }

    private JsArrayBuffer CreateJsArrayBuffer(JsValue constructor, byte[]? block, ulong byteLength, uint? maxByteLength)
    {
        var obj = OrdinaryCreateFromConstructor(
            constructor,
            static intrinsics => intrinsics.ArrayBuffer.PrototypeObject,
            static (engine, _, state) =>
            {
                var buffer = new JsArrayBuffer(engine, [], state.MaxByteLength)
                {
                    _arrayBufferData = state.Block ?? (state.ByteLength > 0 ? JsArrayBuffer.CreateByteDataBlock(engine.Realm, state.ByteLength) : []),
                };

                return buffer;
            },
            new ConstructState(block, byteLength, maxByteLength));

        return obj;
    }

    private readonly record struct ConstructState(byte[]? Block, ulong ByteLength, uint? MaxByteLength);
}
