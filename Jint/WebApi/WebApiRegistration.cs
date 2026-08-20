#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
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

        // DOMException has no feature flag of its own: it is how every other web API reports a failure, so it
        // exists whenever any of them does. As a WebIDL interface object it is writable and configurable but
        // NOT enumerable — https://webidl.spec.whatwg.org/#es-interfaces.
        Install(global, engine, "DOMException", static e => e.Realm.Intrinsics.DomException, PropertyFlag.NonEnumerable);

        if ((options.WebApi.Features & WebApiFeatures.Console) != WebApiFeatures.None)
        {
            // A WebIDL namespace object is exposed through an accessor pair; installing it as an ordinary
            // enumerable data property is a deliberate simplification, documented on ConsoleInstance.
            Install(global, engine, "console", static e => e.Realm.Intrinsics.Console, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((options.WebApi.Features & WebApiFeatures.Timers) != WebApiFeatures.None)
        {
            // The queue is per engine, not per Options: two engines built from one shared Options instance
            // get one each, and neither can see the other's timers. Both the clock and the cap are read here,
            // once, which is what their documentation promises. This is the only place the engine's web-API
            // state is created today; a later feature that needs state of its own has to extend the object
            // rather than assign a second one over it.
            var timerOptions = options.WebApi.Timers;
            var timers = new TimerQueue(timerOptions.TimeProvider ?? TimeProvider.System, timerOptions.MaxActiveTimers);
            engine._webApi = new WebApiEngineState(engine, timers);

            // WebIDL operations on the global: writable, enumerable and configurable —
            // https://webidl.spec.whatwg.org/#es-operations.
            Install(global, engine, "setTimeout", static e => e.Realm.Intrinsics.Timers.SetTimeout, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "setInterval", static e => e.Realm.Intrinsics.Timers.SetInterval, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "clearTimeout", static e => e.Realm.Intrinsics.Timers.ClearTimeout, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "clearInterval", static e => e.Realm.Intrinsics.Timers.ClearInterval, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "queueMicrotask", static e => e.Realm.Intrinsics.Timers.QueueMicrotask, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((options.WebApi.Features & WebApiFeatures.Encoding) != WebApiFeatures.None)
        {
            Install(global, engine, "TextDecoder", static e => e.Realm.Intrinsics.TextDecoder, PropertyFlag.NonEnumerable);
            Install(global, engine, "TextEncoder", static e => e.Realm.Intrinsics.TextEncoder, PropertyFlag.NonEnumerable);
        }

        if ((options.WebApi.Features & WebApiFeatures.Base64) != WebApiFeatures.None)
        {
            // Operations of a WebIDL interface mixin on the global are enumerable, unlike interface
            // objects — https://webidl.spec.whatwg.org/#es-operations.
            Install(global, engine, "atob", static e => e.Realm.Intrinsics.Atob, PropertyFlag.ConfigurableEnumerableWritable);
            Install(global, engine, "btoa", static e => e.Realm.Intrinsics.Btoa, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if ((options.WebApi.Features & WebApiFeatures.StructuredClone) != WebApiFeatures.None)
        {
            // A WebIDL operation on the global is a writable, enumerable, configurable data property —
            // https://webidl.spec.whatwg.org/#es-operations.
            Install(global, engine, "structuredClone", static e => e.Realm.Intrinsics.StructuredClone, PropertyFlag.ConfigurableEnumerableWritable);
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
