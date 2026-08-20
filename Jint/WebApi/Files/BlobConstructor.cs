#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Files;

/// <summary>
/// The <c>Blob</c> interface object.
/// <para>
/// https://w3c.github.io/FileAPI/#dfn-Blob
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor(optional sequence&lt;BlobPart&gt; blobParts, optional BlobPropertyBag options = {})</c>.
/// Called without <c>new</c> it raises a <c>TypeError</c>, which is what <see cref="Constructor"/> already
/// does for every interface object shape.
/// </remarks>
internal sealed class BlobConstructor : Constructor
{
    private static readonly JsString _functionName = new("Blob");

    internal BlobConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new BlobPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal BlobPrototype PrototypeObject { get; }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#constructorBlob
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        // blobParts is optional with no default value, so an explicitly passed `undefined` means the
        // argument is missing rather than something to convert — https://webidl.spec.whatwg.org/#es-overloads.
        // The options are still read, which is why this is not the whole of step 1.
        var blobParts = arguments.At(0);

        // Arguments are converted left to right, so the sequence is drained — and every element that is
        // neither a buffer source nor a blob stringified — before the options dictionary is read.
        var bytes = blobParts.IsUndefined() ? [] : FileApi.ProcessBlobParts(_realm, blobParts);
        var type = FileApi.ReadBlobPropertyBag(_realm, arguments.At(1));

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Blob.PrototypeObject,
            static (Engine engine, Realm _, (byte[] Bytes, string Type) state) => new JsBlob(engine, state.Bytes, state.Type),
            (Bytes: bytes, Type: type));
    }
}
#endif
