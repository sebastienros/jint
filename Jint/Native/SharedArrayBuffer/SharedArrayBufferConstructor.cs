using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.SharedArrayBuffer;

/// <summary>
/// https://tc39.es/ecma262/#sec-sharedarraybuffer-constructor
/// </summary>
[JsObject]
internal sealed partial class SharedArrayBufferConstructor : Constructor
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
        CreateProperties_Generated();
        CreateSymbols_Generated();
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
    /// https://tc39.es/ecma262/#sec-get-arraybuffer-@@species
    /// </summary>
    [JsSymbolAccessor("Species")]
    private static JsValue Species(JsValue thisObject)
    {
        return thisObject;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Constructor SharedArrayBuffer requires 'new'");
        return Undefined;
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

        return AllocateSharedArrayBuffer(newTarget, byteLength, requestedMaxByteLength);
    }

    private JsSharedArrayBuffer AllocateSharedArrayBuffer(JsValue constructor, uint byteLength, uint? maxByteLength = null)
    {
        var allocatingGrowableBuffer = maxByteLength != null;

        if (allocatingGrowableBuffer && byteLength > maxByteLength)
        {
            Throw.RangeError(_realm, "byteLength exceeds maxByteLength");
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
