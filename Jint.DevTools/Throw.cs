using System.Diagnostics.CodeAnalysis;
using Jint.DevTools.Protocol;

namespace Jint.DevTools;

/// <summary>
/// Raises the protocol's failures, in the shape <c>Jint/Runtime/Throw.cs</c> established: every helper is
/// <see cref="DoesNotReturnAttribute"/>, so the paths that do not fail stay allocation-free and a caller
/// needing a value can write <c>Throw.MethodNotFound&lt;T&gt;(…)</c> as its whole body.
/// </summary>
internal static class Throw
{
    /// <summary>Raises the protocol's parse error, <c>-32700</c>.</summary>
    [DoesNotReturn]
    internal static void ParseError(string message)
    {
        throw new ProtocolException(ProtocolErrorCodes.ParseError, message);
    }

    /// <summary>Raises the protocol's invalid-request error, <c>-32600</c>.</summary>
    [DoesNotReturn]
    internal static void InvalidRequest(string message)
    {
        throw new ProtocolException(ProtocolErrorCodes.InvalidRequest, message);
    }

    /// <inheritdoc cref="InvalidRequest(string)"/>
    [DoesNotReturn]
    internal static T InvalidRequest<T>(string message)
    {
        throw new ProtocolException(ProtocolErrorCodes.InvalidRequest, message);
    }

    /// <summary>Raises the protocol's method-not-found error, <c>-32601</c>, in Chrome's own wording.</summary>
    [DoesNotReturn]
    internal static void MethodNotFound(string method)
    {
        throw new ProtocolException(ProtocolErrorCodes.MethodNotFound, "'" + method + "' wasn't found");
    }

    /// <inheritdoc cref="MethodNotFound(string)"/>
    [DoesNotReturn]
    internal static T MethodNotFound<T>(string method)
    {
        throw new ProtocolException(ProtocolErrorCodes.MethodNotFound, "'" + method + "' wasn't found");
    }

    /// <summary>Raises the protocol's invalid-parameters error, <c>-32602</c>.</summary>
    [DoesNotReturn]
    internal static void InvalidParams(string message, string? details = null)
    {
        throw new ProtocolException(ProtocolErrorCodes.InvalidParams, message, details);
    }

    /// <inheritdoc cref="InvalidParams(string, string?)"/>
    [DoesNotReturn]
    internal static T InvalidParams<T>(string message, string? details = null)
    {
        throw new ProtocolException(ProtocolErrorCodes.InvalidParams, message, details);
    }

    /// <summary>Raises the protocol's server error, <c>-32000</c>.</summary>
    [DoesNotReturn]
    internal static void ServerError(string message, string? details = null)
    {
        throw new ProtocolException(ProtocolErrorCodes.ServerError, message, details);
    }

    /// <inheritdoc cref="ServerError(string, string?)"/>
    [DoesNotReturn]
    internal static T ServerError<T>(string message, string? details = null)
    {
        throw new ProtocolException(ProtocolErrorCodes.ServerError, message, details);
    }

    /// <summary>Raises the protocol's session-not-found error, <c>-32001</c>, in Chrome's own wording.</summary>
    [DoesNotReturn]
    internal static T SessionNotFound<T>()
    {
        throw new ProtocolException(ProtocolErrorCodes.SessionNotFound, "Session with given id not found.");
    }

    /// <summary>Raises <see cref="ArgumentNullException"/> for a host-supplied argument.</summary>
    [DoesNotReturn]
    internal static void ArgumentNull(string parameterName)
    {
        throw new ArgumentNullException(parameterName);
    }

    /// <summary>Raises <see cref="InvalidOperationException"/>.</summary>
    [DoesNotReturn]
    internal static void InvalidOperation(string message)
    {
        throw new InvalidOperationException(message);
    }
}
