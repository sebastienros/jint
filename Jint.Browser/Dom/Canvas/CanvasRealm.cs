using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.HtmlParser;
using Jint.WebApi.Files;
using Jint.WebApi.Events;
using Jint.WebApi.Streams;
using Jint.Browser.Observers;
using Jint.Browser.Runtime;

namespace Jint.Browser.Dom.Canvas;

/// <summary>Per-realm, lazily constructed non-rendering canvas interfaces and per-element contexts.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#2dcontext</remarks>
internal sealed class CanvasRealm(DomRealm dom)
{
    internal static readonly string[] InterfaceNames =
    [
        "CanvasRenderingContext2D", "OffscreenCanvasRenderingContext2D", "OffscreenCanvas",
        "CanvasGradient", "CanvasPattern", "Path2D", "TextMetrics", "ImageData",
    ];

    private readonly Dictionary<string, ObjectInstance> _prototypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HostInterfaceObject> _interfaces = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<Element, JsCanvasContext> _contexts = new();

    internal DomRealm Dom { get; } = dom;
    internal Engine Engine => Dom.Engine;
    internal Realm Realm => Dom.OwningRealm;

    internal ObjectInstance Prototype(string name)
    {
        if (_prototypes.TryGetValue(name, out var prototype)) return prototype;
        Func<JsValue[], ObjectInstance>? construct = name switch
        {
            "Path2D" => args =>
            {
                if (args.Length > 0 && !args[0].IsUndefined() && args[0] is not JsCanvasPath)
                    _ = TypeConverter.ToString(args[0]);
                return new JsCanvasPath(this);
            }
            ,
            "ImageData" => args => JsCanvasImageData.Construct(this, args),
            "OffscreenCanvas" => args =>
            {
                CanvasConvert.Require(this, args, 2, name);
                var width = CanvasConvert.Dimension(this, args[0]);
                var height = CanvasConvert.Dimension(this, args[1]);
                return new JsOffscreenCanvas(this, width, height);
            }
            ,
            _ => null,
        };
        var length = name is "ImageData" or "OffscreenCanvas" ? 2 : 0;
        prototype = HostInterfaceObject.Instantiate(Engine, CanvasShapes.For(name), name, length, construct, out var iface, Realm);
        if (name == "OffscreenCanvas")
        {
            prototype.Prototype = Realm.Intrinsics.EventTarget.PrototypeObject;
            iface.Prototype = Realm.Intrinsics.EventTarget;
        }
        _prototypes.Add(name, prototype);
        _interfaces.Add(name, iface);
        return prototype;
    }

    internal JsValue InterfaceObject(string name)
    {
        _ = Prototype(name);
        return _interfaces[name];
    }

    internal JsValue GetContext(Element canvas, JsValue options)
    {
        if (_contexts.TryGetValue(canvas, out var context)) return context;
        var settings = CanvasSettings.Read(this, options);
        if (_contexts.TryGetValue(canvas, out context)) return context;
        context = new JsCanvasContext(this, Dom.WrapNode(canvas), settings, false);
        context.Watch(canvas);
        _contexts.Add(canvas, context);
        return context;
    }

    internal JsBlob Blob(byte[] bytes) => new(Engine, bytes, "image/png")
    {
        Prototype = Realm.Intrinsics.Blob.PrototypeObject,
    };

    internal void Post(Action action)
    {
        if (PageRuntime.Find(Engine) is { } runtime) ObserverTask.Post(runtime, action);
        else Engine.AddToEventLoop(action, EventLoopJobKind.Task);
    }
}

/// <summary>A canvas without a DOM element, carrying a context but no pixels.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#the-offscreencanvas-interface</remarks>
internal sealed class JsOffscreenCanvas : JsEventTarget
{
    private JsCanvasContext? _context;
    internal JsOffscreenCanvas(CanvasRealm owner, double width, double height) : base(owner.Engine, owner.Realm)
    {
        Owner = owner;
        Width = width;
        Height = height;
        Prototype = owner.Prototype("OffscreenCanvas");
    }

