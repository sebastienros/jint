namespace Jint.HtmlParser;

/// <summary>Output limits for a single serialization operation.</summary>
public sealed class SerializationLimits
{
    private long _maxOutputCharacters;

    internal static SerializationLimits Unbounded { get; } = new();

    /// <summary>Maximum emitted UTF-16 code units, including escaping and generated markup. Zero is unbounded.</summary>
    public long MaxOutputCharacters
    {
        get => _maxOutputCharacters;
        init => _maxOutputCharacters = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}

/// <summary>The output would exceed the configured serialization limit; no partial string is returned.</summary>
public sealed class SerializationLimitException : Exception
{
    internal SerializationLimitException(long limit, long observed)
        : base($"Serialization output limit of {limit} was exceeded at {observed}.")
    {
        Limit = limit;
        Observed = observed;
    }

    /// <summary>The configured output limit.</summary>
    public long Limit { get; }
    /// <summary>The output size attempted by the append that exceeded the limit.</summary>
    public long Observed { get; }
}
