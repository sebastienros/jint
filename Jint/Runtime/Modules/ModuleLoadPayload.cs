using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Runtime.Modules;

/// <summary>
/// The <c>payload</c> argument threaded through
/// <see href="https://tc39.es/ecma262/#sec-HostLoadImportedModule">HostLoadImportedModule</see> and handed
/// back to <see href="https://tc39.es/ecma262/#sec-FinishLoadingImportedModule">FinishLoadingImportedModule</see>.
/// </summary>
/// <remarks>
/// The spec's FinishLoadingImportedModule branches on whether the payload is a GraphLoadingState Record
/// (continue the load phase) or a PromiseCapability Record (continue a dynamic import); this models the
/// branch as a virtual call instead, so a third kind of load — the engine's own
/// <c>Engine.ModuleOperations.Import</c>, which needs the module record itself rather than a namespace — is
/// a third subclass rather than a third arm of an <c>if</c>.
/// <para>
/// <see cref="Continue"/> always runs on the engine thread. A host that finishes a load from a background
/// thread reaches it through <see cref="ModuleLoadCompletion"/>, which marshals onto the event loop.
/// </para>
/// </remarks>
internal abstract class ModuleLoadPayload
{
    /// <summary>
    /// Consumes the completion of one <c>HostLoadImportedModule</c> call: exactly one of
    /// <paramref name="module"/> (a normal completion) and <paramref name="error"/> (a throw completion) is
    /// non-null.
    /// </summary>
    internal abstract void Continue(Module? module, JsValue? error);
}

/// <summary>
/// https://tc39.es/ecma262/#graphloadingstate-record
/// </summary>
internal sealed class GraphLoadingState : ModuleLoadPayload
{
    internal GraphLoadingState(PromiseCapability promiseCapability)
    {
        PromiseCapability = promiseCapability;
    }

    internal PromiseCapability PromiseCapability { get; }

    /// <summary>[[IsLoading]]</summary>
    internal bool IsLoading { get; set; } = true;

    /// <summary>[[PendingModulesCount]] — starts at one, for the root of the load itself.</summary>
    internal int PendingModulesCount { get; set; } = 1;

    /// <summary>
    /// [[Visited]]. A list rather than a set on purpose: every <see cref="Module"/> inherits
    /// <see cref="JsValue.GetHashCode"/>, which answers from the value's type alone, so a hash set of module
    /// records is one bucket scanned linearly anyway — with the added downside of looking like it is not.
    /// The graph reached by a single load is small.
    /// </summary>
    internal List<CyclicModule> Visited { get; } = [];

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ContinueModuleLoading
    /// </summary>
    internal override void Continue(Module? module, JsValue? error)
    {
        // Step 1: If state.[[IsLoading]] is false, return unused. Reached when an earlier sibling has
        // already failed the load and rejected the capability, and this completion belongs to a request
        // that was still in flight at the time.
        if (!IsLoading)
        {
            return;
        }

        if (error is null)
        {
            CyclicModule.InnerModuleLoading(this, module!);
        }
        else
        {
            IsLoading = false;
            PromiseCapability.Reject(error);
        }
    }

    /// <summary>
    /// Steps 3-5 of <see href="https://tc39.es/ecma262/#sec-InnerModuleLoading">InnerModuleLoading</see>:
    /// account for one settled request and, when the last one settles, promote every visited module out of
    /// <see cref="ModuleStatus.New"/> and resolve the load promise.
    /// </summary>
    internal void SettleOnePendingModule()
    {
        if (PendingModulesCount < 1)
        {
            Throw.InvalidOperationException("Error while loading module: pending module count underflow");
        }

        PendingModulesCount--;
        if (PendingModulesCount != 0)
        {
            return;
        }

        IsLoading = false;
        foreach (var loaded in Visited)
        {
            loaded.OnGraphLoaded();
        }

        PromiseCapability.Resolve(JsValue.Undefined);
    }
}

/// <summary>
/// The dynamic-<c>import()</c> payload: a promise capability handed to
/// <see href="https://tc39.es/ecma262/#sec-ContinueDynamicImport">ContinueDynamicImport</see>, which owns the
/// rest of the pipeline — load the requested modules, link, evaluate, resolve with the namespace.
/// </summary>
internal sealed class DynamicImportPayload : ModuleLoadPayload
{
    private readonly Engine _engine;
    private readonly ModuleRequest _moduleRequest;

    internal DynamicImportPayload(Engine engine, ModuleRequest moduleRequest, PromiseCapability promiseCapability)
    {
        _engine = engine;
        _moduleRequest = moduleRequest;
        PromiseCapability = promiseCapability;
    }

    internal PromiseCapability PromiseCapability { get; }

    internal override void Continue(Module? module, JsValue? error)
    {
        if (error is not null)
        {
            PromiseCapability.Reject(error);
            return;
        }

        _engine._host.ContinueDynamicImport(module!, _moduleRequest, PromiseCapability);
    }
}

/// <summary>
/// The payload used when the engine itself needs the loaded module record rather than a namespace. The
/// synchronous <c>Engine.ModuleOperations.Import</c> and its asynchronous counterparts all start by loading
/// the root of the graph, which is a <c>HostLoadImportedModule</c> call like any other and may therefore
/// complete on a later event-loop turn.
/// </summary>
internal sealed class RootModuleLoadPayload : ModuleLoadPayload
{
    /// <summary>The loaded module, or null while the load is in flight or has failed.</summary>
    internal Module? Module { get; private set; }

    /// <summary>The error the load failed with, or null.</summary>
    internal JsValue? Error { get; private set; }

    internal bool IsCompleted { get; private set; }

    internal override void Continue(Module? module, JsValue? error)
    {
        IsCompleted = true;
        Module = module;
        Error = error;
    }
}
