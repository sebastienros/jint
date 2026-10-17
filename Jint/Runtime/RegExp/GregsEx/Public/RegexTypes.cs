#nullable enable
namespace Jint.Runtime.RegExp.GregsEx;

/// <summary>The storage representation used for matching and native offsets.</summary>
internal enum RegexInputKind
{
    /// <summary>UTF-16 code units.</summary>
    Utf16,
    /// <summary>Strict UTF-8 bytes.</summary>
    Utf8,
    /// <summary>Uninterpreted bytes.</summary>
    Bytes
}

/// <summary>The requested execution policy.</summary>
internal enum RegexExecutionMode
{
    /// <summary>Use the bounded interpreter.</summary>
    Interpreted,
    /// <summary>Use measured eligible compilation when available, otherwise interpretation.</summary>
    Auto
}

/// <summary>The backend actually selected for a pattern.</summary>
internal enum RegexExecutionBackend
{
    /// <summary>Instructions or tables.</summary>
    Interpreted,
    /// <summary>Previously generated executable code.</summary>
    Compiled
}

/// <summary>The units of a diagnostic source location.</summary>
internal enum RegexOffsetUnit
{
    /// <summary>UTF-16 code units.</summary>
    Utf16CodeUnits,
    /// <summary>Bytes.</summary>
    Bytes
}

/// <summary>The outcome of a workspace search.</summary>
internal enum RegexSearchStatus
{
    /// <summary>A selected match exists.</summary>
    Match,
    /// <summary>No selected match exists.</summary>
    NoMatch,
    /// <summary>The subject or scalar start boundary is invalid.</summary>
    InvalidInput,
    /// <summary>The caller's capture destination is too short.</summary>
    DestinationTooSmall,
    /// <summary>A work or workspace ceiling was exhausted.</summary>
    LimitExceeded,
    /// <summary>The operation was cancelled.</summary>
    Cancelled,
    /// <summary>The operation's deadline elapsed.</summary>
    TimedOut
}

/// <summary>A range in the original input's native units.</summary>
/// <param name="Index">Inclusive start.</param>
/// <param name="Length">Length in native units.</param>
internal readonly record struct RegexMatchRange(int Index, int Length);

/// <summary>A final capture; empty participation differs from absence.</summary>
/// <param name="Participated">Whether the group participated.</param>
/// <param name="Index">Inclusive native-unit start, or zero when absent.</param>
/// <param name="Length">Native-unit length, or zero when absent.</param>
internal readonly record struct RegexCapture(bool Participated, int Index, int Length);

/// <summary>Structured pattern compilation outcomes. None is the default success value.</summary>
internal enum RegexDiagnosticCode
{
    /// <summary>No compilation error.</summary>
    None,
    /// <summary>Malformed pattern syntax.</summary>
    InvalidSyntax,
    /// <summary>A construct is outside the explicit profile inventory.</summary>
    UnsupportedFeature,
    /// <summary>The pattern is outside the profile's legal text domain.</summary>
    InvalidPatternEncoding,
    /// <summary>The requested input representation is unsupported.</summary>
    UnsupportedRepresentation,
    /// <summary>The pattern-unit ceiling was exceeded.</summary>
    PatternLimitExceeded,
    /// <summary>The syntax nesting ceiling was exceeded.</summary>
    NestingLimitExceeded,
    /// <summary>The instruction ceiling was exceeded.</summary>
    ProgramLimitExceeded,
    /// <summary>The class-range ceiling was exceeded.</summary>
    ClassLimitExceeded,
    /// <summary>The group-count ceiling was exceeded.</summary>
    CaptureLimitExceeded,
    /// <summary>The compile-work ceiling was exceeded.</summary>
    CompileWorkLimitExceeded,
    /// <summary>The peak live compilation-memory ceiling was exceeded.</summary>
    CompileMemoryLimitExceeded,
    /// <summary>The retained plan-byte ceiling was exceeded.</summary>
    PlanLimitExceeded,
    /// <summary>An internal program does not satisfy the frozen IR contract.</summary>
    InvalidProgram
}

/// <summary>An owned, structured pattern diagnostic; successful paths need no message.</summary>
/// <param name="Code">Diagnostic identity.</param>
/// <param name="Offset">Source start in LocationUnit.</param>
/// <param name="Length">Source length in LocationUnit.</param>
/// <param name="LocationUnit">Native pattern location units.</param>
/// <param name="Message">Optional owned explanation.</param>
internal readonly record struct RegexDiagnostic(
    RegexDiagnosticCode Code, int Offset, int Length, RegexOffsetUnit LocationUnit, string? Message = null);

/// <summary>Distinct execution failures. None is the default no-failure value.</summary>
internal enum RegexExecutionFailureCode
{
    /// <summary>No execution failure.</summary>
    None,
    /// <summary>Malformed UTF-16 or UTF-8 subject.</summary>
    InvalidEncoding,
    /// <summary>The requested start falls inside an encoded scalar.</summary>
    InvalidStartBoundary,
    /// <summary>The capture destination is shorter than GroupCount.</summary>
    DestinationTooSmall,
    /// <summary>The operation work ceiling was exhausted.</summary>
    WorkLimitExceeded,
    /// <summary>Prepared workspace capacity is insufficient and growth is disabled.</summary>
    WorkspaceCapacityExceeded,
    /// <summary>The workspace memory ceiling was exceeded.</summary>
    WorkspaceLimitExceeded,
    /// <summary>Cancellation was requested.</summary>
    Cancelled,
    /// <summary>The deadline elapsed.</summary>
    TimedOut,
    /// <summary>Well-formed encoded input contains a character outside the profile's legal domain.</summary>
    InvalidInputDomain
}

/// <summary>Execution failure details, reset at the start of each operation.</summary>
/// <param name="Code">Failure identity.</param>
/// <param name="Offset">Native input position at detection.</param>
/// <param name="WorkConsumed">Successfully charged work units.</param>
/// <param name="RequiredCapacity">Required capture count for DestinationTooSmall; otherwise zero.</param>
internal readonly record struct RegexExecutionFailure(
    RegexExecutionFailureCode Code, int Offset, long WorkConsumed, long RequiredCapacity = 0);

/// <summary>Exception raised by the throwing parse convenience API.</summary>
internal sealed class RegexParseException : ArgumentException
{
    /// <summary>The original structured diagnostic.</summary>
    public RegexDiagnostic Diagnostic { get; }

    /// <summary>Creates an exception from an actual failing diagnostic.</summary>
    public RegexParseException(RegexDiagnostic diagnostic)
        : base(diagnostic.Message ?? diagnostic.Code.ToString(), "pattern")
    {
        if (diagnostic.Code == RegexDiagnosticCode.None)
        {
            throw new ArgumentException("A parse exception requires a failing diagnostic.", nameof(diagnostic));
        }

        Diagnostic = diagnostic;
    }
}

/// <summary>Exception raised by boolean convenience APIs on execution failure.</summary>
internal sealed class RegexExecutionException : Exception
{
    /// <summary>The original structured failure.</summary>
    public RegexExecutionFailure Failure { get; }

    /// <summary>Creates an exception from an actual failing execution.</summary>
    public RegexExecutionException(RegexExecutionFailure failure) : base(failure.Code.ToString())
    {
        if (failure.Code == RegexExecutionFailureCode.None)
        {
            throw new ArgumentException("An execution exception requires a failure.", nameof(failure));
        }

        Failure = failure;
    }
}
