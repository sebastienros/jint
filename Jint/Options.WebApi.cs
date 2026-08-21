#if NET8_0_OR_GREATER
using Jint.WebApi;

namespace Jint;

public partial class Options
{
    /// <summary>
    /// Opt-in WHATWG web platform APIs (<c>console</c>, <c>DOMException</c>, …). Nothing here is installed
    /// unless <see cref="WebApiOptions.Features"/> names it, so a default engine is byte-for-byte the engine
    /// it was before these existed.
    /// <para>
    /// <b>Requires .NET 8 or higher.</b> The whole surface is compiled only for <c>net8.0</c> and later; on
    /// <c>net462</c>, <c>netstandard2.0</c> and <c>netstandard2.1</c> the property does not exist at all.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reachable through <see cref="WebApiOptionsExtensions.UseWebApis(Options)"/> and friends, which is the
    /// spelling most hosts want. The group is here for the settings those extensions do not name.
    /// </para>
    /// <para>
    /// Unlike the other option groups this one is allocated on first touch rather than with the
    /// <see cref="Options"/> instance, so a host that never asks for a web API pays nothing for the group
    /// existing — <see cref="Apply"/> reads the backing field and never forces it. Touching the property is
    /// therefore a host-thread act like any other option mutation; an engine build never does it.
    /// </para>
    /// </remarks>
    public WebApiOptions WebApi => _webApi ??= new WebApiOptions();

    private WebApiOptions? _webApi;

    /// <summary>
    /// Configuration for the opt-in web platform APIs. Requires .NET 8 or higher.
    /// </summary>
    /// <remarks>
    /// Like every other <see cref="Options"/> group this may be shared by any number of engines, including
    /// concurrent ones: nothing on it is engine-affine. The one obligation that carries is on
    /// <see cref="ConsoleOptions.Sink"/> — see its documentation.
    /// </remarks>
    public class WebApiOptions
    {
        /// <summary>
        /// Which web APIs this engine exposes. Defaults to <see cref="WebApiFeatures.None"/>, which installs
        /// nothing at all — not even <c>DOMException</c>.
        /// </summary>
        /// <remarks>
        /// Read once, when an engine is built, so this is the set an engine <i>starts</i> with rather than the
        /// set it has: it always reads back exactly what the host asked for, while the engine additionally
        /// carries the feature closure, and a host may turn further features on afterwards with
        /// <c>Engine.Advanced.EnableWebApis</c>. What a given engine actually has is
        /// <c>engine.Advanced.WebApiFeatures</c>.
        /// </remarks>
        public WebApiFeatures Features { get; set; }

        /// <summary>
        /// Settings for the <c>console</c> object, installed when <see cref="Features"/> contains
        /// <see cref="WebApiFeatures.Console"/>.
        /// </summary>
        public ConsoleOptions Console { get; } = new();

        /// <summary>
        /// Settings for the timer functions, installed when <see cref="Features"/> contains
        /// <see cref="WebApiFeatures.Timers"/>.
        /// </summary>
        public TimerOptions Timers { get; } = new();

        /// <summary>
        /// Settings for <c>fetch</c>, installed when <see cref="Features"/> contains
        /// <see cref="WebApiFeatures.Fetch"/> — which <see cref="WebApiFeatures.Default"/> never does.
        /// </summary>
        public FetchOptions Fetch { get; } = new();

        /// <summary>
        /// Where the engine reports script errors nobody caught. Unlike everything else in this group it is
        /// not tied to a feature flag: setting <see cref="DiagnosticsOptions.Sink"/> arms the channel by
        /// itself, and <see cref="WebApiFeatures.Reporting"/> additionally gives script the
        /// <c>reportError</c> function that feeds it.
        /// </summary>
        public DiagnosticsOptions Diagnostics { get; } = new();

        /// <summary>
        /// Settings for <c>localStorage</c> and <c>sessionStorage</c>, installed when <see cref="Features"/>
        /// contains <see cref="WebApiFeatures.Storage"/>.
        /// </summary>
        public StorageOptions Storage { get; } = new();

        /// <summary>
        /// Settings for the <c>caches</c> object, installed when <see cref="Features"/> contains
        /// <see cref="WebApiFeatures.CacheApi"/> — which <see cref="WebApiFeatures.Default"/> never does.
        /// </summary>
        public CacheOptions Cache { get; } = new();
    }

    /// <summary>
    /// Settings for the <c>localStorage</c> and <c>sessionStorage</c> globals. Requires .NET 8 or higher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Storage is not in <see cref="WebApiFeatures.Default"/>.</b> Like <c>fetch</c> it has to be asked
    /// for by name — <see cref="WebApiOptionsExtensions.UseStorage(Options)"/> — because it is the one web
    /// API that hands a script somewhere to <i>keep</i> things. A host that says "give me the web APIs"
    /// expects globals, not a place where the script it ran last week left data for the script it runs now;
    /// with a host-supplied provider that is exactly what this is, and even the in-box default gives a
    /// script state that outlives an evaluation. Everything in <see cref="WebApiFeatures.Default"/> is
    /// surprise-free in that sense, and this is how it stays that way.
    /// </para>
    /// <para>
    /// The two providers are read once, when the engine is built. Assigning the same instance to both makes
    /// <c>localStorage</c> and <c>sessionStorage</c> one store, which is a thing a browser can never do and
    /// is occasionally what a host wants.
    /// </para>
    /// </remarks>
    public class StorageOptions
    {
        /// <summary>
        /// Five mebibytes: the quota browsers converged on, and what a defaulted
        /// <see cref="InMemoryStorageProvider"/> is built with.
        /// </summary>
        internal const long DefaultMaxTotalBytes = 5 * 1024 * 1024;

