using System.Runtime.CompilerServices;

namespace Jint.Runtime.Environments;

/// <summary>
/// Immutable snapshot of a successful hops 1-3 slot-cache reachability walk: from
/// <see cref="Start"/>, following exactly the pinned links, the cached slot environment was
/// reached and none of the probed intermediates (<see cref="Start"/>, <see cref="Next1"/>,
/// <see cref="Next2"/>) owned the name. While every link still matches by identity and
/// <see cref="Engine._envBindingInjectionEpoch"/> is unchanged, the per-hop shadow probes are
/// provably still false and the walk can be skipped entirely.
/// </summary>
/// <remarks>
/// Validity reasoning, mirroring <c>JintIdentifierExpression.NestedChainMemo</c> (the global
/// read cache's chain memo) with a declarative terminal instead of the global environment:
/// a declarative environment's name set is a deterministic function of its defining AST
/// node/definition — every reuse channel (per-function-instance env reuse, the recursive env
/// pool, definition-level dynamic-function envs, per-node block/loop/catch env caches, the
/// per-source eval env pool) re-initializes an instance with the identical name set. The only
/// mutations that grow a PRE-EXISTING environment's name set (sloppy direct eval var/function
/// hoisting, AnnexB block-function var-scope copies) bump the injection epoch; deletions only
/// shrink it (a pinned "does not own the name" stays true), and an environment's class
/// (ObjectEnvironment vs declarative) is immutable. Chain-LINK identity is required, not just
/// the start: pooled environments are re-attached under different outers across entries, and
/// validation follows the CURRENT <c>_outerEnv</c> pointers, so a re-attached or replaced link
/// falls through to the walk. A memo is published as one immutable object through a single
/// reference field so cross-engine shared handler trees observe consistent snapshots; a memo
/// pinned by another engine always fails the start-identity check because environments are
/// per-engine. The terminal is intentionally NOT pinned: the memo's claim is only about the
/// probed intermediates, so it stays valid when the node re-resolves to a new slot env behind
/// the same intermediates.
/// </remarks>
internal sealed class SlotChainMemo
{
    internal readonly Environment Start;
    internal readonly Environment? Next1;
    internal readonly Environment? Next2;
    internal readonly int InjectionEpoch;

