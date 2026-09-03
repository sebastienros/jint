using System.Globalization;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Observers;

/// <summary>
/// An <c>IntersectionObserver</c> with no viewport to intersect against: every target it is given is reported
/// once, fully intersecting.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a stub, and the shape of the lie is the point.</b>
/// <a href="https://w3c.github.io/IntersectionObserver/">Intersection Observer</a> is a layout question, and
/// there is no layout. The two possible answers are "never intersecting", which stops every lazy-loading list,
/// infinite scroller and reveal-on-scroll animation on the web dead, and "always intersecting", which loads
/// everything at once — which is what a headless agent reading a page wants anyway. So each observed target
/// gets exactly one entry, with <c>isIntersecting: true</c> and <c>intersectionRatio: 1</c>, delivered as a
/// task after <c>observe()</c> returns; nothing is reported afterwards, because with no layout nothing can
/// change.
/// </para>
/// <para>
/// <c>root</c>, <c>rootMargin</c> and <c>thresholds</c> are parsed, validated and reflected exactly as the
/// specification requires, because a page reads them back and because a wrong value is a page bug worth
/// reporting. They have no effect on what is delivered. The rectangles are
/// <see cref="ObserverGeometry">zeros</see>.
/// </para>
/// </remarks>
internal sealed class JsIntersectionObserver : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private readonly ICallable _callback;
    private readonly List<INode> _targets = [];
    private readonly List<JsIntersectionObserverEntry> _queue = [];
    private readonly ObjectInstance _entryPrototype;
    private bool _scheduled;

    internal JsIntersectionObserver(
        PageRuntime runtime,
        ObjectInstance prototype,
        ObjectInstance entryPrototype,
        ICallable callback,
        JsValue root,
        string rootMargin,
        double[] thresholds)
        : base(runtime.Engine)
    {
        _runtime = runtime;
        _callback = callback;
        _entryPrototype = entryPrototype;
        Root = root;
        RootMargin = rootMargin;
        Thresholds = thresholds;
        Prototype = prototype;
    }

    /// <summary>https://w3c.github.io/IntersectionObserver/#dom-intersectionobserver-root.</summary>
    internal JsValue Root { get; }

    /// <summary>https://w3c.github.io/IntersectionObserver/#dom-intersectionobserver-rootmargin, serialized.</summary>
    internal string RootMargin { get; }

    /// <summary>https://w3c.github.io/IntersectionObserver/#dom-intersectionobserver-thresholds, sorted.</summary>
    internal double[] Thresholds { get; }

    /// <inheritdoc />
    public override string ToString() => "[object IntersectionObserver]";

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsIntersectionObserver Brand(JsValue thisObject, string member)
        => ObserverGeometry.Brand<JsIntersectionObserver>(thisObject, "IntersectionObserver", member);

    /// <summary>https://w3c.github.io/IntersectionObserver/#dom-intersectionobserver-observe.</summary>
    internal JsValue Observe(JsValue[] arguments)
    {
        var target = Target(arguments, "observe");

        if (_targets.Contains(target))
        {
            return JsValue.Undefined;
        }

        _targets.Add(target);
        _queue.Add(new JsIntersectionObserverEntry(_runtime, _entryPrototype, target));
        Schedule();
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/IntersectionObserver/#dom-intersectionobserver-unobserve.</summary>
    internal JsValue Unobserve(JsValue[] arguments)
    {
        var target = Target(arguments, "unobserve");
        _targets.Remove(target);
        _queue.RemoveAll(entry => ReferenceEquals(entry.Node, target));
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/IntersectionObserver/#dom-intersectionobserver-disconnect.</summary>
    internal JsValue Disconnect()
    {
        _targets.Clear();
        _queue.Clear();
        return JsValue.Undefined;
    }

    /// <summary>https://w3c.github.io/IntersectionObserver/#dom-intersectionobserver-takerecords.</summary>
    internal JsValue TakeRecords() => Drain();

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

        var entries = Drain();

        try
        {
            _callback.Call(this, [entries, this]);
        }
        catch (JavaScriptException exception)
        {
            _runtime.Recorder.Add(
                PageErrorKind.UncaughtCallbackError,
                PageRecorder.Diagnostics.Describe(exception.Error, exception),
                "IntersectionObserver");
        }
    }

    private JsArray Drain()
    {
        var values = new JsValue[_queue.Count];
        for (var i = 0; i < _queue.Count; i++)
        {
            values[i] = _queue[i];
        }

        _queue.Clear();
        return _runtime.Engine._mainRealm.Intrinsics.Array.ConstructFast(values);
    }

    private INode Target(JsValue[] arguments, string member)
    {
        if (arguments.At(0) is IDomWrapper { DomTarget: IElement element })
        {
            return element;
        }

        Throw.TypeError(
            _runtime.Engine._mainRealm,
            "Failed to execute '" + member + "' on 'IntersectionObserver': parameter 1 is not of type 'Element'.");
        return null!;
    }

    /// <summary>
    /// https://w3c.github.io/IntersectionObserver/#dom-intersectionobserver-intersectionobserver — the
    /// options dictionary, validated the way the constructor's steps 1-6 validate it.
    /// </summary>
    internal static (JsValue Root, string RootMargin, double[] Thresholds) ReadOptions(Engine engine, JsValue value)
    {
        var realm = engine._mainRealm;
        var options = value as ObjectInstance;

        var root = options?.Get("root") ?? JsValue.Undefined;
        if (root.IsUndefined())
        {
            root = JsValue.Null;
        }
        else if (!root.IsNull() && root is not IDomWrapper { DomTarget: IElement or IDocument })
        {
            Throw.TypeError(realm, "Failed to construct 'IntersectionObserver': member root is not of type Element or Document.");
        }

        var margin = options?.Get("rootMargin") is { } m && !m.IsUndefined() ? TypeConverter.ToString(m) : "0px";

        return (root, SerializeMargin(engine, margin), ReadThresholds(realm, options?.Get("threshold")));
    }

    /// <summary>
    /// The margin is one to four <c>&lt;length-percentage&gt;</c> components, serialized back as four. Only
    /// <c>px</c> and <c>%</c> are units a margin may carry, and a zero may be written bare.
    /// </summary>
    private static string SerializeMargin(Engine engine, string margin)
    {
        var parts = margin.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length is 0 or > 4)
        {
            BadMargin(engine, "rootMargin must be one to four length or percentage components");
        }

        // CSS's one-to-four shorthand expansion, in the order top, right, bottom, left.
        var components = new string[4];
        for (var i = 0; i < 4; i++)
        {
            var source = parts.Length switch
            {
                1 => 0,
                2 => i % 2,
                3 => i == 3 ? 1 : i,
                _ => i,
            };

            components[i] = Component(engine, parts[source]);
        }

        return string.Join(" ", components);
    }

    private static string Component(Engine engine, string part)
    {
        var value = part;
        var unit = "px";

        if (value.EndsWith('%'))
        {
            unit = "%";
            value = value[..^1];
        }
        else if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^2];
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            BadMargin(engine, "rootMargin component '" + part + "' is not a length or a percentage");
        }

        return number.ToString("0.####", CultureInfo.InvariantCulture) + unit;
    }

    /// <summary>
    /// A malformed <c>rootMargin</c> is a <c>SyntaxError</c> <c>DOMException</c> rather than a
    /// <c>TypeError</c>: the value crossed WebIDL's <c>DOMString</c> conversion cleanly and failed the
    /// interface's own parse, which is the distinction the two error types carry.
    /// </summary>
    private static void BadMargin(Engine engine, string what)
    {
        var error = engine._mainRealm.Intrinsics.DomException.CreateException(
            Jint.WebApi.DomException.DomExceptionNames.Syntax,
            "Failed to construct 'IntersectionObserver': " + what + ".");

        Throw.JavaScriptException(engine, error, engine.GetLastSyntaxElement()?.Location ?? default);
    }

    private static double[] ReadThresholds(Realm realm, JsValue? value)
    {
        if (value is null || value.IsUndefined())
        {
            return [0];
        }

        var thresholds = new List<double>();

        if (value is ObjectInstance sequence && !value.IsNumber())
        {
            var length = (int) TypeConverter.ToUint32(sequence.Get("length"));
            for (var i = 0; i < length; i++)
            {
                thresholds.Add(TypeConverter.ToNumber(sequence.Get(i.ToString(CultureInfo.InvariantCulture))));
            }
        }
        else
        {
            thresholds.Add(TypeConverter.ToNumber(value));
        }

        if (thresholds.Count == 0)
        {
            thresholds.Add(0);
        }

        foreach (var threshold in thresholds)
        {
            if (threshold is < 0 or > 1 || double.IsNaN(threshold))
            {
                Throw.RangeError(realm, "Failed to construct 'IntersectionObserver': Threshold values must be numbers between 0 and 1.");
            }
        }

        thresholds.Sort();
        return [.. thresholds];
    }
}

