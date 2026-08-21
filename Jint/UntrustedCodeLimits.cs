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
/// This type deliberately has no defaults. Limits for untrusted code depend on the request and workload,
/// so the host must choose every value. Values that the ordinary constraint helpers interpret as
/// "unlimited" are rejected instead of silently disabling a protection.
/// </para>
/// <para>
/// <see cref="TimeoutInterval"/>, <see cref="MaxStatements"/> and <see cref="MemoryLimit"/> apply to one
/// top-level engine entry. <see cref="MaxOperationDuration"/> spans a host operation containing several
/// entries, but only while the host brackets that operation with <see cref="BeginOperation"/>.
/// </para>
/// </remarks>
public sealed class UntrustedCodeLimits
{
    /// <summary>
    /// Creates a complete set of finite limits for untrusted code.
    /// </summary>
    /// <param name="timeoutInterval">Maximum wall-clock time for one engine entry.</param>
    /// <param name="maxStatements">Maximum statements executed by one engine entry.</param>
    /// <param name="memoryLimit">Maximum bytes allocated by one engine entry.</param>
    /// <param name="maxRecursionDepth">
    /// Maximum recursion depth. Zero forbids recursion; unlike <see cref="OptionsExtensions.LimitRecursion"/>,
    /// negative and saturated values are rejected rather than treated as unlimited or effectively unlimited.
    /// </param>
    /// <param name="maxArraySize">Maximum JavaScript array size.</param>
    /// <param name="regexTimeout">
    /// Maximum time one regular expression operation may run. A prepared script or module carries the timeout
    /// from its own parsing options, so prepare untrusted code with the same value.
    /// </param>
    /// <param name="promiseTimeout">Maximum time the engine may wait for a promise to settle.</param>
    /// <param name="maxOperationDuration">
    /// Maximum wall-clock time for a host operation containing one or more engine entries. The host must
    /// activate this limit with <see cref="BeginOperation"/> around each operation.
    /// </param>
    /// <param name="maxSourceLength">Maximum UTF-16 source length for every engine-owned parse.</param>
    /// <param name="maxNodeCount">Maximum AST nodes for every engine-owned parse.</param>
    /// <param name="maxModuleCount">Maximum distinct module records registered by one engine.</param>
    /// <param name="maxTotalModuleSourceBytes">Maximum aggregate module source bytes registered by one engine.</param>
    /// <param name="maxModuleGraphDepth">Maximum graph depth for one top-level module load.</param>
    /// <param name="maxModuleResolutionHops">Maximum resolver calls for one top-level module load.</param>
    /// <param name="resultLimits">Bounds conversion, JSON serialization, and error rendering.</param>
    public UntrustedCodeLimits(
        TimeSpan timeoutInterval,
        int maxStatements,
        long memoryLimit,
        int maxRecursionDepth,
        uint maxArraySize,
        TimeSpan regexTimeout,
        TimeSpan promiseTimeout,
        TimeSpan maxOperationDuration,
        int maxSourceLength = 1_000_000,
        int maxNodeCount = 250_000,
        int maxModuleCount = 100,
        long maxTotalModuleSourceBytes = 10_000_000,
        int maxModuleGraphDepth = 32,
        int maxModuleResolutionHops = 1_000,
        ResultLimits? resultLimits = null)
    {
        ValidateFiniteTimeout(timeoutInterval, nameof(timeoutInterval));
        ValidateRange(maxStatements, nameof(maxStatements));
        ValidateRange(memoryLimit, nameof(memoryLimit));

        if (maxRecursionDepth < 0 || maxRecursionDepth == int.MaxValue)
        {
            Throw.ArgumentOutOfRangeException(
                nameof(maxRecursionDepth),
                "The recursion depth must be between zero and Int32.MaxValue - 1.");
        }

        if (maxArraySize == 0 || maxArraySize == uint.MaxValue)
        {
            Throw.ArgumentOutOfRangeException(
                nameof(maxArraySize),
                "The array size must be between one and UInt32.MaxValue - 1.");
        }

        ValidateFiniteTimeout(regexTimeout, nameof(regexTimeout));
        ValidateFiniteTimeout(promiseTimeout, nameof(promiseTimeout));
        ValidateFiniteTimeout(maxOperationDuration, nameof(maxOperationDuration));
        ValidateRange(maxSourceLength, nameof(maxSourceLength));
        ValidateRange(maxNodeCount, nameof(maxNodeCount));
        ValidateRange(maxModuleCount, nameof(maxModuleCount));
        ValidateRange(maxTotalModuleSourceBytes, nameof(maxTotalModuleSourceBytes));
        ValidateRange(maxModuleGraphDepth, nameof(maxModuleGraphDepth));
        ValidateRange(maxModuleResolutionHops, nameof(maxModuleResolutionHops));
        resultLimits ??= ResultLimits.Conservative;
        ValidateResultLimits(resultLimits, nameof(resultLimits));

        TimeoutInterval = timeoutInterval;
        MaxStatements = maxStatements;
        MemoryLimit = memoryLimit;
        MaxRecursionDepth = maxRecursionDepth;
        MaxArraySize = maxArraySize;
        RegexTimeout = regexTimeout;
        PromiseTimeout = promiseTimeout;
        MaxOperationDuration = maxOperationDuration;
        MaxSourceLength = maxSourceLength;
        MaxNodeCount = maxNodeCount;
        MaxModuleCount = maxModuleCount;
        MaxTotalModuleSourceBytes = maxTotalModuleSourceBytes;
        MaxModuleGraphDepth = maxModuleGraphDepth;
        MaxModuleResolutionHops = maxModuleResolutionHops;
        ResultLimits = resultLimits;
    }

