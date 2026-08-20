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
    /// Like every other option group this may be shared by any number of engines, including concurrent ones:
    /// nothing on it is engine-affine. Two members carry a thread-safety obligation, because they are called
    /// from whichever thread the HTTP stack happens to be on — see <see cref="UrlFilter"/> and
    /// <see cref="HttpClientFactory"/>.
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
        /// The most bytes a response body may decompress to. Defaults to 32 MiB; a response that exceeds it is
        /// abandoned and the promise rejects with a <c>TypeError</c>.
        /// </summary>
        /// <remarks>
        /// Counted after decompression, so a compression bomb is bounded by the number a host actually chose
        /// rather than by its compressed size. The bytes are buffered in memory, so this is also the ceiling on
        /// what one request can cost the process.
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
    /// cryptographically secure generator, plus <c>crypto.subtle.digest</c> for SHA-1, SHA-256, SHA-384 and
    /// SHA-512. Every other <c>SubtleCrypto</c> operation — everything needing key material — is not
    /// implemented and is absent rather than present-and-throwing, so feature detection sees the truth.
    /// <c>subtle</c> has no flag of its own because it is a readonly attribute of the very same
    /// <c>Crypto</c> interface: asking for one is asking for the other.
    /// </summary>
    Crypto = 1 << 5,

    /// <summary>
    /// The <c>performance</c> object: <c>now()</c> and <c>timeOrigin</c>. Both read the clock in
    /// <see cref="Options.TimerOptions.TimeProvider"/>, so a fake one drives them and the timers together.
    /// Marks, measures and the performance timeline are not implemented.
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
    /// form submission is made of. No streaming (<c>Blob.stream()</c> is absent) and no
    /// <c>multipart/form-data</c> serialization, which arrives with fetch.
    /// </summary>
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
    /// The web APIs a host normally wants: everything except outbound network access. Today that is
    /// <see cref="Console"/>, <see cref="Timers"/>, <see cref="Encoding"/>, <see cref="Base64"/>,
    /// <see cref="StructuredClone"/>, <see cref="Crypto"/>, <see cref="Performance"/>, <see cref="Events"/>,
    /// <see cref="Url"/> and <see cref="Files"/>; it grows as further features land, and never comes to
    /// include fetch.
    /// </summary>
    Default = Console | Timers | Encoding | Base64 | StructuredClone | Crypto | Performance | Events | Url | Files,
}
#endif
