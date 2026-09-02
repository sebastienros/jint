namespace Jint.DevTools.Protocol;

/// <summary>
/// The error codes the Chrome DevTools Protocol answers with.
/// </summary>
/// <remarks>
/// <para>
/// These are Chromium's, not JSON-RPC's generic set: they come from <c>crdtp/dispatch.cc</c>, which is what
/// every client was written against. The wording matters as much as the number for
/// <see cref="MethodNotFound"/> — a client feature-detecting a domain reads the message.
/// </para>
/// </remarks>
internal static class ProtocolErrorCodes
{
    /// <summary>The message was not valid JSON.</summary>
    internal const int ParseError = -32700;

    /// <summary>The message was JSON, but not a request the protocol admits.</summary>
    internal const int InvalidRequest = -32600;

    /// <summary>No domain answers the method, or the domain does not implement it.</summary>
    internal const int MethodNotFound = -32601;

    /// <summary>The parameters did not deserialize into what the command declares.</summary>
    internal const int InvalidParams = -32602;

    /// <summary>The server failed for a reason that is not the caller's doing.</summary>
    internal const int InternalError = -32603;

    /// <summary>The command was understood and refused, which is where "not supported" lives.</summary>
    internal const int ServerError = -32000;

    /// <summary>The message named a <c>sessionId</c> no attachment answers to.</summary>
    /// <remarks>
    /// Chromium's <c>SESSION_NOT_FOUND</c>, one below the generic server error and carrying its own
    /// wording — <c>Session with given id not found.</c> — which clients match on to tell a stale session
    /// apart from a command that failed.
    /// </remarks>
    internal const int SessionNotFound = -32001;
}

/// <summary>
/// A failure that becomes an <c>error</c> member of a protocol response rather than escaping to the host.
/// </summary>
/// <remarks>
/// Anything else a command throws reaches the client as <see cref="ProtocolErrorCodes.ServerError"/>, so
/// raising this is how a domain says which of the protocol's codes its failure actually is.
/// </remarks>
internal sealed class ProtocolException : Exception
{
    internal ProtocolException(int code, string message, string? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }

    internal ProtocolException(string message) : base(message)
    {
        Code = ProtocolErrorCodes.ServerError;
    }

    internal ProtocolException(string message, Exception innerException) : base(message, innerException)
    {
        Code = ProtocolErrorCodes.ServerError;
    }

    internal ProtocolException()
    {
        Code = ProtocolErrorCodes.ServerError;
    }

    /// <summary>Gets the protocol error code this failure answers with.</summary>
    internal int Code { get; }

    /// <summary>
    /// Gets what goes into the error's <c>data</c> member, which is where a deserialization complaint or a
    /// refusal's reason belongs. Named <c>Details</c> because <see cref="Exception.Data"/> is taken.
    /// </summary>
    internal string? Details { get; }
}
