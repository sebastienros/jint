using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>
/// <c>location</c>: every member an own property of the one location object, over the page's own URL.
/// <para>
/// https://html.spec.whatwg.org/multipage/nav-history-apis.html#the-location-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member is installed here, not only the navigating ones.</b> The generated <c>Location</c>
/// prototype reads and writes AngleSharp's <c>ILocation</c>, and a write to it raises
/// <c>Location.Changed</c>, which AngleSharp answers with a fire-and-forget <c>IBrowsingContext.OpenAsync</c>
/// on whatever thread the setter ran on — a second thread in the DOM, inert today only because no requester
/// is registered. A page assigning <c>location.pathname</c> means a navigation, and a navigation is the
/// page's to run: so the whole interface is shadowed by own properties over the page's authoritative URL,
/// AngleSharp's location is never written, and the hazard is gone rather than merely dormant.
/// </para>
/// <para>
/// <b>The URL is the runtime's, not AngleSharp's.</b> <see cref="PageRuntime.DocumentUrl"/> is what a
/// navigation commits and what <c>pushState</c> moves, so it is what every getter reads. AngleSharp's
/// document address stays at whatever the parse was given, which is the URL relative resolution inside the
/// parse used and is exactly right for that; nothing else reads it.
/// </para>
/// <para>
/// <b>The attributes are WebIDL's <c>[LegacyUnforgeable]</c> ones</b>, which is what every member of
/// <c>Location</c> carries: enumerable, and neither writable nor configurable, so a page cannot delete
/// <c>location.assign</c> or redefine <c>location.href</c> out of the way.
/// </para>
/// </remarks>
internal static class LocationInstaller
{
    /// <summary>Whether this object has already been given its members.</summary>
    internal static bool IsInstalled(ObjectInstance wrapper) => wrapper.HasOwnProperty("assign");

    /// <summary>Adds every <c>Location</c> member to <paramref name="wrapper"/> as an own property.</summary>
    internal static void Attach(PageRuntime runtime, ObjectInstance wrapper)
    {
        var engine = runtime.Engine;

        Accessor(engine, wrapper, "href",
            static runtime => runtime.DocumentUrl,
            static (runtime, value) => runtime.Page.RequestNavigation(value, replace: false, engine: runtime.Engine));

        Accessor(engine, wrapper, "protocol",
            static runtime => Read(runtime, static url => url.SerializeProtocol()),
            static (runtime, value) => Write(runtime, value, static (url, v) => UrlSetters.SetProtocol(url, v)));

        Accessor(engine, wrapper, "host",
            static runtime => Read(runtime, static url => url.SerializeHostAndPort()),
            static (runtime, value) => Write(runtime, value, static (url, v) => UrlSetters.SetHost(url, v)));

        Accessor(engine, wrapper, "hostname",
            static runtime => Read(runtime, static url => url.SerializeHost()),
            static (runtime, value) => Write(runtime, value, static (url, v) => UrlSetters.SetHostname(url, v)));

        Accessor(engine, wrapper, "port",
            static runtime => Read(runtime, static url => url.SerializePort()),
            static (runtime, value) => Write(runtime, value, static (url, v) => UrlSetters.SetPort(url, v)));

        Accessor(engine, wrapper, "pathname",
            static runtime => Read(runtime, static url => url.SerializePath()),
            static (runtime, value) => Write(runtime, value, static (url, v) => UrlSetters.SetPathname(url, v)));

        Accessor(engine, wrapper, "search",
            static runtime => Read(runtime, static url => url.SerializeSearch()),
            static (runtime, value) => Write(runtime, value, static (url, v) => UrlSetters.SetSearch(url, v)));

        Accessor(engine, wrapper, "hash",
            static runtime => Read(runtime, static url => url.SerializeHash()),
            static (runtime, value) => Write(runtime, value, static (url, v) => UrlSetters.SetHash(url, v)));

        // Read-only: an origin is derived, and HTML declares no setter for it.
        Accessor(engine, wrapper, "origin", static runtime => Read(runtime, static url => url.SerializeOrigin()), setter: null);

        Method(engine, wrapper, "assign", 1, static (runtime, value) => runtime.Page.RequestNavigation(value, replace: false, engine: runtime.Engine));
        Method(engine, wrapper, "replace", 1, static (runtime, value) => runtime.Page.RequestNavigation(value, replace: true, engine: runtime.Engine));

        wrapper.DefineOwnPropertyUnchecked(
            "reload",
            new PropertyDescriptor(
                new ClrFunction(engine, "reload", static (thisObject, _) =>
                {
                    var runtime = PageRuntime.Of(thisObject, "reload");

                    // reload: the document's URL may already carry a fragment, and re-navigating to it would
                    // otherwise be recognized as a fragment navigation and reload nothing at all.
                    runtime.Page.RequestNavigation(runtime.DocumentUrl, replace: true, reload: true);
                    return JsValue.Undefined;
                }),
                PropertyFlag.OnlyEnumerable));

        // AngleSharp's ILocation has no [DomName] for it, so String(location) would otherwise answer
        // [object Location] instead of the URL.
        wrapper.DefineOwnPropertyUnchecked(
            "toString",
            new PropertyDescriptor(
                new ClrFunction(engine, "toString", static (thisObject, _) =>
                    JsString.Create(PageRuntime.Of(thisObject, "toString").DocumentUrl)),
                PropertyFlag.OnlyEnumerable));
    }