        /// <summary>
        /// The map behind <c>localStorage</c>. <see langword="null"/> — the default — gives each engine its
        /// own <see cref="InMemoryStorageProvider"/>, which stores nothing anywhere and dies with the engine.
        /// </summary>
        /// <remarks>
        /// <b>Persistence and cross-engine sharing are entirely the provider's business.</b> Assign an
        /// instance of your own to put storage on disk, in a database or in a per-tenant cache; assign one
        /// instance to an <see cref="Options"/> object several engines share to give them one store, and
        /// remember that a provider reached from concurrently running engines must be thread-safe.
        /// </remarks>
        public StorageProvider? LocalStorageProvider { get; set; }

        /// <summary>
        /// The map behind <c>sessionStorage</c>. <see langword="null"/> — the default — gives each engine its
        /// own <see cref="InMemoryStorageProvider"/>, separate from <c>localStorage</c>'s.
        /// </summary>
        /// <remarks>
        /// With the in-box provider the two globals differ only in that they are two stores: the lifetime
        /// difference the names carry in a browser is something only a host-supplied provider can express.
        /// </remarks>
        public StorageProvider? SessionStorageProvider { get; set; }

        /// <summary>
        /// The quota a defaulted <see cref="InMemoryStorageProvider"/> enforces, in the UTF-16 bytes its
        /// documentation describes. Defaults to five mebibytes. A <c>setItem</c> that would exceed it throws
        /// a <c>DOMException</c> named <c>QuotaExceededError</c>, which the script can catch.
        /// </summary>
        /// <remarks>
        /// Read once, when the engine is built, and only for a provider this engine defaulted: a
        /// host-supplied provider enforces whatever limit it likes and this value never reaches it. There is
        /// no "unlimited" sentinel — <see cref="long.MaxValue"/> is how that is spelled.
        /// </remarks>
        public long MaxTotalBytes { get; set; } = DefaultMaxTotalBytes;
    }

    /// <summary>
    /// Settings for the Cache API: where a script's cached request/response pairs are kept. Requires .NET 8
    /// or higher.
    /// </summary>
    /// <remarks>
    /// Like every other option group this may be shared by any number of engines, including concurrent ones.
    /// Sharing one <see cref="Provider"/> between them is what makes them share a cache, and such a provider
    /// must be thread-safe — see <see cref="CacheStorageProvider"/>.
    /// </remarks>
    public class CacheOptions
    {
        /// <summary>
        /// Where the caches live, or <see langword="null"/> to give each engine a private
        /// <see cref="InMemoryCacheStorageProvider"/> of its own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The default is deliberately per engine rather than one instance on this options object: two
        /// engines built from one shared <see cref="Options"/> would otherwise see each other's cached data,
        /// which is not something a host asking for a web API should inherit. Sharing is what assigning a
        /// provider here is <i>for</i>, and it is then an explicit act.
        /// </para>
        /// <para>
        /// <b>The default has no quota</b>, so a script can go on caching until the process runs out of
        /// memory; a host running untrusted script implements <see cref="CacheStorageProvider"/> and throws
        /// <see cref="CacheQuotaExceededException"/> to impose one. Cached data also survives
        /// <c>Engine.Advanced.RestoreGlobalSnapshot</c>, which reverts the engine's global bindings and not
        /// host storage.
        /// </para>
        /// <para>
        /// Read once, when the engine is built, so assigning it afterwards does not affect an engine that
        /// already exists.
        /// </para>
        /// </remarks>
        public CacheStorageProvider? Provider { get; set; }
    }

    /// <summary>
    /// Settings for <c>fetch</c>: which requests it may make, how large an answer it will read and how long it
    /// will wait. Requires .NET 8 or higher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Enabling <c>fetch</c> gives the script the host's network position.</b> Anything the host process can
    /// reach — an internal service, a cloud metadata endpoint, a database admin port — a script can reach too
    /// unless something here says otherwise. The defaults bound the obvious resource questions (size, time,
    /// concurrency) and restrict the scheme, but they cannot know which <i>hosts</i> are legitimate: that is
    /// what <see cref="UrlFilter"/> is for, and a deployment exposed to untrusted script wants one.
    /// </para>
    /// <para>
    /// <b><see cref="WebApiFeatures.WebSocket"/> reads this group too</b>, because a socket is outbound
    /// access to the same hosts from the same process. <see cref="AllowedSchemes"/> admits <c>ws</c> wherever
    /// it admits <c>http</c> and <c>wss</c> wherever it admits <c>https</c> (naming <c>ws</c> or <c>wss</c>
    /// outright works too); <see cref="UrlFilter"/> is shown the <c>ws:</c> URL itself;
    /// <see cref="MaxResponseBytes"/> is the ceiling on one incoming message and on the bytes <c>send()</c>
    /// may have unwritten; <see cref="MaxConcurrentRequests"/> is how many sockets one engine may have open,
    /// counted separately from the requests in flight; and <see cref="Timeout"/> bounds the opening and
    /// closing handshakes rather than the connection, which is meant to be long-lived. The other three
    /// members are about HTTP alone and a socket ignores them: <see cref="HttpClient"/>,
    /// <see cref="HttpClientFactory"/> and <see cref="MaxRedirects"/> — the WebSocket handshake is not
    /// allowed to be redirected at all.
    /// </para>
    /// <para>
    /// Like every other option group this may be shared by any number of engines, including concurrent ones:
    /// nothing on it is engine-affine. Two members carry a thread-safety obligation, because they are called
    /// from whichever thread the HTTP stack happens to be on — see <see cref="UrlFilter"/> and
    /// <see cref="HttpClientFactory"/>.
    /// </para>
    /// <para>
    /// <b>Not only <c>fetch</c> reads this.</b> <see cref="WebApiFeatures.EventSource"/> is a second, separate
    /// grant of outbound network access and takes its transport and its policy from here too; three members
    /// mean something different for a stream than for a document, and that flag's documentation says which.
    /// </para>
    /// </remarks>
    public class FetchOptions
    {
        /// <summary>
        /// The <see cref="System.Net.Http.HttpClient"/> every request goes through, or <see langword="null"/>
        /// to use a lazily-created client Jint shares process-wide.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Supplying one is how a host takes control of the transport: a <c>DelegatingHandler</c> is the seam
        /// for authentication, per-tenant headers, logging and test doubles, and owning the client is how a
        /// host controls its lifetime — the shared default is deliberately never disposed.
        /// </para>
        /// <para>
        /// Jint drives redirects itself so that every hop is re-checked against <see cref="AllowedSchemes"/>
        /// and <see cref="UrlFilter"/>; a client whose handler has <c>AllowAutoRedirect</c> left on would
        /// follow them underneath that check, so set it to <see langword="false"/> on a handler you supply.
        /// </para>
        /// </remarks>
        public System.Net.Http.HttpClient? HttpClient { get; set; }