    internal SlotChainMemo(Environment start, Environment? next1, Environment? next2, int injectionEpoch)
    {
        Start = start;
        Next1 = next1;
        Next2 = next2;
        InjectionEpoch = injectionEpoch;
    }
}

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
/// at any time, and probing it would be observable through proxy traps). A successful walk
/// is memoized as a <see cref="SlotChainMemo"/> so steady-state closure reads validate with
/// a handful of reference compares instead of re-walking; the walk stays authoritative for
/// every miss.
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
    private SlotChainMemo? _chainMemo;

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
        // Hop 0 first: the overwhelmingly common case is that the cached env IS the current
        // lexical environment, so it gets a straight-line identity check before any loop
        // machinery and needs no shadow probes — no environment sits between the reader and
        // the binding. The engine-identity gate stays in front so a cached env from another
        // engine (handler trees shared via Prepared<Script>) is never dereferenced for slots.
        var cached = _cachedSlotEnv;
        if (cached is not null && ReferenceEquals(cached._engine, engine) && ReferenceEquals(env, cached))
        {
            slotEnv = cached;
            slotIndex = _cachedSlotIndex;
            return true;
        }

        // Permanently declined nodes (binding can never be slot-stored) also answer inline:
        // consuming lanes re-ask on every evaluation, so the miss must not pay a call.
        if (cached is null && _disabled)
        {
            slotEnv = null!;
            slotIndex = -1;
            return false;
        }

        return TryResolveNonLocal(engine, env, name, out slotEnv, out slotIndex);
    }

    /// <summary>
    /// Out-of-line tail for everything that is not a hop-0 hit or a permanent decline: the
    /// bounded hops 1-3 reachability check (chain memo, then walk), the decline check for
    /// nodes with a stale cached env, and first-time population. Kept out of
    /// <see cref="TryResolve"/> so the aggressively-inlined fast path stays small in every
    /// consuming lane.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryResolveNonLocal(
        Engine engine,
        Environment env,
        Environment.BindingName name,
        out DeclarativeEnvironment slotEnv,
        out int slotIndex)
    {
        var cached = _cachedSlotEnv;
        if (cached is not null
            && ReferenceEquals(cached._engine, engine)
            && CanReachAtOuterHop(engine, env, cached, name.Key, ref _chainMemo))
        {
            slotEnv = cached;
            slotIndex = _cachedSlotIndex;
            return true;
        }

        // The cached location is not reachable from this chain. Handler trees are shared
        // across function instances (per-engine definition reuse for re-evaluated scripts,
        // class members, nested declarations), so the same node runs under a fresh
        // environment per instance — re-resolve against the current chain exactly as a
        // fresh node would (mirroring the cross-engine Prepared<Script> fall-through
        // below) instead of declining forever. One bounded walk per instance switch;
        // subsequent reads hit the re-populated cache.

        if (_disabled)
        {
            slotEnv = null!;
            slotIndex = -1;
            return false;
        }

        return ResolveAndPopulate(engine, env, name, out slotEnv, out slotIndex);
    }

    /// <summary>
    /// Bounded hops 1-3 reachability check for a slot-cache probe whose caller has already
    /// rejected hop 0 (<paramref name="env"/> itself is not <paramref name="cached"/>).
    /// Because hop 0 failed, the current environment sits BETWEEN the reader and the cached
    /// env and must be probed like any other intermediate before hopping outward. A valid
    /// <see cref="SlotChainMemo"/> answers with reference compares alone (see its remarks for
    /// why identity + epoch freeze the probe results); everything else takes the walk, which
    /// re-publishes the memo on success.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool CanReachAtOuterHop(Engine engine, Environment env, DeclarativeEnvironment cached, Key key, ref SlotChainMemo? memoSlot)
    {
        var memo = memoSlot;
        if (memo is not null
            && ReferenceEquals(env, memo.Start)
            && engine._envBindingInjectionEpoch == memo.InjectionEpoch)
        {
            // Re-derive the chain from the CURRENT _outerEnv pointers against the pinned
            // links: identity per link leaves no room for an inserted environment, and a
            // pooled link re-attached under a different outer fails here and re-walks.
            var next = env._outerEnv;
            if (memo.Next1 is null
                ? ReferenceEquals(next, cached)
                : ReferenceEquals(next, memo.Next1)
                  && (memo.Next2 is null
                      ? ReferenceEquals(memo.Next1._outerEnv, cached)
                      : ReferenceEquals(memo.Next1._outerEnv, memo.Next2)
                        && ReferenceEquals(memo.Next2._outerEnv, cached)))
            {
                return true;
            }
        }

        return WalkAndMemoize(engine, env, cached, key, ref memoSlot);
    }

    /// <summary>
    /// The authoritative bounded walk: probes every intermediate declarative environment
    /// (pure <see cref="DeclarativeEnvironment.HasBinding(Key)"/>) and refuses to cross an
    /// <see cref="ObjectEnvironment"/>. On success the probed intermediates are published as
    /// a <see cref="SlotChainMemo"/> so subsequent reads on the same chain skip the probes.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool WalkAndMemoize(Engine engine, Environment env, DeclarativeEnvironment cached, Key key, ref SlotChainMemo? memoSlot)
    {
        var search = env;
        Environment? next1 = null;
        Environment? next2 = null;
        for (var hops = 1; hops < MaxChainDepth; hops++)
        {
            if (search is ObjectEnvironment)
            {
                // a with-object between us and the cached slot may shadow the name dynamically
                return false;
            }

            // An intermediate environment holding the name shadows the cached resolution.
            // This happens when a node runs under a different chain than the one that
            // populated the cache (an eval-cache-shared body under a block that declares
            // the name, or a nested eval of the same source) or when a sloppy direct eval
            // injected the name into an enclosing function environment. HasBinding on a
            // declarative environment is pure, and the common case — a hop-0 hit on the
            // binding env itself — never enters this walk at all.
            if (search is DeclarativeEnvironment intermediate && intermediate.HasBinding(key))
            {
                return false;
            }

            search = search._outerEnv;
            if (search is null)
            {
                return false;
            }

            if (ReferenceEquals(search, cached))
            {
                memoSlot = new SlotChainMemo(env, next1, next2, engine._envBindingInjectionEpoch);
                return true;
            }

            if (hops == 1)
            {
                next1 = search;
            }
            else
            {
                next2 = search;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool ResolveAndPopulate(
        Engine engine,
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
        Environment? next1 = null;
        Environment? next2 = null;
        var depth = 0;
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
                    if ((uint) (depth - 1) < MaxChainDepth - 1)
                    {
                        // Found at hops 1-3: every traversed environment was declarative and
                        // did not own the name — exactly the walk's success condition, so the
                        // memo can be published without a second walk on the next read.
                        _chainMemo = new SlotChainMemo(env, next1, next2, engine._envBindingInjectionEpoch);
                    }
                    slotEnv = declarativeEnvironment;
                    slotIndex = index;
                    return true;
                }

                // dictionary-stored binding
                break;
            }

            if (depth == 1)
            {
                next1 = record;
            }
            else if (depth == 2)
            {
                next2 = record;
            }

            record = record._outerEnv;
            depth++;
        }

        // the binding for this node can never be slot-stored; stop attempting
        _disabled = true;
        return false;
    }
}
