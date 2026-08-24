#if NET8_0_OR_GREATER
using System.Collections.Concurrent;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.WebApi.Idle;
using Jint.WebApi.Scheduling;
using Jint.WebApi.Timers;
using Jint.WebApi.Workers;

namespace Jint.WebApi;

/// <summary>
/// Installs the globals for the web APIs an engine opted into. Invoked from <c>Options.Apply</c>, which is
/// the sanctioned conditional-install site — the same one <c>Interop.Enabled</c> and
/// <c>Modules.RegisterRequire</c> use — and from <c>Engine.Advanced.EnableWebApis</c>, which is the same
/// work applied to an engine that already exists.
/// </summary>
/// <remarks>
/// <para>
/// Each global is a <see cref="LazyPropertyDescriptor{T}"/> installed through
/// <c>GlobalObject.SetProperty</c>, which is what <c>Options.AddLazyGlobal</c> does and for the same two
/// reasons: <c>SetProperty</c> bumps the own-property version every global-identifier and member-read inline
/// cache revalidates against, and a lazy descriptor is what <c>Engine.Advanced.RestoreGlobalSnapshot</c> can
/// return to its unmaterialized state, so an engine whose script never named the global has still built
/// nothing and a restore puts it back to having built nothing.
/// </para>
/// <para>
/// <b>That is not the same as rebuilding the object, and this file used to say it was.</b> A restore reverts
/// the <i>descriptor</i>; the next read therefore runs the value factory a second time — but every factory
/// here is <c>e =&gt; e.Realm.Intrinsics.Something</c>, and those intrinsics memoize per realm, so the second
/// run hands back the object the previous cycle had, monkey-patches and all. It is exactly what
/// <c>globalThis.Math</c> and <c>globalThis.JSON</c> do, and for exactly the same reason: they are installed
/// by the same emitted lazy descriptor over the same memo (see <c>GlobalObject.Properties.cs</c>). Reverting
/// them would mean re-creating the realm, which is <c>new Engine</c> — and would not even isolate, since a
/// singleton's methods live on an interface prototype that is a separate intrinsic and would survive
/// regardless. <c>RestoreGlobalSnapshot</c> is honest about this: object graphs behind restored bindings are
/// on its documented list of things it does not revert, and a web-API singleton is one of them.
/// </para>
/// <para>
/// The rule for a host reading this and installing globals of its own: <b>a restore reverts a descriptor,
/// never whatever that descriptor's factory reads from.</b> A factory that constructs gives the next cycle a
/// fresh object — Jint's own <c>process</c> shim is the one built-in global of that shape. A factory that
/// hands back something it is holding gives the next cycle what the previous one mutated.
/// </para>
/// <para>
/// Nothing here touches anything but the principal realm's global object, so a <c>ShadowRealm</c> never
/// carries these globals. That is deliberately more conservative than a browser, where the web APIs are
/// <c>[Exposed=*]</c>; a host that wants them inside a shadow realm has <c>Host.InitializeShadowRealm</c>.
/// </para>
/// </remarks>
internal static class WebApiRegistration
{
    internal static void Apply(Options options, Engine engine)
    {
        var features = ExpandFeatures(options.WebApi.Features);

        // Recorded on the engine because two host APIs — Engine.Advanced.CreateMessagePortPair and
        // Engine.Advanced.EnableWebApis — have to be able to ask what an engine already carries, and Options
        // is shareable so it cannot be asked later.
        engine._webApiFeatures = features;

        CreateEngineState(options, engine, features);
        InstallGlobals(engine, features);
    }

    /// <summary>
    /// The post-construction door: <c>Engine.Advanced.EnableWebApis</c>. Additive only, and deliberately the
    /// very same closure, state-creation and install code <see cref="Apply"/> runs — the only differences are
    /// that the state may have to be <i>extended</i> rather than created, and that the install lands on an
    /// engine whose inline caches may already hold a resolved binding for one of these names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole feature set is re-installed rather than only the newly added flags, because several globals
    /// are conditioned on a <i>pair</i> of features — <c>TextDecoderStream</c> needs both
    /// <see cref="WebApiFeatures.Encoding"/> and <see cref="WebApiFeatures.Streams"/> — so adding one half
    /// completes a pair the other half could not install on its own. <see cref="Install"/> leaves every name
    /// the global already owns alone, and does so with a probe, so re-running it neither replaces an
    /// already-installed global nor materializes an unread one.
    /// </para>
    /// <para>
    /// <paramref name="configure"/> runs only when something is actually being enabled, so that a call naming
    /// nothing new is a no-op in every sense rather than one that still mutates the options.
    /// </para>
    /// </remarks>
    /// <returns>The features this call added, after closure expansion; <see cref="WebApiFeatures.None"/> when
    /// everything asked for was already present.</returns>
    internal static WebApiFeatures ApplyLive(Engine engine, WebApiFeatures requested, Action<Options.WebApiOptions>? configure)
    {
        var existing = engine._webApiFeatures;

        // The closure is monotone and `existing` has already been through it, so expanding the request alone
        // gives the same answer as expanding the union — and this is the one place the rules live.
        var added = ExpandFeatures(requested) & ~existing;
        if (added == WebApiFeatures.None)
        {
            return WebApiFeatures.None;
        }

        // Touching the WebApi property allocates the group if the engine was built from options that never
        // named a web API — a host-thread act, like every other option mutation, and the only place engine
        // code ever does it. That is exactly what this call is: the host asking, on the engine's own thread,
        // for something the options never said.
        var options = engine.Options;
        if (configure is not null)
        {
            // The engine froze its options when it was built, and this callback is documented to configure
            // the very group the engine reads its web-API settings from — the one sanctioned write to an
            // engine's own options after construction. The suspension is scoped to this group's subtree and
            // to this thread; see Options.BeginLiveWebApiConfiguration for why the group's own flag is the
            // wrong thing to toggle when the Options may be shared with engines being built right now.
            var webApi = options.WebApi;
            var previous = Options.BeginLiveWebApiConfiguration(webApi);
            try
            {
                configure(webApi);
            }
            finally
            {
                Options.EndLiveWebApiConfiguration(previous);
            }
        }

        var combined = existing | added;
        engine._webApiFeatures = combined;

        var state = engine._webApi;
        if (state is null)
        {
            CreateEngineState(options, engine, combined);
        }
        else
        {
            // `added` rather than `combined`: the options group belonging to a feature that was already on has
            // been read once already, and reading it again is exactly the "read once, when the engine is
            // built" promise every one of those settings makes.
            ExtendEngineState(options, engine, state, added);
        }

        InstallGlobals(engine, combined);
        return added;
    }

