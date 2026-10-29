using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintIdentifierExpression : JintExpression
{
    private readonly Environment.BindingName _identifier;
    private Environment? _cachedEnvironment;
    private bool _cachedStrict;

    // Slot-binding location cache: when an identifier resolves to a slot in some
    // (possibly outer) DeclarativeEnvironment, remember the env reference and slot
    // index so subsequent reads can skip the env-chain walk and SlotIndexOf scan.
    // Validity: walk from the current execution env up looking for _cachedSlotEnv via
    // ReferenceEquals — closure-captured envs cannot be pooled (escape-detection prevents it),
    // so their identity is stable for the lifetime of the function instance that captured them.
    // Strict-mode is intentionally not validated: a single AST node's strictness is fixed by
    // its enclosing scope at parse time, so it cannot change between calls.
    private DeclarativeEnvironment? _cachedSlotEnv;
    private int _cachedSlotIndex = -1;

    // Chain memo for the hops 1-3 slot reads (closure reads of outer bindings): pins the exact
    // chain links a successful reachability walk probed, so steady-state loop reads validate
    // with reference compares + the injection epoch instead of re-probing every intermediate.
    // See SlotChainMemo for the soundness argument; the walk stays authoritative for misses.
    private SlotChainMemo? _slotChainMemo;

    // Layout-described location of the same slot binding (see ScopePath): validated by the layout of each
    // environment on the chain instead of its identity, so a fresh activation of the function this node
    // runs in — or a fresh iteration of its loop — still hits, where _cachedSlotEnv and _slotChainMemo
    // describe the previous one and miss.
    private ScopePath? _scopePath;

    // Module-binding location cache: a read that resolves to a module environment (a module's own top-level
    // binding, or an import followed to the module that owns the value) remembers where the value is stored,
    // and how the chain reaches that environment (a ScopePath), so a read from a function nested in the module
    // neither walks the chain nor hashes the name — and an import does not resolve it twice — on each
    // evaluation. A module environment's name set is fixed once it is linked, which is what makes the stored
    // entry stable. Published as one immutable object so a torn read is impossible.
    private ModuleBindingLocation? _moduleBinding;

    private sealed class ModuleBindingLocation
    {
        internal readonly ScopePath Path;
        internal readonly Collections.StringDictionarySlim<Binding> Bindings;
        internal readonly int Entry;
        internal readonly Key Key;

        internal ModuleBindingLocation(ScopePath path, Collections.StringDictionarySlim<Binding> bindings, int entry, Key key)
        {
            Path = path;
            Bindings = bindings;
            Entry = entry;
            Key = key;
        }
    }

    // Version-based inline cache for global bindings: when resolution (from any scope depth)
    // lands on a plain writable data property of the real GlobalObject, remember the
    // descriptor. Valid while the global object's own-property shape and the set of global
    // lexical declarations are unchanged AND a bounded walk from the current env reaches the
    // global env without an intermediate that could own the name — value writes mutate the
    // descriptor in place and bump neither version. Mirrors JintMemberExpression's read cache.
    internal GlobalEnvironment? _cachedGlobalEnv;
    private Runtime.Descriptors.PropertyDescriptor? _cachedGlobalDescriptor;
    private uint _cachedGlobalShapeVersion;
    private int _cachedGlobalLexicalVersion;
    private NestedChainMemo? _nestedChainMemo;

    // Bounded walk: chain depth is typically 1-3 in real code; deeper chains fall through.
    private const int MaxSlotCacheChainDepth = 4;

    /// <summary>
    /// Memo of a successful nested-scope global walk: from the pinned start environment,
    /// following the pinned intermediate links, the walk reached the cached GlobalEnvironment
    /// with no intermediate owning the name. Chain-LINK identity is required, not just the
    /// start: pooled loop environments are re-attached under different outers across entries,
    /// so the same start instance can sit on a different chain. Post-creation binding injection
    /// into a pinned intermediate (sloppy direct eval hoisting, AnnexB function copies) is the
    /// one mutation identity cannot see — covered by <see cref="Engine._envBindingInjectionEpoch"/>.
    /// Instances are immutable and published through a single reference field so cross-engine
    /// shared handler trees observe consistent snapshots; a mismatch falls through to the walk
    /// and re-publishes (never a permanent decline).
    /// </summary>
    private sealed class NestedChainMemo
    {
        internal readonly Environment Start;
        internal readonly Environment? Next1;
        internal readonly Environment? Next2;
        internal readonly Environment? Next3;
        internal readonly int InjectionEpoch;

        internal NestedChainMemo(Environment start, Environment? next1, Environment? next2, Environment? next3, int injectionEpoch)
        {
            Start = start;
            Next1 = next1;
            Next2 = next2;
            Next3 = next3;
            InjectionEpoch = injectionEpoch;
        }
    }

    public JintIdentifierExpression(Identifier expression) : base(expression)
    {
        _identifier = expression.UserData as Environment.BindingName
            ?? new Environment.BindingName(expression.Name);
    }

    // Separate slot-location cache for the unboxed numeric read (WS-5); kept apart from the
    // GetValue caches above so it never perturbs the materializing read path.
    private SlotLocationCache _numberSlotCache;

    public Environment.BindingName Identifier => _identifier;

    /// <summary>
    /// Reads this identifier as a raw double when it resolves to a slot-stored number binding,
    /// without materializing a JsNumber. Returns false for everything else (binding not slot-stored,
    /// not a number, uninitialized) so the caller falls back to the boxed path before any side effect.
    /// </summary>
    internal bool TryReadNumber(EvaluationContext context, out double value)
    {
        var engine = context.Engine;
        if (_numberSlotCache.TryResolve(engine, engine.ExecutionContext.LexicalEnvironment, _identifier, out var slotEnv, out var slotIndex))
        {
            return slotEnv.TryGetNumberSlot(slotIndex, out value);
        }

        value = 0;
        return false;
    }

    public bool HasEvalOrArguments
    {
        get
        {
            var key = _identifier.Key;
            return key == KnownKeys.Eval || key == KnownKeys.Arguments;
        }
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        var engine = context.Engine;
        ref readonly var executionContext = ref engine.ExecutionContext;
        var env = executionContext.LexicalEnvironment;
        var strict = executionContext.Strict;

        if (ReferenceEquals(env, _cachedEnvironment)
            && _cachedStrict == strict
            && env.HasBinding(_identifier))
        {
            return engine._referencePool.Rent(env, _identifier.Value, strict, thisValue: null);
        }

        if (!JintEnvironment.TryGetIdentifierEnvironmentWithBinding(env, _identifier, out var identifierEnvironment))
        {
            // Binding not found - create unresolvable reference
            return engine._referencePool.Rent(Reference.Unresolvable, _identifier.Value, strict, thisValue: null);
        }

        if (ReferenceEquals(identifierEnvironment, env))
        {
            _cachedEnvironment = env;
            _cachedStrict = strict;
        }
        else
        {
            _cachedEnvironment = null;
        }

        return engine._referencePool.Rent(identifierEnvironment, _identifier.Value, strict, thisValue: null);
    }

    public override JsValue GetValue(EvaluationContext context)
    {
        // need to notify correct node when taking shortcut
        context.LastSyntaxElement = _expression;

        var identifier = Identifier;
        if (identifier.CalculatedValue is not null)
        {
            return identifier.CalculatedValue;
        }

        var engine = context.Engine;
        ref readonly var executionContext = ref engine.ExecutionContext;
        var env = executionContext.LexicalEnvironment;
        var strict = executionContext.Strict;
        JsValue? value;

        // Slot-cache fast path, hop 0 first: the overwhelmingly common case is that the
        // cached resolving env IS the current lexical environment, so it gets a straight-line
        // identity check before any loop machinery and needs no shadow probes — no environment
        // sits between the reader and the binding. Reads at hops 1-3 (closure reads of outer
        // bindings) take the out-of-line bounded reachability check: a pinned chain memo
        // answers with reference compares, everything else re-walks with per-intermediate
        // shadow probes and refuses to cross an ObjectEnvironment; see SlotLocationCache and
        // SlotChainMemo for why those probes (and the memo's identity + epoch guard) are
        // load-bearing.
        // Engine-identity gate: a Prepared<Script> reused across multiple Engine instances
        // shares JintIdentifierExpression nodes, so the cached env may be from a previous
        // Engine and must not be dereferenced for slots. Gating up front also skips the walk —
        // significant on harnesses (DromaeoBenchmark, SunSpiderBenchmark) that create a
        // new Engine per iteration.
        var cachedSlotEnv = _cachedSlotEnv;
        if (cachedSlotEnv is not null && ReferenceEquals(cachedSlotEnv._engine, engine))
        {
            if (ReferenceEquals(env, cachedSlotEnv)
                || SlotLocationCache.CanReachAtOuterHop(engine, env, cachedSlotEnv, identifier.Key, ref _slotChainMemo))
            {
                var slots = cachedSlotEnv._slots;
                var idx = _cachedSlotIndex;
                if (slots is not null && (uint) idx < (uint) slots.Length)
                {
                    // PeekValue rather than Value: an owned string is marked by HandOn below, which tests the
                    // value's _type bit in place of the arguments type test this lane always made.
                    ref var binding = ref slots[idx];
                    value = binding.HasReferenceValue
                        ? binding.PeekValue
                        : DeclarativeEnvironment.MaterializeUnboxedOrNull(ref binding);
                    if (value is null)
                    {
                        ThrowNotInitialized(engine);
                    }
                    return HandOn(value);
                }
                // stale slot metadata for a reachable env — fall through to full resolution
            }
        }

        if (_moduleBinding is not null && TryReadModuleBinding(engine, env, out value))
        {
            return value;
        }

        if (_scopePath is { } scopePath && scopePath.Resolve(engine, env) is { _slots: { } pathSlots }
            && (uint) scopePath.SlotIndex < (uint) pathSlots.Length)
        {
            ref var binding = ref pathSlots[scopePath.SlotIndex];
            value = binding.HasReferenceValue
                ? binding.Value
                : DeclarativeEnvironment.MaterializeUnboxedOrNull(ref binding);
            if (value is null)
            {
                ThrowNotInitialized(engine);
            }
            return MaterializeIfArguments(value);
        }


        // Global-binding fast path: top-level identifier reads hit the cached descriptor
        // directly, skipping the property dictionary lookup entirely. The null pre-check keeps
        // the cost for non-global identifiers to a single field test; the out-of-line validator
        // keeps this method's code size unaffected.
        if (_cachedGlobalEnv is not null)
        {
            var cachedGlobalDescriptor = TryGetValidatedGlobalDescriptor(engine, env);
            if (cachedGlobalDescriptor is not null)
            {
                value = cachedGlobalDescriptor._value;
                if (value is null)
                {
                    ThrowNotInitialized(engine);
                }
                return HandOn(value);
            }
        }

        if (ReferenceEquals(env, _cachedEnvironment)
            && _cachedStrict == strict
            && env.TryGetBinding(identifier, strict, out value))
        {
            if (value is null)
            {
                ThrowNotInitialized(engine);
            }
        }
        else if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                     env,
                     identifier,
                     strict,
                     out var identifierEnvironment,
                     out value))
        {
            if (ReferenceEquals(identifierEnvironment, env))
            {
                _cachedEnvironment = env;
                _cachedStrict = strict;
            }
            else
            {
                _cachedEnvironment = null;
            }

            // Populate slot-binding cache when the resolved env stores this binding in a fixed slot.
            // Closure-captured envs cannot be pooled (escape detection prevents it), so caching the
            // env reference is safe; slot indices are immutable for the lifetime of the env.
            if (identifierEnvironment is DeclarativeEnvironment denv)
            {
                var slotIndex = denv.FindSlotIndex(identifier.Key);
                if (slotIndex >= 0)
                {
                    _cachedSlotEnv = denv;
                    _cachedSlotIndex = slotIndex;
                    _scopePath = ScopePath.TryCreate(engine, env, denv, identifier.Key, slotIndex);
                }
            }
            else if (identifierEnvironment is GlobalEnvironment globalEnv)
            {
                // No hop restriction: the validator re-walks with shadow probes on every read,
                // so nested-scope reads of globals (loop bodies, closures) can use the cache.
                TryRememberGlobalBinding(globalEnv);
            }

            if (identifierEnvironment is ModuleEnvironment moduleEnv)
            {
                RememberModuleBinding(engine, env, moduleEnv);
            }

            if (value is null)
            {
                ThrowNotInitialized(engine);
            }
        }
        else
        {
            var reference = engine._referencePool.Rent(Reference.Unresolvable, identifier.Value, strict, thisValue: null);
            value = engine.GetValue(reference, returnReferenceToPool: true);
        }

        return HandOn(value);
    }

    /// <summary>
    /// The module-binding cache's read. A binding still in its temporal dead zone (no value yet) is left to the
    /// full path, which owns the error and its message.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadModuleBinding(Engine engine, Environment env, [NotNullWhen(true)] out JsValue? value)
    {
        var location = _moduleBinding!;
        if (location.Path.Reaches(engine, env))
        {
            ref var binding = ref location.Bindings.GetValueRefAtEntry(location.Entry, location.Key);
            if (!Unsafe.IsNullRef(ref binding) && binding.HasReferenceValue && binding.Value is { } found)
            {
                value = found;
                return true;
            }
        }

        value = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RememberModuleBinding(Engine engine, Environment env, ModuleEnvironment moduleEnv)
    {
        if (moduleEnv.TryGetBindingLocation(Identifier.Key, out var bindings, out var entry, out var key)
            && ScopePath.TryCreateToEnvironment(engine, env, moduleEnv, Identifier.Key) is { } path)
        {
            _moduleBinding = new ModuleBindingLocation(path, bindings, entry, key);
        }
    }

    /// <summary>
    /// Validates the global-binding cache for the current environment: neither the global
    /// object's own-property shape nor the global lexical declaration set may have changed,
    /// and a bounded walk from the current lexical env must reach the cached GlobalEnvironment
    /// without passing an environment that could own the name (mirrors the slot-cache walk:
    /// with-objects and declarative envs holding the binding — e.g. sloppy direct eval injected
    /// a var — bail to full resolution; the per-read probes are what make nested-scope use
    /// safe). Returns the cached plain writable data descriptor, or null on miss.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal Runtime.Descriptors.PropertyDescriptor? TryGetValidatedGlobalDescriptor(Engine engine, Environment env)
    {
        var cachedGlobalEnv = _cachedGlobalEnv;
        if (cachedGlobalEnv is null)
        {
            return null;
        }

        // Hop 0 (top-level code) keeps the single identity compare up front and this method
        // compact — it is by far the most common case (var-heavy scripts validate here many
        // times per loop iteration) and must not pay for the nested-scope walk's code size.
        if (ReferenceEquals(env, cachedGlobalEnv))
        {
            return ReferenceEquals(cachedGlobalEnv._engine, engine)
                   && cachedGlobalEnv._global._propertiesVersion == _cachedGlobalShapeVersion
                   && cachedGlobalEnv._lexicalMutations == _cachedGlobalLexicalVersion
                ? _cachedGlobalDescriptor
                : null;
        }

        return TryGetValidatedGlobalDescriptorNested(engine, env, cachedGlobalEnv);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Runtime.Descriptors.PropertyDescriptor? TryGetValidatedGlobalDescriptorNested(Engine engine, Environment env, GlobalEnvironment cachedGlobalEnv)
    {
        if (!ReferenceEquals(cachedGlobalEnv._engine, engine)
            || cachedGlobalEnv._global._propertiesVersion != _cachedGlobalShapeVersion
            || cachedGlobalEnv._lexicalMutations != _cachedGlobalLexicalVersion)
        {
            return null;
        }

        // Chain-shape memo: when the exact pinned chain (every link by identity) still connects
        // the current env to the global env and no binding has been injected into a pre-existing
        // environment since (epoch), the per-hop shadow probes of the walk below are provably
        // still false — the name sets of pinned envs are otherwise immutable.
        var memo = _nestedChainMemo;
        if (memo is not null
            && ReferenceEquals(env, memo.Start)
            && engine._envBindingInjectionEpoch == memo.InjectionEpoch)
        {
            var next = env._outerEnv;
            if (memo.Next1 is null
                ? ReferenceEquals(next, cachedGlobalEnv)
                : ReferenceEquals(next, memo.Next1)
                  && (memo.Next2 is null
                      ? ReferenceEquals(memo.Next1._outerEnv, cachedGlobalEnv)
                      : ReferenceEquals(memo.Next1._outerEnv, memo.Next2)
                        && (memo.Next3 is null
                            ? ReferenceEquals(memo.Next2._outerEnv, cachedGlobalEnv)
                            : ReferenceEquals(memo.Next2._outerEnv, memo.Next3)
                              && ReferenceEquals(memo.Next3._outerEnv, cachedGlobalEnv))))
            {
                return _cachedGlobalDescriptor;
            }
        }

        var key = _identifier.Key;
        var search = env;
        Environment? next1 = null, next2 = null, next3 = null;
        for (var hops = 0; hops < MaxSlotCacheChainDepth; hops++)
        {
            if (search is ObjectEnvironment)
            {
                return null;
            }

            if (search is DeclarativeEnvironment intermediate && intermediate.HasBinding(key))
            {
                return null;
            }

            search = search._outerEnv;
            if (search is null)
            {
                return null;
            }

            if (ReferenceEquals(search, cachedGlobalEnv))
            {
                _nestedChainMemo = new NestedChainMemo(env, next1, next2, next3, engine._envBindingInjectionEpoch);
                return _cachedGlobalDescriptor;
            }

            if (hops == 0)
            {
                next1 = search;
            }
            else if (hops == 1)
            {
                next2 = search;
            }
            else
            {
                next3 = search;
            }
        }

        return null;
    }

    /// <summary>
    /// Caches the resolved global binding when it is a plain writable data property on the
    /// real GlobalObject (no CustomValue indirection — materialized lazy built-ins qualify,
    /// live accessor-like descriptors never do) and no lexical declaration shadows it. Reads
    /// may then use the descriptor directly while the versions hold; writers must re-check
    /// <see cref="Runtime.Descriptors.PropertyDescriptor.Writable"/> through the cached
    /// reference because defineProperty flips flags in place without bumping the versions.
    /// Re-probing the global's own storage here is also what keeps a name resolved from the
    /// global's prototype chain out of the cache: a prototype mutation bumps no global version,
    /// so caching such a descriptor would serve stale values.
    /// </summary>
    internal void TryRememberGlobalBinding(GlobalEnvironment globalEnv)
    {
        var identifier = _identifier;
        if (globalEnv._globalObject is not { } globalObject
            || globalEnv._declarativeRecord.HasBinding(identifier.Key))
        {
            return;
        }

        Runtime.Descriptors.PropertyDescriptor? descriptor = null;
        globalObject._properties?.TryGetValue(identifier.Key, out descriptor);
        if (descriptor is null && (globalObject._type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            // built-ins on a shaped global live in shape slots; materialization is stable
            // (same stored instance every read) and never bumps the version, while any slot
            // redefine/deopt does bump it — so the cached reference validates correctly
            var shaped = globalObject.GetOwnProperty(identifier.Key);
            if (shaped != Runtime.Descriptors.PropertyDescriptor.Undefined)
            {
                descriptor = shaped;
            }
        }

        if (descriptor is not null
            && descriptor.IsDataDescriptor()
            && descriptor.Writable
            && (descriptor._flags & Runtime.Descriptors.PropertyFlag.CustomJsValue) == Runtime.Descriptors.PropertyFlag.None)
        {
            _cachedGlobalEnv = globalEnv;
            _cachedGlobalDescriptor = descriptor;
            _cachedGlobalShapeVersion = globalObject._propertiesVersion;
            _cachedGlobalLexicalVersion = globalEnv._lexicalMutations;
        }
    }

    /// <summary>
    /// Every value this read returns comes through here, and the two kinds a read must act on carry
    /// <see cref="InternalTypes.ReadHook"/>: an arguments object (materialized, so the frame's pooled
    /// one is not reused under whoever now holds it) and a string a binding owns (marked shared, so the
    /// binding's next <c>+=</c> allocates rather than extending what this read hands on). One test of the
    /// value's _type, which is what the arguments type test it replaced cost.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsValue HandOn(JsValue value)
    {
        if ((value._type & InternalTypes.ReadHook) != InternalTypes.Empty)
        {
            OnReadHook(value);
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OnReadHook(JsValue value)
    {
        if (value is JsArguments argumentsInstance)
        {
            argumentsInstance.Materialize();
        }
        else if (value is JsString.ConcatenatedString owned)
        {
            owned.MarkShared();
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowNotInitialized(Engine engine)
    {
        Throw.ReferenceError(engine.Realm, $"{_identifier.Key.Name} has not been initialized");
    }
}
