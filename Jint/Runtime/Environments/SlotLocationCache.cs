using System.Runtime.CompilerServices;

namespace Jint.Runtime.Environments;

/// <summary>
/// Per-AST-node cache of a binding's slot location (environment + slot index), shared by the
/// identifier read fast path and the numeric read-modify-write discard fast paths.
/// </summary>
/// <remarks>
/// Validity reasoning: closure-captured environments cannot be pooled (escape detection
/// prevents it), so a cached env reference is stable for the lifetime of the function
/// instance that captured it, and slot indices are immutable for the lifetime of the env.
/// Slot layout is deterministic per AST node, so a node shared across engines via
/// <c>Prepared&lt;Script&gt;</c> resolves to the same index everywhere; the engine-identity
/// gate only avoids wasted walks against another engine's environments.
/// The chain walk refuses to skip over an <see cref="ObjectEnvironment"/> (a sloppy-mode
/// <c>with</c> block): a with-object can gain a shadowing property at any time, so only the
/// full resolution path can decide who owns the binding.
/// Nodes whose binding can never be slot-stored (global/object/dictionary environments)
/// disable themselves permanently so failed attempts don't re-walk the chain.
/// </remarks>
internal struct SlotLocationCache
{
    // Bounded walk: chain depth is typically 1-3 in real code; deeper chains fall through.
    internal const int MaxChainDepth = 4;

    private DeclarativeEnvironment? _cachedSlotEnv;
    private int _cachedSlotIndex;
    private bool _disabled;

    /// <summary>
    /// Resolves the slot location for <paramref name="name"/> against the current lexical
    /// environment, using and maintaining the cache. Returns false when the binding is not
    /// slot-stored, the cached location is not safely reachable, or resolution must go
    /// through the full materialized path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryResolve(
        Engine engine,
        Environment env,
        Environment.BindingName name,
        out DeclarativeEnvironment slotEnv,
        out int slotIndex)
    {
        var cached = _cachedSlotEnv;
        if (cached is not null && ReferenceEquals(cached._engine, engine))
        {
            var search = env;
            for (var hops = 0; hops < MaxChainDepth && search is not null; hops++)
            {
                if (ReferenceEquals(search, cached))
                {
                    slotEnv = cached;
                    slotIndex = _cachedSlotIndex;
                    return true;
                }

                if (search is ObjectEnvironment)
                {
                    // a with-object between us and the cached slot may shadow the name dynamically
                    break;
                }

                search = search._outerEnv;
            }

            slotEnv = null!;
            slotIndex = -1;
            return false;
        }

        if (_disabled)
        {
            slotEnv = null!;
            slotIndex = -1;
            return false;
        }

        return ResolveAndPopulate(env, name, out slotEnv, out slotIndex);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool ResolveAndPopulate(
        Environment env,
        Environment.BindingName name,
        out DeclarativeEnvironment slotEnv,
        out int slotIndex)
    {
        slotEnv = null!;
        slotIndex = -1;

        if (!JintEnvironment.TryGetIdentifierEnvironmentWithBinding(env, name, out var record))
        {
            // unresolvable; the full path produces the proper error
            return false;
        }

        if (record is DeclarativeEnvironment declarativeEnvironment)
        {
            var index = declarativeEnvironment.FindSlotIndex(name.Key);
            if (index >= 0)
            {
                _cachedSlotEnv = declarativeEnvironment;
                _cachedSlotIndex = index;
                slotEnv = declarativeEnvironment;
                slotIndex = index;
                return true;
            }
        }

        // the binding for this node can never be slot-stored; stop attempting
        _disabled = true;
        return false;
    }
}
