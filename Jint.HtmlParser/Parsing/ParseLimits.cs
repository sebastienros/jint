namespace Jint.HtmlParser;

/// <summary>Inclusive bounds on the original input and lexical parsing work. Zero disables a bound.</summary>
public sealed class ParseLimits
{
    private long _maxInputCharacters;
    private int _maxTokenCharacters;
    private int _maxNestingDepth;
    private long _maxEntityExpansionCharacters = 10_000_000;

    /// <summary>Shared all-zero limits.</summary>
    public static ParseLimits Unbounded { get; } = new() { MaxEntityExpansionCharacters = 0 };

    internal static ParseLimits Default { get; } = new();

    /// <summary>Maximum original UTF-16 input units.</summary>
    public long MaxInputCharacters
    {
        get => _maxInputCharacters;
        init => _maxInputCharacters = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Maximum raw source units in one atomic lexical token.</summary>
    public int MaxTokenCharacters
    {
        get => _maxTokenCharacters;
        init => _maxTokenCharacters = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>Maximum nested CSS functions and simple blocks.</summary>
    public int MaxNestingDepth
    {
        get => _maxNestingDepth;
        init => _maxNestingDepth = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Maximum UTF-16 replacement units consumed by XML general and parameter entities; 10,000,000 by default.
    /// Set zero explicitly to disable this bound.
    /// </summary>
    public long MaxEntityExpansionCharacters
    {
        get => _maxEntityExpansionCharacters;
        init => _maxEntityExpansionCharacters = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}

/// <summary>The resource bound exceeded by a parser.</summary>
public enum ParseLimitKind
{
    InputCharacters,
    TokenCharacters,
    NestingDepth,
    EntityExpansionCharacters
}

/// <summary>A parser stopped before accepting an amount beyond a configured inclusive bound.</summary>
public sealed class ParseLimitException : Exception
{
    internal ParseLimitException(ParseLimitKind kind, long limit, long observed)
        : base($"Parse limit {kind} of {limit} was exceeded at {observed}.")
    {
        Kind = kind;
        Limit = limit;
        Observed = observed;
    }

    /// <summary>The bound that was exceeded.</summary>
    public ParseLimitKind Kind { get; }

    /// <summary>The configured inclusive bound.</summary>
    public long Limit { get; }

    /// <summary>The first measured amount beyond the bound, or the known complete input length.</summary>
    public long Observed { get; }
}
