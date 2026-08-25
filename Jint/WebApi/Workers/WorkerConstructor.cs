#if NET8_0_OR_GREATER
using System.Threading;
using Jint.Constraints;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Modules;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;

namespace Jint.WebApi.Workers;

/// <summary>
/// The <c>Worker</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#dedicated-workers-and-the-worker-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>Worker</c> inherits from <c>EventTarget</c>, so its <c>[[Prototype]]</c> is the <c>EventTarget</c>
/// interface object — https://webidl.spec.whatwg.org/#interface-object.
/// </para>
/// <para>
/// <b>The constructor runs entirely on the parent's thread and runs none of the worker's script.</b> What it
/// does is build the request, ask the host's <see cref="WorkerProvider"/> for an engine, validate that engine,
/// entangle a port pair with it, install its global scope, and queue the module import as a job on the
/// <i>worker's</i> event loop. The first pump the host gives that engine is what runs any of it.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class WorkerConstructor : Constructor
{
    private static readonly JsString _functionName = new("Worker");

    /// <summary>The <c>WorkerType</c> enumeration, https://html.spec.whatwg.org/multipage/workers.html#workertype.</summary>
    private const string ClassicType = "classic";

    private const string ModuleType = "module";

    internal WorkerConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new WorkerPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal WorkerPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#dom-worker — the constructor steps, in the design's
    /// order: WebIDL, quota, token, provider, validation, entanglement, global scope, start job, hand-off.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to construct 'Worker': 1 argument required, but only 0 present.");
        }

        // Step 1: WebIDL first, and before anything with a side effect — a badly typed call must not have cost
        // a quota slot or reached the host at all. Jint does not URL-parse the specifier, so the
        // specification's SyntaxError for an unparseable URL becomes whatever the worker's own loader reports
        // later, as a startup failure.
        var specifier = TypeConverter.ToString(arguments[0]);
        var options = ReadOptions(arguments.At(1));

        var registry = _engine._webApi?.Workers;
        if (registry is null)
        {
            // Unreachable: the global that reaches this constructor is installed only where the registry was
            // created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("The Worker global was reached on an engine that has no worker provider.");
            return null!;
        }

        // Step 2: the per-engine backstop. The provider is the policy — this only stops a script manufacturing
        // engines faster than any host policy was written to notice.
        var live = registry.LiveCount;
        if (live >= registry.MaxWorkers)
        {
            WorkerErrors.ThrowQuotaExceededError(
                _engine,
                _realm,
                $"Failed to construct 'Worker': this engine already has {live} live workers, which is its Options.WebApi.Workers.MaxWorkers limit.",
                quota: Math.Max(0, registry.MaxWorkers),
                requested: (double) live + 1);
        }

        // Step 3: minted before the provider is called, so that CreateDefaultOptions can register it on
        // options the provider has not built yet.
        var termination = new CancellationTokenSource();

        var request = new WorkerRequest(
            _engine,
            specifier,
            ReferencingLocation(),
            WorkerType.Module,
            options.Name,
            registry.Depth,
            live,
            termination.Token);

        // Step 4: the host's decision. Anything it throws propagates unchanged — nothing here is translated,
        // because a provider's exception is host code failing and not a script's error.
        var workerEngine = registry.Provider.CreateWorkerEngine(request);
        if (workerEngine is null)
        {
            WorkerErrors.ThrowDomException(
                _engine,
                _realm,
                DomExceptionNames.Security,
                $"Failed to construct 'Worker': the host's worker provider refused to create a worker for '{specifier}'.");
        }

        return CreateWorker(registry, request, workerEngine!, termination, newTarget);
    }

    /// <summary>
    /// Steps 5 to 10, with the worker engine <b>owned</b> throughout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything from here on mutates the worker engine — entangling a port materializes its
    /// <c>MessagePort</c> intrinsics, installing its global scope writes to its global object — so the engine
    /// has to be quiescent, and that is validated rather than trusted: a provider that hands back a pre-warmed
    /// engine another thread is already pumping gets the engine's own concurrent-use exception here, at
    /// <c>new Worker()</c>, instead of silent corruption later.
    /// </para>
    /// <para>
    /// The ownership is released before <see cref="WorkerProvider.OnWorkerStarted"/>, which is where the host
    /// starts pumping: holding it across that call would make the host's very first <c>ProcessTasks</c> on its
    /// own thread the concurrent-use exception.
    /// </para>
    /// </remarks>
    private JsWorker CreateWorker(
        WorkerRegistry registry,
        WorkerRequest request,
        Engine workerEngine,
        CancellationTokenSource termination,
        JsValue newTarget)
    {
        var worker = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Worker.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsWorker(engine, realm),
            (object?) null);

        WorkerLink link;

        using (workerEngine.EnterHostCall())
        {
            // Step 5.
            Validate(workerEngine, request);

            // Steps 6 to 8.
            link = new WorkerLink(
                _engine,
                workerEngine,
                registry,
                worker,
                request.Name,
                request.Specifier,
                request.ReferencingLocation,
                termination);

            worker.Link = link;
            workerEngine._webApi!.OwningWorkerLink = link;

            // The child's own depth, so that a provider which deliberately granted this worker the ability to
            // create workers of its own sees a Depth that says how deep the tree already is.
            if (workerEngine._webApi.Workers is { } childRegistry)
            {
                childRegistry.Depth = registry.Depth + 1;
            }

            link.EnableParentHalf();
            WorkerGlobalScope.Install(link, request.Name);

            // Step 9.
            link.QueueStartJob();
        }

        // Registered before the host is told, so that a connection which ends on the worker's thread the
        // instant the host starts pumping is removed from a list it is already in.
        registry.Add(link);

        // Step 10.
        registry.Provider.OnWorkerStarted(link.Connection);

        return worker;
    }

    /// <summary>
    /// Step 5: what the engine a provider handed back has to be. Every failure is an
    /// <see cref="InvalidOperationException"/> whose message names the fix, because each of them is host code
    /// having made a mistake rather than a script having done anything.
    /// </summary>
    private void Validate(Engine workerEngine, WorkerRequest request)
    {
        if (ReferenceEquals(workerEngine, _engine))
        {
            Throw.InvalidOperationException(
                "WorkerProvider.CreateWorkerEngine returned the parent engine. A worker is a second engine with a global of its own; build one with new Engine(request.CreateDefaultOptions()).");
        }

        if ((workerEngine._webApiFeatures & WebApiFeatures.Messaging) == WebApiFeatures.None
            || workerEngine._webApi is null)
        {
            Throw.InvalidOperationException(
                "WorkerProvider.CreateWorkerEngine returned an engine without WebApiFeatures.Messaging, which the worker's own postMessage is built out of. Start from request.CreateDefaultOptions(), which sets it.");
        }

        if (workerEngine._webApi!.OwningWorkerLink is not null)
        {
            Throw.InvalidOperationException(
                "WorkerProvider.CreateWorkerEngine returned an engine that is already running a worker. Each worker needs an engine of its own — a second connection would give one global two parents.");
        }

        if (!ObservesTerminationToken(workerEngine, request.TerminationToken))
        {
            Throw.InvalidOperationException(
                "WorkerProvider.CreateWorkerEngine returned an engine that does not observe request.TerminationToken, so terminate() could close its ports but never stop its script — a worker that is deaf and mute while still burning a thread. Register it with options.ObserveCancellation(request.TerminationToken), which request.CreateDefaultOptions() already does.");
        }
    }

    /// <summary>
    /// Whether the engine carries a <see cref="CancellationConstraint"/> for this request's token.
    /// </summary>
    /// <remarks>
    /// Every registration is looked at rather than only the first, because a worker whose parent registered a
    /// cancellation token of its own has two of them and the replay puts the parent's beside ours.
    /// </remarks>
    private static bool ObservesTerminationToken(Engine workerEngine, CancellationToken terminationToken)
    {
        foreach (var constraint in workerEngine._constraints)
        {
            if (constraint is CancellationConstraint cancellation && cancellation.Token == terminationToken)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>Module.Location</c> of the module the constructor was reached from, so a provider can resolve a
    /// relative specifier the way <c>import()</c> would have; null when the call came from a script.
    /// </summary>
    private string? ReferencingLocation()
        => _engine.GetActiveScriptOrModule() is Jint.Runtime.Modules.ModuleRecord module ? module.Location : null;

    /// <summary>
    /// The <c>WorkerOptions</c> dictionary, https://html.spec.whatwg.org/multipage/workers.html#workeroptions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WebIDL converts a dictionary's members in <b>lexicographical order of their identifiers</b>
    /// (https://webidl.spec.whatwg.org/#es-dictionary), which is <c>credentials</c>, <c>name</c>, <c>type</c> —
    /// so an invalid <c>credentials</c> is the failure a script sees even when <c>type</c> is wrong too.
    /// </para>
    /// <para>
    /// <c>credentials</c> is validated and then <b>ignored</b>: it only parameterizes the fetch, and the fetch
    /// belongs to the worker engine's own <c>IModuleLoader</c>. Validating it anyway is what keeps a typo a
    /// <c>TypeError</c> here rather than a silently different policy later.
    /// </para>
    /// </remarks>
    private WorkerOptionsValues ReadOptions(JsValue options)
    {
        if (options.IsNullOrUndefined())
        {
            // The dictionary's own defaults, which means type 'classic' — refused below, exactly as an
            // explicit one is.
            ThrowClassicRefusal();
        }

        if (options is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, "Failed to construct 'Worker': The provided value is not of type 'WorkerOptions'.");
            return default;
        }

        var credentials = dictionary.Get("credentials");
        if (!credentials.IsUndefined())
        {
            var value = TypeConverter.ToString(credentials);
            if (value is not ("omit" or "same-origin" or "include"))
            {
                Throw.TypeError(_realm, $"Failed to construct 'Worker': Failed to read the 'credentials' property from 'WorkerOptions': The provided value '{value}' is not a valid enum value of type RequestCredentials.");
            }
        }

        var name = dictionary.Get("name");
        var workerName = name.IsUndefined() ? string.Empty : TypeConverter.ToString(name);

        var type = dictionary.Get("type");
        var typeValue = type.IsUndefined() ? ClassicType : TypeConverter.ToString(type);

        if (typeValue is not (ClassicType or ModuleType))
        {
            // The WebIDL enumeration conversion, https://webidl.spec.whatwg.org/#es-enumeration.
            Throw.TypeError(_realm, $"Failed to construct 'Worker': Failed to read the 'type' property from 'WorkerOptions': The provided value '{typeValue}' is not a valid enum value of type WorkerType.");
        }

        if (string.Equals(typeValue, ClassicType, StringComparison.Ordinal))
        {
            ThrowClassicRefusal();
        }

        return new WorkerOptionsValues(workerName);
    }

    /// <summary>
    /// Jint's own refusal of the specification's own default, and the one message here that names a fix:
    /// there is no classic-script loader to run one with, and a synchronous fetch-and-execute inside a
    /// statement is the one thing this feature family refuses. Every non-browser runtime that ships workers
    /// has converged on module workers for the same reasons.
    /// </summary>
    private void ThrowClassicRefusal()
        => Throw.TypeError(_realm, "Failed to construct 'Worker': Jint runs module workers only. Pass { type: 'module' } — the HTML Standard's default is 'classic', which Jint does not implement.");

    /// <summary>
    /// What the constructor keeps from <c>WorkerOptions</c>. <c>type</c> has already decided whether there is
    /// a worker at all, and <c>credentials</c> is validated and discarded.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct WorkerOptionsValues(string Name);
}
#endif