        /// <summary>
        /// A per-request source of <see cref="System.Net.Http.HttpClient"/>s, which wins over
        /// <see cref="HttpClient"/> when both are set.
        /// </summary>
        /// <remarks>
        /// Called on the engine's thread, once per <c>fetch</c> call, before anything is sent — so it may read
        /// per-request host state through <c>engine.Advanced.HostDefined</c>, which is how a multi-tenant host
        /// hands each tenant its own <c>IHttpClientFactory</c>-managed client. It must not return
        /// <see langword="null"/>, and it must not block: it runs while the calling script is suspended.
        /// </remarks>
        public Func<Engine, System.Net.Http.HttpClient>? HttpClientFactory { get; set; }

        /// <summary>
        /// The URL schemes a request may use. Defaults to <c>https</c> and <c>http</c>; a request to anything
        /// else is refused with a <c>TypeError</c> before a socket is opened.
        /// </summary>
        /// <remarks>
        /// Compared ASCII-case-insensitively against the scheme the WHATWG URL parser produced, which is
        /// already lowercased. Emptying the list refuses every request. Read once per request, so a host may
        /// change it between evaluations; it is not read on a background thread.
        /// </remarks>
        public List<string> AllowedSchemes { get; } = new() { "https", "http" };

        /// <summary>
        /// The last word on whether a request may be made. Defaults to allowing everything the scheme list
        /// already admitted.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Re-run on every redirect hop</b>, which is the point: a filter that only saw the first URL would
        /// be defeated by a server answering <c>302 Location: http://169.254.169.254/</c>. The <see cref="Uri"/>
        /// passed is the absolute URL about to be requested.
        /// </para>
        /// <para>
        /// <b>Must be thread-safe and must not block.</b> The first call is on the engine's thread; the
        /// redirect calls are on whichever thread the HTTP stack completed the previous hop on. It must not
        /// touch the <see cref="Engine"/> — the engine is not thread-safe, and the script that started the
        /// fetch may be running.
        /// </para>
        /// <para>
        /// Refusing is a <c>TypeError</c> rejection carrying no detail about why, which is deliberate: a
        /// message naming the rule would let a script map the host's internal network by probing it.
        /// </para>
        /// </remarks>
        public Func<Uri, bool> UrlFilter { get; set; } = static _ => true;

        /// <summary>
        /// The most bytes a response body may decompress to. Defaults to 32 MiB; a body that exceeds it is
        /// abandoned and reported as a <c>TypeError</c>.
        /// </summary>
        /// <remarks>
        /// Counted after decompression, so a compression bomb is bounded by the number a host actually chose
        /// rather than by its compressed size, and enforced on the running total as the body streams — the
        /// connection is dropped at the chunk that crosses the line.
        /// <para>
        /// <b>Where the failure surfaces depends on when it is known.</b> A <c>Content-Length</c> that already
        /// exceeds the cap is refused while the headers are being read, so the <c>fetch</c> promise itself
        /// rejects. A body that only breaks the cap later cannot reject that promise, because it has already
        /// resolved with the response — as the standard prescribes and as a browser does — so it <i>errors the
        /// response's body stream</i> instead, and every consumer of that body reports it.
        /// </para>
        /// <para>
        /// <b><see cref="long.MaxValue"/> means unlimited</b>, and zero or less refuses every body. This is
        /// deliberately unlike the execution constraints' saturated sentinels, where
        /// <c>LimitMemory(long.MaxValue)</c> removes the constraint: there is no constraint to remove here, and
        /// a cap that quietly meant "no cap" would be the more dangerous reading of the two.
        /// </para>
        /// </remarks>
        public long MaxResponseBytes { get; set; } = 32 * 1024 * 1024;

        /// <summary>
        /// How many redirects one request may follow before the promise rejects with a <c>TypeError</c>.
        /// Defaults to 20, which is what browsers use.
        /// </summary>
        public int MaxRedirects { get; set; } = 20;

