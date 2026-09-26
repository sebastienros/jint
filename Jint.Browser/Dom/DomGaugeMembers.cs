using Jint.HtmlParser;

namespace Jint.Browser.Dom;

/// <summary>Demand-driven HTML §4.10.13–14 progress and meter values over native attributes.</summary>
internal static class DomGaugeMembers
{
    // https://html.spec.whatwg.org/multipage/form-elements.html#the-meter-element
    internal static double Meter(DomRealm realm, Element element, string member)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        var result = MeterValue(work, element, member);
        work.Check();
        return result;
    }

    private static double MeterValue(DomReadWork work, Element element, string member)
    {
        var minimum = Number(work, element, "min", 0);
        if (member == "min") return minimum;
        var maximum = Math.Max(minimum, Number(work, element, "max", 1));
        if (member == "max") return maximum;
        if (member == "value") return Math.Clamp(Number(work, element, "value", 0), minimum, maximum);
        if (member == "optimum")
        {
            // Avoid overflowing the midpoint for finite endpoints of the same sign.
            var midpoint = Math.Sign(minimum) == Math.Sign(maximum)
                ? minimum + (maximum - minimum) / 2 : minimum / 2 + maximum / 2;
            return Math.Clamp(Number(work, element, "optimum", midpoint), minimum, maximum);
        }
        var low = Math.Clamp(Number(work, element, "low", minimum), minimum, maximum);
        return member == "low" ? low : Math.Clamp(Number(work, element, "high", maximum), low, maximum);
    }

    // https://html.spec.whatwg.org/multipage/form-elements.html#the-progress-element
    internal static double Progress(DomRealm realm, Element element, bool position)
        => Progress(element, position, realm.NativeReadCheckpoint, realm.CancellationToken);

    internal static double Progress(Element element, bool position, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var raw = work.Attribute(element, "value");
        var result = position ? -1d : 0d;
        if (raw is not null)
        {
            var maximum = Maximum(work, element);
            var current = Math.Clamp(Parse(work, raw, 0), 0, maximum);
            result = position ? current / maximum : current;
        }
        work.Check();
        return result;
    }

    internal static double ProgressMaximum(DomRealm realm, Element element)
        => ProgressMaximum(element, realm.NativeReadCheckpoint, realm.CancellationToken);

    internal static double ProgressMaximum(Element element, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new DomReadWork(checkpoint, token);
        work.Check();
        var result = Maximum(work, element);
        work.Check();
        return result;
    }

    private static double Maximum(DomReadWork work, Element element)
    {
        var value = Number(work, element, "max", 1);
        return value > 0 ? value : 1;
    }

    private static double Number(DomReadWork work, Element element, string attribute, double fallback)
        => Parse(work, work.Attribute(element, attribute), fallback);

    private static double Parse(DomReadWork work, string? raw, double fallback)
        => raw is not null && HtmlInputNumberSyntax.TryParsePrefix(raw.AsSpan(), out var value,
            _ => work.Check(), work.Token) ? value : fallback;
}