    private static void InstallGlobals(Engine engine, WebApiFeatures features)
    {
        // The PRINCIPAL realm, deliberately, and not Engine.Realm: during construction the two are the same,
        // but the live door can be called from anywhere — including a host callback running inside a
        // ShadowRealm — and these globals belong to the engine's own realm and to no other.
        var global = engine._mainRealm.GlobalObject;

        if (features == WebApiFeatures.None)
        {
            // Reachable only for an engine whose host set a diagnostics sink and named no feature: it gets the
            // reporting channel the state carries and no globals whatever — not even DOMException, which
            // exists to let a web API report a failure to script and here there is no web API to have one.
            return;
        }

        // DOMException has no feature flag of its own: it is how every other web API reports a failure, so it
        // exists whenever any of them does. As a WebIDL interface object it is writable and configurable but
        // NOT enumerable — https://webidl.spec.whatwg.org/#es-interfaces.
        Install(global, engine, "DOMException", static e => e.Realm.Intrinsics.DomException, PropertyFlag.NonEnumerable);

        // And its one derived interface, https://webidl.spec.whatwg.org/#quotaexceedederror, for the same
        // reason and under the same (absent) flag: several of the features below refuse a request for want of
        // room, and this is the shape WebIDL now gives that refusal. `e.constructor === QuotaExceededError` is
        // how a script — and web-platform-tests' own assert_throws_quotaexceedederror — tells the interface
        // apart from a plain DOMException wearing the name, so the interface object has to be reachable
        // wherever one can be thrown.
        Install(global, engine, "QuotaExceededError", static e => e.Realm.Intrinsics.QuotaExceededError, PropertyFlag.NonEnumerable);

        if ((features & WebApiFeatures.Console) != WebApiFeatures.None)
        {
            // A WebIDL namespace object is exposed through an accessor pair; installing it as an ordinary
            // enumerable data property is a deliberate simplification, documented on ConsoleInstance.
            Install(global, engine, "console", static e => e.Realm.Intrinsics.Console, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.Timers) != WebApiFeatures.None)
        {
            // WebIDL operations on the global: writable, enumerable and configurable —
            // https://webidl.spec.whatwg.org/#es-operations.
            Install(global, engine, "setTimeout", static e => e.Realm.Intrinsics.Timers.SetTimeout, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "setInterval", static e => e.Realm.Intrinsics.Timers.SetInterval, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "clearTimeout", static e => e.Realm.Intrinsics.Timers.ClearTimeout, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "clearInterval", static e => e.Realm.Intrinsics.Timers.ClearInterval, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "queueMicrotask", static e => e.Realm.Intrinsics.Timers.QueueMicrotask, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.Encoding) != WebApiFeatures.None)
        {
            Install(global, engine, "TextDecoder", static e => e.Realm.Intrinsics.TextDecoder, PropertyFlag.NonEnumerable);
            Install(global, engine, "TextEncoder", static e => e.Realm.Intrinsics.TextEncoder, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Base64) != WebApiFeatures.None)
        {
            // Operations of a WebIDL interface mixin on the global are enumerable, unlike interface
            // objects — https://webidl.spec.whatwg.org/#es-operations.
            Install(global, engine, "atob", static e => e.Realm.Intrinsics.Atob, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "btoa", static e => e.Realm.Intrinsics.Btoa, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.StructuredClone) != WebApiFeatures.None)
        {
            // A WebIDL operation on the global is a writable, enumerable, configurable data property —
            // https://webidl.spec.whatwg.org/#es-operations.
            Install(global, engine, "structuredClone", static e => e.Realm.Intrinsics.StructuredClone, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.Files) != WebApiFeatures.None)
        {
            Install(global, engine, "Blob", static e => e.Realm.Intrinsics.Blob, PropertyFlag.NonEnumerable);
            Install(global, engine, "File", static e => e.Realm.Intrinsics.File, PropertyFlag.NonEnumerable);
            Install(global, engine, "FormData", static e => e.Realm.Intrinsics.FormData, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Url) != WebApiFeatures.None)
        {
            Install(global, engine, "URL", static e => e.Realm.Intrinsics.WebApiUrl, PropertyFlag.NonEnumerable);
            Install(global, engine, "URLSearchParams", static e => e.Realm.Intrinsics.WebApiUrlSearchParams, PropertyFlag.NonEnumerable);

            // URLPattern rides this flag rather than carrying one of its own: it is the same standard family, it
            // is defined entirely in terms of the URL parser and its component canonicalization, and a pattern is
            // matched against a URL — so an engine that has no URL has nothing for it to be useful on.
            Install(global, engine, "URLPattern", static e => e.Realm.Intrinsics.WebApiUrlPattern, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Events) != WebApiFeatures.None)
        {
            Install(global, engine, "Event", static e => e.Realm.Intrinsics.Event, PropertyFlag.NonEnumerable);
            Install(global, engine, "CustomEvent", static e => e.Realm.Intrinsics.CustomEvent, PropertyFlag.NonEnumerable);
            Install(global, engine, "EventTarget", static e => e.Realm.Intrinsics.EventTarget, PropertyFlag.NonEnumerable);
            Install(global, engine, "AbortController", static e => e.Realm.Intrinsics.AbortController, PropertyFlag.NonEnumerable);
            Install(global, engine, "AbortSignal", static e => e.Realm.Intrinsics.AbortSignal, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.GlobalEvents) != WebApiFeatures.None)
        {
            // WebIDL operations on the global: writable, enumerable and configurable —
            // https://webidl.spec.whatwg.org/#es-operations. A browser's Window inherits these three from
            // EventTarget.prototype instead, because its global implements EventTarget; ours does not, and
            // GlobalEventTarget says why.
            Install(global, engine, "addEventListener", static e => e.Realm.Intrinsics.GlobalEventFunctions.AddEventListener, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "removeEventListener", static e => e.Realm.Intrinsics.GlobalEventFunctions.RemoveEventListener, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "dispatchEvent", static e => e.Realm.Intrinsics.GlobalEventFunctions.DispatchEvent, PropertyFlag.ConfigurableEnumerableWritable);

            // HTML exposes `self` through a [Replaceable] accessor pair on Window; an ordinary enumerable data
            // property is the same simplification console, crypto and navigator are installed with. The
            // PRINCIPAL realm's global object, deliberately: the property lives on that object, so answering
            // with whichever realm happens to be current when it is first read could make `self === globalThis`
            // false. https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-self
            //
            // Window's shape is the only one that can be decided here, because an engine being built does not
            // know yet whether it is going to be a worker — and WorkerGlobalScope.self is a plain
            // `readonly attribute` with no [Replaceable]
            // (https://html.spec.whatwg.org/multipage/workers.html#dom-workerglobalscope-self). So the
            // descriptor is recorded, and WorkerGlobalScope.Install replaces exactly it and nothing else. That
            // identity is what keeps the replacement non-clobbering: a `self` the host owns is never the one
            // recorded here, because this call left it alone and recorded null.
            //
            // Recorded only when the install actually happened, never unconditionally: ApplyLive re-runs this
            // whole method for an engine that already has `self`, and writing back the null that second call
            // returns would erase a record a worker still needs.
            if (Install(global, engine, "self", static e => e._mainRealm.GlobalObject, PropertyFlag.ConfigurableEnumerableWritable) is { } installedSelf)
            {
                engine._webApi!.InstalledSelf = installedSelf;
            }

            // The two event interfaces the engine fires at that target. Ordinary WebIDL interface objects:
            // writable and configurable but not enumerable — https://webidl.spec.whatwg.org/#es-interfaces.
            Install(global, engine, "ErrorEvent", static e => e.Realm.Intrinsics.ErrorEvent, PropertyFlag.NonEnumerable);
            Install(global, engine, "PromiseRejectionEvent", static e => e.Realm.Intrinsics.PromiseRejectionEvent, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.FetchEvents) != WebApiFeatures.None)
        {
            // A listener that cannot build a Response has nothing to respond with, so this feature installs
            // the object model exactly as SetFetchHandler does — and, like it, pointedly not `fetch`. Unlike
            // it, the install happens while the engine is being built, so a module that constructs a Response
            // at top level works without the host having had to register anything first.
            InstallFetchModel(engine);

            Install(global, engine, "FetchEvent", static e => e.Realm.Intrinsics.FetchEvent, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Crypto) != WebApiFeatures.None)
        {
            // WebIDL exposes crypto through a [Replaceable] accessor pair; an ordinary enumerable data
            // property is the same simplification console is installed with, documented on CryptoPrototype.
            Install(global, engine, "crypto", static e => e.Realm.Intrinsics.CryptoObject, PropertyFlag.ConfigurableEnumerableWritable);

            // The two interface objects behind those, so that `crypto instanceof Crypto` and
            // `crypto.subtle instanceof SubtleCrypto` are writable — WinterTC §5.1 lists both, and neither is
            // constructible, which is what their own IDL says. SubtleCrypto is [SecureContext] in a browser and
            // is exposed unconditionally here for the reason SubtleCryptoConstructor gives: an embedded engine
            // has no origin and no transport for the bit to describe.
            Install(global, engine, "Crypto", static e => e.Realm.Intrinsics.Crypto, PropertyFlag.NonEnumerable);
            Install(global, engine, "SubtleCrypto", static e => e.Realm.Intrinsics.SubtleCrypto, PropertyFlag.NonEnumerable);

            // The interface object of the keys crypto.subtle hands out, so that `key instanceof CryptoKey`
            // works. It is not constructible, which is what its own IDL says.
            Install(global, engine, "CryptoKey", static e => e.Realm.Intrinsics.CryptoKey, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Performance) != WebApiFeatures.None)
        {
            Install(global, engine, "performance", static e => e.Realm.Intrinsics.PerformanceObject, PropertyFlag.ConfigurableEnumerableWritable);

            // Its interface object, which WinterTC §5.1 lists and which `performance instanceof Performance`
            // needs. Not constructible, and — see PerformanceConstructor — it does not claim the EventTarget
            // this interface inherits from in the specification, because nothing here fires an event at it.
            Install(global, engine, "Performance", static e => e.Realm.Intrinsics.Performance, PropertyFlag.NonEnumerable);

            // The entry types are ordinary WebIDL interface objects — a script holds a mark and asks
            // `entry instanceof PerformanceMark`, which only works if the interface object is reachable.
            Install(global, engine, "PerformanceEntry", static e => e.Realm.Intrinsics.PerformanceEntry, PropertyFlag.NonEnumerable);
            Install(global, engine, "PerformanceMark", static e => e.Realm.Intrinsics.PerformanceMark, PropertyFlag.NonEnumerable);
            Install(global, engine, "PerformanceMeasure", static e => e.Realm.Intrinsics.PerformanceMeasure, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Navigator) != WebApiFeatures.None)
        {
            // WebIDL exposes navigator through a [Replaceable] accessor pair; an ordinary enumerable data
            // property is the same simplification console and crypto are installed with, documented on
            // NavigatorPrototype.
            Install(global, engine, "navigator", static e => e.Realm.Intrinsics.NavigatorObject, PropertyFlag.ConfigurableEnumerableWritable);

            // Its interface object, which `navigator instanceof Navigator` needs and which is where
            // `userAgent` actually lives. HTML declares it [Exposed=Window] with no constructor operation, so
            // it is a function that refuses to construct; NavigatorConstructor says why an engine whose global
            // is not a Window carries the name anyway, and Node 24 — whose global is not one either — agrees.
            Install(global, engine, "Navigator", static e => e.Realm.Intrinsics.Navigator, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Fetch) != WebApiFeatures.None)
        {
            InstallFetchModel(engine);

            // A WebIDL operation on the global is a writable, enumerable, configurable data property, unlike
            // the interface objects above — https://webidl.spec.whatwg.org/#es-operations.
            Install(global, engine, "fetch", static e => e.Realm.Intrinsics.Fetch, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.Streams) != WebApiFeatures.None)
        {
            // Every interface the Streams Standard declares [Exposed=*], which is all thirteen of them. The
            // five a script constructs by name come first; the eight below are the readers, the writer, the
            // four controllers and ReadableStreamBYOBRequest, none of which a script builds itself but each of
            // which it names — for an `instanceof`, for a prototype patch, or for the feature detection a
            // library performing stream work starts with. They used to be reachable only as
            // Object.getPrototypeOf(stream.getReader()).constructor, which made `reader instanceof
            // ReadableStreamDefaultReader` unwritable while the object it would have named was sitting right
            // there. Each is a lazy descriptor like every other global here, so the widening costs an engine
            // that never mentions one exactly eight property slots and no object at all.
            Install(global, engine, "ReadableStream", static e => e.Realm.Intrinsics.ReadableStream, PropertyFlag.NonEnumerable);
            Install(global, engine, "WritableStream", static e => e.Realm.Intrinsics.WritableStream, PropertyFlag.NonEnumerable);
            Install(global, engine, "TransformStream", static e => e.Realm.Intrinsics.TransformStream, PropertyFlag.NonEnumerable);
            Install(global, engine, "ByteLengthQueuingStrategy", static e => e.Realm.Intrinsics.ByteLengthQueuingStrategy, PropertyFlag.NonEnumerable);
            Install(global, engine, "CountQueuingStrategy", static e => e.Realm.Intrinsics.CountQueuingStrategy, PropertyFlag.NonEnumerable);

            // Constructible, because the Streams Standard gives all three a constructor operation taking the
            // stream to lock — https://streams.spec.whatwg.org/#default-reader-constructor and its siblings.
            Install(global, engine, "ReadableStreamDefaultReader", static e => e.Realm.Intrinsics.ReadableStreamDefaultReader, PropertyFlag.NonEnumerable);
            Install(global, engine, "ReadableStreamBYOBReader", static e => e.Realm.Intrinsics.ReadableStreamBYOBReader, PropertyFlag.NonEnumerable);
            Install(global, engine, "WritableStreamDefaultWriter", static e => e.Realm.Intrinsics.WritableStreamDefaultWriter, PropertyFlag.NonEnumerable);

            // Not constructible: an interface that declares no constructor operation still has an interface
            // object, and that object refuses to construct — https://webidl.spec.whatwg.org/#es-interface-call.
            // ReadableStreamBYOBRequest joined them in whatwg/streams#870, which took away a constructor that
            // could build a request out of step with its stream.
            Install(global, engine, "ReadableStreamDefaultController", static e => e.Realm.Intrinsics.ReadableStreamDefaultController, PropertyFlag.NonEnumerable);
            Install(global, engine, "ReadableByteStreamController", static e => e.Realm.Intrinsics.ReadableByteStreamController, PropertyFlag.NonEnumerable);
            Install(global, engine, "ReadableStreamBYOBRequest", static e => e.Realm.Intrinsics.ReadableStreamBYOBRequest, PropertyFlag.NonEnumerable);
            Install(global, engine, "WritableStreamDefaultController", static e => e.Realm.Intrinsics.WritableStreamDefaultController, PropertyFlag.NonEnumerable);
            Install(global, engine, "TransformStreamDefaultController", static e => e.Realm.Intrinsics.TransformStreamDefaultController, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Scheduler) != WebApiFeatures.None)
        {
            // WebIDL exposes scheduler through a [Replaceable] accessor pair; an ordinary enumerable data
            // property is the same simplification console, crypto and performance are installed with, and it
            // is documented on SchedulerPrototype.
            Install(global, engine, "scheduler", static e => e.Realm.Intrinsics.SchedulerObject, PropertyFlag.ConfigurableEnumerableWritable);

            // The interface object of that singleton, alongside the three this API already exposed. Not
            // constructible: https://wicg.github.io/scheduling-apis/#sec-scheduler declares no constructor
            // operation, unlike TaskController and TaskPriorityChangeEvent below.
            Install(global, engine, "Scheduler", static e => e.Realm.Intrinsics.Scheduler, PropertyFlag.NonEnumerable);

            Install(global, engine, "TaskController", static e => e.Realm.Intrinsics.TaskController, PropertyFlag.NonEnumerable);
            Install(global, engine, "TaskSignal", static e => e.Realm.Intrinsics.TaskSignal, PropertyFlag.NonEnumerable);
            Install(global, engine, "TaskPriorityChangeEvent", static e => e.Realm.Intrinsics.TaskPriorityChangeEvent, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Messaging) != WebApiFeatures.None)
        {
            Install(global, engine, "MessageChannel", static e => e.Realm.Intrinsics.MessageChannel, PropertyFlag.NonEnumerable);
            Install(global, engine, "MessagePort", static e => e.Realm.Intrinsics.MessagePort, PropertyFlag.NonEnumerable);
            Install(global, engine, "MessageEvent", static e => e.Realm.Intrinsics.MessageEvent, PropertyFlag.NonEnumerable);

            // BroadcastChannel rides this flag rather than carrying one of its own: it is the same section of
            // the same standard, it delivers the same MessageEvent, and it is the same structured clone across
            // the same event loop — the only difference is that it addresses a name instead of a peer.
            Install(global, engine, "BroadcastChannel", static e => e.Realm.Intrinsics.BroadcastChannel, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Workers) != WebApiFeatures.None && engine._webApi?.Workers is not null)
        {
            // The one global in this file conditioned on something other than a flag, and deliberately: with
            // the feature on and no provider there is no execution resource for a worker to run on, so the
            // constructor could do nothing but throw. Absent rather than throwing is this family's convention,
            // and it is what lets a script feature-detect with `typeof Worker`.
            Install(global, engine, "Worker", static e => e.Realm.Intrinsics.Worker, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Reporting) != WebApiFeatures.None)
        {
            // A WebIDL operation on the global — https://webidl.spec.whatwg.org/#es-operations. Installed
            // whether or not a sink exists to hear it, so that feature detection sees the same surface either
            // way and a script written for a browser does not have to guard the call.
            Install(global, engine, "reportError", static e => e.Realm.Intrinsics.ReportError, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.Storage) != WebApiFeatures.None)
        {
            Install(global, engine, "Storage", static e => e.Realm.Intrinsics.Storage, PropertyFlag.NonEnumerable);

            // WebIDL exposes both of these through a [Replaceable] accessor pair on Window; an ordinary
            // enumerable data property is the same simplification console and crypto are installed with.
            Install(global, engine, "localStorage", static e => e.Realm.Intrinsics.LocalStorage, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "sessionStorage", static e => e.Realm.Intrinsics.SessionStorage, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.EventSource) != WebApiFeatures.None)
        {
            Install(global, engine, "EventSource", static e => e.Realm.Intrinsics.EventSource, PropertyFlag.NonEnumerable);

            // MessageEvent is the interface an event source dispatches with, so it exists wherever one does.
            // The messaging feature installs the same intrinsic; Install is non-clobbering, so an engine with
            // both flags gets one object either way.
            Install(global, engine, "MessageEvent", static e => e.Realm.Intrinsics.MessageEvent, PropertyFlag.NonEnumerable);
        }

        // The transform streams other standards define need two flags each, because each of them is one
        // standard's algorithm running inside the Streams Standard's machinery: an engine that asked for
        // only one half of the pair gets neither, rather than an interface whose readable and writable
        // sides it has no way to consume.
        const WebApiFeatures TextTransforms = WebApiFeatures.Encoding | WebApiFeatures.Streams;
        if ((features & TextTransforms) == TextTransforms)
        {
            Install(global, engine, "TextDecoderStream", static e => e.Realm.Intrinsics.TextDecoderStream, PropertyFlag.NonEnumerable);
            Install(global, engine, "TextEncoderStream", static e => e.Realm.Intrinsics.TextEncoderStream, PropertyFlag.NonEnumerable);
        }

        const WebApiFeatures CompressionTransforms = WebApiFeatures.Compression | WebApiFeatures.Streams;
        if ((features & CompressionTransforms) == CompressionTransforms)
        {
            Install(global, engine, "CompressionStream", static e => e.Realm.Intrinsics.CompressionStream, PropertyFlag.NonEnumerable);
            Install(global, engine, "DecompressionStream", static e => e.Realm.Intrinsics.DecompressionStream, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.WebSocket) != WebApiFeatures.None)
        {
            Install(global, engine, "WebSocket", static e => e.Realm.Intrinsics.WebSocket, PropertyFlag.NonEnumerable);
            Install(global, engine, "CloseEvent", static e => e.Realm.Intrinsics.CloseEvent, PropertyFlag.NonEnumerable);

            // MessageEvent is the HTML Standard's; the messaging and event-source features install the same
            // intrinsic, and Install is non-clobbering, so an engine with any combination gets one object.
            Install(global, engine, "MessageEvent", static e => e.Realm.Intrinsics.MessageEvent, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.CacheApi) != WebApiFeatures.None)
        {
            // A cache is a list of request/response pairs, so the fetch object model is the Cache API's model
            // too — without it a script has nothing it could put in a cache. The network function is not part
            // of that and stays behind its own flag; these three are installed by the block above as well
            // when it ran, and Install leaves a name that already exists alone.
            Install(global, engine, "Headers", static e => e.Realm.Intrinsics.Headers, PropertyFlag.NonEnumerable);
            Install(global, engine, "Request", static e => e.Realm.Intrinsics.Request, PropertyFlag.NonEnumerable);
            Install(global, engine, "Response", static e => e.Realm.Intrinsics.Response, PropertyFlag.NonEnumerable);

            Install(global, engine, "Cache", static e => e.Realm.Intrinsics.Cache, PropertyFlag.NonEnumerable);
            Install(global, engine, "CacheStorage", static e => e.Realm.Intrinsics.CacheStorage, PropertyFlag.NonEnumerable);

            // WebIDL exposes caches through a [SameObject] accessor pair; an ordinary enumerable data
            // property is the same simplification console, crypto and performance are installed with.
            Install(global, engine, "caches", static e => e.Realm.Intrinsics.Caches, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.IdleCallback) != WebApiFeatures.None)
        {
            Install(global, engine, "requestIdleCallback", static e => e.Realm.Intrinsics.IdleCallbacks.RequestIdleCallback, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "cancelIdleCallback", static e => e.Realm.Intrinsics.IdleCallbacks.CancelIdleCallback, PropertyFlag.ConfigurableEnumerableWritable);

            // Exposed even though nothing but the engine can create one: it is what makes
            // `deadline instanceof IdleDeadline` and feature detection work.
            Install(global, engine, "IdleDeadline", static e => e.Realm.Intrinsics.IdleDeadline, PropertyFlag.NonEnumerable);
        }
    }

