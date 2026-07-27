using System.Diagnostics;
using System.Runtime.InteropServices;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint;

public partial class Engine
{
    public partial class AdvancedOperations
    {
        /// <summary>
        /// Captures the engine's current global bindings — the global object's own properties (string and
        /// symbol), its prototype and extensibility, and the set of top-level lexical (let/const/class)
        /// declarations — so that <see cref="RestoreGlobalSnapshot"/> can later return the engine to this
        /// exact global surface without rebuilding the engine. Capture after host configuration
        /// (<see cref="Engine.SetValue(string, JsValue)"/> calls, module setup) and before evaluating
        /// scripts.
        /// </summary>
        /// <remarks>
        /// Cost is proportional to the number of global properties. The snapshot holds references into this
        /// engine and can only be restored on it. Not thread-safe; call between evaluations only. Capturing
        /// does not force lazily-materialized globals (built-in function slots, lazy host globals) into
        /// existence.
        /// </remarks>
        /// <returns>An opaque snapshot for <see cref="RestoreGlobalSnapshot"/>.</returns>
        public GlobalSnapshot CaptureGlobalSnapshot() => GlobalSnapshot.Capture(_engine);

        /// <summary>
        /// Restores the global bindings captured by <see cref="CaptureGlobalSnapshot"/>: global own
        /// properties (additions removed, deletions reinstated, overwritten values and flags reverted,
        /// prototype and extensibility restored), top-level let/const/class declarations cleared to the
        /// captured set, pending promise continuations discarded, RegExp legacy statics
        /// (<c>RegExp.$1</c> …) cleared, and interop wrapper identity caches reset. Engine warm-up caches
        /// (prepared-script handler trees and their inline caches) are preserved — re-running a cached
        /// prepared script after a restore keeps warm-engine performance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is a configuration-reuse primitive, not an isolation or security boundary.</b> It
        /// reverses the passive traces an evaluation leaves in the global scope; it does NOT reverse:
        /// mutations of built-in prototypes and other intrinsics (<c>Object.prototype.x = 1</c>, a frozen or
        /// poisoned prototype — which can deliberately break or observe later evaluations), mutations inside
        /// object graphs reachable from restored bindings (<c>globalThis.config.x = 1</c> on a
        /// host-provided object persists), host CLR state changed through interop, <c>Symbol.for</c>
        /// registrations, or registered modules. Run mutually distrusting scripts on separate engines.
        /// </para>
        /// <para>
        /// The global object and its environment keep their identity across a restore, so host references
        /// to them stay valid. A lazily-materialized global that was still unmaterialized at capture time
        /// is returned to that state, so its factory runs again on the next access.
        /// </para>
        /// </remarks>
        /// <param name="snapshot">A snapshot captured from this engine.</param>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="snapshot"/> was captured from another engine.</exception>
        /// <exception cref="InvalidOperationException">An evaluation is in progress, or the engine's realm
        /// no longer has the global object the snapshot was taken from.</exception>
        public void RestoreGlobalSnapshot(GlobalSnapshot snapshot)
        {
            if (snapshot is null)
            {
                Throw.ArgumentNullException(nameof(snapshot));
            }

            var engine = _engine;
            if (!ReferenceEquals(snapshot.Engine, engine))
            {
                Throw.ArgumentException("The snapshot was captured from a different engine.", nameof(snapshot));
            }

            // The base global execution context is pushed once at realm initialization and never popped, so
            // a depth above it means script is on the stack — a re-entrant host callback trying to reset the
            // globals out from under the code that called it. _activeEvaluationContext catches the same case
            // for paths that do not push a context of their own.
            if (engine._activeEvaluationContext is not null || engine._executionContexts.Count > 1)
            {
                Throw.InvalidOperationException("The global snapshot cannot be restored while an evaluation is in progress.");
            }

            snapshot.Restore();
        }
    }

    /// <summary>
    /// Clears the ambient per-evaluation state that is not part of any global binding but would otherwise
    /// be observed by the next evaluation. Called from <see cref="GlobalSnapshot"/> restore.
    /// </summary>
    internal void ResetTransientEvaluationState()
    {
        // A continuation left behind by an unsettled promise would otherwise run during the NEXT
        // evaluation's drain and observe — and mutate — the freshly restored globals.
        _eventLoop.Clear();

        _error = null;
        _lastSyntaxElement = null;

        // RegExp legacy statics: every successful match writes them, so RegExp.$1 in the next
        // evaluation would read the previous one's subject text. Reached through the field rather than
        // the Intrinsics property so that an engine which never used RegExp does not build one here.
        Realm.Intrinsics.RegExpIfMaterialized?.ResetLegacyProperties();

        // A script's expandos on a wrapped CLR object (`wrapper.foo = 1`) live on the wrapper, so they
        // would resurface the next time the same CLR object is wrapped.
        _recentObjectWrapperCache?.Clear();
        _objectWrapperCache = null;
    }
}

