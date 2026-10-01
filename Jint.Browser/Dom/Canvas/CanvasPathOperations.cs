using Jint.Browser.Geometry;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;

namespace Jint.Browser.Dom.Canvas;

/// <summary>An opaque path shell; path geometry is deliberately not rasterized or hit-tested.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#path2d-objects</remarks>
internal sealed class JsCanvasPath : ObjectInstance
{
    internal JsCanvasPath(CanvasRealm owner) : base(owner.Engine)
    {
        Owner = owner;
        Prototype = owner.Prototype("Path2D");
    }
    internal CanvasRealm Owner { get; }
}

/// <summary>Conversion and radius validation shared by contexts and Path2D.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#canvaspath</remarks>
internal static class CanvasPathOperations
{
    internal static JsValue Invoke(CanvasRealm owner, string name, JsValue[] args)
    {
        var length = name switch
        {
            "closePath" => 0,
            "moveTo" or "lineTo" => 2,
            "quadraticCurveTo" or "rect" or "roundRect" => 4,
            "arc" or "arcTo" => 5,
            "bezierCurveTo" => 6,
            "ellipse" => 7,
            _ => throw new InvalidOperationException("Unknown path operation: " + name),
        };
        var numbers = CanvasConvert.Numbers(owner, args, length, name);
        if (name is "arc" or "ellipse") _ = TypeConverter.ToBoolean(args.At(length));
        if (name == "roundRect")
        {
            RoundRect(owner, numbers, args.At(4));
            return JsValue.Undefined;
        }
        if (!CanvasConvert.Finite(numbers)) return JsValue.Undefined;
        if ((name == "arcTo" && numbers[4] < 0) || (name == "arc" && numbers[2] < 0)
            || (name == "ellipse" && (numbers[2] < 0 || numbers[3] < 0)))
            CanvasConvert.Error(owner, "IndexSizeError", "A radius cannot be negative.");
        return JsValue.Undefined;
    }

    private static void RoundRect(CanvasRealm owner, double[] coordinates, JsValue value)
    {
        var radii = new List<(double X, double Y)>(4);
        if (value is ObjectInstance obj && obj.GetMethod(GlobalSymbolRegistry.Iterator) is not null)
        {
            var iterator = value.GetIterator(owner.Realm);
            try
            {
                while (iterator.TryIteratorStepValue(out var entry))
                {
                    owner.Engine.Constraints.Check();
                    radii.Add(Radius(owner, entry));
                }
            }
            catch
            {
                iterator.Close(Jint.Runtime.CompletionType.Throw);
                throw;
            }
        }
        else radii.Add(value.IsUndefined() ? (0, 0) : Radius(owner, value));
        if (radii.Count is < 1 or > 4) Throw.RangeError(owner.Realm, "roundRect requires one to four radii.");
        if (!CanvasConvert.Finite(coordinates)) return;
        foreach (var radius in radii)
            if (!double.IsFinite(radius.X) || !double.IsFinite(radius.Y)) return;
        foreach (var radius in radii)
            if (radius.X < 0 || radius.Y < 0) Throw.RangeError(owner.Realm, "A radius cannot be negative.");
    }

    private static (double X, double Y) Radius(CanvasRealm owner, JsValue value)
    {
        if (value.IsObject() || value.IsNull())
        {
            var point = GeometryConversion.PointInit(owner.Realm, value);
            return (point.X, point.Y);
        }
        var number = TypeConverter.ToNumber(value);
        return (number, number);
    }
}
