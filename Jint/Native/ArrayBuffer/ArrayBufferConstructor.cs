using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.ArrayBuffer;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-arraybuffer-constructor
/// </summary>
[JsObject]
public sealed partial class ArrayBufferConstructor : Constructor
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
        CreateProperties_Generated();
        CreateSymbols_Generated();
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
            Throw.TypeError(_realm, $"Constructor {_nameDescriptor?.Value} requires 'new'");
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
    [JsSymbolAccessor("Species")]
    private static JsValue Species(JsValue thisObject)
    {
        return thisObject;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraybuffer.isview
    /// </summary>
    [JsFunction(Length = 1)]
    private static JsValue IsView(JsValue thisObject, JsValue arg)
    {
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
            Throw.RangeError(_realm, "byteLength exceeds maxByteLength");
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
