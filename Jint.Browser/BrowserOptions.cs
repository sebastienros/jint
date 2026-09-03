using System.Threading;

namespace Jint.Browser;

/// <summary>
/// What every page of a <see cref="Browser"/> is built from: its viewport, its budgets, and the engine
/// configuration a host adds on top.
/// </summary>
/// <remarks>
/// <para>
/// One instance configures a whole browser, and a page reads it when its engine is built — which is once per
/// top-level navigation, because a navigation is a new realm here and therefore a new engine. Mutating it
/// after a page exists affects the next navigation, not the running one.
/// </para>
/// <para>
/// The callbacks registered with <see cref="ConfigureEngine"/> run last, after the package has set the web-API
/// features, the sinks and the document base URL, so a host can override any of them. They run on the page
/// loop thread and must not capture state belonging to one engine, because they are invoked for every engine.
/// </para>
/// </remarks>
public sealed class BrowserOptions
{
    private readonly List<Action<Options>> _engineConfiguration = [];
    private string _userAgent = DefaultUserAgent;
    private TimeSpan _pumpIdle = TimeSpan.FromMilliseconds(50);
    private int _maxRecordedEvents = 1000;
    private long _maxDocumentBytes = 32 * 1024 * 1024;
    private long _maxSubresourceBytes = 8 * 1024 * 1024;
    private long _maxCapturedResponseBytes = 16 * 1024 * 1024;
    private TimeSpan _subresourceTimeout = TimeSpan.FromSeconds(30);
    private int _maxRedirects = 20;
    private TimeSpan? _maxTaskDuration;
    private long? _memoryLimit;
    private int _maxActiveTimers = 1000;
    private long _maxResponseBytes = 32 * 1024 * 1024;
    private TimeSpan _fetchTimeout = TimeSpan.FromSeconds(30);
    private int _maxDomNodes;