        /// <summary>
        /// How long one <c>fetch</c> may take, from the call to the last byte of the body. Defaults to 30
        /// seconds; exceeding it rejects with a <c>TimeoutError</c> <c>DOMException</c>, the same failure
        /// <c>AbortSignal.timeout()</c> produces.
        /// </summary>
        /// <remarks>
        /// Enforced CLR-side, on the request's cancellation token, deliberately: it must fire even for an
        /// engine nobody is pumping, so that an abandoned request cannot hold a socket open forever.
        /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> and any non-positive value mean no timeout, which leaves the
        /// underlying <see cref="System.Net.Http.HttpClient"/>'s own timeout as the only bound.
        /// </remarks>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How many requests one engine may have in flight at once. Defaults to 10; a <c>fetch</c> call that
        /// would exceed it rejects with a <c>TypeError</c> rather than queueing.
        /// </summary>
        /// <remarks>
        /// Rejecting rather than queueing is the honest answer: a queue would turn a script's burst into an
        /// unbounded backlog of held sockets, and a script that wants to throttle itself can already do so with
        /// the promise it gets back. Counted per engine, and a request stops counting when its promise settles
        /// or when the engine's globals are restored. <see cref="int.MaxValue"/> is the way to spell
        /// effectively unbounded; zero or less refuses every request.
        /// </remarks>
        public int MaxConcurrentRequests { get; set; } = 10;
    }

    /// <summary>
    /// Settings for the <c>console</c> object. Requires .NET 8 or higher.
    /// </summary>
    public class ConsoleOptions
    {
        /// <summary>
        /// Where <c>console</c> output goes. Defaults to <see cref="ConsoleSink.Null"/>, which discards
        /// everything — enabling the feature never starts writing to the host's standard output by surprise.
        /// </summary>
        /// <remarks>
        /// The sink is read afresh on every emit, so a host may swap it between evaluations. A sink assigned
        /// to an <see cref="Options"/> instance shared by concurrently running engines is called from each of
        /// their threads and must be thread-safe; one belonging to a single engine is only ever called on
        /// that engine's thread. Assigning <see langword="null"/> is read back as
        /// <see cref="ConsoleSink.Null"/>.
        /// </remarks>
        public ConsoleSink Sink { get; set; } = ConsoleSink.Null;
    }

    /// <summary>
    /// Settings for <c>setTimeout</c>, <c>setInterval</c> and their <c>clear</c> counterparts. Requires
    /// .NET 8 or higher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Timers fire only while the engine is being pumped, and Jint never starts a thread to pump it.</b>
    /// A callback runs on the first drain of the event loop at or after its due time — a blocking
    /// <c>UnwrapIfPromise</c>, an <c>await</c> of <c>EvaluateAsync</c>, or the host's own
    /// <c>engine.Advanced.ProcessTasks()</c> loop. An engine nobody pumps never fires a timer, which is what
    /// makes this safe in a request handler that returns as soon as the script does.
    /// </para>
    /// </remarks>
    public class TimerOptions
    {
        /// <summary>
        /// The clock the timers are scheduled against. Defaults to <see cref="TimeProvider.System"/>; a fake
        /// one makes a suite that exercises timers deterministic and instant.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>performance.now()</c> and <c>performance.timeOrigin</c> read this same clock, so a fake one
        /// drives the timers and the high-resolution readings coherently. It is deliberately not the engine's
        /// <see cref="Options.TimeSystem"/>, which is what <c>Date</c> is built on: one is a monotonic clock
        /// for measuring durations, the other a wall clock for naming instants.
        /// </para>
        /// Read once, when the engine is built, so assigning it afterwards does not affect an engine that
        /// already exists. Only <see cref="TimeProvider.GetTimestamp"/>,
        /// <see cref="TimeProvider.GetUtcNow"/> (once, for the time origin) and
        /// <see cref="TimeProvider.GetElapsedTime(long, long)"/> are ever called:
        /// <see cref="TimeProvider.CreateTimer"/> is deliberately not, because a background timer would run
        /// script off the engine's thread. Assigning <see langword="null"/> is read back as
        /// <see cref="TimeProvider.System"/>.
        /// </remarks>
        public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

        /// <summary>
        /// The most timers one engine may have registered at once. Defaults to 1000. A
        /// <c>setTimeout</c> or <c>setInterval</c> call that would exceed it throws a <c>DOMException</c>
        /// named <c>QuotaExceededError</c> — the script sees a normal exception it can catch, and the engine
        /// stays usable.
        /// </summary>
        /// <remarks>
        /// Counts timers that are <i>registered</i>, so a <c>setInterval</c> occupies one slot for as long as
        /// it runs and a fired <c>setTimeout</c> frees its slot before its callback runs. Read once, when the
        /// engine is built. There is no "unlimited" sentinel: <see cref="int.MaxValue"/> is the way to spell
        /// effectively unbounded, and a value of zero or less refuses every timer.
        /// </remarks>
        public int MaxActiveTimers { get; set; } = 1000;

        /// <summary>
        /// How much of a pump an engine may spend running <c>requestIdleCallback</c> callbacks. Defaults to
        /// 50 milliseconds, which is the ceiling the standard itself recommends for an idle deadline.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It lives beside the timer settings because it is measured on the same
        /// <see cref="TimeProvider"/>, and because the <c>timeout</c> option of an idle callback rides the
        /// same queue and counts against the same <see cref="MaxActiveTimers"/> cap.
        /// </para>
        /// <para>
        /// An idle period begins when a pump has run out of everything else — every queued job, every
        /// scheduler task, every due timer — and ends when this budget elapses or the callbacks run out,
        /// whichever comes first; whatever is left waits for the next pump. It is what
        /// <c>IdleDeadline.timeRemaining()</c> counts down from, so a callback that chunks its work against
        /// that value is really being told how much of this budget is left.
        /// </para>
        /// <para>
        /// <b>Zero or less means the host has no idle time</b>, and then an idle callback runs only if it was
        /// requested with a <c>timeout</c> — which is the honest setting for a host that pumps the engine on a
        /// hard deadline it does not want script to eat into. Read once, when the engine is built.
        /// </para>
        /// </remarks>
        public TimeSpan IdleBudget { get; set; } = TimeSpan.FromMilliseconds(50);
    }

