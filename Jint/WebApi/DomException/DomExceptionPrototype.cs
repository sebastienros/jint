#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.DomException;

/// <summary>
/// <c>DOMException.prototype</c> — the interface prototype object.
/// <para>
/// https://webidl.spec.whatwg.org/#idl-DOMException
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Its <c>[[Prototype]]</c> is <c>%Error.prototype%</c>, which is what makes <c>instanceof Error</c> hold for
/// an instance and what gives it <c>toString</c>. <c>name</c>, <c>message</c> and <c>code</c> are accessors
/// here rather than own properties of the instance, as WebIDL specifies attributes; each brand-checks its
/// receiver and raises a <c>TypeError</c> for anything that is not a <c>DOMException</c> — including
/// <c>DOMException.prototype</c> itself, which is not one.
/// </para>
/// <para>
/// The 25 legacy constants appear on both this object and the interface object, per
/// https://webidl.spec.whatwg.org/#es-constants, with the attributes constants are given there:
/// <c>{ writable: false, enumerable: true, configurable: false }</c>. They are declared — and, thanks to
/// <c>PreserveDeclarationOrder</c>, enumerate — in the order the IDL declares them, which is the order that
/// section defines them in and the one the interface object is read in.
/// </para>
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class DomExceptionPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly DomExceptionConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString DomExceptionToStringTag = new("DOMException");

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

    internal DomExceptionPrototype(
        Engine engine,
        Realm realm,
        DomExceptionConstructor constructor,
        ObjectInstance errorPrototype) : base(engine, realm)
    {
        _prototype = errorPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dom-domexception-name
    /// </summary>
    [JsAccessor("name", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NameGet(JsValue thisObject)
    {
        return Brand(thisObject).Name;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dom-domexception-message
    /// </summary>
    [JsAccessor("message", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString MessageGet(JsValue thisObject)
    {
        return Brand(thisObject).Message;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dom-domexception-code
    /// </summary>
    [JsAccessor("code", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber CodeGet(JsValue thisObject)
    {
        return Brand(thisObject).Code;
    }

    /// <summary>
    /// The WebIDL brand check every attribute getter performs: a receiver that is not a platform object
    /// implementing the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsDomException Brand(JsValue thisObject)
    {
        if (thisObject is JsDomException exception)
        {
            return exception;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a DOMException");
        return null!;
    }
}
#endif
