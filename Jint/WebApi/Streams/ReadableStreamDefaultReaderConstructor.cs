#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableStreamDefaultReader</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#default-reader-class
/// </para>
/// </summary>
/// <remarks>
/// The interface declares <c>constructor(ReadableStream stream)</c>, so this <i>is</i> constructible, and
/// <c>new ReadableStreamDefaultReader(stream)</c> is equivalent to <c>stream.getReader()</c> — including
/// raising a <c>TypeError</c> for a stream that is already locked.
/// <para>
/// It is deliberately not installed as a global: the enabled globals are the five interfaces a script
/// constructs directly. This one stays reachable, as
/// <c>Object.getPrototypeOf(stream.getReader()).constructor</c>, which is where <c>reader instanceof</c>
/// checks and prototype patching go. That is a documented divergence from a browser, where every one of
/// these interfaces is a global.
/// </para>
/// </remarks>
internal sealed class ReadableStreamDefaultReaderConstructor : Constructor
{
    private static readonly JsString _functionName = new("ReadableStreamDefaultReader");

    internal ReadableStreamDefaultReaderConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ReadableStreamDefaultReaderPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal ReadableStreamDefaultReaderPrototype PrototypeObject { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-reader-constructor
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        if (arguments.At(0) is not JsReadableStream stream)
        {
            Throw.TypeError(_realm, "Failed to construct 'ReadableStreamDefaultReader': the argument is not a ReadableStream");
            return null!;
        }

        var reader = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.ReadableStreamDefaultReader.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsReadableStreamDefaultReader(engine, realm));

        ReadableStreamOperations.SetUpDefaultReader(reader, stream);
        return reader;
    }
}
#endif