    /// <summary>
    /// Installs the three interface objects of the fetch object model, and deliberately not <c>fetch</c>
    /// itself.
    /// </summary>
    /// <remarks>
    /// Split out because the model has a second door: <c>Engine.Advanced.SetFetchHandler</c> routes an
    /// inbound request into script, which needs <c>Response</c> to answer with and is no reason at all to
    /// grant the script outbound network access. Both doors install the same three globals, the same way and
    /// with the same WebIDL attributes — an interface object is writable and configurable but not enumerable,
    /// https://webidl.spec.whatwg.org/#es-interfaces.
    /// </remarks>
    internal static void InstallFetchModel(Engine engine)
    {
        var global = engine._mainRealm.GlobalObject;
        Install(global, engine, "Headers", static e => e.Realm.Intrinsics.Headers, PropertyFlag.NonEnumerable);
        Install(global, engine, "Request", static e => e.Realm.Intrinsics.Request, PropertyFlag.NonEnumerable);
        Install(global, engine, "Response", static e => e.Realm.Intrinsics.Response, PropertyFlag.NonEnumerable);
    }

    /// <summary>
    /// The feature closure: a feature whose own surface is built out of another feature's interfaces brings
    /// that one with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computed here rather than in <c>UseFetch</c> because this is the only place every door leads through:
    /// a host may also assign <c>options.WebApi.Features</c> outright, and a closure applied in one extension
    /// method would leave that host with a <c>fetch</c> whose <c>Request</c> has no <c>AbortSignal</c> to
    /// build. The consequence is deliberate and worth stating: <c>options.WebApi.Features</c> keeps reading
    /// back exactly what the host asked for — enabling fetch does not silently rewrite it to name four
    /// features — while the engine carries the closure.
    /// </para>
    /// <para>
    /// <c>Request</c> always has an <c>AbortSignal</c> (<see cref="WebApiFeatures.Events"/>), its URL is a
    /// WHATWG URL record (<see cref="WebApiFeatures.Url"/>) and <c>response.blob()</c> answers with a
    /// <c>Blob</c> (<see cref="WebApiFeatures.Files"/>); none of the three is optional to the implementation,
    /// so installing fetch without them would ship an interface that throws on its own members. An
    /// <c>EventSource</c> is an <c>EventTarget</c> for the same kind of reason, and the reconnect delay it
    /// schedules rides the queue that flag creates.
    /// A <c>WebSocket</c> <i>is</i> an <c>EventTarget</c> too, and answers a binary message with a
    /// <c>Blob</c> whenever <c>binaryType</c> says so — the same argument for the same two features.
    /// </para>
    /// </remarks>
    private static WebApiFeatures ExpandFeatures(WebApiFeatures features)
    {
        if ((features & WebApiFeatures.Fetch) != WebApiFeatures.None)
        {
            // Streams joined the closure when bodies became streams: response.body is a ReadableStream, so
            // installing fetch without it would ship an interface returning an unnameable object.
            features |= WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files | WebApiFeatures.Streams;
        }

        // Deliberately BEFORE the GlobalEvents rule below, which is what turns the flag added here into
        // WebApiFeatures.Events as well. What a fetch listener is registered with is the global
        // addEventListener, and what it answers with is a Response — so the closure is exactly the set
        // fetch-handler hosting already demands (Events | Url | Files), reached through the feature that owns
        // the listener list. Pointedly not WebApiFeatures.Fetch: dispatching an inbound request into script is
        // a different grant from letting the script reach out to the network, the same split
        // Engine.Advanced.SetFetchHandler makes when it installs the object model and not `fetch`.
        if ((features & WebApiFeatures.FetchEvents) != WebApiFeatures.None)
        {
            features |= WebApiFeatures.GlobalEvents | WebApiFeatures.Url | WebApiFeatures.Files;
        }

        // A worker connection IS a MessagePort pair — the Worker object and the worker's global scope are its
        // two unexposed ends — and the worker global's self, addEventListener and error events are what the
        // global-events feature installs. Deliberately BEFORE the GlobalEvents rule below, which is what turns
        // the flag added here into WebApiFeatures.Events as well. It applies to the PARENT: a worker engine's
        // own set is the provider's to decide, and WorkerRequest.CreateDefaultOptions forces the same two.
        if ((features & WebApiFeatures.Workers) != WebApiFeatures.None)
        {
            features |= WebApiFeatures.Messaging | WebApiFeatures.GlobalEvents;
        }

        // The three global operations register listeners on an EventTarget and dispatch an Event, and the two
        // interface objects the feature adds are Event subclasses — so without the events feature it would
        // install operations with nothing to use them on.
        if ((features & WebApiFeatures.GlobalEvents) != WebApiFeatures.None)
        {
            features |= WebApiFeatures.Events;
        }

        // Deliberately not the other way round: fetch does not bring server-sent events and server-sent
        // events do not bring fetch. They are two separate grants of outbound network access.
        if ((features & WebApiFeatures.EventSource) != WebApiFeatures.None)
        {
            features |= WebApiFeatures.Events;
        }

        if ((features & WebApiFeatures.WebSocket) != WebApiFeatures.None)
        {
            features |= WebApiFeatures.Events | WebApiFeatures.Files;
        }

        // The Cache API stores Request/Response pairs, so it needs the same three for the same reasons — and
        // the fetch interface objects, which the install block adds. It deliberately does not bring
        // WebApiFeatures.Fetch: caching is not network access, and only Cache.add/addAll need one.
        if ((features & WebApiFeatures.CacheApi) != WebApiFeatures.None)
        {
            features |= WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files;
        }

        return features;
    }

