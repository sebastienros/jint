using System.Globalization;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi;

namespace Jint.Browser.Runtime;

/// <summary>
/// The <c>navigator</c> members a page has and an embedded interpreter does not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The engine's <c>Navigator</c> carries exactly one member and that is deliberate.</b>
/// <c>Jint/WebApi/Navigator</c> publishes <c>userAgent</c> because WinterTC's Minimum Common API requires it
/// and leaves <c>language</c>, <c>platform</c>, <c>maxTouchPoints</c>, <c>hardwareConcurrency</c> and
/// <c>geolocation</c> absent, on the ground that they describe a user agent with a user, a document and a
/// network stack. A page <em>is</em> that, so this is where they arrive — and where
/// <c>Emulation.setUserAgentOverride</c>, <c>setTouchEmulationEnabled</c>,
/// <c>setHardwareConcurrencyOverride</c> and <c>setGeolocationOverride</c> become observable.
/// </para>
/// <para>
/// <b>They are accessors on <c>Navigator.prototype</c></b>, where WebIDL puts them. The engine's prototype is
/// a shaped object, and its hybrid addition lane keeps the fixed shared layout serving the engine's own
/// <c>userAgent</c> slot while these browser members follow it in a per-realm side dictionary. They therefore
/// carry WebIDL's enumerable/configurable attributes without making <c>Object.keys(navigator)</c> or an object
/// spread report inherited members, and without giving up the prototype-method inline cache.
/// </para>
/// <para>
/// <b><c>userAgent</c> is the exception, and it is left to the prototype.</b> The page's user agent is
/// <see cref="BrowserOptions.UserAgent"/> and a client's override rather than <c>Jint/&lt;version&gt;</c>, and
/// it used to be shadowed here to say so — which meant two answers to one IDL attribute, since the accessor
/// the standard declares on <c>Navigator.prototype</c> went on answering the engine's own token
/// (<see href="https://github.com/sebastienros/jint/issues/3655">#3655</see>). The engine now names its own:
/// <c>Options.WebApi.Navigator.UserAgent</c> is what a page's engine is built with and
/// <c>Engine.WebApi.UserAgent</c> is what an override moves, so one accessor answers and there is nothing
/// here to shadow it with.
/// </para>
/// <para>
/// The whole object is installed as a lazy global, so a page that never mentions <c>navigator</c> builds
/// neither it nor its prototype.
/// </para>
/// </remarks>
internal static class NavigatorInstaller
{
    /// <summary>
    /// Replaces the engine's <c>navigator</c> global with one carrying the page's members.
    /// </summary>
    /// <remarks>
    /// A host that turned <see cref="WebApiFeatures.Navigator"/> off gets no navigator at all, because the
    /// object this decorates is the engine's own — the page adds members to it and does not conjure one.
    /// </remarks>
    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;

        if ((engine.Options.WebApi.Features & WebApiFeatures.Navigator) == WebApiFeatures.None)
        {
            return;
        }

