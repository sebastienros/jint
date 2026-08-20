#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.WebApi.Scheduling;
using Jint.WebApi.Timers;

namespace Jint.WebApi;

/// <summary>
/// Installs the globals for the web APIs an engine opted into. Invoked from <c>Options.Apply</c>, which is
/// the sanctioned conditional-install site — the same one <c>Interop.Enabled</c> and
/// <c>Modules.RegisterRequire</c> use.
/// </summary>
/// <remarks>
/// <para>
/// Each global is a <see cref="LazyPropertyDescriptor{T}"/> installed through
/// <c>GlobalObject.SetProperty</c>, which is what <c>Options.AddLazyGlobal</c> does and for the same two
/// reasons: <c>SetProperty</c> bumps the own-property version every global-identifier and member-read inline
/// cache revalidates against, and a lazy descriptor is what
/// <c>Engine.Advanced.RestoreGlobalSnapshot</c> can return to its unmaterialized state so a pooled engine
/// rebuilds the object for the next cycle rather than inheriting the previous one's.
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
        var global = engine.Realm.GlobalObject;
        var features = ExpandFeatures(options.WebApi.Features);

        // Recorded on the engine because one host API — Engine.Advanced.CreateMessagePortPair — has to be
        // able to refuse an engine that never opted in, and Options is shareable so it cannot be asked later.
        engine._webApiFeatures = features;

        CreateEngineState(options, engine, features);

        if (features == WebApiFeatures.None)
        {
            // Reachable only for an engine whose host set a diagnostics sink and named no feature: it gets the
            // reporting channel the state above carries and no globals whatever — not even DOMException, which
            // exists to let a web API report a failure to script and here there is no web API to have one.
            return;
        }

        // DOMException has no feature flag of its own: it is how every other web API reports a failure, so it
        // exists whenever any of them does. As a WebIDL interface object it is writable and configurable but
        // NOT enumerable — https://webidl.spec.whatwg.org/#es-interfaces.
        Install(global, engine, "DOMException", static e => e.Realm.Intrinsics.DomException, PropertyFlag.NonEnumerable);

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
        }

        if ((features & WebApiFeatures.Events) != WebApiFeatures.None)
        {
            Install(global, engine, "Event", static e => e.Realm.Intrinsics.Event, PropertyFlag.NonEnumerable);
            Install(global, engine, "CustomEvent", static e => e.Realm.Intrinsics.CustomEvent, PropertyFlag.NonEnumerable);
            Install(global, engine, "EventTarget", static e => e.Realm.Intrinsics.EventTarget, PropertyFlag.NonEnumerable);
            Install(global, engine, "AbortController", static e => e.Realm.Intrinsics.AbortController, PropertyFlag.NonEnumerable);
            Install(global, engine, "AbortSignal", static e => e.Realm.Intrinsics.AbortSignal, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Crypto) != WebApiFeatures.None)
        {
            // WebIDL exposes crypto through a [Replaceable] accessor pair; an ordinary enumerable data
            // property is the same simplification console is installed with, documented on CryptoInstance.
            Install(global, engine, "crypto", static e => e.Realm.Intrinsics.Crypto, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.Performance) != WebApiFeatures.None)
        {
            Install(global, engine, "performance", static e => e.Realm.Intrinsics.Performance, PropertyFlag.ConfigurableEnumerableWritable);

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
            // NavigatorInstance.
            Install(global, engine, "navigator", static e => e.Realm.Intrinsics.Navigator, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.Fetch) != WebApiFeatures.None)
        {
            Install(global, engine, "Headers", static e => e.Realm.Intrinsics.Headers, PropertyFlag.NonEnumerable);
            Install(global, engine, "Request", static e => e.Realm.Intrinsics.Request, PropertyFlag.NonEnumerable);
            Install(global, engine, "Response", static e => e.Realm.Intrinsics.Response, PropertyFlag.NonEnumerable);

            // A WebIDL operation on the global is a writable, enumerable, configurable data property, unlike
            // the interface objects above — https://webidl.spec.whatwg.org/#es-operations.
            Install(global, engine, "fetch", static e => e.Realm.Intrinsics.Fetch, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((features & WebApiFeatures.Streams) != WebApiFeatures.None)
        {
            // Only the five interfaces a script constructs directly are globals. ReadableStreamDefaultReader,
            // the three controllers and WritableStreamDefaultWriter exist as ordinary interface objects and
            // are reached through their instances' prototypes — a deliberate, documented narrowing of the
            // browser surface, since nothing but feature detection ever names them.
            Install(global, engine, "ReadableStream", static e => e.Realm.Intrinsics.ReadableStream, PropertyFlag.NonEnumerable);
            Install(global, engine, "WritableStream", static e => e.Realm.Intrinsics.WritableStream, PropertyFlag.NonEnumerable);
            Install(global, engine, "TransformStream", static e => e.Realm.Intrinsics.TransformStream, PropertyFlag.NonEnumerable);
            Install(global, engine, "ByteLengthQueuingStrategy", static e => e.Realm.Intrinsics.ByteLengthQueuingStrategy, PropertyFlag.NonEnumerable);
            Install(global, engine, "CountQueuingStrategy", static e => e.Realm.Intrinsics.CountQueuingStrategy, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Scheduler) != WebApiFeatures.None)
        {
            // WebIDL exposes scheduler through a [Replaceable] accessor pair; an ordinary enumerable data
            // property is the same simplification console, crypto and performance are installed with, and it
            // is documented on SchedulerInstance.
            Install(global, engine, "scheduler", static e => e.Realm.Intrinsics.Scheduler, PropertyFlag.ConfigurableEnumerableWritable);

            Install(global, engine, "TaskController", static e => e.Realm.Intrinsics.TaskController, PropertyFlag.NonEnumerable);
            Install(global, engine, "TaskSignal", static e => e.Realm.Intrinsics.TaskSignal, PropertyFlag.NonEnumerable);
            Install(global, engine, "TaskPriorityChangeEvent", static e => e.Realm.Intrinsics.TaskPriorityChangeEvent, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Messaging) != WebApiFeatures.None)
        {
            Install(global, engine, "MessageChannel", static e => e.Realm.Intrinsics.MessageChannel, PropertyFlag.NonEnumerable);
            Install(global, engine, "MessagePort", static e => e.Realm.Intrinsics.MessagePort, PropertyFlag.NonEnumerable);
            Install(global, engine, "MessageEvent", static e => e.Realm.Intrinsics.MessageEvent, PropertyFlag.NonEnumerable);
        }

        if ((features & WebApiFeatures.Reporting) != WebApiFeatures.None)
        {
            // A WebIDL operation on the global — https://webidl.spec.whatwg.org/#es-operations. Installed
            // whether or not a sink exists to hear it, so that feature detection sees the same surface either
            // way and a script written for a browser does not have to guard the call.
            Install(global, engine, "reportError", static e => e.Realm.Intrinsics.ReportError, PropertyFlag.ConfigurableEnumerableWritable);
        }
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
    /// Fetch is the only feature with implications today. <c>Request</c> always has an <c>AbortSignal</c>
    /// (<see cref="WebApiFeatures.Events"/>), its URL is a WHATWG URL record
    /// (<see cref="WebApiFeatures.Url"/>) and <c>response.blob()</c> answers with a <c>Blob</c>
    /// (<see cref="WebApiFeatures.Files"/>); none of the three is optional to the implementation, so
    /// installing fetch without them would ship an interface that throws on its own members.
    /// </para>
    /// </remarks>
    private static WebApiFeatures ExpandFeatures(WebApiFeatures features)
    {
        if ((features & WebApiFeatures.Fetch) != WebApiFeatures.None)
        {
            features |= WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files;
        }

        return features;
    }

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
        // The timer globals are the obvious reason for a queue; AbortSignal.timeout() and a delayed
        // scheduler.postTask() are the others, and they need one whether or not the host also asked for
        // setTimeout. The events, messaging and performance features additionally read the time origin
        // (Event.timeStamp, MessageEvent.timeStamp, performance.now), fetch keeps its settings and its
        // in-flight set here, and the scheduler keeps its own task queues here, which is why each of them
        // wants the state even without the timers flag.
        const WebApiFeatures NeedsEngineState =
            WebApiFeatures.Timers | WebApiFeatures.Events | WebApiFeatures.Performance | WebApiFeatures.Fetch | WebApiFeatures.Scheduler | WebApiFeatures.Messaging;

        // The diagnostics sink is the one thing here no feature flag governs: a host that set one gets the
        // channel whatever else it did or did not ask for, which is why it is read before the flags are.
        var diagnostics = options.WebApi.Diagnostics.Sink;

        if ((features & NeedsEngineState) == WebApiFeatures.None && diagnostics is null)
        {
            return;
        }

        var timerOptions = options.WebApi.Timers;
        var timeProvider = timerOptions.TimeProvider ?? TimeProvider.System;

        // The queue exists for the timer globals, for AbortSignal.timeout() and for a delayed
        // scheduler.postTask(), each of which needs it whether or not the host also asked for setTimeout; a
        // performance-only engine reads just the time origin and never schedules, so it carries no queue at
        // all.
        const WebApiFeatures NeedsTimerQueue = WebApiFeatures.Timers | WebApiFeatures.Events | WebApiFeatures.Scheduler;
        var timers = (features & NeedsTimerQueue) != WebApiFeatures.None
            ? new TimerQueue(timeProvider, timerOptions.MaxActiveTimers, diagnostics)
            : null;

        // The fetch settings are read here, once, so that nothing on a background thread ever reaches into
        // Options — and so that a host mutating them afterwards does not change an engine that already exists.
        var fetch = (features & WebApiFeatures.Fetch) != WebApiFeatures.None ? options.WebApi.Fetch : null;

        var scheduler = (features & WebApiFeatures.Scheduler) != WebApiFeatures.None
            ? new SchedulerQueue(engine)
            : null;

        engine._webApi = new WebApiEngineState(engine, timeProvider, timers, fetch, scheduler, diagnostics);
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
    private static void Install(
        ObjectInstance global,
        Engine engine,
        string name,
        Func<Engine, JsValue> valueFactory,
        PropertyFlag flags)
    {
        if (global.HasOwnProperty(JsString.Create(name)))
        {
            return;
        }

        global.SetProperty(name, new LazyPropertyDescriptor<Engine>(engine, valueFactory, flags));
    }
}
#endif
