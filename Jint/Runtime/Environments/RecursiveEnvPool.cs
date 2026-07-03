namespace Jint.Runtime.Environments;

/// <summary>
/// Bounded reuse pool for one direct-recursive <see cref="Native.Function.ScriptFunction"/> instance's
/// call environments, stored in the function's <c>_envReuse</c> slot.
/// </summary>
/// <remarks>
/// The single-env reuse slot cannot serve recursion — only the topmost frame would ever be reusable — so
/// each simultaneously live recursive frame rents a distinct env (with its cleared fixed-slot Binding[]
/// still attached). Bounded so deep recursion cannot retain an unbounded number of environments.
/// Held by the function instance, not by the shared <see cref="Interpreter.JintFunctionDefinition.State"/>:
/// an environment roots its creating engine, and a prepared script's State is shared by every engine that
/// runs it, so pooling environments on the State kept the last-caller engine alive (issue #2560). A function
/// instance is created per engine and — like its engine — is only ever used from a single thread at a time,
/// so no synchronization is needed here, unlike the old shared-State pool.
/// </remarks>
internal sealed class RecursiveEnvPool
{
    private const int PoolSize = 16;
    private readonly FunctionEnvironment?[] _pool = new FunctionEnvironment?[PoolSize];
    private int _count;

    // Single-threaded (see remarks), so a plain LIFO stack pointer suffices — no slot scanning
    // like the old lock-free shared-State pool needed.
    internal FunctionEnvironment? TryRent()
    {
        if (_count == 0)
        {
            return null;
        }

        var env = _pool[--_count];
        _pool[_count] = null;
        return env;
    }

    internal void Return(FunctionEnvironment env)
    {
        // Drop when full so deep recursion (depth >> pool size) cannot retain unbounded environments.
        if (_count < PoolSize)
        {
            _pool[_count++] = env;
        }
    }
}
