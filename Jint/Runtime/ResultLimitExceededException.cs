namespace Jint.Runtime;

/// <summary>
/// Thrown when host-side conversion or serialization exceeds a configured <see cref="ResultLimits"/> boundary.
/// </summary>
public sealed class ResultLimitExceededException : JintException
{
    internal ResultLimitExceededException(ResultLimit limit, long maximum, long observed)
        : base($"Result {limit} limit of {maximum} was exceeded (observed {observed}).")
    {
        Limit = limit;
        Maximum = maximum;
        Observed = observed;
    }

    public ResultLimit Limit { get; }

    public long Maximum { get; }

    public long Observed { get; }
}
