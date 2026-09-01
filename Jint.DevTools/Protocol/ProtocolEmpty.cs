namespace Jint.DevTools.Protocol;

/// <summary>
/// The parameters of a command that declares none.
/// </summary>
/// <remarks>
/// It exists so that every generated command virtual has the same two-argument shape, which is what lets the
/// generated dispatch be one switch with no special cases. A client is free to send a <c>params</c> member
/// anyway; the protocol ignores members a command does not declare, and so does this.
/// </remarks>
internal sealed record EmptyParameters
{
    /// <summary>The instance to hand a command that takes no parameters.</summary>
    internal static readonly EmptyParameters Instance = new();
}

/// <summary>
/// The result of a command that returns nothing, which serializes to <c>{}</c>.
/// </summary>
/// <remarks>
/// The protocol has no notion of a void result: a command that succeeds answers with an empty <c>result</c>
/// object, and a client waiting on the response would hang on anything else.
/// </remarks>
internal sealed record EmptyResult
{
    /// <summary>The instance every command returning nothing answers with.</summary>
    internal static readonly EmptyResult Instance = new();
}
