using System;
using System.Threading;
using Jint.Constraints;

namespace Jint
{
    public static class ConstraintsOptionsExtensions
    {
        /// <summary>
        /// Limits the allowed statement count that can be run as part of the program.
        /// </summary>
        public static Options MaxStatements(this Options options, int maxStatements = 0)
        {
            options.WithoutConstraint(x => x is MaxStatements);

            if (maxStatements > 0 && maxStatements < int.MaxValue)
            {
                options.Constraint(new MaxStatements(maxStatements));
            }
            return options;
        }

        public static Options LimitMemory(this Options options, long memoryLimit)
        {
            options.WithoutConstraint(x => x is MemoryLimit);

            if (memoryLimit > 0 && memoryLimit < int.MaxValue)
            {
                options.Constraint(new MemoryLimit(memoryLimit));
            }
            return options;
        }

        public static Options TimeoutInterval(this Options options, TimeSpan timeoutInterval)
        {
            options.WithoutConstraint(x => x is TimeConstraint);

            if (timeoutInterval > TimeSpan.Zero && timeoutInterval < TimeSpan.MaxValue)
            {
                options.Constraint(new TimeConstraint2(timeoutInterval));
            }
            return options;
        }

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
}
