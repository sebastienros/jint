using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom.Canvas;

/// <summary>Engine-independent WebIDL prototype shapes for non-rendering canvas objects.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#2dcontext</remarks>
internal static class CanvasShapes
{
    private static readonly (string Name, int Length)[] PathMethods =
    [
        ("closePath", 0), ("moveTo", 2), ("lineTo", 2), ("quadraticCurveTo", 4), ("bezierCurveTo", 6),
        ("arcTo", 5), ("rect", 4), ("roundRect", 4), ("arc", 5), ("ellipse", 7),
    ];

    private static readonly Dictionary<string, JsObjectShape> Shapes = new(StringComparer.Ordinal)
    {
        ["CanvasRenderingContext2D"] = Context(false),
        ["OffscreenCanvasRenderingContext2D"] = Context(true),
        ["Path2D"] = Path(),
        ["CanvasGradient"] = Base("CanvasGradient")
            .Method("addColorStop", static (t, a) => CanvasConvert.Brand<JsCanvasGradient>(t, "CanvasGradient").AddColorStop(a), 2).Build(),
        ["CanvasPattern"] = Base("CanvasPattern")
            .Method("setTransform", static (t, a) => CanvasConvert.Brand<JsCanvasPattern>(t, "CanvasPattern").SetTransform(a)).Build(),
        ["ImageData"] = ImageData(),
        ["TextMetrics"] = Metrics(),
        ["OffscreenCanvas"] = Offscreen(),
    };

    internal static JsObjectShape For(string name) => Shapes[name];
    private static JsObjectShape.Builder Base(string name) => new JsObjectShape.Builder().PerRealmSlot("constructor").ToStringTag(name);

    private static JsObjectShape Context(bool offscreen)
    {
        var name = offscreen ? "OffscreenCanvasRenderingContext2D" : "CanvasRenderingContext2D";
        var builder = Base(name).Accessor("canvas", (t, _) => ContextBrand(t, offscreen).Canvas);
        foreach (var property in CanvasDrawingState.Defaults)
        {
            var key = property.Key;
            builder.Accessor(key, (t, _) => ContextBrand(t, offscreen).State.Values[key],
                (t, a) => ContextBrand(t, offscreen).Set(key, a.At(0)));
        }
        (string Name, int Length)[] methods =
        [
            ("getContextAttributes", 0), ("save", 0), ("restore", 0), ("reset", 0), ("isContextLost", 0),
            ("scale", 2), ("rotate", 1), ("translate", 2), ("transform", 6), ("getTransform", 0), ("setTransform", 0), ("resetTransform", 0),
            ("createLinearGradient", 4), ("createRadialGradient", 6), ("createConicGradient", 3), ("createPattern", 2),
            ("clearRect", 4), ("fillRect", 4), ("strokeRect", 4), ("beginPath", 0), ("fill", 0), ("stroke", 0), ("clip", 0),
            ("isPointInPath", 2), ("isPointInStroke", 2), ("fillText", 3), ("strokeText", 3), ("measureText", 1), ("drawImage", 3),
            ("createImageData", 1), ("getImageData", 4), ("putImageData", 3), ("setLineDash", 1), ("getLineDash", 0),
        ];
        foreach (var method in methods)
        {
            var key = method.Name;
            builder.Method(key, (t, a) => ContextBrand(t, offscreen).Invoke(key, a), method.Length);
        }
        foreach (var method in PathMethods)
        {
            var key = method.Name;
            builder.Method(key, (t, a) =>
            {
                var context = ContextBrand(t, offscreen);
                return CanvasPathOperations.Invoke(context.Owner, key, a);
            }, method.Length);
        }
        if (!offscreen)
        {
            builder.Method("drawFocusIfNeeded", static (t, a) => ContextBrand(t, false).Invoke("drawFocusIfNeeded", a), 1);
            builder.Method("scrollPathIntoView", static (t, a) => ContextBrand(t, false).Invoke("scrollPathIntoView", a));
        }
        return builder.Build();
    }

