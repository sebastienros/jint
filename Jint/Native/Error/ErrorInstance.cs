using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Error;

/// <summary>
/// Host-only facts about the CLR origin of an <see cref="ErrorInstance"/>, held behind a single reference
/// rather than as one field per fact. Errors that came out of CLR interop are a small minority of the errors
/// an engine builds, so a field per fact would have every error object carrying the ones it never fills.
/// </summary>
internal sealed record ClrErrorContext(
    Type? ResolutionType,
    string? ResolutionMemberName,
    Exception? ClrException,
    object? ModuleErrorPolicyToken);

/// <summary>
/// Marks an object that carries the specification's <c>[[ErrorData]]</c> internal slot, which is the brand
/// <c>Error.isError</c> tests — https://tc39.es/ecma262/#sec-error.iserror.
/// </summary>
/// <remarks>
/// <para>
/// The slot is <b>not</b> a property of <see cref="ErrorInstance"/>: <c>%Error.prototype%</c> and every
/// <c>%NativeError.prototype%</c> derive from it and must answer <see langword="false"/>, because
/// https://tc39.es/ecma262/#sec-properties-of-the-error-prototype-object says such a prototype "is an
/// ordinary object" and "is not an Error instance and does not have an [[ErrorData]] internal slot". So the
/// slot is asserted by exactly the types that have it, one of which — <see cref="JsError"/> — is sealed and
/// cannot simply be widened to cover the other.
/// </para>
/// <para>
/// A marker interface rather than an <c>InternalTypes</c> bit: <c>InternalTypes</c> says of itself that "no
/// int flag bits remain" — <c>FastCallGuard</c> already borrows the two above its top — and the only consumer
/// is <c>Error.isError</c>, which is on no hot path and would not repay a bit even if one were free. The
/// check keeps <see cref="JsError"/>'s sealed exact-type test first, so the interface-map scan is reached
/// only by the rare error object that is not a <see cref="JsError"/>.
/// </para>
/// </remarks>
internal interface IErrorData;

public class ErrorInstance : ObjectInstance
{
    private protected ErrorInstance(Engine engine, ObjectClass objectClass)
        : base(engine, objectClass)
    {
    }

    /// <summary>
    /// Host-only facts about the CLR origin of this error, or null for an error that has none. This is plain
    /// CLR state and not JavaScript properties, so a running script cannot observe it; the host reads it
    /// through the <see cref="JintException"/> accessors.
    /// </summary>
    private ClrErrorContext? _clrContext;

    /// <summary>
    /// When this error was produced by a failed CLR interop method or constructor resolution, the
    /// originating CLR type. Read host-side via <see cref="JintException.TryGetClrType"/>.
    /// </summary>
    internal Type? ClrResolutionType => _clrContext?.ResolutionType;

    internal string? ClrResolutionMemberName => _clrContext?.ResolutionMemberName;

    /// <summary>
    /// When this error stands in for a CLR exception thrown out of host code — a delegate, a reflected member,
    /// a proxy trap — the exception itself. Read host-side via <see cref="JintException.TryGetClrException"/>.
    /// </summary>
    internal Exception? ClrException => _clrContext?.ClrException;

    internal bool HasModuleErrorPolicyToken(object token)
        => ReferenceEquals(_clrContext?.ModuleErrorPolicyToken, token);

    /// <summary>
    /// Records where a failed CLR resolution came from, keeping any exception already recorded.
    /// </summary>
    /// <remarks>
    /// The facts behind <see cref="ClrErrorContext"/> are independent, and each setter owns only its own.
    /// No in-box path reaches both on one error — the three call sites each construct the error they
    /// annotate — so this is defensive; it is the record that made it worth stating, because folding one
    /// field per fact behind a single reference turned "set the other fact" into "replace every fact",
    /// where before the fields were separate and one setter could not reach the other's.
    /// </remarks>
    internal void SetClrResolutionInfo(Type clrType, string? memberName)
    {
        _clrContext = new ClrErrorContext(
            clrType,
            memberName,
            _clrContext?.ClrException,
            _clrContext?.ModuleErrorPolicyToken);
    }

    /// <summary>
    /// Records the CLR exception this error stands in for, keeping any resolution origin already recorded.
    /// See <see cref="SetClrResolutionInfo"/> for why the two must not clobber each other.
    /// </summary>
    internal void SetClrException(Exception clrException)
    {
        var existing = _clrContext;
        _clrContext = new ClrErrorContext(
            existing?.ResolutionType,
            existing?.ResolutionMemberName,
            clrException,
            existing?.ModuleErrorPolicyToken);
    }

    internal void SetModuleErrorPolicyToken(object token)
    {
        var existing = _clrContext;
        _clrContext = new ClrErrorContext(
            existing?.ResolutionType,
            existing?.ResolutionMemberName,
            existing?.ClrException,
            token);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-installerrorcause
    /// </summary>
    internal void InstallErrorCause(JsValue options)
    {
        if (options is ObjectInstance oi && oi.HasProperty(CommonProperties.Cause))
        {
            var cause = oi.Get(CommonProperties.Cause);
            CreateNonEnumerableDataPropertyOrThrow(CommonProperties.Cause, cause);
        }
    }

    public override string ToString()
    {
        return Engine.Realm.Intrinsics.Error.PrototypeObject.ToString(this).ToObject()?.ToString() ?? "";
    }
}
