using Acornima.Ast;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;
using Jint.WebApi;
using Jint.WebApi.DomException;
using Jint.WebApi.Messaging;
using Jint.WebApi.Workers;

namespace Jint.Browser.Workers;

/// <summary>Runs shared worker script on the existing worker provider's thread and task-budget pump.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/workers.html#run-a-worker</remarks>
internal sealed class SharedWorkerRun
{
    private readonly SharedWorkerRegistry _registry;
    private readonly SharedWorkerEntry _entry;
    private readonly Engine _engine;
    private readonly int _generation;
    private readonly CancellationTokenSource _termination;
    private bool _closing;

    private SharedWorkerRun(SharedWorkerRegistry registry, SharedWorkerEntry entry, Engine engine,
        CancellationTokenSource termination)
    {
        _registry = registry;
        _entry = entry;
        _engine = engine;
        _generation = engine.EventLoopGeneration;
        _termination = termination;
        // The shared lifetime has no distinguished parent. Do not retain the first owner's engine.
        Connection = new WorkerConnection(engine, engine, entry.Settings.Name, Ended, termination.Token);
    }

    internal WorkerConnection Connection { get; }

    internal static void Start(PageRuntime owner, ThreadPerWorkerProvider provider,
        SharedWorkerRegistry registry, SharedWorkerEntry entry, Uri url)
    {
        var termination = new CancellationTokenSource();
        var request = new WorkerRequest(owner.Engine, url.AbsoluteUri, null, WorkerType.Module,
            entry.Settings.Name, 0, registry.LiveCount, termination.Token);
        var engine = provider.CreateSharedWorkerEngine(request, url,
            entry.Settings.Type == "classic" ? "same-origin" : entry.Settings.Credentials);
        if (engine is null)
        {
            registry.End(entry, WorkerEndReason.StartupFailed);
            return;
        }

        var run = new SharedWorkerRun(registry, entry, engine, termination);
        try
        {
            engine._webApi!.OwningSharedWorker = run.Connection;
            WorkerGlobalScope.InstallShared(run.Connection, run.Close, run.ImportScripts);
            engine.AddToEventLoop(run.StartScript, run._generation, EventLoopJobKind.Task);
            if (!registry.Publish(entry, run))
            {
                run.Connection.End();
                engine.Dispose();
                return;
            }
            provider.StartPump(run.Connection, (exception, name) => registry.ReportError(entry, exception, name));
        }
        catch
        {
            run.Connection.End();
            engine.Dispose();
            throw;
        }
    }

    internal void QueueConnect(MessagePortEndpoint endpoint)
    {
        // Only the endpoint crosses. The port and event are built on the worker's owning thread.
        _engine.AddToEventLoop(() =>
        {
            if (_closing || Connection.IsEnded || endpoint.Closed) return;
            var realm = _engine._mainRealm;
            var port = new JsMessagePort(_engine, realm, endpoint);
            var ev = realm.Intrinsics.MessageEvent.CreateTrustedConnectEvent(port);
            _engine._webApi!.GlobalEventTarget.DispatchEvent(ev);
        }, _generation, EventLoopJobKind.Task);
    }

    private void StartScript()
    {
        if (Connection.IsEnded) return;
        if (_entry.Settings.Type == "classic")
        {
            Prepared<Script> script;
            try
            {
                var source = Loader.LoadScript(_engine, new Uri(_entry.Key.Url), "same-origin");
                script = _engine.ParseForExecution(source, _entry.Key.Url, parsingOptions: null);
            }
            catch (Exception exception) when (!Throw.MustPropagateHostException(exception))
            {
                Fail(exception.Message);
                return;
            }
            try
            {
                _engine.Execute(script);
            }
            catch (JavaScriptException exception)
            {
                _engine._webApi!.FireGlobalErrorEvent(exception);
            }
            _registry.Ready(_entry);
            return;
        }

        try
        {
            var operation = _engine.Modules.StartImport(_entry.Key.Url);
            var promise = (JsPromise) operation.Promise;
            var fulfilled = new ClrFunction(_engine, "", (_, _) =>
            {
                _registry.Ready(_entry);
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);
            var rejected = new ClrFunction(_engine, "", (_, arguments) =>
            {
                var error = arguments.At(0);
                if (_engine.Modules.TryGetRegisteredModule(new Engine.ModuleCacheKey(_entry.Key.Url, []), out var module)
                    && module is CyclicModuleRecord { Status: ModuleStatus.Evaluated })
                {
                    _engine._webApi!.ReportError(_engine._mainRealm, error);
                    _registry.Ready(_entry);
                }
                else
                {
                    Fail(Throw.SafeToDisplayString(error));
                }
                return JsValue.Undefined;
            }, 1, PropertyFlag.Configurable);
            PromiseOperations.PerformPromiseThen(_engine, promise, fulfilled, rejected, resultCapability: null!);
        }
        catch (Exception exception) when (!Throw.MustPropagateHostException(exception))
        {
            Fail(exception.Message);
        }
    }

    private PageModuleLoader Loader
        => _engine.Options.Modules.ModuleLoader as PageModuleLoader
            ?? throw new InvalidOperationException("Shared workers require an HTTP(S) document and script URL.");

    /// <summary>Classic imports run synchronously on the worker thread, never on a page thread.</summary>
    /// <remarks>https://html.spec.whatwg.org/multipage/workers.html#import-scripts-into-worker-global-scope</remarks>
    private JsValue ImportScripts(JsValue receiver, JsValue[] arguments)
    {
        if (_entry.Settings.Type == "module")
        {
            Throw.TypeError(_engine.Realm, "Module scripts don't support importScripts().");
        }
        var urls = new Uri[arguments.Length];
        for (var i = 0; i < urls.Length; i++)
        {
            var parsed = PageUrl.Parse(TypeConverter.ToString(arguments[i]), _entry.Key.Url);
            if (parsed is null || PageUrl.ToUri(parsed) is not { } uri)
            {
                WorkerErrors.ThrowDomException(_engine, _engine.Realm, DomExceptionNames.Syntax,
                    "Invalid importScripts URL.");
                return JsValue.Undefined;
            }
            urls[i] = uri;
        }
        foreach (var url in urls)
        {
            string source;
            try
            {
                source = Loader.LoadScript(_engine, url, "same-origin");
            }
            catch (Exception exception) when (!Throw.MustPropagateHostException(exception))
            {
                WorkerErrors.ThrowDomException(_engine, _engine.Realm, DomExceptionNames.Network, exception.Message);
                return JsValue.Undefined;
            }
            _engine.Execute(source, url.AbsoluteUri);
        }
        return JsValue.Undefined;
    }

    private void Close()
    {
        if (_closing || Connection.IsEnded) return;
        _closing = true;
        _registry.BeginClose(_entry);
        _engine.AddToEventLoop(() => Connection.TryEnd(WorkerEndReason.ClosedByWorker, null),
            _generation, EventLoopJobKind.Microtask);
    }

    private void Fail(string message)
        => Connection.TryEnd(WorkerEndReason.StartupFailed, new InvalidOperationException(message));

    private void Ended(WorkerEndReason reason, List<Action>? deferred)
    {
        _registry.End(_entry, reason);
        if (reason != WorkerEndReason.ClosedByWorker) _termination.Cancel();
    }
}