    /// <summary>
    /// Settings for the engine's diagnostics channel — the script errors nobody caught. Requires .NET 8 or
    /// higher.
    /// </summary>
    public class DiagnosticsOptions
    {
        /// <summary>
        /// Where an unhandled promise rejection, a value handed to <c>reportError</c>, and an exception that
        /// escaped a timer callback or an event listener are reported. Defaults to <see langword="null"/>,
        /// which is not a sink that discards but the absence of a channel: nothing is reported, and an
        /// exception escaping a callback the engine invoked erupts as it always did.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Setting this changes behaviour, which is why it has no default.</b> With a sink, a
        /// <c>JavaScriptException</c> from a timer handler or an event listener is reported and the engine
        /// carries on — the exception behaviour HTML and DOM specify for those callbacks. Errors that bound
        /// execution (a timeout, a cancellation, the statement, memory and recursion budgets) are never
        /// reported and always erupt. <see cref="DiagnosticsSink.Null"/> is the way to say "report and
        /// continue, and discard the report".
        /// </para>
        /// <para>
        /// Read once, when the engine is built, so assigning it afterwards does not affect an engine that
        /// already exists — unlike <see cref="ConsoleOptions.Sink"/>, because this one also decides whether a
        /// callback's exception erupts, and that contract has to hold still for an engine's lifetime. A sink
        /// on an <see cref="Options"/> instance shared by concurrently running engines is called from each of
        /// their threads and must be thread-safe; see <see cref="DiagnosticsSink"/> for that and for what may
        /// be done with the values a report carries.
        /// </para>
        /// </remarks>
        public DiagnosticsSink? Sink { get; set; }
    }
}

/// <summary>
/// The web platform APIs an engine can be asked to expose. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// Unlike TC39 built-ins, which Jint registers unconditionally, WHATWG web APIs are host APIs: an engine
/// embedded in a workflow runner or a template renderer has no business carrying them, so they are installed
/// only when named here.
/// </para>
/// <para>
/// The bit layout is fixed ahead of the implementations so that a value persisted by a host keeps its meaning
/// as the surface grows. A flag is declared here only once the feature behind it actually exists, so that
/// naming one can never compile into an engine that silently does not have it.
/// </para>
/// <para>
/// <see cref="Default"/> grows as each non-network feature lands, and <b>will never include the fetch
/// flag</b>: outbound network access from script is a decision a host has to make explicitly, never one it
/// inherits from asking for "the web APIs".
/// </para>
/// </remarks>
[Flags]
public enum WebApiFeatures
{
    /// <summary>
    /// No web API is installed. This is the default, and the engine is then indistinguishable from one built
    /// by a Jint that never had this feature.
    /// </summary>
    None = 0,

    /// <summary>
    /// The <c>console</c> object (<c>log</c>, <c>warn</c>, <c>group</c>, <c>count</c>, <c>time</c>, …). Output
    /// goes to <see cref="Options.ConsoleOptions.Sink"/>, which discards it unless the host sets one.
    /// </summary>
    Console = 1 << 0,

    /// <summary>
    /// <c>setTimeout</c>, <c>setInterval</c>, <c>clearTimeout</c>, <c>clearInterval</c> and
    /// <c>queueMicrotask</c>. Timers fire only while the engine is being pumped and no thread is ever started
    /// to pump it — see <see cref="Options.TimerOptions"/>.
    /// </summary>
    Timers = 1 << 1,

    /// <summary>
    /// <c>TextEncoder</c> and <c>TextDecoder</c> — https://encoding.spec.whatwg.org/#api. The encoder is
    /// UTF-8 only, as the standard requires; the decoder reads UTF-8, UTF-16LE and UTF-16BE.
    /// <para>
    /// Their streaming counterparts, <c>TextEncoderStream</c> and <c>TextDecoderStream</c>, need
    /// <see cref="Streams"/> as well and are installed only when both flags are present.
    /// </para>
    /// </summary>
    Encoding = 1 << 2,

    /// <summary>
    /// The <c>atob</c> and <c>btoa</c> functions —
    /// https://html.spec.whatwg.org/multipage/webappapis.html#atob.
    /// </summary>
    Base64 = 1 << 3,

    /// <summary>
    /// The global <c>structuredClone</c> function, which deep-clones a value through the HTML Standard's
    /// structured-clone algorithm and can transfer <c>ArrayBuffer</c>s into the clone.
    /// </summary>
    StructuredClone = 1 << 4,