    /// <summary>What a page reports itself as, in script and on the wire.</summary>
    /// <remarks>
    /// <para>
    /// It is what <c>navigator.userAgent</c> answers and what every request the page makes carries, so the
    /// two never disagree. The engine's own <c>Navigator</c> publishes <c>"Jint/&lt;version&gt;"</c> and is
    /// not configurable; a page shadows that member with this value, which is why a host setting it here
    /// changes what a script reads.
    /// </para>
    /// <para>
    /// A protocol client's <c>Emulation.setUserAgentOverride</c> takes precedence over it for the page it
    /// was sent to, which is what an override means.
    /// </para>
    /// </remarks>
    public string UserAgent
    {
        get => _userAgent;
        set => _userAgent = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The size and pixel ratio every page reports; 1280 × 720 at a ratio of 1 by default.</summary>
    public Viewport Viewport { get; set; } = Viewport.Default;

    /// <summary>Whether a page records what its scripts got wrong into <see cref="Page.Errors"/>.</summary>
    /// <remarks>
    /// On by default, and it is not only a recording: installing a diagnostics sink is what turns an exception
    /// escaping a timer or a listener into a report instead of an eruption out of the pump, so a page with
    /// this off is a page a single bad callback can stop.
    /// </remarks>
    public bool RecordErrors { get; set; } = true;

    /// <summary>Whether a page records what its scripts printed into <see cref="Page.ConsoleMessages"/>.</summary>
    public bool RecordConsoleMessages { get; set; } = true;

    /// <summary>How many errors and how many console messages one page keeps; 1000 each.</summary>
    /// <remarks>
    /// A page in a loop can print without limit, so both recordings are ring-bounded rather than unbounded.
    /// Once the bound is reached the oldest entry is dropped.
    /// </remarks>
    public int MaxRecordedEvents
    {
        get => _maxRecordedEvents;
        set => _maxRecordedEvents = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxRecordedEvents must be positive.");
    }

    /// <summary>How long one turn of a page's loop may run before it is cut short; five seconds.</summary>
    /// <remarks>
    /// <para>
    /// A <b>turn</b> is one unit of work the page's thread does with the engine: one call posted by a
    /// <see cref="Page"/> member, one <c>ProcessTasks</c> drain (which is every timer callback, microtask,
    /// promise reaction and animation frame that was due), and one inline <c>&lt;script&gt;</c> run during a
    /// parse. Each is bracketed with an <c>OperationDeadlineConstraint</c>, which is one of the two
    /// constraints a per-entry reset never rewinds — a plain <c>Options.LimitExecutionTime</c> bounds
    /// neither a pumped job chain nor a sequence of host calls, which is why this exists.
    /// </para>
    /// <para>
    /// A turn that runs out fails differently depending on which turn it was. A <see cref="Page"/> call
    /// fails its own task with <see cref="TimeoutException"/>; a job chain and an inline script are recorded
    /// as a <see cref="PageErrorKind.BudgetExceeded"/> entry in <see cref="Page.Errors"/> and the page goes
    /// on — a page survives its scripts.
    /// </para>
    /// <para>
    /// <see cref="Timeout.InfiniteTimeSpan"/>, zero and any negative value all mean no time bound at all,
    /// which is what they mean to the rest of .NET. Closing the page still ends a running turn, because the
    /// page's cancellation token is registered with the engine separately.
    /// </para>
    /// </remarks>
    public TimeSpan MaxTaskDuration
    {
        get => _maxTaskDuration ?? TimeSpan.FromSeconds(5);
        set => _maxTaskDuration = value;
    }

    /// <summary>How much a page's scripts may allocate in one turn, in bytes; zero, meaning no limit.</summary>
    /// <remarks>
    /// <para>
    /// The allocation half of <see cref="MaxTaskDuration"/>, armed over the same turn with a
    /// <c>MemoryLimitConstraint</c> — the other constraint the per-entry reset never rewinds. It is a
    /// managed-allocation budget rather than a retained-memory one, so a script that allocates and discards
    /// reaches it just as a script that keeps everything does, and it is what bounds a page growing its DOM
    /// before <see cref="MaxDomNodes"/> ever counts a node.
    /// </para>
    /// <para>
    /// Exceeding it ends the turn the way <see cref="MaxTaskDuration"/> does. Setting it costs the
    /// interpreter's tight-loop lane, which is what a memory limit costs on any engine.
    /// </para>
    /// <para>
    /// <b>Zero is not available under <see cref="ForUntrustedContent"/>.</b> That profile requires a finite
    /// allocation budget, so it replaces an unset or unusable value with its own and this property then reads
    /// back what the pages are given.
    /// </para>
    /// </remarks>
    public long MemoryLimit
    {
        get => _memoryLimit ?? 0;
        set => _memoryLimit = value;
    }

    /// <summary>How many timers one page engine may have active at once; 1000.</summary>
    /// <remarks>
    /// The page-sized name for <c>Options.WebApi.Timers.MaxActiveTimers</c>, applied before the
    /// <see cref="ConfigureEngine"/> callbacks so a host can still override it per engine. A
    /// <c>requestAnimationFrame</c> batch rides the same queue and counts against the same cap.
    /// </remarks>
    public int MaxActiveTimers
    {
        get => _maxActiveTimers;
        set => _maxActiveTimers = value;
    }

    /// <summary>The most bytes one <c>fetch</c> or <c>XMLHttpRequest</c> response may be; 32 MiB.</summary>
    /// <remarks>
    /// The page-sized name for <c>Options.WebApi.Fetch.MaxResponseBytes</c>, applied before the
    /// <see cref="ConfigureEngine"/> callbacks. <see cref="MaxDocumentBytes"/> is the separate bound on a
    /// navigation, which reaches no engine.
    /// </remarks>
    public long MaxResponseBytes
    {
        get => _maxResponseBytes;
        set => _maxResponseBytes = value;
    }

    /// <summary>How long one <c>fetch</c> a script makes may take; 30 seconds.</summary>
    /// <remarks>
    /// The page-sized name for <c>Options.WebApi.Fetch.Timeout</c>, applied before the
    /// <see cref="ConfigureEngine"/> callbacks. It bounds a whole redirect chain rather than one hop, and it
    /// is unrelated to <see cref="NavigationOptions.Timeout"/>, which bounds a navigation.
    /// </remarks>
    public TimeSpan FetchTimeout
    {
        get => _fetchTimeout;
        set => _fetchTimeout = value;
    }

    /// <summary>How many DOM nodes one document may reach; zero, meaning no limit.</summary>
    /// <remarks>
    /// <para>
    /// One number, checked against the two quantities a page's DOM is made of. <b>After each parse</b>,
    /// against the nodes the document contains: a navigation whose document is larger fails with
    /// <see cref="NavigationFailedException"/> and nothing is shown. <b>As wrappers are created</b>, against
    /// the nodes one document has handed to script: the projection that would pass the limit throws a
    /// <c>RangeError</c> into the script that asked for it, which reaches <see cref="Page.Errors"/> when
    /// nothing catches it and never ends the page.
    /// </para>
    /// <para>
    /// The two are separate on purpose, so a page is bounded at roughly twice this number rather than at it:
    /// counting a document's own nodes against the script's allowance would make merely <i>walking</i> a
    /// document of the permitted size a refusal, and a framework touching most of its own tree is the
    /// ordinary case. Read it as "this big a document, and this many nodes handed to script".
    /// </para>
    /// <para>
    /// <b>It is the second bound on DOM growth, not the first.</b> <see cref="MemoryLimit"/> is what bounds a
    /// script building nodes it never hands to script — an <c>innerHTML</c> assignment materializes a subtree
    /// with no wrapper of its own — and this is what bounds the count once they are reached.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxDomNodes
    {
        get => _maxDomNodes;
        set => _maxDomNodes = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxDomNodes cannot be negative.");
    }

    /// <summary>The most bytes one document may be; 32 MiB by default.</summary>
    /// <remarks>
    /// It bounds the navigation itself rather than a script's <c>fetch</c>, which
    /// <c>Options.WebApi.Fetch.MaxResponseBytes</c> bounds and which a host changes through
    /// <see cref="ConfigureEngine"/>. A response that declares or reaches more is abandoned and the
    /// navigation fails with a <see cref="NavigationFailedException"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public long MaxDocumentBytes
    {
        get => _maxDocumentBytes;
        set => _maxDocumentBytes = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxDocumentBytes must be positive.");
    }

    /// <summary>The most bytes one subresource may be; 8 MiB by default.</summary>
    /// <remarks>
    /// A script, a module and a style sheet are bounded separately from the document that referenced them,
    /// because a page pulls many of them and one ceiling for the lot would be the wrong shape. A resource
    /// that declares or reaches more is abandoned: the element gets an <c>error</c> event and the page goes
    /// on loading, which is what a browser does with a resource it could not read.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public long MaxSubresourceBytes
    {
        get => _maxSubresourceBytes;
        set => _maxSubresourceBytes = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxSubresourceBytes must be positive.");
    }

    /// <summary>How many bytes of response body a page holds for a client to read back; 16 MiB.</summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing is copied until a protocol client asks for it.</b> The capture is armed by
    /// <c>Network.enable</c> and emptied by <c>Network.disable</c>, which is the protocol's own rule, so a
    /// page nobody is driving pays nothing for this at all.
    /// </para>
    /// <para>
    /// It is a bound on the <i>total</i> the page holds rather than on one body, and the oldest capture is
    /// dropped to stay under it — so <c>Network.getResponseBody</c> for a request a client waited too long to
    /// ask about answers that there is no body rather than the page growing without limit. A single response
    /// larger than the whole budget is not kept at all, because half a body is not the body.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public long MaxCapturedResponseBytes
    {
        get => _maxCapturedResponseBytes;
        set => _maxCapturedResponseBytes = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxCapturedResponseBytes cannot be negative.");
    }

    /// <summary>How long one subresource has to answer; 30 seconds by default.</summary>
    /// <remarks>
    /// It bounds one script, module or style-sheet load — and, taken together, the module phase of a load —
    /// so that a server that accepts a connection and then says nothing cannot hold a page in
    /// <c>loading</c> for ever. Exceeding it is an <c>error</c> at the element and a page error, not a failed
    /// navigation.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public TimeSpan SubresourceTimeout
    {
        get => _subresourceTimeout;
        set => _subresourceTimeout = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "SubresourceTimeout must be positive.");
    }

