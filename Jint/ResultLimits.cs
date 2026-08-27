namespace Jint;

/// <summary>
/// Bounds host-side conversion and serialization of values produced by script.
/// </summary>
/// <remarks>
/// <para>
/// Every dimension is an <c>init</c> property with an unlimited default, so a policy is written by naming
/// the dimensions it bounds — either from scratch, or by adjusting a preset:
/// <c>ResultLimits.Conservative with { MaxStringLength = 4_096 }</c>.
/// </para>
/// <para>
/// These limits do not replace execution constraints. Property access during conversion and JSON serialization
/// can invoke getters, proxy traps, <c>toJSON</c>, and replacer functions, so untrusted code also needs time,
/// statement, cancellation, memory, and stack limits.
/// </para>
/// </remarks>
public sealed record ResultLimits
{
    /// <summary>
    /// No result-specific limits. This is the compatibility default.
    /// </summary>
    public static ResultLimits Unlimited { get; } = new();

    /// <summary>
    /// A conservative starting point for untrusted results. Hosts should tune these values for their schema and
    /// still enforce an outer response and process budget.
    /// </summary>
    public static ResultLimits Conservative { get; } = new()
    {
        MaxDepth = 32,
        MaxPropertyCount = 10_000,
        MaxStringLength = 1_000_000,
        MaxOutputCharacters = 2_000_000,
        MaxOutputBytes = 4_000_000,
    };

    /// <summary>
    /// Creates limits that bound nothing, to be narrowed through the property initializers that follow.
    /// </summary>
    public ResultLimits()
    {
    }

    /// <summary>
    /// Gets or sets the maximum number of nested containers. A primitive has depth zero and the root container
    /// has depth one.
    /// </summary>
    public int MaxDepth
    {
        get;
        init
        {
            ValidateNonNegative(value, nameof(MaxDepth));
            field = value;
        }
    } = int.MaxValue;

    /// <summary>
    /// Gets or sets the maximum cumulative number of object properties, array elements, map entries, or set
    /// elements visited.
    /// </summary>
    /// <remarks>
    /// This is the structural-work and container-allocation bound for
    /// <see cref="Engine.ConvertResult"/>. Shared, non-cyclic references are converted once
    /// per occurrence, so hosts processing untrusted graphs must set this limit even when string and binary
    /// limits are configured.
    /// </remarks>
    public long MaxPropertyCount
    {
        get;
        init
        {
            ValidateNonNegative(value, nameof(MaxPropertyCount));
            field = value;
        }
    } = long.MaxValue;

    /// <summary>
    /// Gets or sets the maximum UTF-16 length of any individual string value or property name.
    /// </summary>
    public int MaxStringLength
    {
        get;
        init
        {
            ValidateNonNegative(value, nameof(MaxStringLength));
            field = value;
        }
    } = int.MaxValue;

    /// <summary>
    /// Gets or sets the maximum cumulative UTF-16 characters copied into a CLR result, or characters in a JSON
    /// document.
    /// </summary>
    public long MaxOutputCharacters
    {
        get;
        init
        {
            ValidateNonNegative(value, nameof(MaxOutputCharacters));
            field = value;
        }
    } = long.MaxValue;

    /// <summary>
    /// Gets or sets the maximum bytes in a JSON UTF-8 document, or cumulatively across binary values in a CLR
    /// result.
    /// </summary>
    /// <remarks>
    /// For CLR conversion this counts binary payloads only. It does not estimate dictionary, array, map, or set
    /// allocation; <see cref="MaxPropertyCount"/> bounds that structural output.
    /// </remarks>
    public long MaxOutputBytes
    {
        get;
        init
        {
            ValidateNonNegative(value, nameof(MaxOutputBytes));
            field = value;
        }
    } = long.MaxValue;

    private static void ValidateNonNegative(long value, string paramName)
    {
        if (value < 0)
        {
            Runtime.Throw.ArgumentOutOfRangeException(paramName, "Result limits cannot be negative.");
        }
    }
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