/// <summary>
/// Opaque capture of an engine's global bindings, produced by
/// <see cref="Engine.AdvancedOperations.CaptureGlobalSnapshot"/> and consumed by
/// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/>. Engine-affine; it has no public members
/// and cannot be restored onto any other engine.
/// </summary>
public sealed class GlobalSnapshot
{
    private readonly ObjectInstance _global;
    private readonly GlobalEnvironment _globalEnv;

    // Non-null exactly when the global was in built-in-shape mode at capture time: one entry per shape
    // slot, in slot order, with a null Descriptor for a slot that had not been materialized yet.
    private readonly DescriptorState[]? _builtinSlots;

    // The ordinary property dictionary, which on a shaped global is the hybrid overflow for names the
    // fixed layout does not carry. Capture order is own-key order and restore rebuilds in it.
    private readonly NamedState[] _properties;
    private readonly SymbolState[] _symbols;
    private readonly ObjectInstance? _prototype;
    private readonly bool _extensible;
    private readonly LexicalBinding[] _lexicalBindings;

    private GlobalSnapshot(
        Engine engine,
        ObjectInstance global,
        GlobalEnvironment globalEnv,
        DescriptorState[]? builtinSlots,
        NamedState[] properties,
        SymbolState[] symbols,
        ObjectInstance? prototype,
        bool extensible,
        LexicalBinding[] lexicalBindings)
    {
        Engine = engine;
        _global = global;
        _globalEnv = globalEnv;
        _builtinSlots = builtinSlots;
        _properties = properties;
        _symbols = symbols;
        _prototype = prototype;
        _extensible = extensible;
        _lexicalBindings = lexicalBindings;
    }

    internal Engine Engine { get; }

