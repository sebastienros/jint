namespace Jint.DevTools;

/// <summary>
/// How one <see cref="EngineTarget"/> presents itself to a client, and which thread runs it.
/// </summary>
/// <remarks>
/// Read once, when the target is constructed. Changing an instance afterwards changes nothing about a
/// target already built from it.
/// </remarks>
public sealed class EngineTargetOptions
{
    /// <summary>Creates a set of options, each at its default.</summary>
    public EngineTargetOptions()
    {
    }

    /// <summary>Gets or sets the name a client lists the target under. Defaults to <c>Jint engine</c>.</summary>
    public string Title { get; set; } = "Jint engine";

    /// <summary>Gets or sets the location a client shows for the target. Defaults to the empty string.</summary>
    /// <remarks>
    /// An engine target has no document, so there is nothing honest to put here; a host that runs one named
    /// script per engine is the one that has something worth showing. An absolute filesystem path is
    /// published as a <c>file://</c> URL, which is the same mapping a script's source name goes through.
    /// </remarks>
    public string Url { get; set; } = "";

    /// <summary>Gets or sets which thread runs the engine. Defaults to <see cref="ThreadMode.HostOwned"/>.</summary>
    public ThreadMode ThreadMode { get; set; } = ThreadMode.HostOwned;

    /// <summary>
    /// Gets or sets whether the first work posted to the target is held until a client says to run it.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Node's <c>--inspect-brk</c>: the target reports <c>waitingForDebugger</c> when a session attaches,
    /// and nothing the host posted runs until some session sends <c>Runtime.runIfWaitingForDebugger</c>. A
    /// <see cref="ThreadMode.HostOwned"/> host waits for that with
    /// <see cref="EngineTarget.WaitForDebugger"/>; a <see cref="ThreadMode.LibraryOwned"/> one needs to do
    /// nothing, because its own thread is already pumping.
    /// </remarks>
    public bool WaitForDebuggerOnStart { get; set; }

    /// <summary>
    /// Gets or sets what names a value the protocol has a vocabulary for and this package does not
    /// recognize, or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    /// The seam <c>Jint.Browser</c> answers <c>subtype: "node"</c> through. Internal rather than public
    /// because the type it takes publishes the protocol's own vocabulary, and the first member of this
    /// package's surface to do that is the one that carries <c>JINTDT001</c>; there is no third-party
    /// describer yet to justify making that decision now.
    /// </remarks>
    internal Domains.RemoteObjectDescriber? RemoteObjectDescriber { get; set; }
}
