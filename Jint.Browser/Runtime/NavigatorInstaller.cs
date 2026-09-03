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
/// <b>They are own properties of the <c>navigator</c> object rather than accessors on
/// <c>Navigator.prototype</c></b>, which is where a browser has them and where WebIDL puts them. The engine's
/// prototype is a <em>shaped</em> object shared by every realm's, and a property it did not declare would
/// deoptimize it and lose the prototype-method inline cache with it; one instance per document takes the
/// own properties instead, exactly as <c>document</c> does for <c>defaultView</c> and <c>currentScript</c>
/// (<see cref="WindowInstaller.AttachDocumentMembers"/> argues it at length). They are non-enumerable for
/// the same reason those two are: a browser answers <c>[]</c> to <c>Object.keys(navigator)</c> because its
/// members are inherited, and an own enumerable accessor would put every one of them in an object spread.
/// </para>
/// <para>
/// <b><c>userAgent</c> is shadowed rather than left to the prototype</b>, because the page's user agent is
/// <see cref="BrowserOptions.UserAgent"/> and a client's override, not <c>Jint/&lt;version&gt;</c> — and
/// because it has to be the same string every request the page makes carries.
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
                Attach(e, navigator);
                return navigator;
            },
            PropertyFlag.ConfigurableEnumerableWritable);
    }

    /// <summary>What <c>navigator.userAgent</c> answers and what every request the page makes carries.</summary>
    internal static string UserAgentOf(PageRuntime runtime)
        => runtime.Emulation.UserAgent is { Length: > 0 } overridden ? overridden : runtime.Options.UserAgent;

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

    private static void Attach(Engine engine, ObjectInstance navigator)
    {
        Accessor(engine, navigator, "userAgent", static runtime => JsString.Create(UserAgentOf(runtime)));
        Accessor(engine, navigator, "language", static runtime => JsString.Create(LanguageOf(runtime)));
        Accessor(engine, navigator, "languages", static runtime => Languages(runtime));
        Accessor(engine, navigator, "platform", static runtime => JsString.Create(runtime.Emulation.Platform ?? ""));

        // https://w3c.github.io/pointerevents/#dom-navigator-maxtouchpoints — zero is what a device with no
        // touch screen reports, and it is the second half of the `'ontouchstart' in window` test every
        // responsive framework writes.
        Accessor(engine, navigator, "maxTouchPoints", static runtime =>
            JsNumber.Create(runtime.Emulation.TouchEnabled ? runtime.Emulation.MaxTouchPoints : 0));

        // https://html.spec.whatwg.org/multipage/workers.html#dom-navigator-hardwareconcurrency — the host's
        // own processor count unless a client overrode it, because a library sizing a worker pool from it
        // wants a number that means something.
        Accessor(engine, navigator, "hardwareConcurrency", static runtime =>
            JsNumber.Create(runtime.Emulation.HardwareConcurrency ?? Environment.ProcessorCount));

        // Both are true and neither is a guess: every request goes out over the context's own HttpClient, and
        // the context's cookie jar stores what a page sets. Emulation.setDocumentCookieDisabled does not move
        // the second, and says so.
        Accessor(engine, navigator, "onLine", static _ => JsBoolean.True);
        Accessor(engine, navigator, "cookieEnabled", static _ => JsBoolean.True);

        Accessor(engine, navigator, "geolocation", static runtime => runtime.Views.Geolocation);
    }

    private static void Accessor(Engine engine, ObjectInstance navigator, string name, Func<PageRuntime, JsValue> read)
    {
        var member = name;

        navigator.DefineOwnPropertyUnchecked(
            name,
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get " + name, (thisObject, _) => read(Runtime(thisObject, member))),
                set: null,
                PropertyFlag.OnlyConfigurable));
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
    private static PageRuntime Runtime(JsValue thisObject, string member)
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
