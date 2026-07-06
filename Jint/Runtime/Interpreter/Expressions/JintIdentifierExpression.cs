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

    // Bounded walk: chain depth is typically 1-3 in real code; deeper chains fall through.
    private const int MaxSlotCacheChainDepth = 4;

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
        var env = engine.ExecutionContext.LexicalEnvironment;
        var strict = StrictModeScope.IsStrictModeCode;

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
        var env = engine.ExecutionContext.LexicalEnvironment;
        var strict = StrictModeScope.IsStrictModeCode;
        JsValue? value;

        // Slot-cache fast path: walk from the current env up the chain looking for the cached
        // resolving env via ReferenceEquals; the common case matches at hop 0 without any
        // probing. When found, read the slot value directly.
        // Engine-identity gate: a Prepared<Script> reused across multiple Engine instances
        // shares JintIdentifierExpression nodes, so the cached env may be from a previous
        // Engine. Skipping the walk when engines differ avoids 4 wasted hops per call —
        // significant on harnesses (DromaeoBenchmark, SunSpiderBenchmark) that create a
        // new Engine per iteration.
        var cachedSlotEnv = _cachedSlotEnv;
        if (cachedSlotEnv is not null && ReferenceEquals(cachedSlotEnv._engine, engine))
        {
            var search = env;
            for (var hops = 0; hops < MaxSlotCacheChainDepth && search is not null; hops++)
            {
                if (ReferenceEquals(search, cachedSlotEnv))
                {
                    var slots = cachedSlotEnv._slots;
                    var idx = _cachedSlotIndex;
                    if (slots is not null && (uint) idx < (uint) slots.Length)
                    {
                        ref var binding = ref slots[idx];
                        value = binding.HasReferenceValue
                            ? binding.Value
                            : DeclarativeEnvironment.MaterializeUnboxedOrNull(ref binding);
                        if (value is null)
                        {
                            ThrowNotInitialized(engine);
                        }
                        return MaterializeIfArguments(value);
                    }
                    break;
                }

                if (search is ObjectEnvironment)
                {
                    // a with-object between us and the cached slot may shadow the name
                    // dynamically; only the full resolution can decide who owns the binding
                    break;
                }

                // An intermediate environment holding the name shadows the cached resolution
                // (an eval-cache-shared body under a shadowing block, a nested eval of the
                // same source, or a sloppy direct eval injecting the name into an enclosing
                // function environment). HasBinding on a declarative environment is pure.
                if (search is DeclarativeEnvironment intermediate && intermediate.HasBinding(identifier.Key))
                {
                    break;
                }

                search = search._outerEnv;
            }
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
                return MaterializeIfArguments(value);
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
                }
            }
            else if (identifierEnvironment is GlobalEnvironment globalEnv)
            {
                // No hop restriction: the validator re-walks with shadow probes on every read,
                // so nested-scope reads of globals (loop bodies, closures) can use the cache.
                TryRememberGlobalBinding(globalEnv);
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

        return MaterializeIfArguments(value);
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

        var key = _identifier.Key;
        var search = env;
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
                return _cachedGlobalDescriptor;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsValue MaterializeIfArguments(JsValue value)
    {
        // make sure arguments access freezes state
        if (value is JsArguments argumentsInstance)
        {
            argumentsInstance.Materialize();
        }
        return value;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowNotInitialized(Engine engine)
    {
        Throw.ReferenceError(engine.Realm, $"{_identifier.Key.Name} has not been initialized");
    }
}
