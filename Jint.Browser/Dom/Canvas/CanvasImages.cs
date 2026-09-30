using Jint.Native;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.Browser.Dom.Canvas;

/// <summary>An ImageData backed by a real Uint8ClampedArray, independent of the non-rendering canvas.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#imagedata</remarks>
internal sealed class JsCanvasImageData : ObjectInstance
{
    private JsCanvasImageData(CanvasRealm owner, uint width, uint height, JsTypedArray data) : base(owner.Engine)
    {
        Width = width;
        Height = height;
        Data = data;
        Prototype = owner.Prototype("ImageData");
    }
    internal uint Width { get; }
    internal uint Height { get; }
    internal JsTypedArray Data { get; }

    internal static JsCanvasImageData Construct(CanvasRealm owner, JsValue[] args)
    {
        CanvasConvert.Require(owner, args, 2, "ImageData");
        if (args[0] is JsTypedArray data)
        {
            if (data._arrayElementType != TypedArrayElementType.Uint8C || data._viewedArrayBuffer.IsSharedArrayBuffer
                || !data._viewedArrayBuffer.IsFixedLengthArrayBuffer)
                Throw.TypeError(owner.Realm, "ImageData requires a fixed, non-shared Uint8ClampedArray.");
            var width = TypeConverter.ToUint32(args[1]);
            uint? height = args.Length > 2 && !args[2].IsUndefined() ? TypeConverter.ToUint32(args[2]) : null;
            Settings(owner, args.At(3));
            var length = data.Length;
            if (length == 0 || length % 4 != 0)
                CanvasConvert.Error(owner, "InvalidStateError", "ImageData needs a nonempty RGBA array.");
            var pixels = length / 4;
            if (width == 0 || pixels % width != 0 || (height is { } supplied && (ulong) width * supplied != pixels))
                CanvasConvert.Error(owner, "IndexSizeError", "The array length does not match the dimensions.");
            return new JsCanvasImageData(owner, width, pixels / width, data);
        }
        var sw = TypeConverter.ToUint32(args[0]);
        var sh = TypeConverter.ToUint32(args[1]);
        Settings(owner, args.At(2));
        return Allocate(owner, sw, sh);
    }

    internal static JsCanvasImageData Allocate(CanvasRealm owner, long width, long height)
    {
        if (width == 0 || height == 0) CanvasConvert.Error(owner, "IndexSizeError", "ImageData dimensions cannot be zero.");
        if (width < 0 || height < 0 || width > CanvasPng.MaxPixels || height > CanvasPng.MaxPixels || width * height > CanvasPng.MaxPixels)
            Throw.RangeError(owner.Realm, "ImageData exceeds the 16,777,216 pixel allocation limit.");
        owner.Engine.Constraints.Check();
        var constructor = owner.Realm.Intrinsics.Uint8ClampedArray;
        var data = (JsTypedArray) constructor.Construct([JsNumber.Create(width * height * 4)], constructor);
        return new JsCanvasImageData(owner, (uint) width, (uint) height, data);
    }

    internal static void Settings(CanvasRealm owner, JsValue value)
    {
        var settings = CanvasConvert.Dictionary(owner, value);
        var color = settings?.Get("colorSpace") ?? JsValue.Undefined;
        if (!color.IsUndefined()) CanvasConvert.Enum(owner, TypeConverter.ToString(color), "srgb", "display-p3");
        var format = settings?.Get("pixelFormat") ?? JsValue.Undefined;
        if (!format.IsUndefined())
        {
            var name = TypeConverter.ToString(format);
            CanvasConvert.Enum(owner, name, "rgba-unorm8", "rgba-float16");
            if (name == "rgba-float16") CanvasConvert.Error(owner, "NotSupportedError", "Only rgba-unorm8 ImageData is supported.");
        }
    }
}