/// <summary>
/// One <c>IntersectionObserverEntry</c>: the target, the moment it was reported, and the zero rectangles a
/// page with no layout has.
/// </summary>
internal sealed class JsIntersectionObserverEntry : ObjectInstance
{
    private readonly PageRuntime _runtime;

    internal JsIntersectionObserverEntry(PageRuntime runtime, ObjectInstance prototype, INode node)
        : base(runtime.Engine)
    {
        _runtime = runtime;
        Node = node;
        Time = runtime.Now;
        Prototype = prototype;
    }

    /// <summary>The element this entry reports on.</summary>
    internal INode Node { get; }

    /// <summary>https://w3c.github.io/IntersectionObserver/#dom-intersectionobserverentry-time.</summary>
    internal double Time { get; }

    /// <summary>The wrapper for <see cref="Node"/>, which is what <c>entry.target</c> answers.</summary>
    internal JsValue TargetValue => _runtime.Dom.WrapNode(Node);

    /// <summary>
    /// The target's own box, from the flat layout. A fresh object each time; a page may keep or mutate the
    /// one it is given.
    /// </summary>
    internal JsValue Rect()
    {
        var box = Node is IElement element ? _runtime.Layout.Current().ClientBoxOf(element) : null;
        return Layout.DomRects.Of(Engine, box ?? Layout.FlatBox.Empty);
    }

    /// <summary>
    /// https://w3c.github.io/IntersectionObserver/#dom-intersectionobserverentry-rootbounds — the viewport.
    /// </summary>
    /// <remarks>
    /// The root is always the viewport here: an observer given an explicit <c>root</c> element records it,
    /// validates it and intersects against nothing, which is the stub this class already documents.
    /// </remarks>
    internal JsValue RootBounds()
    {
        var viewport = _runtime.Viewport;
        return Layout.DomRects.Of(Engine, new Layout.FlatBox(0, 0, viewport.Width, viewport.Height));
    }

    /// <inheritdoc />
    public override string ToString() => "[object IntersectionObserverEntry]";

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsIntersectionObserverEntry Brand(JsValue thisObject, string member)
        => ObserverGeometry.Brand<JsIntersectionObserverEntry>(thisObject, "IntersectionObserverEntry", member);
}