    /// <summary>How many redirects one navigation may follow; 20, which is what browsers use.</summary>
    /// <remarks>
    /// Every hop is re-checked against <see cref="BrowserContextOptions.UrlFilter"/> and the scheme list, so
    /// this bounds the length of a chain rather than what it may reach.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxRedirects
    {
        get => _maxRedirects;
        set => _maxRedirects = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "MaxRedirects cannot be negative.");
    }

    /// <summary>How long a page loop parks when it has nothing due; 50 ms by default.</summary>
    /// <remarks>
    /// It is a ceiling on the park rather than a polling interval: a timer coming due sooner shortens it, a
    /// request posted from another thread ends it at once, and so does closing the page. Nothing waits for it,
    /// so it costs latency only if a wake is ever missed — which is what it is a ceiling against.
    /// </remarks>
    public TimeSpan PumpIdle
    {
        get => _pumpIdle;
        set => _pumpIdle = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "PumpIdle must be positive.");
    }

    /// <summary>Adds a callback that configures every page engine, after the package has configured it.</summary>
    /// <param name="configure">The configuration to apply; it runs once per engine, on the page loop.</param>
    /// <returns>This instance, so calls chain.</returns>
    /// <remarks>
    /// It can change anything, including the settings that keep a page's values on its own thread: clearing
    /// <c>Options.Interop.CreateClrObject</c> makes the engine's own <c>ToObject</c> answer the object itself,
    /// so <see cref="Page.EvaluateAsync(string)"/> would hand the caller a value belonging to the page loop.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
    public BrowserOptions ConfigureEngine(Action<Options> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _engineConfiguration.Add(configure);
        return this;
    }

    /// <summary>Hardens every page of this browser for content nobody vouches for.</summary>
    /// <param name="limits">
    /// The engine limits to apply; <see cref="UntrustedCodeLimits.Default"/> when omitted, which is a preset
    /// to measure and adjust rather than a value to accept unread.
    /// </param>
    /// <returns>This instance, so calls chain.</returns>
    /// <remarks>
    /// <para>
    /// It applies <c>Options.ForUntrustedCode</c> to every page engine, from inside the package's own
    /// construction callback and therefore before any <see cref="ConfigureEngine"/> callback runs. The
    /// profile is re-expanded over whatever those callbacks wrote, so it wins over them: a callback that
    /// re-enables <c>eval</c> or saturates a limit has written to options the profile is expanded over again.
    /// A callback reaching further — <c>Options.Configure</c>, which runs after the realm and the host have
    /// been built — cannot declare a profile at all, and the engine refuses it rather than half-applying one.
    /// </para>
    /// <para>
    /// <b>What it costs a page.</b> The profile turns off <c>eval</c> and <c>new Function</c>, CLR interop,
    /// the debugger, experimental features and every module loader, and it bounds statements, wall-clock time,
    /// allocation, recursion, array size, regular expressions, promise waits, parsing and result conversion.
    /// A page that needs any of those needs a narrower posture than this, assembled by hand through
    /// <see cref="ConfigureEngine"/>. The web APIs a page is built from — timers, <c>fetch</c>,
    /// <c>XMLHttpRequest</c>, storage, workers — are untouched, because they are the page, and each is
    /// already bounded by a limit of its own.
    /// </para>
    /// <para>
    /// <b>What it changes here.</b> <see cref="MaxTaskDuration"/> and <see cref="MemoryLimit"/> take their
    /// values from the limits unless the host has already set them, and a value the host does set is what the
    /// page engines are given — so the turn bracket and the profile can never disagree about the budget.
    /// Every <see cref="BrowserContext"/> of this browser gets <see cref="BrowserContextOptions.BlockPrivateNetwork"/>
    /// on, unless its own options assigned that property, in which case the context keeps its choice.
    /// </para>
    /// <para>
    /// <b>Workers of an untrusted page are untrusted.</b> A worker's options are built by
    /// <c>WorkerRequest.CreateDefaultOptions</c>, which copies the parent's restrictive settings and replays
    /// its constraint <i>factories</i> — so an untrusted page's worker inherits the same bounds and the same
    /// hardened posture, while every grant is named again one at a time. Its own turns are bracketed the same
    /// way this page's are.
    /// </para>
    /// <para>
    /// One consequence worth knowing: the profile clears <c>Options.RetainFunctionSourceText</c>, which the
    /// package otherwise sets, so a stack in a recorded <see cref="PageError"/> names less than it does for
    /// an ordinary page.
    /// </para>
    /// <para>
    /// <b>Call it before the <see cref="Browser"/> is created.</b> A context reads the browser's posture when
    /// it is created, and the default context is created with the browser; the engine half is read at every
    /// navigation and so would still arrive, which is exactly the half-applied state this sentence exists to
    /// prevent.
    /// </para>
    /// </remarks>
    public BrowserOptions ForUntrustedContent(UntrustedCodeLimits? limits = null)
    {
        var resolved = limits ?? UntrustedCodeLimits.Default;

        _maxTaskDuration ??= resolved.MaxOperationDuration;

        // Not ??=, because zero is a value the host can have assigned and the profile cannot accept: an
        // untrusted engine with no allocation budget is the "limit that cannot be reached" the engine's own
        // rule refuses. Reading the property back has to say what the pages will actually be given.
        if (_memoryLimit is not { } memory || memory <= 0 || memory == long.MaxValue)
        {
            _memoryLimit = resolved.MemoryLimit;
        }

        UntrustedContent = resolved;
        return this;
    }

    internal IReadOnlyList<Action<Options>> EngineConfiguration => _engineConfiguration;

    /// <summary>The limits <see cref="ForUntrustedContent"/> named, or <see langword="null"/>.</summary>
    internal UntrustedCodeLimits? UntrustedContent { get; private set; }

    /// <summary>
    /// The limits a page engine is actually built with: the host's, with the memory budget the page's own
    /// turn bracket arms folded in so the two cannot state different numbers.
    /// </summary>
    /// <remarks>
    /// Only the allocation dimension is rewritten, and only when it is a value the profile accepts. The
    /// wall-clock one is not: <c>MaxOperationDuration</c> is armed by <c>UntrustedCodeLimits.BeginOperation</c>,
    /// which a page never calls — a page's turns are armed from <see cref="MaxTaskDuration"/> — so rewriting
    /// it would state a number nothing reads.
    /// </remarks>
    internal UntrustedCodeLimits? EffectiveUntrustedLimits
    {
        get
        {
            if (UntrustedContent is not { } limits)
            {
                return null;
            }

            var memory = MemoryLimit;
            return memory > 0 && memory != long.MaxValue && memory != limits.MemoryLimit
                ? limits with { MemoryLimit = memory }
                : limits;
        }
    }

    /// <summary>
    /// Whether a context of this browser blocks the private network unless its own options say otherwise.
    /// </summary>
    internal bool BlocksPrivateNetworkByDefault => UntrustedContent is not null;

    internal static string DefaultUserAgent { get; } =
        "Mozilla/5.0 (compatible; Jint.Browser/" + typeof(BrowserOptions).Assembly.GetName().Version?.ToString(3) + "; +https://github.com/sebastienros/jint)";
}
