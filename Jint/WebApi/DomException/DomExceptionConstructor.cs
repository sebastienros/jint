#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Error;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.CallStack;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.DomException;

/// <summary>
/// The <c>DOMException</c> interface object.
/// <para>
/// https://webidl.spec.whatwg.org/#idl-DOMException
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>constructor(optional DOMString message = "", optional DOMString name = "Error")</c>. Its
/// <c>[[Prototype]]</c> is <c>%Function.prototype%</c> — <c>DOMException</c> is a WebIDL interface, not an
/// ECMAScript <c>NativeError</c>, so the interface object does not inherit from <c>Error</c> even though its
/// prototype object does. Called without <c>new</c> it raises a <c>TypeError</c>, which is what
/// <see cref="Constructor"/> already does for every interface object shape.
/// </para>
/// <para>
/// The 25 legacy constants appear here as well as on the prototype, per
/// https://webidl.spec.whatwg.org/#es-constants, which defines them one after another in the order the IDL
/// declares them. That order is observable — a record conversion over the interface object reads
/// <c>[[OwnPropertyKeys]]</c> — so the declarations below are in it and
/// <c>PreserveDeclarationOrder</c> keeps the generator from sorting them by name.
/// </para>
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class DomExceptionConstructor : Constructor
{
    private static readonly JsString _functionName = new("DOMException");
    private static readonly JsString _defaultName = new("Error");

    [JsProperty(Name = "INDEX_SIZE_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber IndexSizeErr = JsNumber.Create(1);
    [JsProperty(Name = "DOMSTRING_SIZE_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber DomStringSizeErr = JsNumber.Create(2);
    [JsProperty(Name = "HIERARCHY_REQUEST_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber HierarchyRequestErr = JsNumber.Create(3);
    [JsProperty(Name = "WRONG_DOCUMENT_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber WrongDocumentErr = JsNumber.Create(4);
    [JsProperty(Name = "INVALID_CHARACTER_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber InvalidCharacterErr = JsNumber.Create(5);
    [JsProperty(Name = "NO_DATA_ALLOWED_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber NoDataAllowedErr = JsNumber.Create(6);
    [JsProperty(Name = "NO_MODIFICATION_ALLOWED_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber NoModificationAllowedErr = JsNumber.Create(7);
    [JsProperty(Name = "NOT_FOUND_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber NotFoundErr = JsNumber.Create(8);
    [JsProperty(Name = "NOT_SUPPORTED_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber NotSupportedErr = JsNumber.Create(9);
    [JsProperty(Name = "INUSE_ATTRIBUTE_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber InUseAttributeErr = JsNumber.Create(10);
    [JsProperty(Name = "INVALID_STATE_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber InvalidStateErr = JsNumber.Create(11);
    [JsProperty(Name = "SYNTAX_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber SyntaxErr = JsNumber.Create(12);
    [JsProperty(Name = "INVALID_MODIFICATION_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber InvalidModificationErr = JsNumber.Create(13);
    [JsProperty(Name = "NAMESPACE_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber NamespaceErr = JsNumber.Create(14);
    [JsProperty(Name = "INVALID_ACCESS_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber InvalidAccessErr = JsNumber.Create(15);
    [JsProperty(Name = "VALIDATION_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber ValidationErr = JsNumber.Create(16);
    [JsProperty(Name = "TYPE_MISMATCH_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber TypeMismatchErr = JsNumber.Create(17);
    [JsProperty(Name = "SECURITY_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber SecurityErr = JsNumber.Create(18);
    [JsProperty(Name = "NETWORK_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber NetworkErr = JsNumber.Create(19);
    [JsProperty(Name = "ABORT_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber AbortErr = JsNumber.Create(20);
    [JsProperty(Name = "URL_MISMATCH_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber UrlMismatchErr = JsNumber.Create(21);
    [JsProperty(Name = "QUOTA_EXCEEDED_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber QuotaExceededErr = JsNumber.Create(22);
    [JsProperty(Name = "TIMEOUT_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber TimeoutErr = JsNumber.Create(23);
    [JsProperty(Name = "INVALID_NODE_TYPE_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber InvalidNodeTypeErr = JsNumber.Create(24);
    [JsProperty(Name = "DATA_CLONE_ERR", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber DataCloneErr = JsNumber.Create(25);

    internal DomExceptionConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectInstance errorPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DomExceptionPrototype(engine, realm, this, errorPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal DomExceptionPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dom-domexception-domexception
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var messageArgument = arguments.At(0);
        var nameArgument = arguments.At(1);

        // The IDL defaults are "" and "Error"; an explicitly passed undefined takes the default too, which
        // is what an optional argument with a default value means in WebIDL.
        var message = messageArgument.IsUndefined() ? JsString.Empty : TypeConverter.ToJsString(messageArgument);
        var name = nameArgument.IsUndefined() ? _defaultName : TypeConverter.ToJsString(nameArgument);

        var exception = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.DomException.PrototypeObject,
            static (Engine engine, Realm _, (JsString Name, JsString Message) state) => new JsDomException(engine, state.Name, state.Message),
            (Name: name, Message: message));

        AttachStack(_engine, this, exception);

        return exception;
    }

    /// <summary>
    /// Builds a <c>DOMException</c> from engine code, for the error names the web APIs raise themselves —
    /// <c>AbortError</c>, <c>TimeoutError</c>, <c>InvalidCharacterError</c> and the rest of
    /// https://webidl.spec.whatwg.org/#dfn-error-names-table.
    /// </summary>
    /// <remarks>
    /// Not for <c>QuotaExceededError</c>: that name has an interface of its own
    /// (https://webidl.spec.whatwg.org/#quotaexceedederror), and
    /// <see cref="QuotaExceededErrorConstructor.CreateException"/> is what raises it. Nothing stops this
    /// method building a plain <c>DOMException</c> wearing the name — a script may do exactly that with
    /// <c>new DOMException('x', 'QuotaExceededError')</c>, and it is not the interface — but no engine code
    /// should.
    /// </remarks>
    /// <param name="name">The error name; <see cref="DomExceptionNames"/> holds the ones with a legacy code.</param>
    /// <param name="message">The message, or the empty string.</param>
    internal JsDomException CreateException(string name, string message)
    {
        var exception = new JsDomException(_engine, JsString.Create(name), JsString.Create(message))
        {
            _prototype = PrototypeObject,
        };

        AttachStack(_engine, this, exception);

        return exception;
    }

    /// <summary>
    /// Gives the instance the own <c>stack</c> property browsers put on a <c>DOMException</c>. It is lazy for
    /// the same reason <c>Error</c>'s is: most exceptions are caught without anyone reading the trace, and
    /// rendering it costs a string build over the retained frames.
    /// </summary>
    /// <remarks>
    /// <paramref name="interfaceObject"/> is the interface object the exception is being constructed by, which
    /// is the frame the capture drops when a script wrote <c>new …(…)</c> itself — so a
    /// <c>QuotaExceededError</c> hides its own constructor and not <c>DOMException</c>'s.
    /// </remarks>
    internal static void AttachStack(Engine engine, Constructor interfaceObject, JsDomException exception)
    {
        var capture = ErrorConstructor.BuildStackTraceCapture(engine, interfaceObject);
        if (capture is null)
        {
            return;
        }

        exception.SetProperty(
            CommonProperties.Stack,
            PropertyDescriptor.CreateLazy(capture, static c => JsString.Create(c.Render()), PropertyFlag.NonEnumerable));
    }
}
#endif
