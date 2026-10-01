using Jint.Browser.Geometry;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Canvas;

/// <summary>A stateful CanvasRenderingContext2D or OffscreenCanvasRenderingContext2D with no backing raster.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#2dcontext</remarks>
internal sealed class JsCanvasContext : ObjectInstance
{
    private CanvasDrawingState _state = new();
    private readonly Stack<CanvasDrawingState> _stack = new();
    private MutationSubscription? _dimensions;
    private bool _resetPending;
    private readonly CanvasSettings _settings;

    internal JsCanvasContext(CanvasRealm owner, JsValue canvas, CanvasSettings settings, bool offscreen) : base(owner.Engine)
    {
        Owner = owner;
        Canvas = canvas;
        Offscreen = offscreen;
        _settings = settings;
        Prototype = owner.Prototype(offscreen ? "OffscreenCanvasRenderingContext2D" : "CanvasRenderingContext2D");
    }

    internal CanvasRealm Owner { get; }
    internal JsValue Canvas { get; }
    internal bool Offscreen { get; }
    internal CanvasDrawingState State
    {
        get
        {
            if (_resetPending || _dimensions?.TakeRecords().Count > 0) Reset();
            return _state;
        }
    }

    internal void Watch(Element element)
    {
        // Only canvases with a context incur a subscription. No callback retains the engine through the native tree.
        _dimensions = element.OwnerDocument!.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, AttributeFilter = ["width", "height"] });
        _dimensions.PendingRecord = new DimensionWatch(this).Changed;
    }

    internal void Reset()
    {
        _state = new CanvasDrawingState();
        _stack.Clear();
        _resetPending = false;
    }

    internal JsValue Set(string name, JsValue value)
    {
        var converted = CanvasDrawingState.Convert(Owner, name, value);
        if (converted is not null) State.Assign(name, converted);
        return JsValue.Undefined;
    }

    internal JsValue Invoke(string name, JsValue[] args)
    {
        Engine.Constraints.Check();
        switch (name)
        {
            case "getContextAttributes": return _settings.ToObject(Owner);
            case "isContextLost": return JsBoolean.False;
            case "save": _stack.Push(State.Copy()); break;
            case "restore":
                _ = State;
                if (_stack.TryPop(out var saved)) _state = saved;
                break;
            case "reset": _ = State; Reset(); break;
            case "getTransform": return Owner.Dom.Geometry.CreateMatrix(true, (double[]) State.Matrix.Clone(), true);
            case "resetTransform": State.Matrix = GeometryMatrix.Identity(); break;
            case "scale":
            case "translate":
            case "rotate":
            case "transform":
            case "setTransform":
                Transform(name, args); break;
            case "getLineDash":
                return new JsArray(Engine, State.LineDash.Select(JsNumber.Create).Cast<JsValue>().ToArray());
            case "setLineDash": SetLineDash(args); break;
            case "createLinearGradient":
            case "createRadialGradient":
            case "createConicGradient":
                {
                    var count = name == "createLinearGradient" ? 4 : name == "createRadialGradient" ? 6 : 3;
                    var values = CanvasConvert.Numbers(Owner, args, count, name, restricted: true);
                    if (name == "createRadialGradient" && (values[2] < 0 || values[5] < 0))
                        CanvasConvert.Error(Owner, "IndexSizeError", "A gradient radius cannot be negative.");
                    return new JsCanvasGradient(Owner);
                }
            case "createPattern": return CreatePattern(args);
            case "fillRect":
            case "strokeRect":
            case "clearRect": _ = CanvasConvert.Numbers(Owner, args, 4, name); break;
            case "beginPath": break;
            case "fill":
            case "clip": FillRule(args, args.At(0) is JsCanvasPath ? 1 : 0); break;
            case "stroke":
            case "scrollPathIntoView":
                if (args.Length > 0) _ = CanvasConvert.Brand<JsCanvasPath>(args[0], "Path2D");
                break;
            case "isPointInPath":
            case "isPointInStroke":
                {
                    var offset = args.At(0) is JsCanvasPath ? 1 : 0;
                    _ = CanvasConvert.Numbers(Owner, args, 2, name, offset: offset);
                    if (name == "isPointInPath") FillRule(args, offset + 2);
                    return JsBoolean.False;
                }
            case "fillText":
            case "strokeText":
                CanvasConvert.Require(Owner, args, 3, name);
                _ = TypeConverter.ToString(args[0]);
                _ = TypeConverter.ToNumber(args[1]);
                _ = TypeConverter.ToNumber(args[2]);
                if (args.Length > 3 && !args[3].IsUndefined()) _ = TypeConverter.ToNumber(args[3]);
                break;
            case "measureText":
                CanvasConvert.Require(Owner, args, 1, name);
                var text = TypeConverter.ToString(args[0]);
                return new JsCanvasTextMetrics(Owner, text, State);
            case "drawImage": DrawImage(args); break;
            case "drawFocusIfNeeded":
                var index = args.At(0) is JsCanvasPath ? 1 : 0;
                CanvasConvert.Require(Owner, args, index + 1, name);
                _ = DomBindings.Bind<Element>(args[index], "Element.drawFocusIfNeeded");
                break;
            case "createImageData": return CreateImageData(args);
            case "getImageData":
                CanvasConvert.Require(Owner, args, 4, name);
                _ = CanvasConvert.Long(Owner, args[0]);
                _ = CanvasConvert.Long(Owner, args[1]);
                var width = Math.Abs((long) CanvasConvert.Long(Owner, args[2]));
                var height = Math.Abs((long) CanvasConvert.Long(Owner, args[3]));
                JsCanvasImageData.Settings(Owner, args.At(4));
                return JsCanvasImageData.Allocate(Owner, width, height);
            case "putImageData": PutImageData(args); break;
            default: Throw.InvalidOperationException("Unknown canvas operation: " + name); break;
        }
        return JsValue.Undefined;
    }

    private void Transform(string name, JsValue[] args)
    {
        if (name == "setTransform" && args.Length <= 1)
        {
            var matrix = CanvasConvert.Matrix(Owner, args.At(0));
            if (CanvasConvert.Finite(matrix)) State.Matrix = matrix;
            return;
        }
        var count = name is "transform" or "setTransform" ? 6 : name == "rotate" ? 1 : 2;
        var values = CanvasConvert.Numbers(Owner, args, count, name);
        if (!CanvasConvert.Finite(values)) return;
        var state = State;
        if (name == "translate") GeometryMatrix.Translate(state.Matrix, values[0], values[1], 0);
        else if (name == "scale") GeometryMatrix.Scale(state.Matrix, values[0], values[1], 1);
        else if (name == "rotate")
        {
            var matrix = GeometryMatrix.Identity();
            matrix[0] = matrix[5] = Math.Cos(values[0]);
            matrix[1] = Math.Sin(values[0]);
            matrix[4] = -matrix[1];
            GeometryMatrix.PostMultiply(state.Matrix, matrix);
        }
        else
        {
            var (matrix, _) = GeometryConversion.FromSequence(Owner.Realm, values, name);
            if (name == "setTransform") state.Matrix = matrix;
            else GeometryMatrix.PostMultiply(state.Matrix, matrix);
        }
    }

    private void SetLineDash(JsValue[] args)
    {
        CanvasConvert.Require(Owner, args, 1, "setLineDash");
        if (!args[0].IsObject()) Throw.TypeError(Owner.Realm, "Line dash segments must be a sequence.");
        var iterator = args[0].GetIterator(Owner.Realm);
        var values = new List<double>();
        try
        {
            while (iterator.TryIteratorStepValue(out var value))
            {
                Engine.Constraints.Check();
                values.Add(TypeConverter.ToNumber(value));
            }
        }
        catch
        {
            iterator.Close(CompletionType.Throw);
            throw;
        }
        foreach (var value in values)
            if (!double.IsFinite(value) || value < 0) return;
        if (values.Count % 2 != 0) values.AddRange(values.ToArray());
        State.LineDash = values.ToArray();
    }

    private void FillRule(JsValue[] args, int index)
    {
        var value = args.At(index);
        var rule = value.IsUndefined() ? "nonzero" : TypeConverter.ToString(value);
        CanvasConvert.Enum(Owner, rule, "nonzero", "evenodd");
    }

    private JsCanvasImageData CreateImageData(JsValue[] args)
    {
        CanvasConvert.Require(Owner, args, 1, "createImageData");
        if (args.Length == 1)
        {
            var source = CanvasConvert.Brand<JsCanvasImageData>(args[0], "ImageData");
            return JsCanvasImageData.Allocate(Owner, source.Width, source.Height);
        }
        var width = Math.Abs((long) CanvasConvert.Long(Owner, args[0]));
        var height = Math.Abs((long) CanvasConvert.Long(Owner, args[1]));
        JsCanvasImageData.Settings(Owner, args.At(2));
        return JsCanvasImageData.Allocate(Owner, width, height);
    }

    private void PutImageData(JsValue[] args)
    {
        CanvasConvert.Require(Owner, args, 3, "putImageData");
        var image = CanvasConvert.Brand<JsCanvasImageData>(args[0], "ImageData");
        if (args.Length is > 3 and < 7) Throw.TypeError(Owner.Realm, "putImageData requires three or seven arguments.");
        var count = args.Length >= 7 ? 7 : 3;
        for (var i = 1; i < count; i++) _ = CanvasConvert.Long(Owner, args[i]);
        if (image.Data._viewedArrayBuffer.IsDetachedBuffer)
            CanvasConvert.Error(Owner, "InvalidStateError", "The ImageData buffer is detached.");
    }

    private JsValue CreatePattern(JsValue[] args)
    {
        CanvasConvert.Require(Owner, args, 2, "createPattern");
        CanvasImages.RequireSource(Owner, args[0]);
        var repetition = args[1].IsNull() ? "" : TypeConverter.ToString(args[1]);
        if (repetition is not ("" or "repeat" or "repeat-x" or "repeat-y" or "no-repeat"))
            CanvasConvert.Error(Owner, "SyntaxError", "Invalid pattern repetition.");
        return CanvasImages.Usable(Owner, args[0], pattern: true) ? new JsCanvasPattern(Owner) : JsValue.Null;
    }

    private void DrawImage(JsValue[] args)
    {
        CanvasConvert.Require(Owner, args, 3, "drawImage");
        if (args.Length is 4 or 6 or 7 or 8) Throw.TypeError(Owner.Realm, "drawImage requires three, five or nine arguments.");
        CanvasImages.RequireSource(Owner, args[0]);
        var count = args.Length >= 9 ? 8 : args.Length >= 5 ? 4 : 2;
        var values = CanvasConvert.Numbers(Owner, args, count, "drawImage", offset: 1);
        if (CanvasConvert.Finite(values)) _ = CanvasImages.Usable(Owner, args[0], pattern: false);
    }

    /// <summary>Native notifications invalidate state without retaining an engine or running author code.</summary>
    /// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#concept-canvas-set-bitmap-dimensions</remarks>
    private sealed class DimensionWatch(JsCanvasContext context)
    {
        private readonly WeakReference<JsCanvasContext> _context = new(context);
        internal void Changed(MutationSubscription subscription)
        {
            _ = subscription.TakeRecords();
            if (_context.TryGetTarget(out var context)) context._resetPending = true;
            else subscription.Dispose();
        }
    }
}

