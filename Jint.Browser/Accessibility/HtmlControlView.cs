using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Browser.Accessibility;

/// <summary>Read-only HTML control values whose authoritative storage is the native content attribute.</summary>
internal static class HtmlControlView
{
    // Reuse HTML's shared prefix parsers and IDL range handling; these getters need no realm or engine.
    private static readonly ReflectedAttribute ProgressMax = ReflectedAttribute.Numeric("HTMLProgressElement.max", "max", ReflectedKind.LimitedDouble, 1);
    private static readonly ReflectedAttribute Value = ReflectedAttribute.Numeric("value", "value", ReflectedKind.Double, 0);
    private static readonly ReflectedAttribute Minimum = ReflectedAttribute.Numeric("min", "min", ReflectedKind.Double, 0);
    private static readonly ReflectedAttribute Maximum = ReflectedAttribute.Numeric("max", "max", ReflectedKind.Double, 1);
    private static readonly ReflectedAttribute Start = ReflectedAttribute.Numeric("HTMLOListElement.start", "start", ReflectedKind.Long, 1);

    internal static int ListStart(Element element) => (int) Start.Get(element).AsNumber();

    // https://html.spec.whatwg.org/multipage/form-elements.html#the-progress-element
    internal static double ProgressMaximum(Element element) => ProgressMax.Get(element).AsNumber();
    internal static double ProgressValue(Element element)
        => Math.Clamp(Value.Get(element).AsNumber(), 0, ProgressMaximum(element));

    // https://html.spec.whatwg.org/multipage/form-elements.html#the-meter-element
    internal static (double Minimum, double Maximum, double Value) Meter(Element element)
    {
        var minimum = Minimum.Get(element).AsNumber();
        var maximum = Math.Max(minimum, Maximum.Get(element).AsNumber());
        return (minimum, maximum, Math.Clamp(Value.Get(element).AsNumber(), minimum, maximum));
    }
}