    /// <summary>
    /// The features that keep something in <c>Engine._webApi</c>. The timer globals are the obvious reason for
    /// a queue; AbortSignal.timeout() and a delayed scheduler.postTask() are the others, and they need one
    /// whether or not the host also asked for setTimeout. The events, messaging and performance features
    /// additionally read the time origin (Event.timeStamp, MessageEvent.timeStamp, performance.now), fetch
    /// keeps its settings and its in-flight set here, the scheduler keeps its own task queues here, and storage
    /// keeps its providers here, which is why each of them wants the state even without the timers flag.
    /// The global events keep their synthetic listener target here. That flag is named explicitly rather than
    /// left to the closure that already brings <see cref="WebApiFeatures.Events"/> with it, because the reason
    /// it needs the state is its own — a target to fire at, not the time origin the events feature wants. The
    /// fetch events are named for the same reason once more removed: what
    /// <c>Engine.Advanced.InvokeFetchHandler</c> reads is that very target plus the registered handler slot,
    /// both of which live here, and a closure is not a reason to depend on one.
    /// </summary>
    private const WebApiFeatures NeedsEngineState =
        WebApiFeatures.Timers | WebApiFeatures.Events | WebApiFeatures.Performance | WebApiFeatures.Fetch | WebApiFeatures.Scheduler | WebApiFeatures.Messaging | WebApiFeatures.Storage | WebApiFeatures.WebSocket | WebApiFeatures.CacheApi | WebApiFeatures.IdleCallback | WebApiFeatures.GlobalEvents | WebApiFeatures.FetchEvents | WebApiFeatures.Workers;

