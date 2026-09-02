using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Runtime;

/// <summary>
/// Everything one page keeps on its engine: the document it is showing, its viewport, its window objects and
/// its animation-frame lane.
/// </summary>
/// <remarks>
/// <para>
/// It is stored the way the binding's own per-engine state is — in a
/// <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed on the engine, rather than in
/// <c>Engine.HostDefined</c>, because that slot belongs to the embedder. Every member the window installer
/// puts on a shape reaches it through <see cref="Of"/>, which is what lets those members be static lambdas
/// shared by every engine.
/// </para>
/// <para>
/// One instance exists per engine, and one engine exists per top-level navigation, so "per page runtime" and
/// "per document" coincide. Everything here is touched on the page loop thread only.
/// </para>
/// </remarks>
internal sealed class PageRuntime
{
    private static readonly ConditionalWeakTable<Engine, PageRuntime> _runtimes = new();

    private readonly long _started;
    private IDocument? _document;
    private Observers.ObserverRealm? _observers;
    private Dom.Views.ViewRealm? _views;
    private List<JsMediaQueryList>? _mediaQueryLists;

    private PageRuntime(
        Engine engine,
        Page page,
        BrowserOptions options,
        PageRecorder recorder,
        PageNetwork network,
        PageNetworkRecorder requests,
        string documentUrl,
        string referrer)
    {
        Engine = engine;
        Page = page;
        Options = options;
        Recorder = recorder;
        Network = network;
        Requests = requests;
        Viewport = options.Viewport;
        Dom = DomRealm.Of(engine);
        Budget = PageBudget.For(engine, options);
        AnimationFrames = new AnimationFrameLane(this);
        DocumentUrl = documentUrl;
        Referrer = referrer;
        MutationObservers = new Observers.MutationObserverLane(this);
        _started = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    /// <summary>The engine this page runs in.</summary>
    internal Engine Engine { get; }

    /// <summary>The page, for the seams that have to reach the host — dialogs and navigation.</summary>
    internal Page Page { get; }

    /// <summary>The browser options every page of this browser was built from.</summary>
    internal BrowserOptions Options { get; }

    /// <summary>Where errors and console output are recorded.</summary>
    internal PageRecorder Recorder { get; }

    /// <summary>The context's client, URL filter, cookie jar and storage partition.</summary>
    internal PageNetwork Network { get; }

    /// <summary>The page's network log, which every document, script and stylesheet load reports to.</summary>
    internal PageNetworkRecorder Requests { get; }

    /// <summary>
    /// The module loader this document's module scripts and <c>import()</c> calls resolve against, or
    /// <see langword="null"/> when a host replaced the engine's loader with one of its own.
    /// </summary>
    internal Parsing.PageModuleScriptLoader? Modules { get; set; }

    /// <summary>
    /// What <c>document.readyState</c> answers: <c>loading</c>, <c>interactive</c> or <c>complete</c>.
    /// </summary>
    /// <remarks>
    /// It is the page's rather than AngleSharp's because <c>Document.ReadyState</c>'s setter is protected and
    /// unreachable from outside its assembly, so the three transitions and the <c>readystatechange</c> events
    /// that go with them are the parser driver's to make. AngleSharp's own value advances on its own schedule
    /// and is read at exactly one point — the moment it starts the deferred queue.
    /// </remarks>
    internal string ReadyState { get; set; } = "loading";

    /// <summary>The viewport this page answers dimension and media queries from.</summary>
    /// <remarks>
    /// Settable through <see cref="SetViewport"/> only, because a change has to be announced: every
    /// <c>MediaQueryList</c> the page is holding recomputes and fires <c>change</c> if its answer moved.
    /// </remarks>
    internal Viewport Viewport { get; private set; }

    /// <summary>The DOM binding state of this engine.</summary>
    internal DomRealm Dom { get; }

    /// <summary>
    /// The two constraints one turn of this engine's loop is bracketed with, held here because the engine is
    /// where they were registered and this is what one engine's page state is.
    /// </summary>
    internal PageBudget Budget { get; }

    /// <summary>The <c>requestAnimationFrame</c> lane, run as a batch on the engine's timer queue.</summary>
    internal AnimationFrameLane AnimationFrames { get; }

    /// <summary>Where mutation records wait for the microtask checkpoint that delivers them.</summary>
    internal Observers.MutationObserverLane MutationObservers { get; }

    /// <summary>The observer interface objects of this engine, built on first use.</summary>
    internal Observers.ObserverRealm Observers => _observers ??= new Observers.ObserverRealm(this);

    /// <summary>The DOM views of this engine — <c>DOMParser</c>, <c>XMLSerializer</c>, <c>Selection</c>.</summary>
    internal Dom.Views.ViewRealm Views => _views ??= new Dom.Views.ViewRealm(this);

    /// <summary>The document this engine is showing, or <see langword="null"/> before the first parse.</summary>
    /// <remarks>
    /// It is published by the parse driver as soon as AngleSharp has created the document, which is
    /// <em>before</em> the parse finishes — an inline script runs during the parse and has to see it.
    /// </remarks>
    internal IDocument? Document
    {
        get => _document;
        set
        {
            _document = value;

            if (value is null)
            {
                DocumentWrapper = null;
                return;
            }

            var wrapper = Dom.WrapNode(value);
            DocumentWrapper = wrapper;
            WindowInstaller.AttachDocumentMembers(this, wrapper);
        }
    }

    /// <summary>The wrapper for <see cref="Document"/>, which is what <c>document</c> answers.</summary>
    internal DomNodeObject? DocumentWrapper { get; private set; }

    /// <summary>The <c>&lt;script&gt;</c> whose text is running, for <c>document.currentScript</c>.</summary>
    internal INode? CurrentScript { get; set; }

    /// <summary><c>Window.prototype</c>, which is the global object's <c>[[Prototype]]</c>.</summary>
    internal ObjectInstance? WindowPrototype { get; set; }

    /// <summary><c>MediaQueryList.prototype</c>, shared by everything <c>matchMedia</c> answers.</summary>
    internal ObjectInstance? MediaQueryListPrototype { get; set; }

    /// <summary><c>window.name</c>, which nothing but a script reads or writes in this version.</summary>
    internal string WindowName { get; set; } = "";

    /// <summary>
    /// The document's URL as the page knows it, which is what <c>location</c>, <c>document.URL</c> and
    /// relative resolution read.
    /// </summary>
    /// <remarks>
    /// It is the runtime's rather than AngleSharp's because <c>pushState</c> and a fragment navigation move
    /// it without reloading, and writing AngleSharp's location instead would raise its own
    /// <c>Location.Changed</c> — a fire-and-forget <c>IBrowsingContext.OpenAsync</c> on this very thread.
    /// AngleSharp's document address stays at whatever the parse was given, which is what the parse resolved
    /// against and is right for that.
    /// </remarks>
    internal string DocumentUrl { get; set; }

    /// <summary>What <c>document.referrer</c> answers: the document this one was reached from.</summary>
    internal string Referrer { get; set; }

    /// <summary><c>history.scrollRestoration</c>, which nothing scrolls and nothing restores.</summary>
    /// <remarks>
    /// Stored and answered so that a router setting it — which many do, on their first line — is not
    /// silently writing to nothing. There is no scrolling here for either value to change.
    /// </remarks>
    internal string ScrollRestoration { get; set; } = "auto";

    /// <summary>
    /// The forms whose entry list is being constructed — HTML's own reentrancy guard, so that a
    /// <c>formdata</c> listener submitting the same form again does not recurse.
    /// </summary>
    internal HashSet<object> SubmittingForms { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Cancelled when this document is left or the page closes, so that everything the engine had in flight
    /// — a <c>fetch</c>, an <c>XMLHttpRequest</c>, a worker's load — is abandoned rather than left to
    /// complete into a realm nobody can reach.
    /// </summary>
    internal CancellationTokenSource? Cancellation { get; set; }

    /// <summary>Milliseconds since the page runtime was created, for a <c>DOMHighResTimeStamp</c>.</summary>
    /// <remarks>
    /// Measured with <see cref="System.Diagnostics.Stopwatch"/> rather than the engine's configured
    /// <c>TimeProvider</c>, because a host substituting a clock for its timers is not thereby asking for a
    /// monotonic frame clock to move with it. The two are independent, and an animation frame is scheduled on
    /// the engine's timer queue either way.
    /// </remarks>
    internal double Now => System.Diagnostics.Stopwatch.GetElapsedTime(_started).TotalMilliseconds;

    /// <summary>
    /// Replaces the viewport and tells every <c>MediaQueryList</c> the page is holding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the seam device emulation drives (campaign item C5): the viewport is the only thing a page can
    /// observe about its window's size, so changing it is the whole of "the window resized" in a browser with
    /// no window. <a href="https://drafts.csswg.org/cssom-view/#dom-mediaquerylist-onchange">CSSOM View</a>
    /// fires <c>change</c> at a list whose answer moved and at no other, which is what a page listens for
    /// rather than polling.
    /// </para>
    /// <para>
    /// It runs on the page loop, like everything else here, and it dispatches synchronously — a listener runs
    /// before this returns, exactly as it does for any other dispatch. A <c>resize</c> event at the window is
    /// deliberately not fired yet: HTML fires it from the "run the resize steps" of update-the-rendering, and
    /// this package has no rendering step to hang it on.
    /// </para>
    /// </remarks>
    internal void SetViewport(Viewport viewport)
    {
        if (Viewport == viewport)
        {
            return;
        }

        Viewport = viewport;

        if (_mediaQueryLists is null)
        {
            return;
        }

        // A copy, because a listener may call matchMedia and append to the list being walked; a list created
        // during the notification cannot have a stale answer to report.
        foreach (var list in _mediaQueryLists.ToArray())
        {
            list.ViewportChanged();
        }
    }

    /// <summary>
    /// Remembers a <c>MediaQueryList</c> so that a viewport change can reach it.
    /// </summary>
    /// <remarks>
    /// A strong reference, held for the life of the engine — which is the life of the document. CSSOM View
    /// keeps a list alive while it has a listener and this keeps every one alive, so a page calling
    /// <c>matchMedia</c> in a loop accumulates them. That is bounded by the document rather than unbounded,
    /// and the alternative — a weak reference per list — would cost a resurrection check on every viewport
    /// change to save a few objects per page.
    /// </remarks>
    internal void Track(JsMediaQueryList list) => (_mediaQueryLists ??= []).Add(list);

    /// <summary>Attaches a runtime to a freshly built engine. Called once, on the page loop.</summary>
    internal static PageRuntime Attach(
        Engine engine,
        Page page,
        BrowserOptions options,
        PageRecorder recorder,
        PageNetwork network,
        PageNetworkRecorder requests,
        string documentUrl,
        string referrer)
    {
        var runtime = new PageRuntime(engine, page, options, recorder, network, requests, documentUrl, referrer);
        _runtimes.Add(engine, runtime);
        return runtime;
    }

    /// <summary>The runtime of the engine <paramref name="thisObject"/> belongs to.</summary>
    /// <remarks>
    /// Every window member starts here, and a receiver that is not an object of an engine carrying a page
    /// runtime is a <c>TypeError</c> — which is what a browser answers for
    /// <c>Window.prototype.matchMedia.call(null)</c>.
    /// </remarks>
    internal static PageRuntime Of(JsValue thisObject, string member)
    {
        if (thisObject is ObjectInstance instance && _runtimes.TryGetValue(instance.Engine, out var runtime))
        {
            return runtime;
        }

        if (thisObject is ObjectInstance other)
        {
            Throw.TypeError(other.Engine.Realm, "Failed to execute '" + member + "' on 'Window': Illegal invocation");
        }

        Throw.TypeErrorNoEngine("Failed to execute '" + member + "' on 'Window': Illegal invocation");
        return null!;
    }

    /// <summary>The runtime attached to <paramref name="engine"/>, or <see langword="null"/> when it has none.</summary>
    internal static PageRuntime? Find(Engine engine)
        => _runtimes.TryGetValue(engine, out var runtime) ? runtime : null;
}
