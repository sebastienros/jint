#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableStreamBYOBReader</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#byob-reader-class
/// </para>
/// </summary>
/// <remarks>
/// The interface declares <c>constructor(ReadableStream stream)</c>, so this <i>is</i> constructible, and
/// <c>new ReadableStreamBYOBReader(stream)</c> is equivalent to
/// <c>stream.getReader({ mode: "byob" })</c> — including raising a <c>TypeError</c> for a stream that is
/// already locked, and for one that is not a byte stream.
/// <para>
/// Like <c>ReadableStreamDefaultReader</c> it is deliberately not installed as a global; it is reachable as
/// <c>Object.getPrototypeOf(stream.getReader({ mode: "byob" })).constructor</c>.
/// </para>
/// </remarks>
internal sealed class ReadableStreamBYOBReaderConstructor : Constructor
{
    private static readonly JsString _functionName = new("ReadableStreamBYOBReader");

    internal ReadableStreamBYOBReaderConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ReadableStreamBYOBReaderPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ReadableStreamBYOBReaderPrototype PrototypeObject { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#byob-reader-constructor
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        if (arguments.At(0) is not JsReadableStream stream)
        {
            Throw.TypeError(_realm, "Failed to construct 'ReadableStreamBYOBReader': the argument is not a ReadableStream");
            return null!;
        }

        var reader = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.ReadableStreamBYOBReader.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsReadableStreamBYOBReader(engine, realm));

        ReadableStreamOperations.SetUpBYOBReader(reader, stream);
        return reader;
    }
}
#endif
