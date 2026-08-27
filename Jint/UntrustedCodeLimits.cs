using System.Threading;
using Jint.Constraints;
using Jint.Runtime;

namespace Jint;

/// <summary>
/// Explicit resource limits for an engine configured with
/// <see cref="OptionsExtensions.ForUntrustedCode"/>.
/// </summary>
/// <remarks>
/// <para>
/// The eight budget dimensions are <c>required</c>: a useful budget depends on the request and the workload,
/// so the host names every one of them, and values the ordinary constraint helpers read as "unlimited" are
/// rejected rather than silently disabling a protection. Parser, module-graph and result limits carry
/// conservative finite defaults that should still be tuned.
/// </para>
/// <para>
/// <see cref="Default"/> is the one place Jint picks the eight itself. It is a preset to adjust —
/// <c>UntrustedCodeLimits.Default with { MaxStatements = 5_000 }</c> — and not a value to accept unread.
/// </para>
/// <para>
/// <see cref="TimeoutInterval"/>, <see cref="MaxStatements"/> and <see cref="MemoryLimit"/> apply to one
/// top-level engine entry. <see cref="MaxOperationDuration"/> spans a host operation containing several
/// entries, but only while the host brackets that operation with <see cref="BeginOperation"/>.
/// </para>
/// </remarks>
public sealed record UntrustedCodeLimits
{
    /// <summary>
    /// A complete conservative profile: the stricter budget of the two deployment examples Jint documents.
    /// </summary>
    /// <remarks>
    /// Every value here is Jint guessing on the host's behalf, which is what the <c>required</c> members
    /// exist to prevent. Measure a normal workload and adjust with a <c>with</c> expression.
    /// </remarks>
    public static UntrustedCodeLimits Default { get; } = new()
    {
        TimeoutInterval = TimeSpan.FromSeconds(1),
        MaxStatements = 50_000,
        MemoryLimit = 16_000_000,
        MaxRecursionDepth = 64,
        MaxArraySize = 10_000,
        RegexTimeout = TimeSpan.FromMilliseconds(250),
        PromiseTimeout = TimeSpan.FromMilliseconds(500),
        MaxOperationDuration = TimeSpan.FromSeconds(2),
    };

    /// <summary>
    /// Creates a set of limits whose eight budget dimensions the object initializer must name.
    /// </summary>
    public UntrustedCodeLimits()
    {
    }

