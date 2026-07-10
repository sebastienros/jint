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
/// Handler trees are shared across function INSTANCES though (per-engine definition reuse
/// for re-evaluated scripts, class members and nested declarations), so a node outlives any
/// one instance's environment: an unreachable cached env means the node moved to another
/// instance and must re-resolve against the current chain, not decline.
/// Slot layout is deterministic per AST node, so a node shared across engines via
/// <c>Prepared&lt;Script&gt;</c> resolves to the same index everywhere; the engine-identity
/// gate only avoids wasted walks against another engine's environments.
/// A cached hit is only valid when no environment between the current one and the cached
/// one owns the name: the same node can run under different chains (an eval-cache-shared
/// body under a shadowing block, a nested eval of the same source) and a sloppy direct
/// eval can inject a shadowing var into an enclosing function environment — so the hit
/// walk re-probes every intermediate declarative environment (pure) and refuses to skip
/// over an <see cref="ObjectEnvironment"/> (a with-object can gain a shadowing property
/// at any time, and probing it would be observable through proxy traps).
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

                // An intermediate environment holding the name shadows the cached resolution.
                // This happens when a node runs under a different chain than the one that
                // populated the cache (an eval-cache-shared body under a block that declares
                // the name, or a nested eval of the same source) or when a sloppy direct eval
                // injected the name into an enclosing function environment. HasBinding on a
                // declarative environment is pure, and the common case — the binding env
                // itself, matched above before any probe — pays nothing.
                if (search is DeclarativeEnvironment intermediate && intermediate.HasBinding(name.Key))
                {
                    break;
                }

                search = search._outerEnv;
            }

            // The cached location is not reachable from this chain. Handler trees are shared
            // across function instances (per-engine definition reuse for re-evaluated scripts,
            // class members, nested declarations), so the same node runs under a fresh
            // environment per instance — re-resolve against the current chain exactly as a
            // fresh node would (mirroring the cross-engine Prepared<Script> fall-through
            // below) instead of declining forever. One bounded walk per instance switch;
            // subsequent reads hit the re-populated cache.
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

        // Probe only declarative environments: their sealed HasBinding is pure (slot scan +
        // dictionary probe). An object environment's probe is observable (proxy has-traps,
        // Symbol.unscopables getters) and a with-object may shadow dynamically, and global
        // bindings are never slot-stored — reaching a non-declarative environment before the
        // binding means this node can never be safely slot-cached, without touching it.
        var record = env;
        while (record is not null)
        {
            if (record is not DeclarativeEnvironment declarativeEnvironment)
            {
                break;
            }

            if (declarativeEnvironment.HasBinding(name.Key))
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

                // dictionary-stored binding
                break;
            }

            record = record._outerEnv;
        }

        // the binding for this node can never be slot-stored; stop attempting
        _disabled = true;
        return false;
    }
}
