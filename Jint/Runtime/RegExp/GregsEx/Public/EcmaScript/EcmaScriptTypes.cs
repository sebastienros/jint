#nullable enable
namespace Jint.Runtime.RegExp.GregsEx;

/// <summary>Native UTF-16 source domains for ECMAScript diagnostics.</summary>
internal enum EcmaScriptDiagnosticLocation
{
    /// <summary>The supplied pattern text.</summary>
    Pattern,
    /// <summary>The supplied flag string.</summary>
    Flags
}

/// <summary>Flavor detail around the shared diagnostic without changing its offset units.</summary>
/// <param name="Diagnostic">Shared diagnostic with UTF-16 source offsets.</param>
/// <param name="Location">Which original text the offsets identify.</param>
/// <param name="FeatureId">Stable ES-nnn capability/early-error key, or null on success.</param>
internal readonly record struct EcmaScriptDiagnostic(RegexDiagnostic Diagnostic,
    EcmaScriptDiagnosticLocation Location, string? FeatureId);

/// <summary>Finite ceilings for allocating host results, separate from search workspace.</summary>
internal sealed record EcmaScriptHostLimits
{
    /// <summary>Maximum retained match or split entries per host call.</summary>
    public int MaxResults { get; }
    /// <summary>Maximum total materialized UTF-16 units per host call.</summary>
    public int MaxOutputUnits { get; }
    /// <summary>Maximum conservatively accounted live output bytes, including old and new growth buffers.</summary>
    public long MaxMemoryBytes { get; }
    /// <summary>Creates finite positive host ceilings; output work also uses the match-work ceiling.</summary>
    public EcmaScriptHostLimits(int maxResults = 1_000_000, int maxOutputUnits = 16_777_216,
        long maxMemoryBytes = 67_108_864)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMemoryBytes);
        MaxResults = maxResults;
        MaxOutputUnits = maxOutputUnits;
        MaxMemoryBytes = maxMemoryBytes;
    }
}

/// <summary>Distinct typed-host errors; search failures retain RegexExecutionException.</summary>
internal enum EcmaScriptHostFailureCode
{
    /// <summary>No failure.</summary>
    None,
    /// <summary>MatchAll or ReplaceAll requires the global flag.</summary>
    GlobalFlagRequired,
    /// <summary>The result count ceiling was reached.</summary>
    ResultLimitExceeded,
    /// <summary>The output UTF-16 unit ceiling was reached.</summary>
    OutputLimitExceeded,
    /// <summary>The live output memory ceiling was reached.</summary>
    MemoryLimitExceeded
}

/// <summary>Failure details for bounded host materialization.</summary>
internal readonly record struct EcmaScriptHostFailure(EcmaScriptHostFailureCode Code,
    long RequiredCapacity, long WorkConsumed);

/// <summary>A typed-host failure; no partial result is returned.</summary>
internal sealed class EcmaScriptHostException : Exception
{
    /// <summary>The structured host failure.</summary>
    public EcmaScriptHostFailure Failure { get; }
    /// <summary>Creates an exception from a non-success host failure.</summary>
    public EcmaScriptHostException(EcmaScriptHostFailure failure) : base(failure.Code.ToString())
    {
        if (!Enum.IsDefined(failure.Code) || failure.Code == EcmaScriptHostFailureCode.None ||
            failure.RequiredCapacity < 0 || failure.WorkConsumed < 0)
        {
            throw new ArgumentException("A host exception requires a valid failing result.", nameof(failure));
        }

        Failure = failure;
    }
}
