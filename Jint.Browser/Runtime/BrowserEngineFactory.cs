using Jint.Browser.Dom;
using Jint.Browser.Workers;
using Jint.WebApi;

namespace Jint.Browser.Runtime;

/// <summary>
/// Builds the engine one navigation runs in: the web APIs a page needs, the page's sinks, its network
/// position, its storage partition, its workers and the DOM.
/// </summary>
/// <remarks>
/// <para>
/// One engine per top-level navigation, so "per document" and "per engine" coincide and no
/// <c>WindowProxy</c> is needed: the previous engine's token is cancelled, it is disposed on the page loop,
/// and the new one starts with a clean realm.
/// </para>
/// <para>
/// <b>Every network grant is the context's, not the engine's.</b> The client, the URL filter, the cookie jar
/// and the storage partition are one per <see cref="BrowserContext"/>, so two pages of a context share them
/// and two contexts share nothing — which is what a browser profile is. The base URL, the referrer and the
/// origin are the <i>document</i>'s, so they change with every navigation.
/// </para>
/// <para>
/// <b>Storage is granted only to a document with an origin.</b> A document loaded from <c>about:blank</c>, a
/// <c>data:</c> URL or <see cref="Page.SetContentAsync"/> has an opaque one, so the feature is withheld and
/// two throwing accessors take its place — see <see cref="PageStorage"/>.
/// </para>
/// <para>
/// The host's own configuration runs last, so it can change anything set here, including the feature set,
/// the observer and the filter.
/// </para>
/// </remarks>
internal static class BrowserEngineFactory
{
    internal static Engine Create(PageEngineRequest request)
    {
        var page = request.Page;
        var options = request.Options;
        var recorder = request.Recorder;
        var origin = PageUrl.OriginOf(request.Url);

        // One source per engine, cancelled when this document is left or the page closes: it is what the
        // fetch machinery reads through Constraints.Find<CancellationConstraint>(), so a navigation really
        // does abandon whatever the outgoing document had in flight.
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(request.PageClosing);
        var hasStorage = false;

        var engine = new Engine(o =>
        {
            o.ObserveCancellation(cancellation.Token);

            hasStorage = PageStorage.Configure(o, request.Network, request.SessionStores, origin);

            var features = WebApiFeatures.Default
                | WebApiFeatures.XmlHttpRequest
                | WebApiFeatures.Fetch
                | WebApiFeatures.Workers;

            if (hasStorage)
            {
                features |= WebApiFeatures.Storage;
            }

            o.UseWebApis(features);

            if (options.RecordErrors)
            {
                // Installing a sink is not only a recording: it is what makes an exception escaping a timer,
                // a listener or a microtask a report instead of an eruption out of the pump.
                o.WebApi.Diagnostics.Sink = new PageRecorder.Diagnostics(recorder);
            }

            if (options.RecordConsoleMessages)
            {
                o.WebApi.Console.Sink = new PageRecorder.Console(recorder);
            }

            ConfigureFetch(o, request, origin);

            o.WebApi.Workers.Provider = request.Workers;

            // So that a stack trace in a recorded error names the function it came from, and so that the
            // protocol can hand a client the source of a page's own script later.
            o.RetainFunctionSourceText = true;

            foreach (var configure in options.EngineConfiguration)
            {
                configure(o);
            }
        });

        var runtime = PageRuntime.Attach(engine, page, options, recorder, request.Url, request.Referrer);
        runtime.Cancellation = cancellation;

        DomBindings.Install(engine);
        WindowInstaller.Install(runtime);

        // Where an activation behaviour's default action goes now that there is a page behind it: a link
        // navigates, a form submits, a file chooser is reported. Without this the events bridge records what
        // it was asked for instead, which is what a binding-only engine gets.
        Events.BrowserEventRealm.Of(engine).ActivationHost = new PageActivationHost(runtime);

        if (!hasStorage)
        {
            PageStorage.InstallOpaque(engine);
        }

        return engine;
    }

#pragma warning disable JINT0002 // FetchObserver is the engine's own network seam; the page's request log is what it is for.
    private static void ConfigureFetch(Options options, PageEngineRequest request, string origin)
    {
        var fetch = options.WebApi.Fetch;

        fetch.UrlFilter = request.Network.UrlFilter;
        fetch.CookieJar = request.Network.CookieJar;
        fetch.Observer = request.Requests;

        if (request.Network.Client is { } client)
        {
            fetch.HttpClient = client;
        }

        if (request.Network.ClientFactory is { } factory)
        {
            fetch.HttpClientFactory = factory;
        }

        // https://html.spec.whatwg.org/multipage/webappapis.html#api-base-url: a relative URL a script hands
        // to fetch, XMLHttpRequest or new URL() resolves against the document's URL.
        if (Uri.TryCreate(request.Url, UriKind.Absolute, out var baseUrl))
        {
            fetch.BaseUrl = baseUrl;
        }

        if (Uri.TryCreate(request.Referrer, UriKind.Absolute, out var referrer))
        {
            fetch.Referrer = referrer;
        }

        // An opaque origin sends no Origin header and makes every same-origin credentials check fail, which
        // is exactly what a document with no origin should do.
        if (!string.Equals(origin, PageUrl.OpaqueOrigin, StringComparison.Ordinal))
        {
            fetch.Origin = origin;
        }
    }
#pragma warning restore JINT0002
}

/// <summary>Everything one page engine is built from, gathered so the factory takes one argument.</summary>
/// <param name="Page">The page the engine belongs to.</param>
/// <param name="Options">What every page of this browser is built from.</param>
/// <param name="Recorder">Where errors and console output go.</param>
/// <param name="Requests">Where the network log goes.</param>
/// <param name="Network">The context's client, filter, jar and storage partition.</param>
/// <param name="Workers">The page's worker provider.</param>
/// <param name="SessionStores">The page's <c>sessionStorage</c>, one store per origin.</param>
/// <param name="Url">The document's URL, which is its base URL and decides its origin.</param>
/// <param name="Referrer">The document this one was reached from, or the empty string.</param>
/// <param name="PageClosing">Cancelled when the page closes, so every engine token is linked to it.</param>
internal readonly record struct PageEngineRequest(
    Page Page,
    BrowserOptions Options,
    PageRecorder Recorder,
    PageNetworkRecorder Requests,
    PageNetwork Network,
    ThreadPerWorkerProvider Workers,
    Dictionary<string, Jint.WebApi.StorageProvider> SessionStores,
    string Url,
    string Referrer,
    CancellationToken PageClosing);
