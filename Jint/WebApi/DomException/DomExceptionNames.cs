#if NET8_0_OR_GREATER
namespace Jint.WebApi.DomException;

/// <summary>
/// The error names web APIs raise, and the legacy numeric codes WebIDL still has to expose for them.
/// <para>
/// https://webidl.spec.whatwg.org/#dfn-error-names-table
/// </para>
/// </summary>
/// <remarks>
/// Only the 25 names in the table's "legacy code value" column have a code; every other name — including
/// every name a script makes up, and the modern additions such as <c>NotAllowedError</c> — reports
/// <c>0</c>, which is what <c>DOMException.prototype.code</c> is specified to return for them.
/// </remarks>
internal static class DomExceptionNames
{
    internal const string IndexSize = "IndexSizeError";
    internal const string HierarchyRequest = "HierarchyRequestError";
    internal const string WrongDocument = "WrongDocumentError";
    internal const string InvalidCharacter = "InvalidCharacterError";
    internal const string NoModificationAllowed = "NoModificationAllowedError";
    internal const string NotFound = "NotFoundError";
    internal const string NotSupported = "NotSupportedError";
    internal const string InUseAttribute = "InUseAttributeError";
    internal const string InvalidState = "InvalidStateError";
    internal const string Syntax = "SyntaxError";
    internal const string InvalidModification = "InvalidModificationError";
    internal const string Namespace = "NamespaceError";
    internal const string InvalidAccess = "InvalidAccessError";
    internal const string TypeMismatch = "TypeMismatchError";
    internal const string Security = "SecurityError";
    internal const string Network = "NetworkError";
    internal const string Abort = "AbortError";
    internal const string UrlMismatch = "URLMismatchError";
    internal const string QuotaExceeded = "QuotaExceededError";
    internal const string Timeout = "TimeoutError";
    internal const string InvalidNodeType = "InvalidNodeTypeError";
    internal const string DataClone = "DataCloneError";
    internal const string NotAllowed = "NotAllowedError";

    /// <summary>
    /// The legacy code for an error name, or <c>0</c> when the name has none.
    /// </summary>
    internal static ushort CodeFor(string name) => name switch
    {
        IndexSize => 1,
        "DOMStringSizeError" => 2,
        HierarchyRequest => 3,
        WrongDocument => 4,
        InvalidCharacter => 5,
        "NoDataAllowedError" => 6,
        NoModificationAllowed => 7,
        NotFound => 8,
        NotSupported => 9,
        InUseAttribute => 10,
        InvalidState => 11,
        Syntax => 12,
        InvalidModification => 13,
        Namespace => 14,
        InvalidAccess => 15,
        "ValidationError" => 16,
        TypeMismatch => 17,
        Security => 18,
        Network => 19,
        Abort => 20,
        UrlMismatch => 21,
        QuotaExceeded => 22,
        Timeout => 23,
        InvalidNodeType => 24,
        DataClone => 25,
        _ => 0,
    };
}
#endif