    /// <summary>
    /// The queue exists for the timer globals, for AbortSignal.timeout() and for a delayed
    /// scheduler.postTask(), each of which needs it whether or not the host also asked for setTimeout; a
    /// performance-only engine reads just the time origin and never schedules, so it carries no queue at all.
    /// </summary>
    private const WebApiFeatures NeedsTimerQueue =
        WebApiFeatures.Timers | WebApiFeatures.Events | WebApiFeatures.Scheduler | WebApiFeatures.IdleCallback;

    /// <summary>
    /// The features that take their transport and their policy from <c>Options.WebApi.Fetch</c>. EventSource
    /// and WebSocket read the same group as fetch: they are further grants of network access over the same
    /// transport and the same policy, so any of the three is reason enough to keep the settings.
    /// </summary>
    private const WebApiFeatures NeedsFetchOptions = WebApiFeatures.Fetch | WebApiFeatures.EventSource | WebApiFeatures.WebSocket;

    /// <summary>
    /// Creates <c>Engine._webApi</c> — once, and before any feature block, because more than one feature keeps
    /// state in it now. A feature that needs none leaves the field null, which is what every hot path that
    /// consults it starts by checking, so a <c>console</c>-only engine is still the engine it was.
    /// </summary>
    /// <remarks>
    /// The state is per engine, not per <c>Options</c>: two engines built from one shared <c>Options</c>
    /// instance get one each, and neither can see the other's timers. The clock and the cap are read here,
    /// once, which is what their documentation promises.
    /// </remarks>
    private static void CreateEngineState(Options options, Engine engine, WebApiFeatures features)
    {
        // The diagnostics sink is the one thing here no feature flag governs: a host that set one gets the
        // channel whatever else it did or did not ask for, which is why it is read before the flags are.
        var diagnostics = options.WebApi.Diagnostics.Sink;

        if ((features & NeedsEngineState) == WebApiFeatures.None && diagnostics is null)
        {
            return;
        }

        var timerOptions = options.WebApi.Timers;
        var timeProvider = timerOptions.TimeProvider ?? TimeProvider.System;

        var timers = (features & NeedsTimerQueue) != WebApiFeatures.None
            ? new TimerQueue(engine, timeProvider, timerOptions.MaxActiveTimers, diagnostics)
            : null;

        // The fetch settings are read here, once, so that nothing on a background thread ever reaches into
        // Options — and so that a host mutating them afterwards does not change an engine that already exists.
        var fetch = (features & NeedsFetchOptions) != WebApiFeatures.None ? options.WebApi.Fetch : null;

        var scheduler = (features & WebApiFeatures.Scheduler) != WebApiFeatures.None
            ? new SchedulerQueue(engine)
            : null;

        // The storage group is passed whole rather than resolved here: which of the two maps an engine ever
        // needs is decided by the global a script touches, so defaulting one costs nothing until then.
        var storage = (features & WebApiFeatures.Storage) != WebApiFeatures.None ? options.WebApi.Storage : null;

        // The provider is resolved here, once, and a host that named none gets one of its own rather than
        // one shared by every engine built from this Options instance — see Options.CacheOptions.Provider.
        var cache = (features & WebApiFeatures.CacheApi) != WebApiFeatures.None
            ? options.WebApi.Cache.Provider ?? new InMemoryCacheStorageProvider()
            : null;

        // The idle queue needs the timer queue for the `timeout` option, and the realm so it can build an
        // IdleDeadline for each invocation. The PRINCIPAL realm, which is the only one these globals are
        // installed in — and the only one this can mean when the live door is called from inside a
        // ShadowRealm callback.
        var idleCallbacks = (features & WebApiFeatures.IdleCallback) != WebApiFeatures.None
            ? new IdleCallbackQueue(engine, engine._mainRealm, timeProvider, timers!, timerOptions.IdleBudget)
            : null;

        // The messaging group is passed whole rather than resolved here, exactly as the storage group is: a
        // host that named no BroadcastChannelBroker gets one of its own, and only once a channel asks for it.
        var messaging = (features & WebApiFeatures.Messaging) != WebApiFeatures.None ? options.WebApi.Messaging : null;

        engine._webApi = new WebApiEngineState(engine, timeProvider, timers, fetch, scheduler, diagnostics, storage, cache, idleCallbacks, messaging);

        AttachWorkers(options, engine._webApi, features);
    }