    internal CanvasRealm Owner { get; }
    internal double Width { get; private set; }
    internal double Height { get; private set; }

    internal void Resize(bool width, JsValue value)
    {
        var dimension = CanvasConvert.Dimension(Owner, value);
        if (width) Width = dimension;
        else Height = dimension;
        _context?.Reset();
    }

    internal JsValue GetContext(JsValue[] args)
    {
        CanvasConvert.Require(Owner, args, 1, "getContext");
        var name = TypeConverter.ToString(args[0]);
        CanvasConvert.Enum(Owner, name, "2d", "bitmaprenderer", "webgl", "webgl2", "webgpu");
        if (name != "2d") return JsValue.Null;
        if (_context is not null) return _context;
        var settings = CanvasSettings.Read(Owner, args.At(1));
        return _context ??= new JsCanvasContext(Owner, this, settings, true);
    }

    internal JsValue ConvertToBlob(JsValue[] args)
    {
        try
        {
            var options = CanvasConvert.Dictionary(Owner, args.At(0));
            if (options is not null)
            {
                var quality = options.Get("quality");
                if (!quality.IsUndefined()) _ = TypeConverter.ToNumber(quality);
                var type = options.Get("type");
                if (!type.IsUndefined()) _ = TypeConverter.ToString(type);
            }
            if (Width == 0 || Height == 0)
                return StreamPromises.RejectedWith(Engine, Owner.Realm,
                    Owner.Realm.Intrinsics.DomException.CreateException("IndexSizeError", "The canvas has no pixels."));
            var bytes = CanvasPng.Encode(Owner, Width, Height);
            if (bytes is null)
                return StreamPromises.RejectedWith(Engine, Owner.Realm,
                    Owner.Realm.Intrinsics.DomException.CreateException("EncodingError", "The canvas exceeds the encoding limit."));
            var promise = StreamPromises.NewPromise(Engine, Owner.Realm);
            Owner.Post(() => promise.Resolve(Owner.Blob(bytes)));
            return StreamPromises.PromiseOf(promise);
        }
        catch (JavaScriptException exception)
        {
            return StreamPromises.RejectedWith(Engine, Owner.Realm, exception.Error);
        }
    }
}

/// <summary>The immutable negotiated settings of a 2D context.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#canvasrenderingcontext2dsettings</remarks>
internal sealed record CanvasSettings(bool Alpha, bool WillReadFrequently)
{
    private static readonly JsObjectLayout Layout = new JsObjectLayout.Builder()
        .Add("alpha").Add("colorSpace").Add("colorType").Add("desynchronized").Add("willReadFrequently").Build();

    internal static CanvasSettings Read(CanvasRealm owner, JsValue value)
    {
        var dictionary = CanvasConvert.Dictionary(owner, value);
        var alpha = dictionary?.Get("alpha") ?? JsValue.Undefined;
        var colorSpace = dictionary?.Get("colorSpace") ?? JsValue.Undefined;
        if (!colorSpace.IsUndefined()) CanvasConvert.Enum(owner, TypeConverter.ToString(colorSpace), "srgb", "display-p3");
        var colorType = dictionary?.Get("colorType") ?? JsValue.Undefined;
        if (!colorType.IsUndefined()) CanvasConvert.Enum(owner, TypeConverter.ToString(colorType), "unorm8", "float16");
        _ = TypeConverter.ToBoolean(dictionary?.Get("desynchronized") ?? JsValue.Undefined);
        var read = TypeConverter.ToBoolean(dictionary?.Get("willReadFrequently") ?? JsValue.Undefined);
        return new CanvasSettings(alpha.IsUndefined() || TypeConverter.ToBoolean(alpha), read);
    }

    internal JsObject ToObject(CanvasRealm owner) => JsObject.Create(owner.Engine, Layout,
        [JsBoolean.Create(Alpha), JsString.Create("srgb"), JsString.Create("unorm8"), JsBoolean.False, JsBoolean.Create(WillReadFrequently)]);
}
