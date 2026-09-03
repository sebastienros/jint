using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// The view interfaces of one engine: each prototype and its interface object, built on first use, plus the
/// document's one <c>Selection</c>.
/// </summary>
/// <remarks>
/// The observers' <c>ObserverRealm</c> is the same thing for the same reason, and they are separate so that a
/// page using neither pays for neither: an engine that never mentions <c>DOMParser</c> never builds its
/// prototype.
/// </remarks>
internal sealed class ViewRealm
{
    private readonly PageRuntime _runtime;

    private ObjectInstance? _domParserPrototype;
    private HostInterfaceObject? _domParser;
    private ObjectInstance? _xmlSerializerPrototype;
    private HostInterfaceObject? _xmlSerializer;
    private ObjectInstance? _selectionPrototype;
    private HostInterfaceObject? _selectionInterface;
    private ObjectInstance? _mediaQueryListEventPrototype;
    private HostInterfaceObject? _mediaQueryListEvent;
    private ObjectInstance? _nodeFilter;
    private ObjectInstance? _cssNamespace;
    private ObjectInstance? _geolocationPrototype;
    private HostInterfaceObject? _geolocationInterface;
    private JsGeolocation? _geolocation;
    private JsSelection? _selection;
    private ObjectInstance? _xPathEvaluatorPrototype;
    private HostInterfaceObject? _xPathEvaluator;
    private ObjectInstance? _xPathExpressionPrototype;
    private HostInterfaceObject? _xPathExpressionInterface;
    private ObjectInstance? _xPathResultPrototype;
    private HostInterfaceObject? _xPathResultInterface;

