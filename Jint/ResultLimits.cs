namespace Jint;

/// <summary>
/// Bounds host-side conversion and serialization of values produced by script.
/// </summary>
/// <remarks>
/// These limits do not replace execution constraints. Property access during conversion and JSON serialization
/// can invoke getters, proxy traps, <c>toJSON</c>, and replacer functions, so untrusted code also needs time,
/// statement, cancellation, memory, and stack limits.
/// </remarks>
public sealed class ResultLimits
{
    /// <summary>
    /// No result-specific limits. This is the compatibility default.
    /// </summary>
    public static ResultLimits Unlimited { get; } = new();

    /// <summary>
    /// A conservative starting point for untrusted results. Hosts should tune these values for their schema and
    /// still enforce an outer response and process budget.
    /// </summary>
    public static ResultLimits Conservative { get; } = new(
        maxDepth: 32,
        maxPropertyCount: 10_000,
        maxStringLength: 1_000_000,
        maxOutputCharacters: 2_000_000,
        maxOutputBytes: 4_000_000);

    public ResultLimits(
        int maxDepth = int.MaxValue,
        long maxPropertyCount = long.MaxValue,
        int maxStringLength = int.MaxValue,
        long maxOutputCharacters = long.MaxValue,
        long maxOutputBytes = long.MaxValue)
    {
        ValidateNonNegative(maxDepth, nameof(maxDepth));
        ValidateNonNegative(maxPropertyCount, nameof(maxPropertyCount));
        ValidateNonNegative(maxStringLength, nameof(maxStringLength));
        ValidateNonNegative(maxOutputCharacters, nameof(maxOutputCharacters));
        ValidateNonNegative(maxOutputBytes, nameof(maxOutputBytes));

        MaxDepth = maxDepth;
        MaxPropertyCount = maxPropertyCount;
        MaxStringLength = maxStringLength;
        MaxOutputCharacters = maxOutputCharacters;
        MaxOutputBytes = maxOutputBytes;
    }

    private static void ValidateNonNegative(long value, string paramName)
    {
        if (value < 0)
        {
            Runtime.Throw.ArgumentOutOfRangeException(paramName, "Result limits cannot be negative.");
        }
    }

    /// <summary>
    /// Maximum number of nested containers. A primitive has depth zero and the root container has depth one.
    /// </summary>
    public int MaxDepth { get; }

    /// <summary>
    /// Maximum cumulative number of object properties, array elements, map entries, or set elements visited.
    /// </summary>
    /// <remarks>
    /// This is the structural-work and container-allocation bound for
    /// <see cref="Engine.AdvancedOperations.ConvertResult"/>. Shared, non-cyclic references are converted once
    /// per occurrence, so hosts processing untrusted graphs must set this limit even when string and binary
    /// limits are configured.
    /// </remarks>
    public long MaxPropertyCount { get; }

    /// <summary>
    /// Maximum UTF-16 length of any individual string value or property name.
    /// </summary>
    public int MaxStringLength { get; }

    /// <summary>
    /// Maximum cumulative UTF-16 characters copied into a CLR result, or characters in a JSON document.
    /// </summary>
    public long MaxOutputCharacters { get; }

    /// <summary>
    /// Maximum bytes in a JSON UTF-8 document or cumulatively across binary values in a CLR result.
    /// </summary>
    /// <remarks>
    /// For CLR conversion this counts binary payloads only. It does not estimate dictionary, array, map, or set
    /// allocation; <see cref="MaxPropertyCount"/> bounds that structural output.
    /// </remarks>
    public long MaxOutputBytes { get; }
}

/// <summary>
/// Identifies the result boundary exceeded by a conversion or serialization operation.
/// </summary>
public enum ResultLimit
{
    Depth,
    PropertyCount,
    StringLength,
    OutputCharacters,
    OutputBytes
}
