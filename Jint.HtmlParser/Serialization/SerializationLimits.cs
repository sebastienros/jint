namespace Jint.HtmlParser.Serialization;

// Published with the complete MarkupSerializer surface in X3d.
internal sealed class SerializationLimits
{
    private long _maxOutputCharacters;

    internal static SerializationLimits Unbounded { get; } = new();

    internal long MaxOutputCharacters
    {
        get => _maxOutputCharacters;
        init => _maxOutputCharacters = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}

internal sealed class SerializationLimitException : Exception
{
    internal SerializationLimitException(long limit, long observed)
        : base($"Serialization output limit of {limit} was exceeded at {observed}.")
    {
        Limit = limit;
        Observed = observed;
    }

    internal long Limit { get; }
    internal long Observed { get; }
}