    internal ViewRealm(PageRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>The global <c>DOMParser</c>.</summary>
    internal HostInterfaceObject DomParser
    {
        get
        {
            if (_domParser is null)
            {
                _domParserPrototype = ViewInstaller.Instantiate(
                    _runtime.Engine,
                    ViewInstaller.DomParserShape,
                    "DOMParser",
                    length: 0,
                    _ => new JsDomParser(_runtime, _domParserPrototype!),
                    parentPrototype: null,
                    parentInterface: null,
                    out var interfaceObject);

                _domParser = interfaceObject;
            }

            return _domParser;
        }
    }

    /// <summary>The global <c>XMLSerializer</c>.</summary>
    internal HostInterfaceObject XmlSerializer
    {
        get
        {
            if (_xmlSerializer is null)
            {
                _xmlSerializerPrototype = ViewInstaller.Instantiate(
                    _runtime.Engine,
                    ViewInstaller.XmlSerializerShape,
                    "XMLSerializer",
                    length: 0,
                    _ => new JsXmlSerializer(_runtime, _xmlSerializerPrototype!),
                    parentPrototype: null,
                    parentInterface: null,
                    out var interfaceObject);

                _xmlSerializer = interfaceObject;
            }

            return _xmlSerializer;
        }
    }

    /// <summary>The global <c>Selection</c>, which is not constructible.</summary>
    internal HostInterfaceObject SelectionInterface
    {
        get
        {
            if (_selectionInterface is null)
            {
                _selectionPrototype = ViewInstaller.Instantiate(
                    _runtime.Engine,
                    ViewInstaller.SelectionShape,
                    "Selection",
                    length: 0,
                    construct: null,
                    parentPrototype: null,
                    parentInterface: null,
                    out var interfaceObject);

                _selectionInterface = interfaceObject;
            }

            return _selectionInterface;
        }
    }

    /// <summary>The global <c>MediaQueryListEvent</c>, whose prototype chains to <c>Event.prototype</c>.</summary>
    internal HostInterfaceObject MediaQueryListEvent
    {
        get
        {
            if (_mediaQueryListEvent is null)
            {
                var engine = _runtime.Engine;

                _mediaQueryListEventPrototype = ViewInstaller.Instantiate(
                    engine,
                    ViewInstaller.MediaQueryListEventShape,
                    "MediaQueryListEvent",
                    length: 1,
                    construct: null,
                    ViewInstaller.EventPrototype(engine),
                    ViewInstaller.EventInterface(engine),
                    out var interfaceObject);

                _mediaQueryListEvent = interfaceObject;
            }

            return _mediaQueryListEvent;
        }
    }

    /// <summary>
    /// The global <c>NodeFilter</c>: a plain object carrying the constants, because DOM declares it as a
    /// callback interface and a callback interface has no instances to construct.
    /// </summary>
    internal ObjectInstance NodeFilter
        => _nodeFilter ??= ViewInstaller.NodeFilterShape.Instantiate(
            _runtime.Engine,
            _runtime.Engine._mainRealm.Intrinsics.Object.PrototypeObject);

    /// <summary>
    /// The global <c>CSS</c>, which is CSSOM's namespace object and therefore a plain object too.
    /// </summary>
    internal ObjectInstance CssNamespace
        => _cssNamespace ??= ViewInstaller.CssNamespaceShape.Instantiate(
            _runtime.Engine,
            _runtime.Engine._mainRealm.Intrinsics.Object.PrototypeObject);

    /// <summary>The global <c>Geolocation</c>, which is not constructible.</summary>
    internal HostInterfaceObject GeolocationInterface
    {
        get
        {
            if (_geolocationInterface is null)
            {
                _geolocationPrototype = ViewInstaller.Instantiate(
                    _runtime.Engine,
                    ViewInstaller.GeolocationShape,
                    "Geolocation",
                    length: 0,
                    construct: null,
                    parentPrototype: null,
                    parentInterface: null,
                    out var interfaceObject);

                _geolocationInterface = interfaceObject;
            }

            return _geolocationInterface;
        }
    }

    /// <summary>The global <c>XPathEvaluator</c>, which is the one of the three a page constructs.</summary>
    internal HostInterfaceObject XPathEvaluator
    {
        get
        {
            if (_xPathEvaluator is null)
            {
                _xPathEvaluatorPrototype = ViewInstaller.Instantiate(
                    _runtime.Engine,
                    ViewInstaller.XPathEvaluatorShape,
                    "XPathEvaluator",
                    length: 0,
                    _ => new JsXPathEvaluator(_runtime, _xPathEvaluatorPrototype!),
                    parentPrototype: null,
                    parentInterface: null,
                    out var interfaceObject);

                _xPathEvaluator = interfaceObject;
            }

            return _xPathEvaluator;
        }
    }

    /// <summary>The global <c>XPathExpression</c>, which only <c>createExpression</c> makes.</summary>
    internal HostInterfaceObject XPathExpressionInterface
    {
        get
        {
            if (_xPathExpressionInterface is null)
            {
                _xPathExpressionPrototype = ViewInstaller.Instantiate(
                    _runtime.Engine,
                    ViewInstaller.XPathExpressionShape,
                    "XPathExpression",
                    length: 0,
                    construct: null,
                    parentPrototype: null,
                    parentInterface: null,
                    out var interfaceObject);

                _xPathExpressionInterface = interfaceObject;
            }

            return _xPathExpressionInterface;
        }
    }

    /// <summary>The global <c>XPathResult</c>, which only an evaluation makes.</summary>
    internal HostInterfaceObject XPathResultInterface
    {
        get
        {
            if (_xPathResultInterface is null)
            {
                _xPathResultPrototype = ViewInstaller.Instantiate(
                    _runtime.Engine,
                    ViewInstaller.XPathResultShape,
                    "XPathResult",
                    length: 0,
                    construct: null,
                    parentPrototype: null,
                    parentInterface: null,
                    out var interfaceObject,
                    XPathEvaluation.ResultConstants);

                _xPathResultInterface = interfaceObject;
            }

            return _xPathResultInterface;
        }
    }

    /// <summary>The prototype every <c>XPathExpression</c> this engine makes is given.</summary>
    internal ObjectInstance XPathExpressionPrototype
    {
        get
        {
            _ = XPathExpressionInterface;
            return _xPathExpressionPrototype!;
        }
    }

    /// <summary>The prototype every <c>XPathResult</c> this engine makes is given.</summary>
    internal ObjectInstance XPathResultPrototype
    {
        get
        {
            _ = XPathResultInterface;
            return _xPathResultPrototype!;
        }
    }

    /// <summary>
    /// The document's one <c>Geolocation</c>, which is what <c>navigator.geolocation</c> answers and which
    /// stays the same object for the life of the document.
    /// </summary>
    internal JsGeolocation Geolocation
    {
        get
        {
            _ = GeolocationInterface;
            return _geolocation ??= new JsGeolocation(_runtime, _geolocationPrototype!);
        }
    }

    /// <summary>
    /// The document's one <c>Selection</c>, which <c>window.getSelection()</c> and
    /// <c>document.getSelection()</c> both answer and which stays the same object for the life of the
    /// document — as the Selection API requires.
    /// </summary>
    internal JsSelection Selection
    {
        get
        {
            _ = SelectionInterface;
            return _selection ??= new JsSelection(_runtime, _selectionPrototype!);
        }
    }

    /// <summary>The <c>change</c> event a media query list fires when its answer moves.</summary>
    internal JsEvent CreateMediaQueryListEvent(string media, bool matches)
    {
        var engine = _runtime.Engine;
        _ = MediaQueryListEvent;

        return new JsMediaQueryListEvent(engine, JsString.Create("change"), default, ViewInstaller.TimeStamp(engine), media, matches)
        {
            IsTrusted = true,
            Prototype = _mediaQueryListEventPrototype!,
        };
    }
}
