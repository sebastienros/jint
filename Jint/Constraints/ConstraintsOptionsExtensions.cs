using System.Threading;
using Jint.Constraints;

// ReSharper disable once CheckNamespace
namespace Jint;

public static class ConstraintsOptionsExtensions
{
    /// <summary>
    /// Limits the allowed statement count that can be run as part of the program.
    /// </summary>
    public static Options MaxStatements(this Options options, int maxStatements = 0)
    {
        options.WithoutConstraint(x => x is MaxStatementsConstraint);

        if (maxStatements > 0 && maxStatements < int.MaxValue)
        {
            options.Constraint(new MaxStatementsConstraint(maxStatements));
        }
        return options;
    }

    /// <summary>
    /// Sets constraint based on memory usage in bytes.
    /// </summary>
    public static Options LimitMemory(this Options options, long memoryLimit)
    {
        options.WithoutConstraint(x => x is MemoryLimitConstraint);

        if (memoryLimit > 0 && memoryLimit < int.MaxValue)
        {
            options.Constraint(new MemoryLimitConstraint(memoryLimit));
        }
        return options;
    }

    /// <summary>
    /// Sets constraint based on fixed time interval.
    /// </summary>
    public static Options TimeoutInterval(this Options options, TimeSpan timeoutInterval)
    {
        if (timeoutInterval > TimeSpan.Zero && timeoutInterval < TimeSpan.MaxValue)
        {
            options.Constraint(new TimeConstraint(timeoutInterval));
        }
        return options;
    }

    /// <summary>
    /// Sets cancellation token to be observed. NOTE that this can be unreliable/imprecise on full framework due to timer logic.
    /// </summary>
    public static Options CancellationToken(this Options options, CancellationToken cancellationToken)
    {
        options.WithoutConstraint(x => x is CancellationConstraint);

        if (cancellationToken != default)
        {
            options.Constraint(new CancellationConstraint(cancellationToken));
        }
        return options;
    }
}