/// <summary>CanvasImageSource branding and usability, without image decoding or origin taint.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#check-the-usability-of-the-image-argument</remarks>
internal static class CanvasImages
{
    internal static void RequireSource(CanvasRealm owner, JsValue value)
    {
        if (value is JsOffscreenCanvas) return;
        if (value is DomNodeObject node && (node.Implements("HTMLCanvasElement") || node.Implements("HTMLImageElement")
            || node.Implements("HTMLVideoElement") || node.Implements("SVGImageElement"))) return;
        Throw.TypeError(owner.Realm, "The image must be a CanvasImageSource.");
    }

    internal static bool Usable(CanvasRealm owner, JsValue value, bool pattern)
    {
        if (value is JsOffscreenCanvas offscreen)
            return CanvasSize(owner, offscreen.Width, offscreen.Height, pattern);
        var node = (DomNodeObject) value;
        var element = (Element) node.DomTarget;
        if (node.Implements("HTMLCanvasElement"))
            return CanvasSize(owner, DomReflected.HTMLCanvasElementWidth.Get(node.DomRealm, element).AsNumber(),
                DomReflected.HTMLCanvasElementHeight.Get(node.DomRealm, element).AsNumber(), pattern);
        if (node.Implements("HTMLImageElement"))
        {
            var width = node.DomRealm.Hooks.ImageNaturalWidth(node.DomRealm, element).AsNumber();
            var height = node.DomRealm.Hooks.ImageNaturalHeight(node.DomRealm, element).AsNumber();
            return width > 0 && height > 0;
        }
        // Video and SVG image sources have no decoded frame in this browser.
        return false;
    }

    private static bool CanvasSize(CanvasRealm owner, double width, double height, bool pattern)
    {
        if (width > 0 && height > 0) return true;
        if (!pattern) CanvasConvert.Error(owner, "InvalidStateError", "The source canvas has no pixels.");
        return false;
    }
}