    /// <summary>
    /// Maximum wall-clock time for one engine entry.
    /// </summary>
    public TimeSpan TimeoutInterval { get; }

    /// <summary>
    /// Maximum statements executed by one engine entry.
    /// </summary>
    public int MaxStatements { get; }

    /// <summary>
    /// Maximum bytes allocated by one engine entry.
    /// </summary>
    public long MemoryLimit { get; }

    /// <summary>
    /// Maximum recursion depth. Zero forbids recursion.
    /// </summary>
    public int MaxRecursionDepth { get; }

    /// <summary>
    /// Maximum JavaScript array size.
    /// </summary>
    public uint MaxArraySize { get; }

    /// <summary>
    /// Maximum time one regular expression operation may run.
    /// </summary>
    /// <remarks>
    /// A prepared script or module carries the timeout selected when it was prepared; set its parsing options
    /// to this value as well.
    /// </remarks>
    public TimeSpan RegexTimeout { get; }

    /// <summary>
    /// Maximum time the engine may wait for a promise to settle.
    /// </summary>
    public TimeSpan PromiseTimeout { get; }

    /// <summary>
    /// Maximum wall-clock time for a host operation containing one or more engine entries.
    /// </summary>
    public TimeSpan MaxOperationDuration { get; }

    /// <summary>Maximum UTF-16 source length for every engine-owned parse.</summary>
    public int MaxSourceLength { get; }

    /// <summary>Maximum AST nodes for every engine-owned parse.</summary>
    public int MaxNodeCount { get; }

    /// <summary>Maximum distinct module records registered by one engine.</summary>
    public int MaxModuleCount { get; }

    /// <summary>Maximum aggregate module source bytes registered by one engine.</summary>
    public long MaxTotalModuleSourceBytes { get; }

    /// <summary>Maximum conservative graph depth for one top-level module load.</summary>
    public int MaxModuleGraphDepth { get; }

    /// <summary>Maximum resolver calls for one top-level module load.</summary>
    public int MaxModuleResolutionHops { get; }

    /// <summary>Bounds result conversion, JSON serialization, and error rendering.</summary>
    public ResultLimits ResultLimits { get; }

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
        if (limits.MaxDepth == int.MaxValue
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