    /// <summary>
    /// The <c>crypto</c> object: <c>getRandomValues</c> and <c>randomUUID</c>, both backed by the BCL's
    /// cryptographically secure generator, plus <c>crypto.subtle</c> and the <c>CryptoKey</c> interface
    /// object. <c>subtle</c> carries <c>digest</c> (SHA-1, SHA-256, SHA-384, SHA-512), <c>sign</c>,
    /// <c>verify</c>, <c>encrypt</c>, <c>decrypt</c>, <c>generateKey</c>, <c>importKey</c> and
    /// <c>exportKey</c> over HMAC and AES-GCM, with <c>raw</c> and <c>jwk</c> key formats. Key derivation,
    /// key wrapping and the asymmetric algorithms are not implemented and are absent rather than
    /// present-and-throwing, so feature detection sees the truth. Neither <c>subtle</c> nor
    /// <c>CryptoKey</c> has a flag of its own: <c>subtle</c> is a readonly attribute of the very same
    /// <c>Crypto</c> interface and <c>CryptoKey</c> is the type of what it hands out, so asking for one is
    /// asking for all three.
    /// </summary>
    Crypto = 1 << 5,

    /// <summary>
    /// The <c>performance</c> object — <c>now()</c>, <c>timeOrigin</c>, and the User Timing surface
    /// (<c>mark</c>, <c>measure</c>, <c>getEntries</c> and friends) with the <c>PerformanceEntry</c>,
    /// <c>PerformanceMark</c> and <c>PerformanceMeasure</c> interface objects behind it. Every reading comes
    /// from the clock in <see cref="Options.TimerOptions.TimeProvider"/>, so a fake one drives them and the
    /// timers together. There is no <c>PerformanceObserver</c>, and the entry buffer is bounded rather than
    /// unbounded — see <c>PerformanceInstance</c>.
    /// </summary>
    Performance = 1 << 6,

    /// <summary>
    /// <c>Event</c>, <c>CustomEvent</c>, <c>EventTarget</c>, <c>AbortController</c> and <c>AbortSignal</c> —
    /// the DOM event and cancellation model, without the node tree a browser dispatches events through.
    /// <c>AbortSignal.timeout()</c> schedules on the same queue the timers use, so it too fires only while the
    /// engine is being pumped.
    /// </summary>
    Events = 1 << 7,

    /// <summary>
    /// The <c>URL</c> and <c>URLSearchParams</c> interfaces — the whole of the WHATWG URL Standard's API,
    /// on the specification's own parser rather than on <see cref="Uri"/>.
    /// </summary>
    Url = 1 << 8,

    /// <summary>
    /// <c>Blob</c>, <c>File</c> and <c>FormData</c> — in-memory byte sequences and the ordered entry list a
    /// form submission is made of. Serializing a <c>FormData</c> as <c>multipart/form-data</c> is a body's
    /// business, so it arrives with <see cref="Fetch"/>.
    /// </summary>
    /// <remarks>
    /// <c>Blob.stream()</c> answers a real <c>ReadableStream</c> whether or not <see cref="Streams"/> is also
    /// enabled — the interface is always there, and the flag only decides whether <c>ReadableStream</c> is a
    /// global a script can name. <see cref="Default"/> has both.
    /// </remarks>
    Files = 1 << 9,

    /// <summary>
    /// <c>fetch</c>, <c>Headers</c>, <c>Request</c> and <c>Response</c> — outbound HTTP from script.
    /// <b>Never part of <see cref="Default"/>:</b> a host asking for "the web APIs" must not inherit the
    /// ability to make network requests, so this flag is only ever set by naming it or by calling
    /// <c>options.UseFetch()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implies <see cref="Events"/>, <see cref="Url"/> and <see cref="Files"/>, whose interfaces are part of
    /// fetch's own surface rather than optional extras: a <c>Request</c> always has an <c>AbortSignal</c>, its
    /// URL is a WHATWG URL, and <c>response.blob()</c> answers with a <c>Blob</c>. The closure is computed
    /// when the engine is built, so <c>options.WebApi.Features</c> still reads back exactly what the host
    /// asked for — <c>Features == WebApiFeatures.Fetch</c> stays true — while the engine carries all four.
    /// </para>
    /// <para>
    /// Read <see cref="Options.FetchOptions"/> before enabling this: what a script can reach is the host
    /// process's network position, and the defaults bound resources rather than destinations.
    /// </para>
    /// </remarks>
    Fetch = 1 << 10,

    /// <summary>
    /// The <c>navigator</c> object, whose single member is <c>userAgent</c> — the one thing WinterTC's
    /// Minimum Common API (https://min-common-api.proposal.wintertc.org/) requires of it and the one a script
    /// identifies a runtime by. Everything else a browser's <c>Navigator</c> carries describes a user agent
    /// with a user, a document and a network stack, so it is absent rather than faked.
    /// </summary>
    Navigator = 1 << 11,

    /// <summary>
    /// <c>ReadableStream</c>, <c>WritableStream</c>, <c>TransformStream</c> and the two queuing strategies
    /// — the WHATWG Streams Standard, including <c>tee()</c>, <c>pipeTo()</c>/<c>pipeThrough()</c> with
    /// <c>AbortSignal</c> support, asynchronous iteration of a readable stream, and readable byte streams:
    /// <c>new ReadableStream({ type: "bytes" })</c>, <c>ReadableByteStreamController</c> with
    /// <c>autoAllocateChunkSize</c> and <c>byobRequest</c>, and BYOB reading through
    /// <c>getReader({ mode: "byob" })</c>. Transferring a stream through <c>postMessage()</c> is the one
    /// part that is absent, there being nothing to transfer it to.
    /// </summary>
    Streams = 1 << 12,

    /// <summary>
    /// The <c>scheduler</c> object — <c>postTask()</c> and <c>yield()</c> — with <c>TaskController</c>,
    /// <c>TaskSignal</c> and <c>TaskPriorityChangeEvent</c>: prioritized tasks, from
    /// https://wicg.github.io/scheduling-apis/. Tasks run only while the engine is being pumped, exactly as
    /// the timers do, and a <c>delay</c> rides the same timer queue.
    /// </summary>
    Scheduler = 1 << 13,

