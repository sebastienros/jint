namespace Jint.Runtime.Environments;

/// <summary>
/// Reuse pool for one <see cref="Native.Function.ScriptFunction"/> instance's call environments and
/// fixed-slot arrays.
/// </summary>
/// <remarks>
/// Held by the function instance, not by the shared <see cref="Interpreter.JintFunctionDefinition.State"/>:
/// a prepared script's State is shared across every engine that runs it, so pooling call environments on the
/// State kept the last engine that called each function alive (issue #2560). A function instance is created
/// per engine, so this pool is inherently per-engine and dies with it. And because a function instance — like
/// its engine — is only ever used from a single thread at a time, no synchronization is needed here, unlike
/// the old shared-State pool which required <c>Interlocked</c> for the parallel fixtures that share a cached
/// State.
/// </remarks>
internal sealed class FunctionEnvPool
{
    /// <summary>
    /// Slot array reused by the next call to the function.
    /// </summary>
    internal Binding[]? CachedSlots;

    /// <summary>
    /// Single environment reused by the next non-recursive call to the function.
    /// </summary>
    internal FunctionEnvironment? CachedEnv;

    // Bounded best-effort pool for direct-recursive activations. The single CachedEnv slot can only hold the
    // topmost frame, so each simultaneously live recursive frame rents a distinct env (with its cleared
    // fixed-slot Binding[] still attached). Bounded so deep recursion cannot retain an unbounded number of envs.
    private const int RecursiveEnvPoolSize = 16;
    private FunctionEnvironment?[]? _recursiveEnvPool;
    private int _recursiveEnvPoolCount;

    internal FunctionEnvironment? TryRentRecursiveEnv()
    {
        if (_recursiveEnvPoolCount == 0)
        {
            return null;
        }

        var pool = _recursiveEnvPool!;
        for (var i = 0; i < pool.Length; i++)
        {
            var env = pool[i];
            if (env is not null)
            {
                pool[i] = null;
                _recursiveEnvPoolCount--;
                return env;
            }
        }

        return null;
    }

    internal void ReturnRecursiveEnv(FunctionEnvironment env)
    {
        // Drop cheaply when the pool is already full — otherwise a deep recursion (depth >> pool size)
        // would scan every slot only to fail on each return during the unwind.
        if (_recursiveEnvPoolCount >= RecursiveEnvPoolSize)
        {
            return;
        }

        var pool = _recursiveEnvPool ??= new FunctionEnvironment?[RecursiveEnvPoolSize];
        for (var i = 0; i < pool.Length; i++)
        {
            if (pool[i] is null)
            {
                pool[i] = env;
                _recursiveEnvPoolCount++;
                return;
            }
        }
    }
}
