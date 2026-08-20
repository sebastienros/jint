#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

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
