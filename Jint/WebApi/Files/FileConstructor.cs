#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Files;

/// <summary>
/// The <c>File</c> interface object.
/// <para>
/// https://w3c.github.io/FileAPI/#file-constructor
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor(sequence&lt;BlobPart&gt; fileBits, USVString fileName, optional FilePropertyBag options = {})</c>.
/// The first two arguments are required, so fewer than two is a <c>TypeError</c> before anything is read.
/// <c>File</c> inherits <c>Blob</c>, so this interface object's <c>[[Prototype]]</c> is the <c>Blob</c>
/// interface object and <c>File.prototype</c>'s is <c>Blob.prototype</c> —
/// https://webidl.spec.whatwg.org/#interface-object.
/// </remarks>
internal sealed class FileConstructor : Constructor
{
    private static readonly JsString _functionName = new("File");

    internal FileConstructor(
        Engine engine,
        Realm realm,
        BlobConstructor blobConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = blobConstructor;
        PrototypeObject = new FilePrototype(engine, realm, this, blobConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal FilePrototype PrototypeObject { get; }

    /// <summary>
    /// https://w3c.github.io/FileAPI/#file-constructor
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        if (arguments.Length < 2)
        {
            Throw.TypeError(_realm, "File constructor: At least 2 arguments required, but only " + arguments.Length + " passed");
        }

        var bytes = FileApi.ProcessBlobParts(_realm, arguments.At(0));
        var name = FileApi.ToScalarValueString(TypeConverter.ToString(arguments.At(1)));
        var type = FileApi.ReadFilePropertyBag(_realm, arguments.At(2), out var lastModified);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.File.PrototypeObject,
            static (Engine engine, Realm _, (byte[] Bytes, string Type, string Name, long LastModified) state)
                => new JsFile(engine, state.Bytes, state.Type, state.Name, state.LastModified),
            (Bytes: bytes, Type: type, Name: name, LastModified: lastModified ?? Now(_engine)));
    }

    /// <summary>
    /// The <c>lastModified</c> default: "the current date and time represented as the number of
    /// milliseconds since the Unix Epoch (which is the equivalent of <c>Date.now()</c>)". It is read from
    /// the engine's own <c>ITimeSystem</c>, so it is the same clock <c>Date.now()</c> answers from and a
    /// host that supplied a fixed one gets a fixed value here.
    /// </summary>
    internal static long Now(Engine engine)
    {
        var ticks = engine.Options.TimeSystem.GetUtcNow().UtcTicks - DateTime.UnixEpoch.Ticks;
        return ticks / TimeSpan.TicksPerMillisecond;
    }
}
#endif
