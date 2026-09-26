namespace Jint.HtmlParser;

/// <summary>HTML range sanitization: exact midpoint, clamp, nearest in-bounds lattice point and upward ties.</summary>
internal static class HtmlInputRangeValue
{
    internal static string Sanitize(string value, in HtmlInputNumericConstraints constraints,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (constraints.Type != HtmlInputType.Range || !constraints.Applies)
            throw new ArgumentException("Range constraints are required.", nameof(constraints));
        cancellationToken.ThrowIfCancellationRequested();
        var min = constraints.Minimum!.Value;
        var max = constraints.Maximum!.Value;
        var ordered = min.CompareTo(max) <= 0;
        var parsed = HtmlInputNumberSyntax.TryParseValue(value, out var number, cancellationToken: cancellationToken);
        var replaced = !parsed;
        var current = parsed ? HtmlInputDecimal.FromDouble(number) : ordered ? min.Add(max).Half() : min;
        if (current.CompareTo(min) < 0) { current = min; replaced = true; }
        if (ordered && current.CompareTo(max) > 0) { current = max; replaced = true; }
        if (constraints.Step.HasValue && constraints.IsMismatch(current))
        {
            var lower = constraints.AlignDown(current);
            var upper = constraints.AlignUp(current);
            var lowerAllowed = lower.CompareTo(min) >= 0 && (!ordered || lower.CompareTo(max) <= 0);
            var upperAllowed = upper.CompareTo(min) >= 0 && (!ordered || upper.CompareTo(max) <= 0);
            if (lowerAllowed && upperAllowed)
            {
                current = current.Subtract(lower).CompareTo(upper.Subtract(current)) < 0 ? lower : upper;
                replaced = true;
            }
            else if (lowerAllowed) { current = lower; replaced = true; }
            else if (upperAllowed) { current = upper; replaced = true; }
        }
        cancellationToken.ThrowIfCancellationRequested();
        // Range's conditional sanitizer writes do not canonicalize an otherwise valid author spelling.
        if (!replaced) return value;
        if (!current.TryPublish(out var published)) throw new InvalidOperationException("A bounded range value must publish finite.");
        var result = HtmlInputNumberFormatter.FormatFinite(published);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
