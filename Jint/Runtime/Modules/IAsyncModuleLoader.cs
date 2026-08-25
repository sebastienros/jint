namespace Jint.Runtime.Modules;

/// <summary>
/// Opt-in extension of <see cref="IModuleLoader"/> for a loader that cannot produce a module synchronously —
/// one fetching module source over HTTP, from a dev server, or out of an asset pipeline. A loader that
/// implements this interface is asked through <see cref="LoadModuleAsync"/> instead of
/// <see cref="IModuleLoader.LoadModule"/>, and no thread is blocked while the load is in flight.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IModuleLoader.Resolve"/> stays synchronous, and is still called on the engine thread: the
/// specification lets
/// <see href="https://tc39.es/ecma262/#sec-HostLoadImportedModule">HostLoadImportedModule</see> take time
/// over <em>fetching</em> a module, not over deciding which module a specifier denotes.
/// </para>
/// <para>
/// The engine keeps at most one load in flight per resolved specifier per engine: several referrers importing
/// the same file while a fetch is airborne attach to that fetch, and a specifier already answerable from the
/// engine's registry or from a module registered through <c>Engine.Modules.Add</c> is never asked at all. A
/// <em>failed</em> load is not recorded, and <c>Engine.Advanced.RestoreGlobalSnapshot</c> discards pending
/// loads, so the loader is asked again after either.
/// </para>
/// <para>
/// A load that must be driven to completion is driven by the engine's event loop, so the host has to give the
/// engine turns: <c>Engine.Modules.ImportAsync</c> awaits them for you,
/// <c>Engine.Modules.StartImport</c> leaves them to a per-frame <c>engine.Tasks.ProcessTasks()</c>,
/// and the synchronous <c>Engine.Modules.Import</c> blocks the calling thread until the graph arrives — which
/// deadlocks if the loader's own completion needs that same thread.
/// </para>
/// </remarks>
public interface IAsyncModuleLoader : IModuleLoader
{
    /// <summary>
    /// Starts loading the module for <paramref name="resolved"/> and returns immediately. The host finishes
    /// the load — now or on any thread, later — by settling <paramref name="completion"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called on the engine thread. Failing to settle <paramref name="completion"/> leaves the importing
    /// promise pending forever; throwing instead of settling it is caught by the engine and turned into the
    /// same rejection <see cref="ModuleLoadCompletion.SetError(Exception)"/> would produce.
    /// </para>
    /// <para>
    /// One class of exception is exempt from that catch, and a host has to know which: the ones that exist to
    /// bound or abort execution — <see cref="ExecutionCanceledException"/>,
    /// <see cref="MemoryLimitExceededException"/>, <see cref="ResultLimitExceededException"/>,
    /// <see cref="StatementsCountOverflowException"/>,
    /// <see cref="TimeoutException"/>, <see cref="OperationCanceledException"/> and
    /// <see cref="OutOfMemoryException"/> — keep propagating, because a constraint that becomes an ordinary
    /// failed import no longer bounds anything. Note what that means for a host cancelling its own fetch:
    /// <see cref="OperationCanceledException"/> (and so <see cref="System.Threading.Tasks.TaskCanceledException"/>)
    /// thrown from here is read as the engine aborting, not as a failed load, and on a queued event-loop turn
    /// it escapes with the importers of that specifier left pending. The same rule applies to
    /// <see cref="ModuleLoadCompletion.SetError(Exception)"/>, faulted tasks and canceled tasks. A host that
    /// deliberately wants cancellation to be an ordinary failed import must report an explicit script-facing
    /// message through <see cref="ModuleLoadCompletion.SetError(string)"/> instead.
    /// </para>
    /// </remarks>
    void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion);
}
