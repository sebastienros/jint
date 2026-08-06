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
/// The engine calls <see cref="LoadModuleAsync"/> at most once per resolved specifier per engine, even when
/// several referrers import the same file, and never for a specifier it can already answer from its own
/// registry or from a module registered through <c>Engine.Modules.Add</c>.
/// </para>
/// <para>
/// A load that must be driven to completion is driven by the engine's event loop, so the host has to give the
/// engine turns: <c>Engine.Modules.ImportAsync</c> awaits them for you,
/// <c>Engine.Modules.StartImport</c> leaves them to a per-frame <c>engine.Advanced.ProcessTasks()</c>,
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
    /// Called on the engine thread. Failing to settle <paramref name="completion"/> leaves the importing
    /// promise pending forever; throwing instead of settling it is caught by the engine and turned into the
    /// same rejection <see cref="ModuleLoadCompletion.SetError(Exception)"/> would produce.
    /// </remarks>
    void LoadModuleAsync(Engine engine, ResolvedSpecifier resolved, ModuleLoadCompletion completion);
}
