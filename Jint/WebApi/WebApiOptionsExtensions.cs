#if NET8_0_OR_GREATER
using Jint.Runtime;
using Jint.WebApi;

// ReSharper disable once CheckNamespace
namespace Jint;

/// <summary>
/// Enables the opt-in WHATWG web platform APIs. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// Every method here <b>adds</b> to <see cref="Options.WebApiOptions.Features"/> rather than replacing it,
/// so the calls compose in any order and none of them can silently switch a feature off that an earlier
/// call switched on. Assign <c>options.WebApi.Features</c> directly to say exactly what the set is.
/// </para>
/// <para>
/// Enabling a feature only makes the engine <i>offer</i> the global: a name the host already registered
/// itself — through <c>engine.SetValue(...)</c> in an <c>options.Configure(...)</c> callback, or through
/// <c>options.AddLazyGlobal(...)</c> — is left exactly as the host left it. The host wins.
/// </para>
/// </remarks>
public static class WebApiOptionsExtensions
{
    /// <summary>
    /// Enables <see cref="WebApiFeatures.Default"/>: the web APIs a host normally wants, which is everything
    /// implemented except outbound network access.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseWebApis(this Options options)
    {
        return UseWebApis(options, WebApiFeatures.Default);
    }

    /// <summary>
    /// Enables the named web APIs, in addition to any already enabled.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="features">
    /// The features to add. <see cref="WebApiFeatures.None"/> adds nothing and, in particular, does not
    /// disable anything.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseWebApis(this Options options, WebApiFeatures features)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        options.WebApi.Features |= features;
        return options;
    }

    /// <summary>
    /// Enables <see cref="WebApiFeatures.Default"/> and then hands the whole web-API options group to
    /// <paramref name="configure"/>, so a host can adjust the feature set and the per-feature settings in one
    /// call.
    /// </summary>
    /// <example>
    /// <code>
    /// var engine = new Engine(o => o.UseWebApis(w =>
    /// {
    ///     w.Console.Sink = ConsoleSink.FromTextWriter(Console.Out);
    /// }));
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="configure">
    /// Runs after the default features are enabled, so it observes them and may narrow the set by assigning
    /// <see cref="Options.WebApiOptions.Features"/> outright.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseWebApis(this Options options, Action<Options.WebApiOptions> configure)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        if (configure is null)
        {
            Throw.ArgumentNullException(nameof(configure));
        }

        options.WebApi.Features |= WebApiFeatures.Default;
        configure(options.WebApi);
        return options;
    }

    /// <summary>
    /// Enables <c>fetch</c>, <c>Headers</c>, <c>Request</c> and <c>Response</c> — and with them
    /// <see cref="WebApiFeatures.Events"/>, <see cref="WebApiFeatures.Url"/> and
    /// <see cref="WebApiFeatures.Files"/>, which are part of fetch's own surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the only call that grants a script outbound network access</b>, and
    /// <see cref="UseWebApis(Options)"/> deliberately never does. What the script can then reach is whatever
    /// the host process can reach: read <see cref="Options.FetchOptions"/> — in particular
    /// <see cref="Options.FetchOptions.UrlFilter"/> — before enabling this in anything that runs untrusted
    /// script.
    /// </para>
    /// <para>
    /// Only the <see cref="WebApiFeatures.Fetch"/> flag is recorded; the three it implies are added when the
    /// engine is built, so <c>options.WebApi.Features</c> still reads back exactly what was asked for.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options.UseWebApis().UseFetch(f =>
    /// {
    ///     f.AllowedSchemes.Remove("http");
    ///     f.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    ///     f.MaxResponseBytes = 1024 * 1024;
    /// }));
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="configure">Optional configuration of the fetch settings, run after the feature is enabled.</param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseFetch(this Options options, Action<Options.FetchOptions>? configure = null)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        options.WebApi.Features |= WebApiFeatures.Fetch;
        configure?.Invoke(options.WebApi.Fetch);
        return options;
    }

    /// <summary>
    /// Enables <c>XMLHttpRequest</c>, <c>XMLHttpRequestUpload</c>, <c>XMLHttpRequestEventTarget</c> and
    /// <c>ProgressEvent</c>, together with the fetch object model their bodies and responses are built out
    /// of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is not a grant of outbound network access.</b> It installs the interface; sending anything
    /// still needs <see cref="UseFetch"/> or an <see cref="Options.FetchOptions.HttpClient"/> of the
    /// host's own. Without one, <c>send()</c> fails exactly as a <c>fetch</c> the policy refused does — an
    /// <c>error</c> event for an asynchronous request, a <c>NetworkError</c> <c>DOMException</c> for a
    /// synchronous one.
    /// </para>
    /// <para>
    /// The destination policy and every resource bound are <see cref="Options.FetchOptions"/>'s, which
    /// <paramref name="configure"/> hands over: a host that has already written a
    /// <see cref="Options.FetchOptions.UrlFilter"/> has written this one too. A relative URL passed to
    /// <c>open()</c> needs <see cref="Options.FetchOptions.BaseUrl"/>, which is what a document's URL is to
    /// a browser.
    /// </para>
    /// <para>
    /// <b><c>open(…, false)</c> blocks the calling thread</b> until the response is whole, which is what
    /// makes a library written for a browser — jQuery's <c>async: false</c> among them — work at all. The
    /// wait is on the HTTP transport, which never touches the engine, so it needs no pump and cannot
    /// deadlock with the host's own loop; what it costs is the thread, for as long as
    /// <see cref="Options.FetchOptions.Timeout"/> allows.
    /// </para>
    /// <para>
    /// <c>responseXML</c> and <c>responseType = "document"</c> answer <c>null</c> unless the host sets
    /// <see cref="Options.XhrOptions.DocumentParser"/>: Jint parses no markup.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options.UseWebApis().UseFetch().UseXmlHttpRequest(net =>
    /// {
    ///     net.BaseUrl = new Uri("https://api.example.org/");
    ///     net.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    /// }));
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="configure">
    /// Optional configuration of the shared network settings, run after the feature is enabled.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseXmlHttpRequest(this Options options, Action<Options.FetchOptions>? configure = null)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        options.WebApi.Features |= WebApiFeatures.XmlHttpRequest;
        configure?.Invoke(options.WebApi.Fetch);
        return options;
    }

    /// <summary>
    /// Enables <c>EventSource</c> and <c>MessageEvent</c> — server-sent events — and with them
    /// <see cref="WebApiFeatures.Events"/>, whose <c>EventTarget</c> an <c>EventSource</c> is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a grant of outbound network access</b>, separate from <see cref="UseFetch"/>: neither
    /// implies the other, and <see cref="UseWebApis(Options)"/> grants neither. It reads its transport and
    /// its policy from the same <see cref="Options.FetchOptions"/> group <c>fetch</c> does — which is what
    /// <paramref name="configure"/> hands you — so a host that has already written a
    /// <see cref="Options.FetchOptions.UrlFilter"/> has written this one too. Three of those settings mean
    /// something different for a stream than for a document; see <see cref="WebApiFeatures.EventSource"/>.
    /// </para>
    /// <para>
    /// A connection delivers, and reconnects, only while the engine is being pumped. Nothing arrives in an
    /// engine nobody pumps, and nothing arrives on a thread the host did not choose.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options.UseWebApis().UseEventSource(net =>
    /// {
    ///     net.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    ///     net.MaxResponseBytes = 64 * 1024;      // the largest single event, not the largest stream
    ///     net.MaxConcurrentRequests = 2;         // at most two streams open at once
    /// }));
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="configure">
    /// Optional configuration of the shared network settings, run after the feature is enabled.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseEventSource(this Options options, Action<Options.FetchOptions>? configure = null)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        options.WebApi.Features |= WebApiFeatures.EventSource;
        configure?.Invoke(options.WebApi.Fetch);
        return options;
    }

    /// <summary>
    /// Enables <c>WebSocket</c>, together with the <c>CloseEvent</c> and <c>MessageEvent</c> interfaces its
    /// events are — and with <see cref="WebApiFeatures.Events"/> and <see cref="WebApiFeatures.Files"/>,
    /// which its own surface is built out of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This grants a script outbound network access</b>, and <see cref="UseWebApis(Options)"/> deliberately
    /// never does. A socket answers to the same policy a fetch does — the <see cref="Options.FetchOptions"/>
    /// group, which <paramref name="configure"/> hands over — with the scheme list read in its WebSocket
    /// sense: <c>http</c> admits <c>ws</c> and <c>https</c> admits <c>wss</c>. The
    /// <see cref="Options.FetchOptions.UrlFilter"/> is shown the <c>ws:</c> URL, so a filter that tests the
    /// scheme needs to know about both.
    /// </para>
    /// <para>
    /// Enabling this does <b>not</b> enable <c>fetch</c>, and enabling <c>fetch</c> does not enable this:
    /// they share their settings, not their permission.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options.UseWebApis().UseWebSocket(net =>
    /// {
    ///     net.AllowedSchemes.Remove("http");                   // wss only
    ///     net.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
    /// }));
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="configure">Optional configuration of the network settings, run after the feature is enabled.</param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseWebSocket(this Options options, Action<Options.FetchOptions>? configure = null)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        options.WebApi.Features |= WebApiFeatures.WebSocket;
        configure?.Invoke(options.WebApi.Fetch);
        return options;
    }

    /// <summary>
    /// Enables the <c>caches</c> object and the <c>Cache</c> and <c>CacheStorage</c> interfaces — and with
    /// them <see cref="WebApiFeatures.Events"/>, <see cref="WebApiFeatures.Url"/> and
    /// <see cref="WebApiFeatures.Files"/>, plus the <c>Headers</c>, <c>Request</c> and <c>Response</c>
    /// classes a cache is made of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UseWebApis(Options)"/> deliberately never does this: a cache outlives the evaluation that
    /// filled it, so where the data goes and what bounds it are decisions a host makes rather than inherits.
    /// Left unconfigured, each engine gets a private in-memory store <b>with no quota</b> — see
    /// <see cref="Options.CacheOptions.Provider"/>.
    /// </para>
    /// <para>
    /// This grants no network access. <c>cache.add</c> and <c>cache.addAll</c> fetch, so they additionally
    /// need <see cref="UseFetch"/>, and reject with a <c>TypeError</c> naming it until they have it; every
    /// other <c>Cache</c> method works without it.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options.UseWebApis().UseCacheApi(cache =>
    /// {
    ///     cache.Provider = myRedisBackedProvider;
    /// }));
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="configure">Optional configuration of the cache settings, run after the feature is enabled.</param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseCacheApi(this Options options, Action<Options.CacheOptions>? configure = null)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        options.WebApi.Features |= WebApiFeatures.CacheApi;
        configure?.Invoke(options.WebApi.Cache);
        return options;
    }

    /// <summary>
    /// Enables the <c>Worker</c> interface object and makes <paramref name="provider"/> the host's answer to
    /// <c>new Worker(...)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Flag and provider in one call, because either alone does nothing: <see cref="WebApiFeatures.Workers"/>
    /// without a provider leaves the global uninstalled, and a provider without the flag is never consulted.
    /// <see cref="WebApiFeatures.Default"/> never includes the flag, so this call — or naming the flag and
    /// assigning <c>options.WebApi.Workers.Provider</c> — is the only way to get it.
    /// </para>
    /// <para>
    /// <b>This grants a script the ability to make the host create engines and run them on host threads.</b>
    /// The provider is the policy: it sees every request and refuses by returning <see langword="null"/>. The
    /// per-engine <c>MaxWorkers</c> cap is only a backstop, and a worker inherits its parent's restrictions
    /// without inheriting its grants — including this one, so nesting is off unless the provider deliberately
    /// turns it on. Read <see cref="WorkerProvider"/> before implementing one: which thread each callback runs
    /// on is the whole contract.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new Engine(options => options.UseWebApis().UseWorkers(new ThreadPerWorkerProvider()));
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="provider">Builds the engine each worker runs on, and decides which thread pumps it.</param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseWorkers(this Options options, WorkerProvider provider)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        if (provider is null)
        {
            Throw.ArgumentNullException(nameof(provider));
        }

        options.WebApi.Features |= WebApiFeatures.Workers;
        options.WebApi.Workers.Provider = provider;
        return options;
    }

    /// <summary>
    /// Enables the <c>console</c> object and sends its output to <paramref name="sink"/>.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="sink">Where console output goes. See <see cref="ConsoleSink"/> for the thread-safety obligation.</param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseConsole(this Options options, ConsoleSink sink)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        if (sink is null)
        {
            Throw.ArgumentNullException(nameof(sink));
        }

        options.WebApi.Features |= WebApiFeatures.Console;
        options.WebApi.Console.Sink = sink;
        return options;
    }

    /// <summary>
    /// Enables the <c>console</c> object and writes each record to <paramref name="writer"/> as one line.
    /// </summary>
    /// <param name="options">Options to modify.</param>
    /// <param name="writer">The destination, e.g. <c>Console.Out</c>.</param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseConsole(this Options options, TextWriter writer)
    {
        return UseConsole(options, ConsoleSink.FromTextWriter(writer));
    }

    /// <summary>
    /// Sends the engine's uncaught script errors to <paramref name="sink"/> and enables the
    /// <c>reportError</c> function script uses to add to them.
    /// </summary>
    /// <remarks>
    /// The sink is what turns an exception escaping a timer callback, a <c>queueMicrotask</c> callback or an
    /// event listener from something that
    /// erupts into something that is reported — read <see cref="DiagnosticsSink"/> before installing one. Only
    /// <c>reportError</c> needs the feature flag; a host that wants the reports without giving script a way to
    /// add to them can assign <c>options.WebApi.Diagnostics.Sink</c> on its own.
    /// </remarks>
    /// <param name="options">Options to modify.</param>
    /// <param name="sink">
    /// Where uncaught script errors go. See <see cref="DiagnosticsSink"/> for the thread-safety obligation and
    /// for how long the values a report carries may be used.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseDiagnostics(this Options options, DiagnosticsSink sink)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        if (sink is null)
        {
            Throw.ArgumentNullException(nameof(sink));
        }

        options.WebApi.Features |= WebApiFeatures.Reporting;
        options.WebApi.Diagnostics.Sink = sink;
        return options;
    }

    /// <summary>
    /// Enables <c>localStorage</c> and <c>sessionStorage</c>, each over its own in-memory store that dies
    /// with the engine.
    /// </summary>
    /// <remarks>
    /// <see cref="WebApiFeatures.Storage"/> is not part of <see cref="WebApiFeatures.Default"/>, so this call
    /// — or naming the flag — is the only way to get it. See <see cref="Options.StorageOptions"/> for why,
    /// and <see cref="UseStorage(Options, StorageProvider, StorageProvider)"/> for putting the data
    /// somewhere that outlives the engine.
    /// </remarks>
    /// <param name="options">Options to modify.</param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseStorage(this Options options)
    {
        if (options is null)
        {
            Throw.ArgumentNullException(nameof(options));
        }

        options.WebApi.Features |= WebApiFeatures.Storage;
        return options;
    }

    /// <summary>
    /// Enables <c>localStorage</c> and <c>sessionStorage</c> over the host's own stores.
    /// </summary>
    /// <example>
    /// <code>
    /// var engine = new Engine(o => o.UseStorage(new MyDatabaseStorage(tenantId), new InMemoryStorageProvider()));
    /// </code>
    /// </example>
    /// <param name="options">Options to modify.</param>
    /// <param name="localStorageProvider">
    /// The map behind <c>localStorage</c>, or <see langword="null"/> to let each engine default its own.
    /// </param>
    /// <param name="sessionStorageProvider">
    /// The map behind <c>sessionStorage</c>, or <see langword="null"/> to let each engine default its own.
    /// Pass the same instance as <paramref name="localStorageProvider"/> to make the two globals one store.
    /// </param>
    /// <returns>Options instance for fluent syntax.</returns>
    public static Options UseStorage(
        this Options options,
        StorageProvider? localStorageProvider,
        StorageProvider? sessionStorageProvider = null)
    {
        UseStorage(options);

        options.WebApi.Storage.LocalStorageProvider = localStorageProvider;
        options.WebApi.Storage.SessionStorageProvider = sessionStorageProvider;
        return options;
    }
}
#endif
