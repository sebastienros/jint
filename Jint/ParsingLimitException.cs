namespace Jint;

/// <summary>
/// The exception thrown when parsing exceeds a configured source-length or AST-node limit.
/// </summary>
/// <remarks>
/// This is a host resource-limit exception. Jint does not convert it to a catchable JavaScript error.
/// </remarks>
public sealed class ParsingLimitException : JintException
{
    internal ParsingLimitException(ParsingLimitKind kind, int limit, long actual)
        : base($"Parsing exceeded the configured {GetDescription(kind)} limit of {limit}: observed {actual}.")
    {
        Kind = kind;
        Limit = limit;
        Actual = actual;
    }

    /// <summary>
    /// Gets the resource whose limit was exceeded.
    /// </summary>
    public ParsingLimitKind Kind { get; }

    /// <summary>
    /// Gets the configured limit.
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Gets the observed value which exceeded the limit.
    /// </summary>
    public long Actual { get; }

    private static string GetDescription(ParsingLimitKind kind) => kind switch
    {
        ParsingLimitKind.SourceLength => "source length",
        ParsingLimitKind.NodeCount => "AST node count",
        _ => "parsing resource",
    };
}

/// <summary>
/// Identifies a parser resource limit.
/// </summary>
public enum ParsingLimitKind
{
    /// <summary>
    /// The number of UTF-16 code units in the parser input.
    /// </summary>
    SourceLength,

    /// <summary>
    /// The number of nodes produced for the abstract syntax tree.
    /// </summary>
    NodeCount,
}
