using System.Runtime.InteropServices;
using Jint.Native;

namespace Jint.DevTools.Domains;

/// <summary>
/// The one hook a package layered on this one has into how a value is described to a client.
/// </summary>
/// <remarks>
/// <para>
/// This package knows the language's values and nothing else, so a DOM node reaches a client as an ordinary
/// object. The protocol has a vocabulary for it — <c>subtype: "node"</c>, and a description a front end
/// renders as <c>div#id.cls</c> — and the package that owns the DOM is the only one that can fill it in.
/// <c>Jint.Browser</c> is that package: AngleSharp plus Jint, never a DOM stack of its own.
/// </para>
/// <para>
/// A describer is consulted <b>first</b>, for every non-primitive value, and answering
/// <see langword="false"/> leaves the ordinary description untouched. It runs on the engine thread with the
/// value in hand, and it is held to the same promise as everything else on that path: <b>describing a value
/// executes none of that value's code</b>. A describer that reads a script-visible accessor breaks the one
/// invariant a client relies on while paused.
/// </para>
/// </remarks>
internal abstract class RemoteObjectDescriber
{
    /// <summary>Names <paramref name="value"/> in the protocol's vocabulary, or declines.</summary>
    /// <param name="value">The value about to be described. Never a primitive.</param>
    /// <param name="hint">What to say about it, when the answer is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when this describer recognized the value.</returns>
    internal abstract bool TryDescribe(JsValue value, out RemoteObjectHint hint);
}

/// <summary>
/// What a <see cref="RemoteObjectDescriber"/> says about a value it recognized.
/// </summary>
/// <remarks>
/// Every member is optional, and one left <see langword="null"/> keeps whatever this package worked out on
/// its own — so a describer that only knows the subtype says only the subtype.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct RemoteObjectHint
{
    /// <summary>Gets the protocol subtype, such as <c>node</c>.</summary>
    internal string? Subtype { get; init; }

    /// <summary>Gets the class name a client shows, such as <c>HTMLDivElement</c>.</summary>
    internal string? ClassName { get; init; }

    /// <summary>Gets the one-line description, such as <c>div#id.cls</c>.</summary>
    internal string? Description { get; init; }
}