/// <summary>An opaque gradient with validated color stops; no raster is produced.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#canvasgradient</remarks>
internal sealed class JsCanvasGradient : ObjectInstance
{
    internal JsCanvasGradient(CanvasRealm owner) : base(owner.Engine)
    {
        Owner = owner;
        Prototype = owner.Prototype("CanvasGradient");
    }
    internal CanvasRealm Owner { get; }
    internal JsValue AddColorStop(JsValue[] args)
    {
        CanvasConvert.Require(Owner, args, 2, "addColorStop");
        var offset = CanvasConvert.Number(Owner, args[0], restricted: true);
        var color = TypeConverter.ToString(args[1]);
        if (offset < 0 || offset > 1) CanvasConvert.Error(Owner, "IndexSizeError", "A color stop must be between zero and one.");
        if (!CanvasColor.TryParse(color, out _)) CanvasConvert.Error(Owner, "SyntaxError", "Invalid CSS color.");
        return JsValue.Undefined;
    }
}

/// <summary>An opaque image pattern, accepting the standard transform initializer.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#canvaspattern</remarks>
internal sealed class JsCanvasPattern : ObjectInstance
{
    internal JsCanvasPattern(CanvasRealm owner) : base(owner.Engine)
    {
        Owner = owner;
        Prototype = owner.Prototype("CanvasPattern");
    }
    internal CanvasRealm Owner { get; }
    internal JsValue SetTransform(JsValue[] args)
    {
        _ = CanvasConvert.Matrix(Owner, args.At(0));
        return JsValue.Undefined;
    }
}

/// <summary>Deterministic half-em-per-code-point text measurements, not measurements of rendered glyphs.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#textmetrics</remarks>
internal sealed class JsCanvasTextMetrics : ObjectInstance
{
    internal static readonly string[] Names =
    [
        "width", "actualBoundingBoxLeft", "actualBoundingBoxRight", "fontBoundingBoxAscent", "fontBoundingBoxDescent",
        "actualBoundingBoxAscent", "actualBoundingBoxDescent", "emHeightAscent", "emHeightDescent",
        "hangingBaseline", "alphabeticBaseline", "ideographicBaseline",
    ];
    private readonly double _width;
    private readonly double _size;
    private readonly double _left;
    private readonly double _baseline;

    internal JsCanvasTextMetrics(CanvasRealm owner, string text, CanvasDrawingState state) : base(owner.Engine)
    {
        Prototype = owner.Prototype("TextMetrics");
        _size = state.FontSize;
        CanvasText.TryLength(state.Values["letterSpacing"].AsString(), _size, out var letter);
        CanvasText.TryLength(state.Values["wordSpacing"].AsString(), _size, out var word);
        var points = 0;
        var spaces = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if ((points++ & 1023) == 0) owner.Engine.Constraints.Check();
            if (rune.Value is 9 or 10 or 12 or 13 or 32) spaces++;
        }
        _width = Math.Max(0, points * (_size * 0.5 + letter) + spaces * word);
        var align = state.Values["textAlign"].AsString();
        var rtl = state.Values["direction"].AsString() == "rtl";
        _left = align == "center" ? _width / 2 : align == "right" || (align == "start" && rtl) || (align == "end" && !rtl) ? _width : 0;
        _baseline = state.Values["textBaseline"].AsString() switch
        {
            "top" => _size * 0.8,
            "hanging" => _size * 0.64,
            "middle" => _size * 0.3,
            "ideographic" or "bottom" => -_size * 0.2,
            _ => 0,
        };
    }

    internal double GetMetric(string name) => name switch
    {
        "width" => _width,
        "actualBoundingBoxLeft" => _left,
        "actualBoundingBoxRight" => _width - _left,
        "fontBoundingBoxAscent" or "actualBoundingBoxAscent" or "emHeightAscent" => _size * 0.8 - _baseline,
        "fontBoundingBoxDescent" or "actualBoundingBoxDescent" or "emHeightDescent" => _size * 0.2 + _baseline,
        "hangingBaseline" => _size * 0.64 - _baseline,
        "alphabeticBaseline" => -_baseline,
        "ideographicBaseline" => -_size * 0.2 - _baseline,
        _ => throw new InvalidOperationException("Unknown text metric: " + name),
    };
}
