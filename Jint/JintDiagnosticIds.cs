namespace Jint;

/// <summary>
/// The diagnostic identifiers Jint attaches to its own API through
/// <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>JINT0001 — declared non-contract.</b> A public member carrying
/// <c>[Experimental("JINT0001")]</c> is reachable and supported to <i>call</i>, but its shape and its
/// answers are explicitly outside Jint's compatibility contract: what it reports names an internal
/// representation, so which value comes back may change in any release and the type may gain members.
/// Neither counts as a breaking change. These members exist for diagnostics, assertions and regression
/// tests; production code must not branch on what they return.
/// </para>
/// <para>
/// The attribute makes that a compiler diagnostic rather than a paragraph of documentation nobody read.
/// A host that has decided it wants one of these anyway acknowledges it the ordinary way:
/// </para>
/// <code>
/// #pragma warning disable JINT0001 // Jint diagnostic API, not part of the compatibility contract
///     var report = engine.Advanced.GetMemoryReport();
/// #pragma warning restore JINT0001
/// </code>
/// <para>
/// Or, for a project that logs a report on every request, once in the project file:
/// <c>&lt;NoWarn&gt;$(NoWarn);JINT0001&lt;/NoWarn&gt;</c>.
/// </para>
/// <para>
/// The identifier is stable: a member marked <c>JINT0001</c> keeps that identifier for as long as it is
/// marked at all, so a suppression a host writes today does not have to be revisited. A future
/// non-contract area gets its own identifier rather than being folded into this one.
/// </para>
/// </remarks>
internal static class JintDiagnosticIds
{
    /// <summary>
    /// A public member whose <i>answers</i> describe an internal representation, and which is therefore
    /// deliberately outside the compatibility contract. See the remarks on <see cref="JintDiagnosticIds"/>.
    /// </summary>
    internal const string NonContractDiagnostic = "JINT0001";
}
