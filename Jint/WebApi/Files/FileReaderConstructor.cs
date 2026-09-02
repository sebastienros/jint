#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Files;

/// <summary>
/// The <c>FileReader</c> interface object.
/// <para>
/// https://w3c.github.io/FileAPI/#dfn-filereader
/// </para>
/// </summary>
/// <remarks>
/// <c>FileReader</c> inherits from <c>EventTarget</c>, so its <c>[[Prototype]]</c> is the <c>EventTarget</c>
/// interface object. The three state constants appear here as well as on the prototype, per
/// https://webidl.spec.whatwg.org/#es-constants, with the attributes constants are given there:
/// <c>{ writable: false, enumerable: true, configurable: false }</c>. That section defines them in the order
/// the IDL declares them and that order is observable, so <c>PreserveDeclarationOrder</c> keeps the generator
/// from sorting them by name.
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class FileReaderConstructor : Constructor
{
    private static readonly JsString _functionName = new("FileReader");

    [JsProperty(Name = "EMPTY", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber ReaderEmpty = JsNumber.Create(JsFileReader.Empty);
    [JsProperty(Name = "LOADING", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber ReaderLoading = JsNumber.Create(JsFileReader.Loading);
    [JsProperty(Name = "DONE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber ReaderDone = JsNumber.Create(JsFileReader.Done);

    internal FileReaderConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new FileReaderPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal FileReaderPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://w3c.github.io/FileAPI/#filereaderConstrctr — the constructor takes no argument and the whole
    /// of its content is the object's initial state.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.FileReader.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsFileReader(engine, realm),
            state: null);
    }
}
#endif
