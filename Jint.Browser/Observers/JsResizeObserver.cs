using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Observers;

/// <summary>
/// A <c>ResizeObserver</c> with no box to measure: every target it is given is reported once, at zero size.
/// </summary>
/// <remarks>
/// <para>
/// <b>A stub, like <see cref="JsIntersectionObserver"/>, and for the same reason.</b>
/// <a href="https://w3c.github.io/csswg-drafts/resize-observer/">Resize Observer</a> reports a box changing
/// size, and there are no boxes. What matters to a page is the <em>initial</em> notification the specification
/// requires — "observe() ... queues an initial observation" — because that is the one a component uses to
/// measure itself when it mounts, and a component whose observer never fires never finishes mounting. So each
/// observed target gets exactly one entry, delivered as a task after <c>observe()</c> returns, and nothing
/// afterwards: with no layout, no size can change.
/// </para>
/// <para>
/// The <c>box</c> option is read and ignored: the three box sizes an entry carries are the same zeros, so
/// choosing between them would be a distinction without a difference. The flat-box model (campaign item C4)
/// is what makes them different numbers.
/// </para>
/// </remarks>
internal sealed class JsResizeObserver : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private readonly ICallable _callback;
    private readonly List<INode> _targets = [];
    private readonly List<JsResizeObserverEntry> _queue = [];
    private readonly ObjectInstance _entryPrototype;
    private bool _scheduled;

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

        if (_targets.Contains(target))
        {
            return JsValue.Undefined;
        }

        _targets.Add(target);
        _queue.Add(new JsResizeObserverEntry(_runtime, _entryPrototype, target));
        Schedule();
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/csswg-drafts/resize-observer/#dom-resizeobserver-unobserve.</summary>
    internal JsValue Unobserve(JsValue[] arguments)
    {
        var target = Target(arguments, "unobserve");
        _targets.Remove(target);
        _queue.RemoveAll(entry => ReferenceEquals(entry.Node, target));
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/csswg-drafts/resize-observer/#dom-resizeobserver-disconnect.</summary>
    internal JsValue Disconnect()
    {
        _targets.Clear();
        _queue.Clear();
        return JsValue.Undefined;
    }

    private void Schedule()
    {
        if (_scheduled)
        {
            return;
        }

        _scheduled = true;
        ObserverTask.Post(_runtime, Deliver);
    }

    private void Deliver()
    {
        _scheduled = false;

        if (_queue.Count == 0)
        {
            return;
        }

        var values = new JsValue[_queue.Count];
        for (var i = 0; i < _queue.Count; i++)
        {
            values[i] = _queue[i];
        }

        _queue.Clear();

        try
        {
            _callback.Call(this, [_runtime.Engine._mainRealm.Intrinsics.Array.ConstructFast(values), this]);
        }
        catch (JavaScriptException exception)
        {
            _runtime.Recorder.Add(new PageError(
                PageErrorKind.UncaughtCallbackError,
                PageRecorder.Diagnostics.Describe(exception.Error, exception),
                "ResizeObserver"));
        }
    }

    private INode Target(JsValue[] arguments, string member)
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
}

/// <summary>
/// One <c>ResizeObserverEntry</c>: the target, and the three box sizes a page with no layout has.
/// </summary>
internal sealed class JsResizeObserverEntry : ObjectInstance
{
    private readonly PageRuntime _runtime;

    internal JsResizeObserverEntry(PageRuntime runtime, ObjectInstance prototype, INode node)
        : base(runtime.Engine)
    {
        _runtime = runtime;
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
    internal JsValue Rect()
    {
        var box = Box();
        return Layout.DomRects.Of(Engine, new Layout.FlatBox(0, 0, box.Width, box.Height));
    }

    /// <summary>A fresh one-element array holding the target's own size.</summary>
    internal JsValue BoxSizes()
    {
        var box = Box();
        return ObserverGeometry.BoxSizes(Engine, box.Width, box.Height);
    }

    /// <summary>The target's box, or the empty one when it has none.</summary>
    private Layout.FlatBox Box()
    {
        var box = Node is IElement element ? _runtime.Layout.Current().ClientBoxOf(element) : null;
        return box ?? Layout.FlatBox.Empty;
    }

    /// <inheritdoc />
    public override string ToString() => "[object ResizeObserverEntry]";

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsResizeObserverEntry Brand(JsValue thisObject, string member)
        => ObserverGeometry.Brand<JsResizeObserverEntry>(thisObject, "ResizeObserverEntry", member);
}
