using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Observers;

/// <summary>
/// The observer interfaces of one engine: each prototype and its interface object, built on first use.
/// </summary>
/// <remarks>
/// It is the observers' half of what <see cref="DomRealm"/> is for the generated interfaces, and it is
/// separate for the same reason <see cref="HostInterfaceObject"/> is separate from
/// <see cref="DomInterfaceObject"/>: these interfaces have no <see cref="DomInterfaceDefinition"/>, because
/// AngleSharp's <c>MutationObserver</c> is a class rather than a <c>[DomName]</c> interface and the other four
/// do not exist in AngleSharp at all.
/// </remarks>
internal sealed class ObserverRealm
{
    private readonly PageRuntime _runtime;

    private ObjectInstance? _mutationObserverPrototype;
    private HostInterfaceObject? _mutationObserver;
    private ObjectInstance? _intersectionObserverPrototype;
    private HostInterfaceObject? _intersectionObserver;
    private ObjectInstance? _intersectionObserverEntryPrototype;
    private HostInterfaceObject? _intersectionObserverEntry;
    private ObjectInstance? _resizeObserverPrototype;
    private HostInterfaceObject? _resizeObserver;
    private ObjectInstance? _resizeObserverEntryPrototype;
    private HostInterfaceObject? _resizeObserverEntry;

    internal ObserverRealm(PageRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>The global <c>MutationObserver</c>.</summary>
    internal HostInterfaceObject MutationObserver
    {
        get
        {
            if (_mutationObserver is null)
            {
                _mutationObserverPrototype = ObserverInstaller.Instantiate(
                    _runtime.Engine,
                    ObserverInstaller.MutationObserverShape,
                    "MutationObserver",
                    length: 1,
                    ConstructMutationObserver,
                    out var interfaceObject);

                _mutationObserver = interfaceObject;
            }

            return _mutationObserver;
        }
    }

    /// <summary>The global <c>IntersectionObserver</c>.</summary>
    internal HostInterfaceObject IntersectionObserver
    {
        get
        {
            if (_intersectionObserver is null)
            {
                _intersectionObserverPrototype = ObserverInstaller.Instantiate(
                    _runtime.Engine,
                    ObserverInstaller.IntersectionObserverShape,
                    "IntersectionObserver",
                    length: 1,
                    ConstructIntersectionObserver,
                    out var interfaceObject);

                _intersectionObserver = interfaceObject;
            }

            return _intersectionObserver;
        }
    }

    /// <summary>The global <c>IntersectionObserverEntry</c>, which is not constructible.</summary>
    internal HostInterfaceObject IntersectionObserverEntry
    {
        get
        {
            if (_intersectionObserverEntry is null)
            {
                _intersectionObserverEntryPrototype = ObserverInstaller.Instantiate(
                    _runtime.Engine,
                    ObserverInstaller.IntersectionObserverEntryShape,
                    "IntersectionObserverEntry",
                    length: 0,
                    construct: null,
                    out var interfaceObject);

                _intersectionObserverEntry = interfaceObject;
            }

            return _intersectionObserverEntry;
        }
    }

    /// <summary>The global <c>ResizeObserver</c>.</summary>
    internal HostInterfaceObject ResizeObserver
    {
        get
        {
            if (_resizeObserver is null)
            {
                _resizeObserverPrototype = ObserverInstaller.Instantiate(
                    _runtime.Engine,
                    ObserverInstaller.ResizeObserverShape,
                    "ResizeObserver",
                    length: 1,
                    ConstructResizeObserver,
                    out var interfaceObject);

                _resizeObserver = interfaceObject;
            }

            return _resizeObserver;
        }
    }

    /// <summary>The global <c>ResizeObserverEntry</c>, which is not constructible.</summary>
    internal HostInterfaceObject ResizeObserverEntry
    {
        get
        {
            if (_resizeObserverEntry is null)
            {
                _resizeObserverEntryPrototype = ObserverInstaller.Instantiate(
                    _runtime.Engine,
                    ObserverInstaller.ResizeObserverEntryShape,
                    "ResizeObserverEntry",
                    length: 0,
                    construct: null,
                    out var interfaceObject);

                _resizeObserverEntry = interfaceObject;
            }

            return _resizeObserverEntry;
        }
    }

    private ObjectInstance ConstructMutationObserver(JsValue[] arguments)
    {
        _ = MutationObserver;
        return new JsMutationObserver(_runtime, _mutationObserverPrototype!, Callback(arguments, "MutationObserver"));
    }

    private ObjectInstance ConstructIntersectionObserver(JsValue[] arguments)
    {
        _ = IntersectionObserver;
        _ = IntersectionObserverEntry;

        var (root, rootMargin, thresholds) = JsIntersectionObserver.ReadOptions(_runtime.Engine, arguments.At(1));

        return new JsIntersectionObserver(
            _runtime,
            _intersectionObserverPrototype!,
            _intersectionObserverEntryPrototype!,
            Callback(arguments, "IntersectionObserver"),
            root,
            rootMargin,
            thresholds);
    }

    private ObjectInstance ConstructResizeObserver(JsValue[] arguments)
    {
        _ = ResizeObserver;
        _ = ResizeObserverEntry;

        return new JsResizeObserver(
            _runtime,
            _resizeObserverPrototype!,
            _resizeObserverEntryPrototype!,
            Callback(arguments, "ResizeObserver"));
    }

    private ICallable Callback(JsValue[] arguments, string interfaceName)
    {
        if (arguments.At(0) is ICallable callback)
        {
            return callback;
        }

        Throw.TypeError(
            _runtime.Engine._mainRealm,
            "Failed to construct '" + interfaceName + "': parameter 1 is not of type 'Function'.");
        return null!;
    }
}