    /// <summary>
    /// <c>MessageChannel</c>, <c>MessagePort</c> and <c>MessageEvent</c> — the HTML Standard's channel
    /// messaging, https://html.spec.whatwg.org/multipage/web-messaging.html. A message is structured-cloned
    /// when it is posted and delivered as an event-loop task, so a port fires only while the engine is being
    /// pumped. The same ports can span <b>two</b> engines through
    /// <c>Engine.Advanced.CreateMessagePortPair</c>, which needs this flag on both of them. Transferring a
    /// port through a port is not supported.
    /// </summary>
    Messaging = 1 << 14,

    /// <summary>
    /// The <c>reportError</c> function, which hands a value to the engine's diagnostics channel as though it
    /// were an uncaught exception —
    /// https://html.spec.whatwg.org/multipage/webappapis.html#dom-reporterror. Where the report goes is
    /// <see cref="Options.DiagnosticsOptions.Sink"/>; with no sink the call is a no-op, and it never throws.
    /// The flag governs only whether script can <i>call</i> it: the sink itself is armed by being set, and
    /// reports unhandled promise rejections and callback failures whether or not this is enabled.
    /// </summary>
    Reporting = 1 << 15,

    /// <summary>
    /// <c>localStorage</c>, <c>sessionStorage</c> and the <c>Storage</c> interface —
    /// https://html.spec.whatwg.org/multipage/webstorage.html. <b>Deliberately not part of
    /// <see cref="Default"/>:</b> like fetch it has to be named, because it is the one web API that gives a
    /// script somewhere to keep things, and where that somewhere is — memory, a file, a tenant's row in a
    /// database — is <see cref="Options.StorageOptions.LocalStorageProvider"/>'s business.
    /// </summary>
    Storage = 1 << 16,

    /// <summary>
    /// <c>EventSource</c> and <c>MessageEvent</c> — server-sent events,
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html. <b>Never part of
    /// <see cref="Default"/>:</b> like <see cref="Fetch"/> it is outbound network access, and it is a
    /// <i>separate</i> decision — enabling fetch does not enable this, and enabling this does not enable
    /// fetch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implies <see cref="Events"/>, whose <c>EventTarget</c> an <c>EventSource</c> is and on whose timer
    /// queue the reconnect delay rides.
    /// </para>
    /// <para>
    /// <b>It reads its transport and its policy from <see cref="Options.FetchOptions"/></b>, the same group
    /// <c>fetch</c> uses — <see cref="Options.FetchOptions.HttpClient"/> or
    /// <see cref="Options.FetchOptions.HttpClientFactory"/>, <see cref="Options.FetchOptions.AllowedSchemes"/>,
    /// <see cref="Options.FetchOptions.UrlFilter"/> (re-run on every redirect hop <i>and</i> on every
    /// reconnection) and <see cref="Options.FetchOptions.MaxRedirects"/>. So a host that has written a policy
    /// for fetch has already written this one, and a host that enables only this still configures it through
    /// <c>options.WebApi.Fetch</c>. Two members mean something different here, because a connection is a
    /// stream rather than a document:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <see cref="Options.FetchOptions.Timeout"/> is <b>not</b> applied. A connection that is idle for an
    /// hour is what an event stream is for, so a deadline on the whole exchange would end every one of them.
    /// </description></item>
    /// <item><description>
    /// <see cref="Options.FetchOptions.MaxResponseBytes"/> does <b>not</b> bound the stream, which is
    /// unbounded by nature. It bounds <i>one event</i>: the data a single event accumulates before its blank
    /// line, which is what actually has to be held in memory. Exceeding it fails the connection, and — like
    /// every other failure the host's own limits produce — that failure does not reconnect.
    /// </description></item>
    /// <item><description>
    /// <see cref="Options.FetchOptions.MaxConcurrentRequests"/> bounds the streams one engine may have open,
    /// counted separately from the fetches in flight. A stream holds its socket for as long as it lives, so
    /// this is the setting that stops a script from opening them without end.
    /// </description></item>
    /// </list>
    /// </remarks>
    EventSource = 1 << 17,

    /// <summary>
    /// <c>WebSocket</c>, and the <c>CloseEvent</c> and <c>MessageEvent</c> interfaces its events are —
    /// a long-lived two-way connection from script.
    /// <b>Never part of <see cref="Default"/>:</b> like <see cref="Fetch"/> this is outbound network access,
    /// so it is only ever set by naming it or by calling <c>options.UseWebSocket()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implies <see cref="Events"/> — a <c>WebSocket</c> <i>is</i> an <c>EventTarget</c> — and
    /// <see cref="Files"/>, because <c>binaryType = "blob"</c> answers a binary message with a <c>Blob</c>.
    /// </para>
    /// <para>
    /// The policy is <see cref="Options.FetchOptions"/>, the same group <see cref="Fetch"/> reads: a socket
    /// reaches the same hosts from the same process. Read it before enabling this — in particular
    /// <see cref="Options.FetchOptions.UrlFilter"/>, which is shown the <c>ws:</c> or <c>wss:</c> URL, and
    /// <see cref="Options.FetchOptions.AllowedSchemes"/>, where <c>http</c> admits <c>ws</c> and
    /// <c>https</c> admits <c>wss</c>.
    /// </para>
    /// </remarks>
    WebSocket = 1 << 18,

