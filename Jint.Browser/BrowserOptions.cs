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
    private int _maxRedirects = 20;

    /// <summary>
    /// What a page reports itself as. It does not reach <c>navigator.userAgent</c> in this version.
    /// </summary>
    /// <remarks>
    /// The engine has no user-agent setting: <c>navigator.userAgent</c> answers <c>"Jint/&lt;version&gt;"</c>
    /// and is not configurable, so this is carried for the network layer and the protocol's
    /// <c>Emulation.setUserAgentOverride</c> and is not yet read by anything a script can see. Making the two
    /// agree needs an engine option, which is a separate change.
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

    /// <summary>How long one turn of the page loop may run before it is cut short. Not yet applied.</summary>
    /// <remarks>
    /// The value is carried and documented so that a host can set it today, but nothing reads it in this
    /// version: bracketing each loop turn with an <c>OperationDeadlineConstraint</c> is the page-constraints
    /// change, and a limit that silently did not limit would be worse than one that says it does not yet.
    /// </remarks>
    public TimeSpan MaxTaskDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How much a page's scripts may allocate in one job chain, in bytes. Not yet applied.</summary>
    /// <remarks>
    /// Carried and not read, for the reason <see cref="MaxTaskDuration"/> gives. Zero means no limit.
    /// </remarks>
    public long MemoryLimit { get; set; }

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

    internal IReadOnlyList<Action<Options>> EngineConfiguration => _engineConfiguration;

    internal static string DefaultUserAgent { get; } =
        "Mozilla/5.0 (compatible; Jint.Browser/" + typeof(BrowserOptions).Assembly.GetName().Version?.ToString(3) + "; +https://github.com/sebastienros/jint)";
}
