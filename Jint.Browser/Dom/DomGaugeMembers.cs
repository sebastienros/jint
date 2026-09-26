using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Demand-driven HTML §4.10.13–14 progress and meter values over native attributes.</summary>
internal static class DomGaugeMembers
{
    // https://html.spec.whatwg.org/multipage/form-elements.html#the-meter-element
    internal static double Meter(DomRealm realm, Element element, string member)
    {
        var minimum = Number(realm, element, "min", 0);
        if (member == "min") return minimum;
        var maximum = Math.Max(minimum, Number(realm, element, "max", 1));
        if (member == "max") return maximum;
        if (member == "value") return Math.Clamp(Number(realm, element, "value", 0), minimum, maximum);
        if (member == "optimum")
        {
            // Avoid overflowing the midpoint for finite endpoints of the same sign.
            var midpoint = Math.Sign(minimum) == Math.Sign(maximum)
                ? minimum + (maximum - minimum) / 2 : minimum / 2 + maximum / 2;
            return Math.Clamp(Number(realm, element, "optimum", midpoint), minimum, maximum);
        }
        var low = Math.Clamp(Number(realm, element, "low", minimum), minimum, maximum);
        return member == "low" ? low : Math.Clamp(Number(realm, element, "high", maximum), low, maximum);
    }

    // https://html.spec.whatwg.org/multipage/form-elements.html#the-progress-element
    internal static double Progress(DomRealm realm, Element element, bool position)
    {
        if (element.GetAttributeNS(null, "value") is null) return position ? -1 : 0;
        var maximum = Number(realm, element, "max", 1);
        if (maximum <= 0) maximum = 1;
        var current = Math.Clamp(Number(realm, element, "value", 0), 0, maximum);
        return position ? current / maximum : current;
    }

    private static double Number(DomRealm realm, Element element, string attribute, double fallback)
    {
        realm.Engine.Constraints.Check();
        realm.CancellationToken.ThrowIfCancellationRequested();
        var raw = element.GetAttributeNS(null, attribute);
        return raw is not null && HtmlInputNumberSyntax.TryParsePrefix(raw.AsSpan(), out var value,
            _ => realm.Engine.Constraints.Check(), realm.CancellationToken) ? value : fallback;
    }
}