        engine.AddLazyGlobal(
            "navigator",
            static e =>
            {
                var navigator = e._mainRealm.Intrinsics.NavigatorObject;
                Attach(e, navigator.Prototype!);
                return navigator;
            },
            PropertyFlag.ConfigurableEnumerableWritable);
    }

    /// <summary>
    /// What <c>navigator.language</c> answers: the first tag of the <c>Accept-Language</c> a user-agent
    /// override named, and otherwise the engine's own culture.
    /// </summary>
    /// <remarks>
    /// The culture is what <c>Emulation.setLocaleOverride</c> moves, and it is fixed when an engine is built
    /// — so this answer, <c>Date</c> and <c>Intl</c> change together, on the next document, and never
    /// disagree with each other in between. The invariant culture has no name; a page reading an empty
    /// language would branch on nothing, so it is reported as <c>en-US</c>.
    /// </remarks>
    internal static string LanguageOf(PageRuntime runtime)
    {
        if (FirstTag(runtime.Emulation.AcceptLanguage) is { } tag)
        {
            return tag;
        }

        var culture = runtime.Engine.Options.Culture;
        return culture.Name.Length != 0 ? culture.Name : "en-US";
    }

    internal static string PlatformOf(PageRuntime runtime) => runtime.Emulation.Platform ?? "";
    internal static JsValue Online(PageRuntime runtime)
    {
        _ = runtime;
        return JsBoolean.True;
    }

    private static void Attach(Engine engine, ObjectInstance navigatorPrototype)
    {
        // userAgent is deliberately absent: it is the one member the engine's own Navigator already declares,
        // and Engine.WebApi.UserAgent is what carries the page's string to it.
        Accessor(engine, navigatorPrototype, "language", static runtime => JsString.Create(LanguageOf(runtime)));
        Accessor(engine, navigatorPrototype, "languages", static runtime => Languages(runtime));
        Accessor(engine, navigatorPrototype, "platform", static runtime => JsString.Create(PlatformOf(runtime)));

        // https://w3c.github.io/pointerevents/#dom-navigator-maxtouchpoints — zero is what a device with no
        // touch screen reports, and it is the second half of the `'ontouchstart' in window` test every
        // responsive framework writes.
        Accessor(engine, navigatorPrototype, "maxTouchPoints", static runtime =>
            JsNumber.Create(runtime.Emulation.TouchEnabled ? runtime.Emulation.MaxTouchPoints : 0));

        // https://html.spec.whatwg.org/multipage/workers.html#dom-navigator-hardwareconcurrency — the host's
        // own processor count unless a client overrode it, because a library sizing a worker pool from it
        // wants a number that means something.
        Accessor(engine, navigatorPrototype, "hardwareConcurrency", static runtime =>
            JsNumber.Create(runtime.Emulation.HardwareConcurrency ?? Environment.ProcessorCount));

        // Both are true and neither is a guess: every request goes out over the context's own HttpClient, and
        // the context's cookie jar stores what a page sets. Emulation.setDocumentCookieDisabled does not move
        // the second, and says so.
        Accessor(engine, navigatorPrototype, "onLine", static runtime => Online(runtime));
        Accessor(engine, navigatorPrototype, "cookieEnabled", static _ => JsBoolean.True);

        Accessor(engine, navigatorPrototype, "geolocation", static runtime => runtime.Views.Geolocation);

        // https://html.spec.whatwg.org/multipage/system-state.html#client-identification — the compatibility
        // constants every browser answers, in the values HTML allows. appVersion is the user agent after its
        // "Mozilla/" token, which is how Chrome derives it and how it stays true to an override.
        Accessor(engine, navigatorPrototype, "appCodeName", static _ => JsString.Create("Mozilla"));
        Accessor(engine, navigatorPrototype, "appName", static _ => JsString.Create("Netscape"));
        Accessor(engine, navigatorPrototype, "appVersion", static runtime => JsString.Create(AppVersion(runtime.Emulation.EffectiveUserAgent)));
        Accessor(engine, navigatorPrototype, "product", static _ => JsString.Create("Gecko"));
        Accessor(engine, navigatorPrototype, "productSub", static _ => JsString.Create("20030107"));
        Accessor(engine, navigatorPrototype, "vendor", static _ => JsString.Empty);
        Accessor(engine, navigatorPrototype, "vendorSub", static _ => JsString.Empty);

        // https://html.spec.whatwg.org/multipage/system-state.html#pdf-viewing-support — there is no PDF
        // viewer, so HTML's answer is false and both legacy collections are empty.
        Accessor(engine, navigatorPrototype, "pdfViewerEnabled", static _ => JsBoolean.False);
        Accessor(engine, navigatorPrototype, "plugins", static runtime => runtime.SystemState.Plugins);
        Accessor(engine, navigatorPrototype, "mimeTypes", static runtime => runtime.SystemState.MimeTypes);
        Method(engine, navigatorPrototype, "javaEnabled", 0, static (_, _) => JsBoolean.False);

        // https://w3c.github.io/webdriver/#dom-navigatorautomationinformation-webdriver — false, as Chrome
        // driven over the DevTools protocol without --enable-automation answers.
        Accessor(engine, navigatorPrototype, "webdriver", static _ => JsBoolean.False);

        // https://www.w3.org/TR/device-memory/#sec-device-memory-js-api — the largest bucket the standard lets
        // a page see.
        Accessor(engine, navigatorPrototype, "deviceMemory", static _ => JsNumber.Create(8));

        // Chrome answers null for the retired Do Not Track preference; https://privacycg.github.io/gpc-spec/
        // is false because nobody expressed one.
        Accessor(engine, navigatorPrototype, "doNotTrack", static _ => JsValue.Null);
        Accessor(engine, navigatorPrototype, "globalPrivacyControl", static _ => JsBoolean.False);

        Accessor(engine, navigatorPrototype, "userAgentData", static runtime => runtime.SystemState.UserAgentData);
        Accessor(engine, navigatorPrototype, "permissions", static runtime => runtime.SystemState.Permissions);
        Accessor(engine, navigatorPrototype, "storage", static runtime => runtime.SystemState.Storage);

        Method(engine, navigatorPrototype, "registerProtocolHandler", 2, static (runtime, args) =>
            SystemState.ProtocolHandlers.Register(runtime, args, "registerProtocolHandler"));
        Method(engine, navigatorPrototype, "unregisterProtocolHandler", 2, static (runtime, args) =>
            SystemState.ProtocolHandlers.Register(runtime, args, "unregisterProtocolHandler"));

        // https://w3c.github.io/gamepad/#dom-navigator-getgamepads — no gamepad is ever connected.
        Method(engine, navigatorPrototype, "getGamepads", 0, static (runtime, _) =>
            runtime.Engine._mainRealm.Intrinsics.Array.ConstructFast(Array.Empty<JsValue>()));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-appversion — the user agent
    /// after its product token, which for every browser's string is what starts with "5.0 (".
    /// </summary>
    internal static string AppVersion(string userAgent)
        => userAgent.StartsWith("Mozilla/", StringComparison.Ordinal) ? userAgent["Mozilla/".Length..] : userAgent;

    private static void Method(Engine engine, ObjectInstance navigatorPrototype, string name, int length, Func<PageRuntime, JsValue[], JsValue> call)
    {
        var member = name;

        navigatorPrototype.DefineOwnProperty(
            name,
            new PropertyDescriptor(
                new ClrFunction(engine, name, (thisObject, arguments) => call(Runtime(thisObject, member), arguments), length, PropertyFlag.Configurable),
                PropertyFlag.ConfigurableEnumerableWritable));
    }

    private static void Accessor(Engine engine, ObjectInstance navigatorPrototype, string name, Func<PageRuntime, JsValue> read)
    {
        var member = name;

        navigatorPrototype.DefineOwnProperty(
            name,
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get " + name, (thisObject, _) => read(Runtime(thisObject, member))),
                set: null,
                PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-languages — every tag the
    /// <c>Accept-Language</c> named, in order, or the one <c>navigator.language</c> answers.
    /// </summary>
    private static JsArray Languages(PageRuntime runtime)
    {
        var engine = runtime.Engine;
        var header = runtime.Emulation.AcceptLanguage;

        if (string.IsNullOrEmpty(header))
        {
            return engine._mainRealm.Intrinsics.Array.ConstructFast((JsValue[]) [JsString.Create(LanguageOf(runtime))]);
        }

        var tags = new List<JsValue>();
        foreach (var entry in header!.Split(','))
        {
            if (Tag(entry) is { } tag)
            {
                tags.Add(JsString.Create(tag));
            }
        }

        if (tags.Count == 0)
        {
            tags.Add(JsString.Create(LanguageOf(runtime)));
        }

        return engine._mainRealm.Intrinsics.Array.ConstructFast((JsValue[]) [.. tags]);
    }

    /// <summary>The first language tag of an <c>Accept-Language</c> header, or <see langword="null"/>.</summary>
    private static string? FirstTag(string? header)
    {
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        var comma = header!.IndexOf(',', StringComparison.Ordinal);
        return Tag(comma < 0 ? header : header[..comma]);
    }

    /// <summary>One entry of an <c>Accept-Language</c> header without its quality weight.</summary>
    private static string? Tag(string entry)
    {
        var semicolon = entry.IndexOf(';', StringComparison.Ordinal);
        var tag = (semicolon < 0 ? entry : entry[..semicolon]).Trim();
        return tag.Length == 0 || string.Equals(tag, "*", StringComparison.Ordinal) ? null : tag;
    }

    /// <summary>
    /// The page behind the receiver, which is a <c>TypeError</c> for anything that is not this realm's
    /// navigator — the brand check the prototype's own <c>userAgent</c> makes, in the same words.
    /// </summary>
    internal static PageRuntime Runtime(JsValue thisObject, string member)
    {
        if (thisObject is Jint.WebApi.Navigator.JsNavigator instance && PageRuntime.Find(instance.Engine) is { } runtime)
        {
            return runtime;
        }

        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"Failed to read the '{member}' property from 'Navigator': illegal invocation, receiver is not a Navigator object.");

        if (thisObject is ObjectInstance other)
        {
            Jint.Runtime.Throw.TypeError(other.Engine.Realm, message);
        }

        Jint.Runtime.Throw.TypeErrorNoEngine(message);
        return null!;
    }
}
