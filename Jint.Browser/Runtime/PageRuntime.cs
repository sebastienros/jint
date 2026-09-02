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

    private PageRuntime(Engine engine, Page page, BrowserOptions options, PageRecorder recorder)
    {
        Engine = engine;
        Page = page;
        Options = options;
        Recorder = recorder;
        Viewport = options.Viewport;
        Dom = DomRealm.Of(engine);
        AnimationFrames = new AnimationFrameLane(this);
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

    /// <summary>The viewport this page answers dimension and media queries from.</summary>
    internal Viewport Viewport { get; }

    /// <summary>The DOM binding state of this engine.</summary>
    internal DomRealm Dom { get; }

    /// <summary>The <c>requestAnimationFrame</c> lane, run as a batch on the engine's timer queue.</summary>
    internal AnimationFrameLane AnimationFrames { get; }

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

    /// <summary>Milliseconds since the page runtime was created, for a <c>DOMHighResTimeStamp</c>.</summary>
    /// <remarks>
    /// Measured with <see cref="System.Diagnostics.Stopwatch"/> rather than the engine's configured
    /// <c>TimeProvider</c>, because a host substituting a clock for its timers is not thereby asking for a
    /// monotonic frame clock to move with it. The two are independent, and an animation frame is scheduled on
    /// the engine's timer queue either way.
    /// </remarks>
    internal double Now => System.Diagnostics.Stopwatch.GetElapsedTime(_started).TotalMilliseconds;

    /// <summary>Attaches a runtime to a freshly built engine. Called once, on the page loop.</summary>
    internal static PageRuntime Attach(Engine engine, Page page, BrowserOptions options, PageRecorder recorder)
    {
        var runtime = new PageRuntime(engine, page, options, recorder);
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