    /// <summary>
    /// Gets or sets the maximum wall-clock time for one engine entry.
    /// </summary>
    public required TimeSpan TimeoutInterval
    {
        get;
        init
        {
            ValidateFiniteTimeout(value, nameof(TimeoutInterval));
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of statements executed by one engine entry.
    /// </summary>
    public required int MaxStatements
    {
        get;
        init
        {
            ValidateRange(value, nameof(MaxStatements));
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of bytes allocated by one engine entry.
    /// </summary>
    public required long MemoryLimit
    {
        get;
        init
        {
            ValidateRange(value, nameof(MemoryLimit));
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum recursion depth. Zero forbids recursion.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Options.ConstraintOptions.MaxRecursionDepth"/>, negative and saturated values are
    /// rejected rather than treated as unlimited.
    /// </remarks>
    public required int MaxRecursionDepth
    {
        get;
        init
        {
            if (value < 0 || value == int.MaxValue)
            {
                Throw.ArgumentOutOfRangeException(
                    nameof(MaxRecursionDepth),
                    "The recursion depth must be between zero and Int32.MaxValue - 1.");
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum JavaScript array size.
    /// </summary>
    public required uint MaxArraySize
    {
        get;
        init
        {
            if (value == 0 || value == uint.MaxValue)
            {
                Throw.ArgumentOutOfRangeException(
                    nameof(MaxArraySize),
                    "The array size must be between one and UInt32.MaxValue - 1.");
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum time one regular expression operation may run.
    /// </summary>
    /// <remarks>
    /// A prepared script or module carries the timeout selected when it was prepared; set its parsing options
    /// to this value as well.
    /// </remarks>
    public required TimeSpan RegexTimeout
    {
        get;
        init
        {
            ValidateFiniteTimeout(value, nameof(RegexTimeout));
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum time the engine may wait for a promise to settle.
    /// </summary>
    public required TimeSpan PromiseTimeout
    {
        get;
        init
        {
            ValidateFiniteTimeout(value, nameof(PromiseTimeout));
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum wall-clock time for a host operation containing one or more engine entries.
    /// </summary>
    /// <remarks>
    /// The host must activate this limit with <see cref="BeginOperation"/> around each operation.
    /// </remarks>
    public required TimeSpan MaxOperationDuration
    {
        get;
        init
        {
            ValidateFiniteTimeout(value, nameof(MaxOperationDuration));
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum UTF-16 source length for every engine-owned parse. Defaults to one million.
    /// </summary>
    public int MaxSourceLength
    {
        get;
        init
        {
            ValidateRange(value, nameof(MaxSourceLength));
            field = value;
        }
    } = 1_000_000;

    /// <summary>
    /// Gets or sets the maximum number of AST nodes for every engine-owned parse. Defaults to 250,000.
    /// </summary>
    public int MaxNodeCount
    {
        get;
        init
        {
            ValidateRange(value, nameof(MaxNodeCount));
            field = value;
        }
    } = 250_000;

    /// <summary>
    /// Gets or sets the maximum number of distinct module records registered by one engine. Defaults to 100.
    /// </summary>
    public int MaxModuleCount
    {
        get;
        init
        {
            ValidateRange(value, nameof(MaxModuleCount));
            field = value;
        }
    } = 100;

    /// <summary>
    /// Gets or sets the maximum aggregate module source bytes one engine may register. Defaults to ten million.
    /// </summary>
    public long MaxTotalModuleSourceBytes
    {
        get;
        init
        {
            ValidateRange(value, nameof(MaxTotalModuleSourceBytes));
            field = value;
        }
    } = 10_000_000;

    /// <summary>
    /// Gets or sets the maximum graph depth for one top-level module load. Defaults to 32.
    /// </summary>
    public int MaxModuleGraphDepth
    {
        get;
        init
        {
            ValidateRange(value, nameof(MaxModuleGraphDepth));
            field = value;
        }
    } = 32;

    /// <summary>
    /// Gets or sets the maximum number of resolver calls for one top-level module load. Defaults to 1,000.
    /// </summary>
    public int MaxModuleResolutionHops
    {
        get;
        init
        {
            ValidateRange(value, nameof(MaxModuleResolutionHops));
            field = value;
        }
    } = 1_000;

    /// <summary>
    /// Gets or sets the bounds on result conversion, JSON serialization, and error rendering.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="ResultLimits.Conservative"/>. Every dimension must be finite, so
    /// <see cref="ResultLimits.Unlimited"/> is rejected.
    /// </remarks>
    public ResultLimits ResultLimits
    {
        get;
        init
        {
            ValidateResultLimits(value, nameof(ResultLimits));
            field = value;
        }
    } = ResultLimits.Conservative;

    /// <summary>
    /// Arms the multi-entry operation deadline installed by
    /// <see cref="OptionsExtensions.ForUntrustedCode"/> and returns a scope that disarms it.
    /// </summary>
    /// <param name="engine">The engine that will execute the operation.</param>
    /// <param name="cancellationToken">A cancellable token tied to the same host operation.</param>
    /// <returns>
    /// A scope that must enclose every engine entry belonging to the operation. Disposing it ends the
    /// operation deadline.
    /// </returns>
    /// <remarks>
    /// The deadline is a cooperative engine constraint. A host callback that does not return cannot be
    /// preempted, and an idle asynchronous wait may observe its separate <see cref="PromiseTimeout"/> before
    /// the engine reaches another constraint checkpoint. Keep the outer worker/request deadline as the hard
    /// stop. Operation scopes cannot overlap on one engine; a nested or concurrent scope is rejected rather
    /// than allowed to disarm the still-active outer deadline.
    /// <para>
    /// The engine has to have been configured with <b>this instance</b>, not merely an equal one: a
    /// <c>with</c> expression produces a different object, so hand the engine the one the scope uses.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The engine was not configured with this limits instance through
    /// <see cref="OptionsExtensions.ForUntrustedCode"/>, or an operation scope is already active.
    /// </exception>
    public IDisposable BeginOperation(Engine engine, CancellationToken cancellationToken)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        if (!cancellationToken.CanBeCanceled)
        {
            Throw.ArgumentException(
                "A cancellable host-operation token is required.",
                nameof(cancellationToken));
        }

        using var ownership = engine!.EnterHostCall();
        if (!ReferenceEquals(engine._untrustedCodeLimits, this))
        {
            Throw.InvalidOperationException(
                "The engine was not configured with this UntrustedCodeLimits instance through Options.ForUntrustedCode.");
        }

        var deadline = engine.Constraints.Find<OperationDeadlineConstraint>();
        var memory = engine.Constraints.Find<MemoryLimitConstraint>();
        if (deadline is null || memory is null)
        {
            Throw.InvalidOperationException(
                "The engine does not contain the complete untrusted-code operation constraints.");
        }

        var scope = new OperationScope(engine, deadline!, memory!);
        if (!deadline.TryBeginScope(scope, MaxOperationDuration, cancellationToken))
        {
            Throw.InvalidOperationException(
                "An untrusted-code operation scope is already active for this engine.");
        }

        try
        {
            memory.Begin();
        }
        catch
        {
            deadline.EndScope(scope);
            throw;
        }

        return scope;
    }

    private static void ValidateFiniteTimeout(TimeSpan value, string paramName)
    {
        if (value <= TimeSpan.Zero || value == TimeSpan.MaxValue)
        {
            Throw.ArgumentOutOfRangeException(
                paramName,
                "The timeout must be greater than zero and less than TimeSpan.MaxValue.");
        }
    }

    private static void ValidateRange(int value, string paramName)
    {
        if (value <= 0 || value == int.MaxValue)
        {
            Throw.ArgumentOutOfRangeException(
                paramName,
                "The limit must be between one and Int32.MaxValue - 1.");
        }
    }

    private static void ValidateRange(long value, string paramName)
    {
        if (value <= 0 || value == long.MaxValue)
        {
            Throw.ArgumentOutOfRangeException(
                paramName,
                "The limit must be between one and Int64.MaxValue - 1.");
        }
    }

    private static void ValidateResultLimits(ResultLimits limits, string paramName)
    {
        if (limits is null)
        {
            Throw.ArgumentNullException(paramName);
        }

        if (limits!.MaxDepth == int.MaxValue
            || limits.MaxPropertyCount == long.MaxValue
            || limits.MaxStringLength == int.MaxValue
            || limits.MaxOutputCharacters == long.MaxValue
            || limits.MaxOutputBytes == long.MaxValue)
        {
            Throw.ArgumentOutOfRangeException(
                paramName,
                "Every result limit must be finite for untrusted code.");
        }
    }

    private sealed class OperationScope(
        Engine engine,
        OperationDeadlineConstraint deadline,
        MemoryLimitConstraint memory) : IDisposable
    {
        private int _state;

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
            {
                return;
            }

            try
            {
                using var ownership = engine.EnterHostCall();
                memory.End();
                deadline.EndScope(this);
                Volatile.Write(ref _state, 2);
            }
            catch
            {
                Volatile.Write(ref _state, 0);
                throw;
            }
        }
    }
}
