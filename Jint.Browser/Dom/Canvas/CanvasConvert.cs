using Jint.Browser.Geometry;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom.Canvas;

/// <summary>WebIDL argument conversions shared by the canvas interfaces.</summary>
/// <remarks>https://webidl.spec.whatwg.org/#es-type-mapping</remarks>
internal static class CanvasConvert
{
    internal static void Require(CanvasRealm owner, JsValue[] args, int length, string name)
    {
        if (args.Length < length) Throw.TypeError(owner.Realm, name + " requires " + length + " arguments.");
        owner.Engine.Constraints.Check();
    }

    internal static ObjectInstance? Dictionary(CanvasRealm owner, JsValue value)
    {
        if (value.IsNullOrUndefined()) return null;
        if (value is ObjectInstance result) return result;
        Throw.TypeError(owner.Realm, "A dictionary must be an object.");
        return null;
    }

    internal static void Enum(CanvasRealm owner, string value, params ReadOnlySpan<string> allowed)
    {
        foreach (var candidate in allowed)
            if (value == candidate) return;
        Throw.TypeError(owner.Realm, "'" + value + "' is not a valid enumeration value.");
    }

    internal static double Dimension(CanvasRealm owner, JsValue value)
    {
        var number = Math.Truncate(TypeConverter.ToNumber(value));
        if (!double.IsFinite(number) || number < 0 || number >= 18446744073709551616d)
            Throw.TypeError(owner.Realm, "The canvas dimension is outside the unsigned long long range.");
        return number;
    }

    internal static int Long(CanvasRealm owner, JsValue value)
    {
        var number = Math.Truncate(TypeConverter.ToNumber(value));
        if (!double.IsFinite(number) || number < int.MinValue || number > int.MaxValue)
            Throw.TypeError(owner.Realm, "The value is outside the long range.");
        return (int) number;
    }

    internal static double Number(CanvasRealm owner, JsValue value, bool restricted = false)
    {
        var number = TypeConverter.ToNumber(value);
        if (restricted && !double.IsFinite(number)) Throw.TypeError(owner.Realm, "The value must be finite.");
        return number;
    }

    internal static double[] Numbers(CanvasRealm owner, JsValue[] args, int length, string name, bool restricted = false, int offset = 0)
    {
        Require(owner, args, offset + length, name);
        var numbers = new double[length];
        for (var i = 0; i < length; i++) numbers[i] = Number(owner, args[i + offset], restricted);
        return numbers;
    }

    internal static bool Finite(double[] values)
    {
        foreach (var value in values)
            if (!double.IsFinite(value)) return false;
        return true;
    }

    // DOMMatrix2DInit deliberately does not read DOMMatrixInit's extra 3D members.
    internal static double[] Matrix(CanvasRealm owner, JsValue value)
    {
        var dictionary = Dictionary(owner, value);
        string[] names = ["a", "b", "c", "d", "e", "f", "m11", "m12", "m21", "m22", "m41", "m42"];
        Span<double?> values = stackalloc double?[12];
        for (var i = 0; i < names.Length; i++)
        {
            var member = dictionary?.Get(names[i]) ?? JsValue.Undefined;
            values[i] = member.IsUndefined() ? null : TypeConverter.ToNumber(member);
        }
        var result = GeometryMatrix.Identity();
        ReadOnlySpan<int> indices = [0, 1, 4, 5, 12, 13];
        for (var i = 0; i < 6; i++)
        {
            if (values[i] is { } alias && values[i + 6] is { } member
                && alias != member && !(double.IsNaN(alias) && double.IsNaN(member)))
                Throw.TypeError(owner.Realm, "The matrix aliases must agree.");
            result[indices[i]] = values[i + 6] ?? values[i] ?? result[indices[i]];
        }
        return result;
    }

    internal static T Brand<T>(JsValue value, string name) where T : ObjectInstance
        => GeometryBrand.Of<T>(value, name, name);

    internal static void Error(CanvasRealm owner, string name, string message)
        => DomFailures.Refuse(owner.Dom, "Canvas", name, message);
}
