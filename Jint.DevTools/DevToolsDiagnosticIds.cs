namespace Jint.DevTools;

/// <summary>
/// The diagnostic identifiers Jint.DevTools attaches to its own API through
/// <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>JINTDT001 — protocol extension point.</b> A member carrying <c>[Experimental("JINTDT001")]</c> is
/// reachable and supported to <i>call</i>, but its shape follows the Chrome DevTools Protocol rather than
/// this repository's compatibility contract. The protocol is a living document: upstream renames methods,
/// moves them between domains and turns optional parameters into required ones, and a pin bump carries
/// every one of those changes into the generated surface. Neither that nor a domain gaining a command
/// counts as a breaking change here.
/// </para>
/// <para>
/// The attribute makes that a compiler diagnostic rather than a paragraph nobody read. A host that wants
/// one of these anyway acknowledges it the ordinary way:
/// </para>
/// <code>
/// #pragma warning disable JINTDT001 // follows the Chrome DevTools Protocol, not Jint's contract
///     server.AddDomain(new MyDomain());
/// #pragma warning restore JINTDT001
/// </code>
/// <para>
/// It is a separate identifier from Jint's own <c>JINT0001</c> rather than a reuse of it, because the two
/// say different things: <c>JINT0001</c> marks answers that describe an internal representation, while this
/// one marks a surface whose shape an external standard owns. A host suppressing one has not decided
/// anything about the other.
/// </para>
/// <para>
/// Nothing in this assembly is marked yet — the whole protocol surface is <c>internal</c> for now, and the
/// first member promoted out of it will carry this identifier. It is declared ahead of that so the contract
/// is written down before there is pressure to widen something in a hurry.
/// </para>
/// </remarks>
internal static class DevToolsDiagnosticIds
{
    /// <summary>
    /// A member whose shape follows the Chrome DevTools Protocol rather than Jint's compatibility contract.
    /// See the remarks on <see cref="DevToolsDiagnosticIds"/>.
    /// </summary>
    internal const string ProtocolExtensionPoint = "JINTDT001";
}