    /// <summary>
    /// A component read: the document's URL through the WHATWG parser, or the empty string for a URL the
    /// parser refuses — which is no URL a navigation ever commits, but is what <c>about:blank</c> parses to
    /// on every component but <c>href</c>.
    /// </summary>
    private static string Read(PageRuntime runtime, Func<UrlRecord, string> component)
    {
        var url = UrlParser.Parse(runtime.DocumentUrl);
        return url is null ? "" : component(url);
    }

    /// <summary>
    /// A component write: the setter's own algorithm applied to a copy of the URL, then a navigation to
    /// whatever that produced.
    /// </summary>
    /// <remarks>
    /// It is a navigation even when the component did not change, which is what HTML says: assigning
    /// <c>location.pathname</c> the value it already has reloads the document. The <c>hash</c> setter is not
    /// special-cased here either — the navigator recognizes a fragment-only change and keeps the document,
    /// which is the one place that decision belongs.
    /// </remarks>
    private static void Write(PageRuntime runtime, string value, Action<UrlRecord, string> setter)
    {
        var url = UrlParser.Parse(runtime.DocumentUrl);
        if (url is null)
        {
            return;
        }

        setter(url, value);
        runtime.Page.RequestNavigation(url.Serialize(), replace: false, engine: runtime.Engine);
    }

    private static void Accessor(
        Engine engine,
        ObjectInstance wrapper,
        string name,
        Func<PageRuntime, string> getter,
        Action<PageRuntime, string>? setter)
    {
        var member = name;

        wrapper.DefineOwnPropertyUnchecked(
            name,
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get " + name, (thisObject, _) =>
                    JsString.Create(getter(PageRuntime.Of(thisObject, member)))),
                setter is null
                    ? null
                    : new ClrFunction(engine, "set " + name, (thisObject, arguments) =>
                    {
                        setter(PageRuntime.Of(thisObject, member), TypeConverter.ToString(arguments.At(0)));
                        return JsValue.Undefined;
                    }),
                PropertyFlag.OnlyEnumerable));
    }

    private static void Method(Engine engine, ObjectInstance wrapper, string name, int length, Action<PageRuntime, string> body)
    {
        var member = name;

        wrapper.DefineOwnPropertyUnchecked(
            name,
            new PropertyDescriptor(
                new ClrFunction(engine, name, (thisObject, arguments) =>
                {
                    body(PageRuntime.Of(thisObject, member), TypeConverter.ToString(arguments.At(0)));
                    return JsValue.Undefined;
                }, length),
                PropertyFlag.OnlyEnumerable));
    }
}
