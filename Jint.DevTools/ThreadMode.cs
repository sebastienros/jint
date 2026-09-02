namespace Jint.DevTools;

/// <summary>
/// Which thread runs the engine behind an <see cref="EngineTarget"/>, and therefore which thread answers
/// the protocol commands addressed to it.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Native.JsValue"/> never leaves the engine thread and a transport thread only ever moves
/// strings, so every command has to cross to whichever thread that is. This says which one it is.
/// </para>
/// <para>
/// It is a property of the target rather than of the server: one server may carry a host-pumped engine and
/// a library-pumped one at the same time.
/// </para>
/// </remarks>
public enum ThreadMode
{
    /// <summary>
    /// The host's own thread runs script and pumps; commands are answered when it next pumps.
    /// </summary>
    /// <remarks>
    /// The default, and the shape of every embedder that already drives its engine — a workflow step, a
    /// request handler, a game loop. Commands are serviced inside
    /// <see cref="Engine.TaskOperations.ProcessTasks"/> or <see cref="EngineTarget.Pump"/>; one that waits
    /// longer than <see cref="DevToolsServerOptions.CommandTimeout"/> fails with <c>-32000</c> and
    /// <c>Engine is not being pumped</c>, which is the diagnostic a host that forgot to pump needs.
    /// </remarks>
    HostOwned,

    /// <summary>
    /// The target owns one thread, which pumps the engine and answers commands on it.
    /// </summary>
    /// <remarks>
    /// For a host that wants an engine attachable without writing a loop. The host submits its own work
    /// with <see cref="EngineTarget.Post(System.Action{Engine})"/> or
    /// <see cref="EngineTarget.PostAsync{T}(System.Func{Engine, T})"/>; touching the engine from any other
    /// thread trips the engine's own single-drainer guard rather than corrupting anything.
    /// </remarks>
    LibraryOwned,
}