    /// <summary>
    /// Gives the state its worker registry, when the flag is on <b>and</b> the host named a provider. Without
    /// a provider there is nothing to attach and, deliberately, no <c>Worker</c> global either: a worker needs
    /// a thread and a pump, and Jint never starts either, so an engine that asked for the feature and supplied
    /// no execution resource has to answer <c>typeof Worker === 'undefined'</c> rather than hand a script a
    /// constructor that can only throw.
    /// </summary>
    /// <remarks>
    /// The provider and the two caps are read here, once, exactly as the clock and the timer cap are — so a
    /// host mutating <c>Options.WebApi.Workers</c> afterwards does not change an engine that already exists.
    /// </remarks>
    private static void AttachWorkers(Options options, WebApiEngineState state, WebApiFeatures features)
    {
        if ((features & WebApiFeatures.Workers) == WebApiFeatures.None || state.Workers is not null)
        {
            return;
        }

        var workers = options.WebApi.Workers;
        if (workers.Provider is not { } provider)
        {
            return;
        }

        state.AttachWorkers(new WorkerRegistry(provider, workers.MaxWorkers, workers.MaxQueuedMessages));
    }

    /// <summary>
    /// Attaches to an existing <c>Engine._webApi</c> whatever the features being enabled <i>now</i> need and it
    /// does not already carry. The other half of <see cref="ApplyLive"/>, and the reason
    /// <see cref="WebApiEngineState"/>'s slots are settable at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every attachment fills a null slot and never replaces a live one, so a queue the engine has been
    /// scheduling on cannot be swapped underneath it. <paramref name="added"/> is the newly enabled set rather
    /// than the union, which is what keeps the "read once, when the engine is built" promise every one of these
    /// settings makes: a group belonging to a feature that was already on is not read a second time.
    /// </para>
    /// <para>
    /// Two things are deliberately <b>not</b> re-read. The <b>clock</b> is the state's own — the timers,
    /// <c>performance.now()</c> and the time origin have to stay on one clock for the engine's whole life, so a
    /// <c>TimeProvider</c> assigned to the options after construction never reaches an engine that already
    /// exists. And the <b>diagnostics sink</b>, whose contract is that it holds still for an engine's lifetime
    /// because it also decides whether a callback's exception erupts.
    /// </para>
    /// </remarks>
    private static void ExtendEngineState(Options options, Engine engine, WebApiEngineState state, WebApiFeatures added)
    {
        var timerOptions = options.WebApi.Timers;

        if ((added & NeedsTimerQueue) != WebApiFeatures.None && state.Timers is null)
        {
            state.AttachTimers(new TimerQueue(engine, state.TimeProvider, timerOptions.MaxActiveTimers, state.Diagnostics));
        }

        if ((added & NeedsFetchOptions) != WebApiFeatures.None && state.FetchOptions is null)
        {
            state.AttachFetchOptions(options.WebApi.Fetch);
        }

        if ((added & WebApiFeatures.Scheduler) != WebApiFeatures.None && state.Scheduler is null)
        {
            state.AttachScheduler(new SchedulerQueue(engine));
        }

        if ((added & WebApiFeatures.Storage) != WebApiFeatures.None)
        {
            state.AttachStorage(options.WebApi.Storage);
        }

        if ((added & WebApiFeatures.Messaging) != WebApiFeatures.None)
        {
            state.AttachMessaging(options.WebApi.Messaging);
        }

        AttachWorkers(options, state, added);

        if ((added & WebApiFeatures.CacheApi) != WebApiFeatures.None && state.CacheProvider is null)
        {
            state.AttachCacheProvider(options.WebApi.Cache.Provider ?? new InMemoryCacheStorageProvider());
        }

        if ((added & WebApiFeatures.IdleCallback) != WebApiFeatures.None && state.IdleCallbacks is null)
        {
            // The timer queue is there: IdleCallback is in NeedsTimerQueue, so either the state already had
            // one or the block above has just attached it.
            state.AttachIdleCallbacks(
                new IdleCallbackQueue(engine, engine._mainRealm, state.TimeProvider, state.Timers!, timerOptions.IdleBudget));
        }
    }