    private static JsCanvasContext ContextBrand(JsValue value, bool offscreen)
    {
        var context = CanvasConvert.Brand<JsCanvasContext>(value, offscreen ? "OffscreenCanvasRenderingContext2D" : "CanvasRenderingContext2D");
        if (context.Offscreen != offscreen) Throw.TypeError(context.Owner.Realm, "Illegal invocation of canvas context member.");
        return context;
    }

    private static JsObjectShape Path()
    {
        var builder = Base("Path2D").Method("addPath", static (t, a) =>
        {
            var path = CanvasConvert.Brand<JsCanvasPath>(t, "Path2D");
            CanvasConvert.Require(path.Owner, a, 1, "addPath");
            _ = CanvasConvert.Brand<JsCanvasPath>(a[0], "Path2D");
            _ = CanvasConvert.Matrix(path.Owner, a.At(1));
            return JsValue.Undefined;
        }, 1);
        foreach (var method in PathMethods)
        {
            var key = method.Name;
            builder.Method(key, (t, a) => CanvasPathOperations.Invoke(CanvasConvert.Brand<JsCanvasPath>(t, "Path2D").Owner, key, a), method.Length);
        }
        return builder.Build();
    }

    private static JsObjectShape ImageData() => Base("ImageData")
        .Accessor("width", static (t, _) => JsNumber.Create(CanvasConvert.Brand<JsCanvasImageData>(t, "ImageData").Width))
        .Accessor("height", static (t, _) => JsNumber.Create(CanvasConvert.Brand<JsCanvasImageData>(t, "ImageData").Height))
        .Accessor("data", static (t, _) => CanvasConvert.Brand<JsCanvasImageData>(t, "ImageData").Data)
        .Accessor("colorSpace", static (t, _) => { CanvasConvert.Brand<JsCanvasImageData>(t, "ImageData"); return JsString.Create("srgb"); })
        .Accessor("pixelFormat", static (t, _) => { CanvasConvert.Brand<JsCanvasImageData>(t, "ImageData"); return JsString.Create("rgba-unorm8"); })
        .Build();

    private static JsObjectShape Metrics()
    {
        var builder = Base("TextMetrics");
        foreach (var name in JsCanvasTextMetrics.Names)
        {
            builder.Accessor(name, (t, _) => JsNumber.Create(CanvasConvert.Brand<JsCanvasTextMetrics>(t, "TextMetrics").GetMetric(name)));
        }
        return builder.Build();
    }

    private static JsObjectShape Offscreen() => Base("OffscreenCanvas")
        .Accessor("width", static (t, _) => JsNumber.Create(CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas").Width),
            static (t, a) => { CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas").Resize(true, a.At(0)); return JsValue.Undefined; })
        .Accessor("height", static (t, _) => JsNumber.Create(CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas").Height),
            static (t, a) => { CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas").Resize(false, a.At(0)); return JsValue.Undefined; })
        .Method("getContext", static (t, a) => CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas").GetContext(a), 1)
        .Method("convertToBlob", static (t, a) =>
        {
            try { return CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas").ConvertToBlob(a); }
            catch (JavaScriptException exception) when (t is Jint.Native.Object.ObjectInstance)
            {
                var receiver = (Jint.Native.Object.ObjectInstance) t;
                return Jint.WebApi.Streams.StreamPromises.RejectedWith(receiver.Engine, receiver.Engine.Realm, exception.Error);
            }
        })
        .Accessor("oncontextlost", static (t, _) => EventHandlerAttributes.Get(CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas"), "contextlost"),
            static (t, a) => EventHandlerAttributes.Set(CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas"), "contextlost", a.At(0)))
        .Accessor("oncontextrestored", static (t, _) => EventHandlerAttributes.Get(CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas"), "contextrestored"),
            static (t, a) => EventHandlerAttributes.Set(CanvasConvert.Brand<JsOffscreenCanvas>(t, "OffscreenCanvas"), "contextrestored", a.At(0)))
        .Build();
}
