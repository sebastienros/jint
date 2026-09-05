using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Layout;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Observers;

/// <summary>
/// Reports changes to the sizes supplied by the page's flat box model.
/// </summary>
/// <remarks>
/// <para>
/// The geometry is synthetic, but it changes when a target is shown, hidden, inserted or removed, when its
/// rendered subtree changes, and when the viewport changes. Keeping the last reported size makes these
/// changes observable without repeatedly reporting unchanged targets.
/// </para>
/// <para>
/// All three box sizes use the same flat dimensions; padding, borders and device-pixel scaling are not
/// modeled. Entries retain their measured dimensions rather than consulting the current DOM on each read.
/// </para>
/// </remarks>
internal sealed class JsResizeObserver : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private readonly ICallable _callback;
    private readonly List<Observation> _targets = [];
    private readonly ObjectInstance _entryPrototype;

    internal JsResizeObserver(PageRuntime runtime, ObjectInstance prototype, ObjectInstance entryPrototype, ICallable callback)
        : base(runtime.Engine)
    {
        _runtime = runtime;
        _callback = callback;
        _entryPrototype = entryPrototype;
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => "[object ResizeObserver]";

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsResizeObserver Brand(JsValue thisObject, string member)
        => ObserverGeometry.Brand<JsResizeObserver>(thisObject, "ResizeObserver", member);

    /// <summary>https://w3c.github.io/csswg-drafts/resize-observer/#dom-resizeobserver-observe.</summary>
    internal JsValue Observe(JsValue[] arguments)
    {
        var target = Target(arguments, "observe");

        if (_targets.Exists(observation => ReferenceEquals(observation.Target, target)))
        {
            return JsValue.Undefined;
        }

        _targets.Add(new Observation(target));
        _runtime.ResizeObservers.Enlist(this);
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/csswg-drafts/resize-observer/#dom-resizeobserver-unobserve.</summary>
    internal JsValue Unobserve(JsValue[] arguments)
    {
        var target = Target(arguments, "unobserve");
        _targets.RemoveAll(observation => ReferenceEquals(observation.Target, target));
        if (_targets.Count == 0)
        {
            _runtime.ResizeObservers.Withdraw(this);
        }
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/csswg-drafts/resize-observer/#dom-resizeobserver-disconnect.</summary>
    internal JsValue Disconnect()
    {
        _targets.Clear();
        _runtime.ResizeObservers.Withdraw(this);
        return JsValue.Undefined;
    }

    /// <summary>https://drafts.csswg.org/resize-observer/#dom-resizeobservation-isactive.</summary>
    internal bool HasChanges(FlatLayout layout)
    {
        foreach (var observation in _targets)
        {
            var box = layout.ClientBoxOf(observation.Target) ?? FlatBox.Empty;
            if (observation.Width != box.Width || observation.Height != box.Height)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>https://drafts.csswg.org/resize-observer/#broadcast-resize-notifications-h.</summary>
    internal void Deliver(FlatLayout layout)
    {
        List<JsValue>? entries = null;
        foreach (var observation in _targets)
        {
            var box = layout.ClientBoxOf(observation.Target) ?? FlatBox.Empty;
            if (observation.Width == box.Width && observation.Height == box.Height)
            {
                continue;
            }

            observation.Width = box.Width;
            observation.Height = box.Height;
            (entries ??= []).Add(new JsResizeObserverEntry(_runtime, _entryPrototype, observation.Target, box));
        }

        if (entries is null)
        {
            return;
        }

        try
        {
            try
            {
                _callback.Call(this, [_runtime.Engine._mainRealm.Intrinsics.Array.ConstructFast(entries.ToArray()), this]);
            }
            finally
            {
                _runtime.Engine.CleanUpAfterRunningScript();
            }
        }
        catch (JavaScriptException exception)
        {
            _runtime.Recorder.Add(
                PageErrorKind.UncaughtCallbackError,
                PageRecorder.Diagnostics.Describe(exception.Error, exception),
                "ResizeObserver");
        }
    }

    private IElement Target(JsValue[] arguments, string member)
    {
        if (arguments.At(0) is IDomWrapper { DomTarget: IElement element })
        {
            return element;
        }

        Throw.TypeError(
            _runtime.Engine._mainRealm,
            "Failed to execute '" + member + "' on 'ResizeObserver': parameter 1 is not of type 'Element'.");
        return null!;
    }

    private sealed class Observation(IElement target)
    {
        internal IElement Target { get; } = target;

        // https://drafts.csswg.org/resize-observer/#dom-resizeobservation-resizeobservation-target-options
        internal double Width { get; set; } = -1;
        internal double Height { get; set; } = -1;
    }
}

/// <summary>
/// One <c>ResizeObserverEntry</c>: the target, and the three box sizes a page with no layout has.
/// </summary>
internal sealed class JsResizeObserverEntry : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private readonly FlatBox _box;

    internal JsResizeObserverEntry(PageRuntime runtime, ObjectInstance prototype, INode node, FlatBox box)
        : base(runtime.Engine)
    {
        _runtime = runtime;
        _box = new FlatBox(0, 0, box.Width, box.Height);
        Node = node;
        Prototype = prototype;
    }

    /// <summary>The element this entry reports on.</summary>
    internal INode Node { get; }

    /// <summary>The wrapper for <see cref="Node"/>, which is what <c>entry.target</c> answers.</summary>
    internal JsValue TargetValue => _runtime.Dom.WrapNode(Node);

    /// <summary>
    /// https://drafts.csswg.org/resize-observer/#dom-resizeobserverentry-contentrect — the target's own
    /// size, at the origin of its own padding box, which is where a content rectangle is measured from.
    /// </summary>
    internal JsValue Rect() => DomRects.Of(Engine, _box);

    /// <summary>A fresh one-element array holding the target's own size.</summary>
    internal JsValue BoxSizes() => ObserverGeometry.BoxSizes(Engine, _box.Width, _box.Height);

    /// <inheritdoc />
    public override string ToString() => "[object ResizeObserverEntry]";

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsResizeObserverEntry Brand(JsValue thisObject, string member)
        => ObserverGeometry.Brand<JsResizeObserverEntry>(thisObject, "ResizeObserverEntry", member);
}