    /// <summary>
    /// Installs one global, unless the global object already owns that name.
    /// </summary>
    /// <remarks>
    /// The host's <c>Options</c> configuration callbacks run before this, so a host that registered its own
    /// <c>console</c> — the thing every embedder that ever wanted logging did — keeps it, and enabling the
    /// feature can never silently replace it. The check probes rather than reads: a probe answers existence
    /// and enumerability without materializing a descriptor's value, so a host's own lazy global is not
    /// forced into existence merely by our looking.
    /// </remarks>
    /// <returns>
    /// The descriptor installed, or <see langword="null"/> when the global object already owned the name.
    /// Every caller but one ignores it; <c>self</c> needs it, because that descriptor is the only thing
    /// <see cref="WorkerGlobalScope.Install"/> is allowed to replace.
    /// </returns>
    private static LazyPropertyDescriptor<Engine>? Install(
        ObjectInstance global,
        Engine engine,
        string name,
        Func<Engine, JsValue> valueFactory,
        PropertyFlag flags)
    {
        if (global.HasOwnProperty(NameOf(name)))
        {
            return null;
        }

        var descriptor = new LazyPropertyDescriptor<Engine>(engine, valueFactory, flags);
        global.SetProperty(name, descriptor);
        return descriptor;
    }

    /// <summary>
    /// The <see cref="JsString"/> for one global's name, interned across every engine in the process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The existence probe above needs a <see cref="JsValue"/>, and <c>JsString.Create</c> allocates a fresh
    /// object for anything longer than one character — so every engine that enabled the web APIs was
    /// allocating one throwaway string object per installed global, whether or not the name was already
    /// taken. There are seventy-odd of them on a <see cref="WebApiFeatures.Default"/> engine, which measured
    /// at roughly 2.2 kB of an engine's construction allocation: the same object, rebuilt per engine, for a
    /// question that is asked and then discarded.
    /// </para>
    /// <para>
    /// The keys are the string literals at the call sites, so the table is bounded by the number of globals
    /// this file can install and is complete after the first engine of a given feature set. It is static
    /// because a <see cref="JsString"/> is immutable and realm-independent — unlike every object these
    /// descriptors <i>produce</i>, which is why the value factories stay per engine.
    /// </para>
    /// </remarks>
    internal static JsString NameOf(string name)
        => _globalNames.GetOrAdd(name, static value => JsString.Create(value));

    private static readonly ConcurrentDictionary<string, JsString> _globalNames = new(StringComparer.Ordinal);
}
#endif
