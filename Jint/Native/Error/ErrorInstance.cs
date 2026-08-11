using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Error;

/// <summary>
/// Host-only facts about the CLR origin of an <see cref="ErrorInstance"/>, held behind a single reference
/// rather than as one field per fact. Errors that came out of CLR interop are a small minority of the errors
/// an engine builds, so a field per fact would have every error object carrying the ones it never fills.
/// </summary>
internal sealed record ClrErrorContext(Type? ResolutionType, string? ResolutionMemberName, Exception? ClrException);

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
        _clrContext = new ClrErrorContext(clrType, memberName, _clrContext?.ClrException);
    }

    /// <summary>
    /// Records the CLR exception this error stands in for, keeping any resolution origin already recorded.
    /// See <see cref="SetClrResolutionInfo"/> for why the two must not clobber each other.
    /// </summary>
    internal void SetClrException(Exception clrException)
    {
        var existing = _clrContext;
        _clrContext = new ClrErrorContext(existing?.ResolutionType, existing?.ResolutionMemberName, clrException);
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