    /// <summary>
    /// The <c>caches</c> object and the <c>Cache</c> and <c>CacheStorage</c> interfaces —
    /// https://w3c.github.io/ServiceWorker/#cache-interface. <b>Never part of
    /// <see cref="Default"/>:</b> a cache outlives the evaluation that filled it, so where the data goes and
    /// what bounds it are decisions a host makes rather than inherits — see
    /// <see cref="Options.CacheOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implies <see cref="Events"/>, <see cref="Url"/> and <see cref="Files"/> for the same reason
    /// <see cref="Fetch"/> does, and additionally installs <c>Headers</c>, <c>Request</c> and
    /// <c>Response</c>: a cache <i>is</i> a list of request/response pairs, so a script that cannot build one
    /// has nothing to put in it. What this flag does not bring is the network — <c>fetch</c> itself stays
    /// behind <see cref="Fetch"/>, and the two <c>Cache</c> methods that reach the network,
    /// <c>add</c> and <c>addAll</c>, reject with a <c>TypeError</c> naming the flag until it is enabled.
    /// Everything else works: a host can populate a cache from its own data and a script can read it back.
    /// </para>
    /// <para>
    /// Storage is delegated to <see cref="Options.CacheOptions.Provider"/>, which defaults to an in-memory
    /// store per engine with no quota at all.
    /// </para>
    /// </remarks>
    CacheApi = 1 << 19,

    /// <summary>
    /// <c>CompressionStream</c> and <c>DecompressionStream</c> — https://compression.spec.whatwg.org/ — for
    /// the three formats the standard's <c>CompressionFormat</c> names and this implementation supports:
    /// <c>gzip</c> (RFC 1952), <c>deflate</c> (the ZLIB wrapper of RFC 1950, as the standard defines that
    /// name) and <c>deflate-raw</c> (RFC 1951). <c>brotli</c> is not implemented and, like any other
    /// unsupported value, is a <c>TypeError</c>.
    /// </summary>
    /// <remarks>
    /// <b>Requires <see cref="Streams"/> as well</b>: both are transform streams, so naming this flag on
    /// its own installs nothing. The bit is 1 &lt;&lt; 20 rather than the next numerically free one — the
    /// bits below it are spoken for by features landing alongside this one, and the layout is fixed ahead
    /// of the implementations so that a value a host persisted keeps its meaning as the surface grows.
    /// </remarks>
    Compression = 1 << 20,

    /// <summary>
    /// <c>requestIdleCallback</c> and <c>cancelIdleCallback</c>, with <c>IdleDeadline</c> —
    /// https://w3c.github.io/requestidlecallback/. An engine has no frames, so an idle period is a pump that
    /// has run out of everything else and its deadline is
    /// <see cref="Options.TimerOptions.IdleBudget"/>; the <c>timeout</c> option rides the timer queue and
    /// counts against <see cref="Options.TimerOptions.MaxActiveTimers"/>.
    /// </summary>
    IdleCallback = 1 << 21,

    /// <summary>
    /// <c>addEventListener</c>, <c>removeEventListener</c>, <c>dispatchEvent</c> and <c>self</c> on the global
    /// scope, with the <c>ErrorEvent</c> and <c>PromiseRejectionEvent</c> interfaces and the
    /// <c>error</c>, <c>unhandledrejection</c> and <c>rejectionhandled</c> events the engine fires at it —
    /// https://html.spec.whatwg.org/multipage/webappapis.html#the-errorevent-interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implies <see cref="Events"/>: what these three operations register listeners on is an
    /// <c>EventTarget</c>, and what they dispatch is an <c>Event</c>, so installing them without that
    /// feature's machinery would ship operations with no types to use them on.
    /// </para>
    /// <para>
    /// The <b>global object itself is untouched</b> — it does not become an <c>EventTarget</c> and gains no
    /// prototype it did not have. The listener list lives on a synthetic target the engine keeps beside its
    /// timers, and the three operations are ordinary global functions bound to it; <c>event.target</c> and a
    /// listener's <c>this</c> are still the global object, which is what a browser reports.
    /// </para>
    /// <para>
    /// <b>The events feed <see cref="Options.DiagnosticsOptions.Sink"/>, they never replace it.</b> A listener
    /// calling <c>preventDefault()</c> suppresses a browser's console report; here the sink is still told,
    /// because a host's log is not something script may switch off. A listener can therefore observe an
    /// uncaught failure, and cannot hide one.
    /// </para>
    /// </remarks>
    GlobalEvents = 1 << 22,

    /// <summary>
    /// The web APIs a host normally wants: everything except outbound network access and persistent state.
    /// Today that is
    /// <see cref="Console"/>, <see cref="Timers"/>, <see cref="Encoding"/>, <see cref="Base64"/>,
    /// <see cref="StructuredClone"/>, <see cref="Crypto"/>, <see cref="Performance"/>, <see cref="Events"/>,
    /// <see cref="Url"/>, <see cref="Files"/>, <see cref="Navigator"/>, <see cref="Streams"/>,
    /// <see cref="Scheduler"/>, <see cref="Messaging"/>, <see cref="Reporting"/>, <see cref="Compression"/>,
    /// <see cref="IdleCallback"/> and <see cref="GlobalEvents"/>; it grows as further features land, and never
    /// comes to include fetch or <see cref="Storage"/>.
    /// </summary>
    Default = Console | Timers | Encoding | Base64 | StructuredClone | Crypto | Performance | Events | Url | Files | Navigator | Streams | Scheduler | Messaging | Reporting | Compression | IdleCallback | GlobalEvents,
}
#endif
