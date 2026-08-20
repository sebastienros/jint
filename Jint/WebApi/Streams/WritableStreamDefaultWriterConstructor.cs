#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>WritableStreamDefaultWriter</c> interface object.
/// <para>
/// https://streams.spec.whatwg.org/#default-writer-class
/// </para>
/// </summary>
/// <remarks>
/// Constructible with a stream, exactly as <c>ReadableStreamDefaultReader</c> is:
/// <c>new WritableStreamDefaultWriter(stream)</c> is <c>stream.getWriter()</c>. Not installed as a global;
/// reached through <c>Object.getPrototypeOf(writer).constructor</c>.
/// </remarks>
internal sealed class WritableStreamDefaultWriterConstructor : Constructor
{
    private static readonly JsString _functionName = new("WritableStreamDefaultWriter");

    internal WritableStreamDefaultWriterConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new WritableStreamDefaultWriterPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal WritableStreamDefaultWriterPrototype PrototypeObject { get; }

    /// <summary>
    /// https://streams.spec.whatwg.org/#default-writer-constructor
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        if (arguments.At(0) is not JsWritableStream stream)
        {
            Throw.TypeError(_realm, "Failed to construct 'WritableStreamDefaultWriter': the argument is not a WritableStream");
            return null!;
        }

        var writer = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.WritableStreamDefaultWriter.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsWritableStreamDefaultWriter(engine, realm));

        WritableStreamOperations.SetUpDefaultWriter(writer, stream);
        return writer;
    }
}
#endif