    internal static GlobalSnapshot Capture(Engine engine)
    {
        var realm = engine.Realm;
        var global = realm.GlobalObject;
        var globalEnv = realm.GlobalEnv;

        // A lazily-initialized global would otherwise replace its whole property bag later, silently
        // invalidating everything captured here. The in-box GlobalObject is initialized at construction.
        global.EnsureInitialized();

        // A host-substituted global that is a hidden-class JsObject keeps its string keys in shape slots
        // instead of _properties; normalizing to the dictionary representation once, at capture time, is
        // what lets the generic path below see them. Never happens for the in-box GlobalObject, which uses
        // the built-in shape (handled separately) and never the hidden-class one.
        global.ConvertToDictionaryMode();

        DescriptorState[]? builtinSlots = null;
        if ((global._type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            var descriptors = ((IBuiltinShaped) global).BuiltinDescriptors!;
            builtinSlots = new DescriptorState[descriptors.Length];
            for (var i = 0; i < descriptors.Length; i++)
            {
                builtinSlots[i] = new DescriptorState(descriptors[i]);
            }
        }

        return new GlobalSnapshot(
            engine,
            global,
            globalEnv,
            builtinSlots,
            CaptureProperties(global._properties),
            CaptureSymbols(global._symbols),
            global._prototype,
            global.Extensible,
            CaptureLexicalBindings(globalEnv._declarativeRecord));
    }

    private static NamedState[] CaptureProperties(PropertyDictionary? properties)
    {
        var count = properties?.Count ?? 0;
        if (count == 0)
        {
            return [];
        }

        var captured = new NamedState[count];
        var i = 0;
        foreach (var pair in properties!)
        {
            captured[i++] = new NamedState(pair.Key, new DescriptorState(pair.Value));
        }

        return captured;
    }

    private static SymbolState[] CaptureSymbols(SymbolDictionary? symbols)
    {
        var count = symbols?.Count ?? 0;
        if (count == 0)
        {
            return [];
        }

        var captured = new SymbolState[count];
        var i = 0;
        foreach (var pair in symbols!)
        {
            captured[i++] = new SymbolState(pair.Key, new DescriptorState(pair.Value));
        }

        return captured;
    }

    private static LexicalBinding[] CaptureLexicalBindings(GlobalEnvironment.GlobalDeclarativeEnvironment declarativeRecord)
    {
        // Fixed-slot binding storage is only ever installed on function environments; the global
        // declarative record is dictionary-only, which is what makes the rebuild below complete.
        Debug.Assert(declarativeRecord._slots is null, "the global declarative record must not use fixed-slot binding storage");

        var dictionary = declarativeRecord._dictionary;
        var count = dictionary?.Count ?? 0;
        if (count == 0)
        {
            return [];
        }

        var captured = new LexicalBinding[count];
        var i = 0;
        foreach (var pair in dictionary!)
        {
            captured[i++] = new LexicalBinding(pair.Key, pair.Value);
        }

        return captured;
    }

    internal void Restore()
    {
        var engine = Engine;
        var global = _global;
        if (!ReferenceEquals(engine.Realm.GlobalObject, global))
        {
            Throw.InvalidOperationException("The engine's realm no longer has the global object this snapshot was captured from.");
        }

        try
        {
            RestoreGlobalObject(global);
            RestoreLexicalDeclarations();
            engine.ResetTransientEvaluationState();
        }
        finally
        {
            // THE INVARIANT: version counters and epochs are always BUMPED here, never restored to their
            // captured values. Every dependent cache — the identifier global-binding cache, the member
            // inline caches, the slot-chain memos — decides an entry is still valid by comparing against
            // the current counter, so moving a counter BACKWARDS could make a stale entry from before the
            // capture compare equal again and be revalidated against state it never saw. That is the one
            // catastrophic failure mode of this design. A monotonic bump can only ever cause a cache miss,
            // which is always safe. Kept in a finally so a partial restore still invalidates.
            unchecked
            {
                global._propertiesVersion++;
                _globalEnv._lexicalMutations++;
                engine._envBindingInjectionEpoch++;
            }
        }
    }

    private void RestoreGlobalObject(ObjectInstance global)
    {
        var builtinSlots = _builtinSlots;
        if (builtinSlots is not null)
        {
            // Re-enter (or stay in) built-in-shape mode. The array must be a FRESH copy of the capture:
            // slot materialization writes the descriptor it built back into this array, so handing out the
            // captured instance would let the next evaluation mutate the snapshot itself.
            var descriptors = new PropertyDescriptor?[builtinSlots.Length];
            for (var i = 0; i < builtinSlots.Length; i++)
            {
                descriptors[i] = builtinSlots[i].Descriptor;
            }

            ((IBuiltinShaped) global).BuiltinDescriptors = descriptors;
            // DeoptBuiltinShape is the only thing that leaves this mode and it touches no other _type bit
            // and no state beyond BuiltinDescriptors and _properties, both of which are rebuilt here.
            global._type |= InternalTypes.BuiltinShapeMode;
        }
        else if ((global._type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            // Defensive: built-in-shape mode is only ever left, never entered, outside this method, so a
            // global captured unshaped cannot have become shaped. If it ever could, the captured dictionary
            // is the whole truth and the shape has to go.
            ((IBuiltinShaped) global).BuiltinDescriptors = null;
            global._type &= ~InternalTypes.BuiltinShapeMode;
        }

        // A host-substituted global may have entered hidden-class shape mode since the capture.
        global.ConvertToDictionaryMode();

        global._properties = BuildProperties();
        global._symbols = BuildSymbols();
        global._prototype = _prototype;
        global.Extensible = _extensible;

        // The rebuilds above put the captured descriptor INSTANCE back wherever the live table held a
        // different one (or nothing), and dropped every key the capture did not have. What they cannot see
        // is the third case: the live table still holds the very descriptor that was captured, and script
        // mutated it in place — a plain write updates Value in place, and defineProperty flips flags in
        // place. Both are repaired here, against the same instance in either case.
        RepairDescriptors();
    }

    private PropertyDictionary? BuildProperties()
    {
        var properties = _properties;
        if (properties.Length == 0)
        {
            return null;
        }

        // checkExistingKeys: the rebuilt dictionary is live storage again, so a later re-define of an
        // existing key must replace rather than append a duplicate (mirrors ObjectInstance.SetProperties).
        var rebuilt = new PropertyDictionary(properties.Length, checkExistingKeys: true);
        foreach (var entry in properties)
        {
            rebuilt[entry.Name] = entry.State.Descriptor!;
        }

        return rebuilt;
    }

    private SymbolDictionary? BuildSymbols()
    {
        var symbols = _symbols;
        if (symbols.Length == 0)
        {
            return null;
        }

        var rebuilt = new SymbolDictionary(symbols.Length);
        foreach (var entry in symbols)
        {
            rebuilt[entry.Symbol] = entry.State.Descriptor!;
        }

        return rebuilt;
    }

    private void RepairDescriptors()
    {
        var builtinSlots = _builtinSlots;
        if (builtinSlots is not null)
        {
            foreach (var slot in builtinSlots)
            {
                slot.Repair();
            }
        }

        foreach (var entry in _properties)
        {
            entry.State.Repair();
        }

        foreach (var entry in _symbols)
        {
            entry.State.Repair();
        }
    }

    private void RestoreLexicalDeclarations()
    {
        var declarativeRecord = _globalEnv._declarativeRecord;

        // Top-level `using` / `await using` at global scope registers its resources here; they belong to
        // the evaluation being discarded, not to the captured surface.
        declarativeRecord.ClearDisposeCapability();

        var bindings = _lexicalBindings;
        if (bindings.Length == 0)
        {
            // The common case by far: nothing lexical existed at capture time, so every top-level
            // let/const/class the evaluation declared goes away and the same script can declare them
            // again. No public API can do this, which is the blocker this feature exists for.
            declarativeRecord.Clear();
            return;
        }

        var rebuilt = new HybridDictionary<Binding>(bindings.Length, checkExistingKeys: true);
        foreach (var binding in bindings)
        {
            rebuilt[binding.Name] = binding.Value;
        }

        declarativeRecord._dictionary = rebuilt;
    }

    /// <summary>
    /// A captured descriptor: the instance itself, plus the two pieces of its state that JavaScript can
    /// mutate without replacing it — the attribute flags and the value slot.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct DescriptorState
    {
        internal DescriptorState(PropertyDescriptor? descriptor)
        {
            Descriptor = descriptor;
            // The raw fields, never the Value property: reading Value runs CustomValue, which is what
            // materializes a lazy host global or a built-in function slot. Capture must never do that.
            Flags = descriptor?._flags ?? PropertyFlag.None;
            Value = descriptor?._value;
#if DEBUG
            AccessorGet = (descriptor as GetSetPropertyDescriptor)?.Get;
            AccessorSet = (descriptor as GetSetPropertyDescriptor)?.Set;
#endif
        }

        internal PropertyDescriptor? Descriptor { get; }
        private PropertyFlag Flags { get; }
        private JsValue? Value { get; }
#if DEBUG
        private JsValue? AccessorGet { get; }
        private JsValue? AccessorSet { get; }
#endif

        internal void Repair()
        {
            var descriptor = Descriptor;
            if (descriptor is null)
            {
                return;
            }

#if DEBUG
            // GetSetPropertyDescriptor is the only descriptor whose getter/setter can be replaced in place,
            // and the two setters that do it (SetGet/SetSet) only ever run on a descriptor being built —
            // defineProperty over an existing accessor allocates a new one. Nothing here could revert such
            // a mutation, so it must not be possible in the first place.
            Debug.Assert(
                descriptor is not GetSetPropertyDescriptor accessor
                || (ReferenceEquals(accessor.Get, AccessorGet) && ReferenceEquals(accessor.Set, AccessorSet)),
                "an accessor descriptor's get/set changed in place, which a snapshot restore cannot revert");
#endif

            // Compare before writing. A built-in constant slot points at a descriptor instance shared by
            // every realm in the process (BuiltinShape.ConstTemplate), so an unconditional store would be a
            // write into state another engine may be reading concurrently, for no gain.
            if (descriptor._flags != Flags)
            {
                descriptor._flags = Flags;
            }

            if (!ReferenceEquals(descriptor._value, Value))
            {
                // Raw field again, so a CustomValue setter (which can write host CLR state, or refuse) is
                // never invoked. For a descriptor that was still lazy at capture time this puts back the
                // "not yet materialized" pair — null value, CustomJsValue flag — so the next read resolves
                // it again, which is exactly the captured state.
                descriptor._value = Value;
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct NamedState(Key Name, DescriptorState State);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct SymbolState(JsSymbol Symbol, DescriptorState State);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct LexicalBinding(Key Name, Binding Value);
}
