#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Crypto;

/// <summary>
/// The engine and realm a <c>SubtleCrypto</c> operation raises its failures in, handed to the algorithm
/// implementations so that each of them can spell the specification's "throw a DataError" as one call.
/// </summary>
/// <remarks>
/// <para>
/// Every throw here is a real exception, and every one of them is caught one frame below script by the
/// operation that started it and turned into a <i>rejection</i> — a promise-returning WebIDL operation
/// reports nothing any other way, https://webidl.spec.whatwg.org/#dfn-create-operation-function. Writing the
/// algorithm steps as throws is what lets them read like the specification's own numbered prose.
/// </para>
/// <para>
/// The error object is "associated with the relevant realm of this" — https://w3c.github.io/webcrypto/#dfn-exceptions —
/// which is the realm carried here, never the current one, so an operation reached across realms still
/// rejects with its own realm's error type.
/// </para>
/// </remarks>
internal readonly struct CryptoContext
{
    internal CryptoContext(Engine engine, Realm realm)
    {
        Engine = engine;
        Realm = realm;
    }

    internal Engine Engine { get; }

    internal Realm Realm { get; }

    /// <summary>
    /// "Creating an ArrayBuffer in realm, containing <c>bytes</c>" — the last step of nearly every operation
    /// here. The array is handed over rather than copied, so every caller passes one nobody else holds:
    /// script may write into what it is given, and a key's material must not be reachable that way.
    /// </summary>
    internal JsArrayBuffer CreateArrayBuffer(byte[] bytes)
    {
        return new JsArrayBuffer(Engine, bytes)
        {
            _prototype = Realm.Intrinsics.ArrayBuffer.PrototypeObject,
        };
    }

    /// <summary>A <c>TypeError</c> — what a WebIDL type-mapping failure raises.</summary>
    [DoesNotReturn]
    internal void ThrowTypeError(string message) => Throw.TypeError(Realm, message);

    /// <summary>"The algorithm is not supported."</summary>
    [DoesNotReturn]
    internal void ThrowNotSupportedError(string message) => ThrowDomException(DomExceptionNames.NotSupported, message);

    /// <summary>"A required parameter was missing or out-of-range."</summary>
    [DoesNotReturn]
    internal void ThrowSyntaxError(string message) => ThrowDomException(DomExceptionNames.Syntax, message);

    /// <summary>"The requested operation is not valid for the provided key."</summary>
    [DoesNotReturn]
    internal void ThrowInvalidAccessError(string message) => ThrowDomException(DomExceptionNames.InvalidAccess, message);

    /// <summary>"Data provided to an operation does not meet requirements."</summary>
    [DoesNotReturn]
    internal void ThrowDataError(string message) => ThrowDomException(DomExceptionNames.Data, message);

    /// <summary>"The operation failed for an operation-specific reason."</summary>
    [DoesNotReturn]
    internal void ThrowOperationError(string message) => ThrowDomException(DomExceptionNames.Operation, message);

    [DoesNotReturn]
    private void ThrowDomException(string name, string message)
    {
        var exception = Realm.Intrinsics.DomException.CreateException(name, message);
        var location = Engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(Engine, exception, in location);
    }
}
#endif
