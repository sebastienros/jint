using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Messaging;
using Jint.WebApi.Workers;

namespace Jint.Browser.Workers;

/// <summary>The window-only shared worker constructor and its document's ownership lease.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/workers.html#dom-sharedworker</remarks>
internal sealed class SharedWorkerConstructor : Constructor
{
    private static readonly JsObjectShape _shape = new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("SharedWorker")
        .Accessor("port", static (receiver, _) => Brand(receiver).Port)
        .Accessor("onerror",
            static (receiver, _) => EventHandlerAttributes.Get(Brand(receiver), "error"),
            static (receiver, arguments) => EventHandlerAttributes.Set(Brand(receiver), "error", arguments.At(0)))
        .Build();

    private readonly PageRuntime _runtime;
    private readonly ThreadPerWorkerProvider _provider;
    private readonly ObjectInstance _instancePrototype;
    private readonly object _owner = new();
    private int _registeredGeneration = -1;

    private SharedWorkerConstructor(PageRuntime runtime, ThreadPerWorkerProvider provider)
        : base(runtime.Engine, runtime.Engine._mainRealm, new JsString("SharedWorker"))
    {
        _runtime = runtime;
        _provider = provider;
        _prototype = _realm.Intrinsics.EventTarget;
        _instancePrototype = _shape.Instantiate(_engine, _realm.Intrinsics.EventTarget.PrototypeObject);
        _instancePrototype.DefineOwnPropertyUnchecked("constructor", new PropertyDescriptor(this, PropertyFlag.NonEnumerable));
        _prototypeDescriptor = new PropertyDescriptor(_instancePrototype, PropertyFlag.AllForbidden);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    internal static void Install(PageRuntime runtime, ThreadPerWorkerProvider provider)
    {
        // Shared workers are a Browser capability, not a stand-alone Engine/provider contract.
        if ((runtime.Engine.WebApi.Features & WebApiFeatures.Workers) != 0
            && ReferenceEquals(runtime.Engine.Options.WebApi.Workers.Provider, provider))
        {
            runtime.Engine.AddLazyGlobal("SharedWorker", _ => new SharedWorkerConstructor(runtime, provider),
                PropertyFlag.NonEnumerable);
        }
    }

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to construct 'SharedWorker': 1 argument required.");
        }

        var specifier = TypeConverter.ToString(arguments[0]);
        var settings = ReadOptions(arguments.At(1));
        var url = PageUrl.Parse(specifier, _runtime.BaseUri);
        if (url is null || PageUrl.ToUri(url) is not { } uri)
        {
            WorkerErrors.ThrowDomException(_engine, _realm, DomExceptionNames.Syntax,
                "Failed to construct 'SharedWorker': invalid script URL.");
            return null!;
        }

        var constructorOrigin = DomDocumentMetadata.CreatorOrigin(_runtime.Dom);
        var origin = constructorOrigin.Serialized;
        if (url.Scheme != "data" && (origin == PageUrl.OpaqueOrigin || url.SerializeOrigin() != origin))
        {
            WorkerErrors.ThrowDomException(_engine, _realm, DomExceptionNames.Security,
                "Failed to construct 'SharedWorker': the script URL must be same-origin.");
        }

        var registry = _runtime.Page.Context.SharedWorkers;
        var limits = _engine.Options.WebApi.Workers;
        var outer = new MessagePortEndpoint { MaxQueuedMessages = limits.MaxQueuedMessages };
        var inner = new MessagePortEndpoint { MaxQueuedMessages = limits.MaxQueuedMessages };
        MessagePortEndpoint.Entangle(outer, inner);
        var worker = new JsSharedWorker(_engine, _realm, new JsMessagePort(_engine, _realm, outer))
        {
            Prototype = GetPrototypeFromConstructor(newTarget, _ => _instancePrototype),
        };
        var generation = _engine.EventLoopGeneration;
        var client = new SharedWorkerClient(_owner, outer, inner,
            () => _engine.AddToEventLoop(() => worker.FireEvent(new JsString("error")),
                generation, EventLoopJobKind.Task), _runtime.Page.RecordWorkerError);

        // Opaque documents must never match each other's serialized "null" origins.
        var key = new SharedWorkerKey(origin, uri.AbsoluteUri, settings.Name,
            constructorOrigin.IsOpaque ? constructorOrigin : null);
        var entry = registry.Connect(key, settings, client, limits.MaxWorkers, out var created, out var quota);
        if (quota)
        {
            outer.Close();
            inner.Close();
            WorkerErrors.ThrowQuotaExceededError(_engine, _realm,
                "Failed to construct 'SharedWorker': Options.WebApi.Workers.MaxWorkers was reached.",
                Math.Max(0, limits.MaxWorkers), Math.Max(0, limits.MaxWorkers) + 1d);
        }

        if (entry is null)
        {
            client.Fail();
            return worker;
        }

        if (created)
        {
            try
            {
                SharedWorkerRun.Start(_runtime, _provider, registry, entry, uri);
            }
            catch
            {
                registry.End(entry, WorkerEndReason.StartupFailed);
                throw;
            }
        }

        // A snapshot may retain the constructor, but its previous ownership lease was released.
        if (_registeredGeneration != generation)
        {
            _engine._webApi!.ReleaseSharedWorkerOwners += () => registry.Release(_owner);
            _registeredGeneration = generation;
        }
        return worker;
    }

    private SharedWorkerSettings ReadOptions(JsValue options)
    {
        if (options is not ObjectInstance && !options.IsNullOrUndefined())
        {
            return new SharedWorkerSettings(TypeConverter.ToString(options), "classic", "same-origin", false);
        }

        var dictionary = options as ObjectInstance;
        var credentials = dictionary?.Get("credentials") ?? Undefined;
        var credentialMode = credentials.IsUndefined() ? "same-origin" : TypeConverter.ToString(credentials);
        if (credentialMode is not ("omit" or "same-origin" or "include"))
        {
            Throw.TypeError(_realm, "Invalid SharedWorker credentials.");
        }
        var name = dictionary?.Get("name") ?? Undefined;
        var workerName = name.IsUndefined() ? "" : TypeConverter.ToString(name);
        var type = dictionary?.Get("type") ?? Undefined;
        var workerType = type.IsUndefined() ? "classic" : TypeConverter.ToString(type);
        if (workerType is not ("classic" or "module"))
        {
            Throw.TypeError(_realm, "Invalid SharedWorker type.");
        }
        var extendedLifetime = TypeConverter.ToBoolean(dictionary?.Get("extendedLifetime") ?? Undefined);
        return new SharedWorkerSettings(workerName, workerType, credentialMode, extendedLifetime);
    }

    private static JsSharedWorker Brand(JsValue receiver)
    {
        if (receiver is JsSharedWorker worker) return worker;
        Throw.TypeErrorNoEngine("Illegal invocation: receiver is not a SharedWorker");
        return null!;
    }

    private sealed class JsSharedWorker : JsEventTarget
    {
        internal JsSharedWorker(Engine engine, Realm realm, JsMessagePort port) : base(engine, realm)
        {
            Port = port;
        }

        internal JsMessagePort Port { get; }
    }
}
