using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using BindingFlags = System.Reflection.BindingFlags;

namespace Jint;

public partial class Engine
{
    public sealed partial class AdvancedOperations
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
        /// <para>
        /// Cost is proportional to the number of global properties. Not thread-safe; call between evaluations
        /// only. Capturing does not force lazily-materialized globals (built-in function slots, lazy host
        /// globals) into existence.
        /// </para>
        /// <para>
        /// <b>Lifetime.</b> A snapshot keeps the engine, its global object and environment, and every captured
        /// descriptor — and through those, every value a global referenced at capture time — strongly
        /// reachable. It can only be restored on the engine it came from. Do not cache one past the lifetime
        /// of that engine: it pins the engine and its whole realm, and a snapshot taken over a global holding
        /// a large object graph pins that too.
        /// </para>
        /// <para>
        /// <b>What a snapshot can see.</b> Only properties the engine itself stores. A descriptor subclass
        /// that overrides <see cref="Runtime.Descriptors.PropertyDescriptor.CustomValue"/> and keeps the value
        /// in state of its own — a field, a CLR property, a lookup against live host data — is captured and
        /// reinstated by <em>reference</em>, and its attribute flags are reverted, but its value is not: the
        /// engine cannot revert what it cannot see, and writing the inherited value field would only
        /// desynchronize it from the value reads resolve to. Keep the value in the inherited field (the plain
        /// <see cref="Runtime.Descriptors.PropertyDescriptor"/> shape) for a global that must be restorable.
        /// </para>
        /// <para>
        /// <b>Representation.</b> If a host-substituted global is a hidden-class object, capturing normalizes
        /// it to the property-dictionary representation, permanently — the snapshot's storage model is the
        /// dictionary one, and the conversion is one-way. The built-in global object is never affected; it
        /// uses the shared built-in layout, which capture and restore handle in full fidelity.
        /// </para>
        /// </remarks>
        /// <example>
        /// The reuse shape: capture once at the end of construction, keep the snapshot in a field beside the
        /// engine, and restore in a <c>finally</c> so an evaluation that throws cannot leave its globals
        /// behind for the next one.
        /// <code>
        /// private readonly object _gate = new();
        /// private readonly Engine _engine;
        /// private readonly GlobalSnapshot _clean;
        ///
        /// public ScriptRunner(object host)
        /// {
        ///     _engine = new Engine();
        ///     _engine.SetValue("host", host);                  // all configuration first
        ///     _clean = _engine.Advanced.CaptureGlobalSnapshot();
        /// }
        ///
        /// public object? Run(string script)
        /// {
        ///     lock (_gate)                                     // an Engine is single-threaded
        ///     {
        ///         try
        ///         {
        ///             return _engine.Evaluate(script).ToObject();
        ///         }
        ///         finally
        ///         {
        ///             _engine.Advanced.RestoreGlobalSnapshot(_clean);
        ///         }
        ///     }
        /// }
        /// </code>
        /// The <c>finally</c> is the point: a script that throws still declared its globals, and restoring
        /// only on the success path hands them to the next caller — <see cref="WithRestoredGlobals"/> is that
        /// <c>try</c>/<c>finally</c> in one call, for a host that would rather not spell it out. The
        /// <c>ToObject()</c> is deliberate too —
        /// returning CLR values means nothing the caller keeps depends on the engine still being in the state
        /// that produced it. With the asynchronous entry points
        /// (<c>EvaluateAsync</c>/<c>ExecuteAsync</c>/<c>InvokeAsync</c>) await the returned <c>Task</c> before
        /// restoring; a restore attempted while one is outstanding throws
        /// <see cref="InvalidOperationException"/> rather than silently restoring underneath it.
        /// </example>
        /// <returns>An opaque snapshot for <see cref="RestoreGlobalSnapshot"/>.</returns>
        /// <exception cref="NotSupportedException">The realm's global object overrides one of the property
        /// storage virtuals (<c>GetOwnProperty</c>, <c>Set</c>, …), so it resolves own properties from state
        /// outside the engine's tables and cannot be captured or restored.</exception>
        public GlobalSnapshot CaptureGlobalSnapshot()
        {
            using var ownership = _engine.EnterHostCall();
            return GlobalSnapshot.Capture(_engine);
        }

        /// <summary>
        /// Restores the global bindings captured by <see cref="CaptureGlobalSnapshot"/>: global own
        /// properties (additions removed, deletions reinstated, overwritten values and flags reverted,
        /// prototype and extensibility restored), top-level let/const/class declarations cleared to the
        /// captured set, RegExp legacy statics (<c>RegExp.$1</c> …) cleared, and interop wrapper identity
        /// caches reset. It also ends the previous evaluation cycle on the event loop: queued jobs are
        /// discarded, and any promise registered before the restore — including one created for a CLR
        /// <see cref="System.Threading.Tasks.Task"/> awaited from script — is dropped when it settles rather
        /// than resuming its continuation against the restored globals. Engine warm-up caches
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
        /// <para>
        /// <b>Running the factory again is not the same as getting a new value, and the difference is the
        /// factory's, not this method's.</b> A restore reverts the descriptor; it cannot revert whatever the
        /// factory reads from, and does not try. So a factory that <em>constructs</em> gives the next cycle
        /// a fresh object — Jint's own <c>process</c> shim, and the global object's built-in function slots
        /// (<c>decodeURI</c> and its eight siblings), are the built-in globals of that shape. A factory that
        /// hands back something it is <em>holding</em> gives the next cycle exactly what the previous one
        /// mutated. Every global Jint installs apart from those is the second kind: the 58 ECMAScript ones
        /// and every opt-in web API are lazy descriptors in front of a realm intrinsic that memoizes, so
        /// <c>Math</c>, <c>JSON</c>, <c>console</c>, <c>crypto</c>, <c>performance</c>, <c>caches</c>,
        /// <c>localStorage</c>, <c>navigator</c> and <c>scheduler</c> all come back carrying whatever the
        /// previous cycle wrote on them. That is the "object graphs reachable from restored bindings" clause
        /// above, seen from the far end, and a host installing lazy globals of its own inherits the same
        /// rule: freshness is something its factory has to provide.
        /// </para>
        /// <para>
        /// <b>What the event-loop fence does and does not cover.</b> It covers everything that reaches the
        /// engine through a promise: a fire-and-forget async function suspended on a host
        /// <see cref="System.Threading.Tasks.Task"/> never resumes after a restore, and a
        /// <see cref="RegisterPromise"/> handle registered before the restore no longer settles into the
        /// engine — so register such a promise <em>after</em> the restore if it is meant to span one. It does
        /// not cover the host calling back in by itself: invoking a function the previous evaluation handed
        /// you (<c>engine.Invoke</c>, a stored <c>ICallable</c>) runs that function against the restored
        /// surface, because that is a new evaluation the host asked for.
        /// </para>
        /// <para>
        /// <b>Restore reverts host changes too</b>, not only script's: a host that keeps the
        /// <see cref="Runtime.Descriptors.PropertyDescriptor"/> it installed and updates its value between
        /// evaluations has that update rolled back, because the contract is "return to this exact surface".
        /// One kind of host state is out of reach in the other direction: a descriptor subclass that
        /// overrides <see cref="Runtime.Descriptors.PropertyDescriptor.CustomValue"/> and keeps the value in
        /// its own field rather than the inherited one is reinstated by reference but not reverted — see
        /// <see cref="CaptureGlobalSnapshot"/>.
        /// </para>
        /// </remarks>
        /// <param name="snapshot">A snapshot captured from this engine.</param>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="snapshot"/> was captured from another engine.</exception>
        /// <exception cref="InvalidOperationException">An evaluation is in progress — script on the stack, or
        /// an <c>EvaluateAsync</c>/<c>ExecuteAsync</c>/<c>InvokeAsync</c> Task still outstanding — or the
        /// engine's realm no longer has the global object the snapshot was taken from.</exception>
        public void RestoreGlobalSnapshot(GlobalSnapshot snapshot)
        {
            using var ownership = _engine.EnterHostCall();
            if (snapshot is null)
            {
                Throw.ArgumentNullException(nameof(snapshot));
            }

            var engine = _engine;
            if (!ReferenceEquals(snapshot.Engine, engine))
            {
                Throw.ArgumentException("The snapshot was captured from a different engine.", nameof(snapshot));
            }

            // IsEvaluationInProgress covers script on the stack — a re-entrant host callback trying to reset
            // the globals out from under the code that called it — and host entries that run without pushing
            // an execution context of their own, such as Engine.Call of a pure ClrFunction.
            //
            // It does not see a suspended EvaluateAsync: its synchronous phase has already run to completion,
            // so the stack is unwound to the base context and the host entry has been popped while the
            // settlement loop sits in its await. Restoring there would either strand the loop on a promise
            // whose settle got discarded (a bogus PromiseTimeout, or a hang when timeouts are off) or let it
            // resume the previous cycle against the restored globals and complete the host's Task with a
            // value computed across two global states. The counter is the only signal that sees it.
            if (engine.IsEvaluationInProgress || engine.HasPendingAsyncOperations)
            {
                Throw.InvalidOperationException("The global snapshot cannot be restored while an evaluation is in progress.");
            }

            snapshot.Restore();
        }

        /// <summary>
        /// Runs <paramref name="action"/> and then restores <paramref name="snapshot"/> in a
        /// <see langword="finally"/>, so an evaluation that throws cannot leave its globals behind for the
        /// next one. The documented reuse recipe with the part that is easy to forget supplied.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Equivalent to <c>try { action(); } finally { RestoreGlobalSnapshot(snapshot); }</c> and nothing
        /// more. Exceptions from <paramref name="action"/> propagate — after the restore has run — and the
        /// restore's own guards are left to speak for themselves rather than being pre-validated here, so a
        /// foreign snapshot or an outstanding asynchronous evaluation fails exactly as
        /// <see cref="RestoreGlobalSnapshot"/> documents, once the action has already run.
        /// </para>
        /// <para>
        /// Everything <see cref="RestoreGlobalSnapshot"/> does and does not revert applies unchanged; in
        /// particular <b>this is a configuration-reuse primitive, not an isolation or security boundary</b>.
        /// See that method for the full list of what survives a restore. This overload adds no isolation of
        /// its own: it is a <see langword="finally"/>, not a sandbox.
        /// </para>
        /// <para>
        /// An <see cref="Engine"/> accepts one host operation at a time. A host sharing one across requests
        /// still needs to serialize the whole call — concurrent entries fail rather than waiting for one
        /// another, and the restore is only correct if nothing else is evaluating.
        /// Asynchronous work has to be awaited <em>inside</em> <paramref name="action"/>: an
        /// <c>EvaluateAsync</c> whose <see cref="System.Threading.Tasks.Task"/> is still outstanding when the
        /// action returns makes the restore throw.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// lock (_gate)
        /// {
        ///     engine.Advanced.WithRestoredGlobals(_clean, () => result = engine.Evaluate(script).ToObject());
        /// }
        /// </code>
        /// </example>
        /// <param name="snapshot">A snapshot captured from this engine.</param>
        /// <param name="action">The work to run before restoring.</param>
        /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> or <paramref name="action"/>
        /// is <see langword="null"/>.</exception>
        public void WithRestoredGlobals(GlobalSnapshot snapshot, Action action)
        {
            using var ownership = _engine.EnterHostCall();
            if (snapshot is null)
            {
                Throw.ArgumentNullException(nameof(snapshot));
            }

            if (action is null)
            {
                Throw.ArgumentNullException(nameof(action));
            }

            try
            {
                action();
            }
            finally
            {
                // Deliberately not guarded by a success flag: the restore is what the caller asked for, and
                // skipping it on the throwing path is precisely the mistake this method exists to prevent.
                RestoreGlobalSnapshot(snapshot);
            }
        }
    }

    /// <summary>
    /// Clears the ambient per-evaluation state that is not part of any global binding but would otherwise
    /// be observed by the next evaluation. Called from <see cref="GlobalSnapshot"/> restore.
    /// </summary>
    internal void ResetTransientEvaluationState()
    {
        // A continuation left behind by an unsettled promise would otherwise run during the NEXT
        // evaluation's drain and observe — and mutate — the freshly restored globals. This is the eager
        // half; the fence for work that has not been enqueued yet is the generation bump in Restore.
        _eventLoop.Clear();

        // An asynchronous module load the host has not finished belongs to the cycle that started it: its
        // completion carries that cycle's generation and will be discarded at dequeue. Forgetting the
        // in-flight entry is what stops the next cycle attaching to a load that can never finish; it will
        // start a fresh one instead. Modules already loaded stay registered — the module registry is
        // deliberately not part of what a restore reverts.
        Modules?.DiscardPendingLoads();

        // An Atomics.waitAsync registered by the cycle that is ending must never have its timeout settled
        // into the globals just restored. The deadlines are the engine's, not the event loop's, so clearing
        // the loop above does not reach them; the generation each waiter captured at registration is the
        // second line of defence for a settlement already promoted into a job. What deliberately does not
        // happen is removing those waits from the waiter lists of their shared data blocks: a wait asking for
        // no timeout has always stayed there across a restore, and what Atomics.notify counts should not
        // depend on whether a wait happened to have a timeout.
        DiscardAtomicsWaiterDeadlines();

#if NET8_0_OR_GREATER
        // A timer registered by the cycle that is ending must never fire into the globals just restored. The
        // queue is the engine's, not the event loop's, so clearing the loop above does not reach it; the
        // generation each entry carries is the second line of defence for one already promoted into a job.
        //
        // This also ends every worker connection this engine is a party to — in both directions, since a
        // worker connection is one object spanning two engines rather than two independent peers. Only the
        // thread-safe half happens there: the host's OnWorkerEnded callbacks come back as a list and are run
        // at the bottom of this method, so a provider that throws cannot leave the engine half-reset.
        var endedWorkers = _webApi?.ResetTransientState();

        // A bridge to a host System.IO.Stream holds an operating-system handle, so the cycle ending has to
        // close it rather than merely stop delivering its chunks: the generation fence already does the
        // latter.
        AbandonHostStreamBridges();
#endif

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

#if NET8_0_OR_GREATER
        // Last of all, with the engine whole again: the host is told about the worker connections this restore
        // ended. It is host code, so it runs outside everything above it rather than in the middle of it.
        WebApiEngineState.NotifyWorkerHosts(endedWorkers);
#endif
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

        EnsureGlobalIsCapturable(global);

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

    /// <summary>
    /// The storage virtuals a snapshot goes around. Capture reads the engine-side property tables directly and
    /// restore writes them directly, which is the only way to put a descriptor <em>instance</em> back and the
    /// only way to reach a table an evaluation may have emptied. A global that overrides any of these resolves
    /// its own properties from state the engine cannot see, so both halves would quietly no-op on it.
    /// <para>
    /// The names are matched against the base definition of each declared override, so a member merely
    /// <em>named</em> like one of these does not trip the check and an override declared in an intermediate
    /// class does. <c>Initialize</c> is deliberately absent — the in-box <c>GlobalObject</c> overrides it, and
    /// capture forces it to have run before reading anything. <c>get_Extensible</c> and <c>GetOwnProperties</c> used to be
    /// listed, for the same reason <see cref="ObjectInstance.PreventExtensions"/> still is; each left the list
    /// when it stopped being virtual, and the enumeration a global overrides is
    /// <see cref="ObjectInstance.GetOwnPropertyKeys"/>, which is still listed.
    /// </para>
    /// </summary>
    private static readonly string[] _storageVirtuals =
    [
        nameof(ObjectInstance.GetOwnProperty),
        nameof(ObjectInstance.SetOwnProperty),
        nameof(ObjectInstance.TryGetOwnPropertyValue),
        nameof(ObjectInstance.ProbeOwnProperty),
        nameof(ObjectInstance.GetOwnPropertyKeys),
        nameof(ObjectInstance.DefineOwnProperty),
        nameof(ObjectInstance.RemoveOwnProperty),
        nameof(ObjectInstance.HasProperty),
        nameof(ObjectInstance.Delete),
        nameof(ObjectInstance.PreventExtensions),
        nameof(ObjectInstance.GetPrototypeOf),
        nameof(JsValue.Get),
        nameof(JsValue.Set),
    ];

    private static readonly ConcurrentDictionary<Type, string?> _globalStorageOverrides = new();

    /// <summary>
    /// Refuses to capture a global whose own properties do not live in the engine's storage. Silently
    /// capturing nothing and silently restoring nothing is the worst outcome available here: the host gets a
    /// snapshot that reports success on every restore while the script's changes stay in place.
    /// </summary>
    private static void EnsureGlobalIsCapturable(ObjectInstance global)
    {
        var overridden = _globalStorageOverrides.GetOrAdd(global.GetType(), static type => FindStorageOverride(type));
        if (overridden is not null)
        {
            Throw.NotSupportedException(
                $"The global object type '{global.GetType()}' overrides '{overridden}', so it resolves own properties from state outside the engine's property tables. "
                + "A global snapshot can only capture and restore properties the engine itself stores; capturing this global would silently produce a snapshot that restores nothing. "
                + "Use the built-in global object, or a Host.CreateGlobalObject result that stores its properties through the base ObjectInstance implementation.");
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Enumerates the declared methods of the global object's own type to detect overrides of ObjectInstance's storage virtuals. If the metadata has been trimmed away the walk finds nothing and capture proceeds, which is the pre-check behaviour — a host that trims its own global's metadata is not made less correct by the check being unable to run.")]
    private static string? FindStorageOverride(Type type)
    {
        const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (var current = type; current is not null && current != typeof(ObjectInstance); current = current.BaseType)
        {
            foreach (var method in current.GetMethods(Declared))
            {
                if (!method.IsVirtual)
                {
                    continue;
                }

                // GetBaseDefinition returns the method itself for a newly introduced virtual, and the original
                // declaration for an override — which is how a name collision on an unrelated member is told
                // apart from a real override of one of ours.
                var baseDefinition = method.GetBaseDefinition();
                if (baseDefinition == method)
                {
                    continue;
                }

                var declaringType = baseDefinition.DeclaringType;
                if (declaringType != typeof(ObjectInstance) && declaringType != typeof(JsValue))
                {
                    continue;
                }

                if (Array.IndexOf(_storageVirtuals, baseDefinition.Name) >= 0)
                {
                    return baseDefinition.Name;
                }
            }
        }

        return null;
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

            // Same rule, applied to time rather than to storage: the event loop's generation only ever moves
            // forward, so a job stamped by an earlier cycle can never be mistaken for current work. Here
            // rather than beside EventLoop.Clear so that a partial restore still ends the cycle — half a
            // restored global surface must not be visible to the previous cycle's continuations either.
            engine.EventLoop.NextGeneration();
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
            // DeoptBuiltinShape is the only thing that leaves this mode, and it touches no state beyond
            // BuiltinDescriptors and _properties — both rebuilt here — plus the _type bits that describe the
            // mode itself. Those must be RE-DERIVED rather than merely re-set: BuiltinShapeIndexAuthoritative
            // rides along with the mode, so entering it with a bare |= BuiltinShapeMode would leave a global
            // that had once deopted permanently without the fast-miss lane, in exactly the capture/restore
            // reuse pattern that lane exists for.
            global._type |= global.BuiltinShapeModeBits();
        }
        else if ((global._type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            // Defensive: built-in-shape mode is only ever left, never entered, outside this method, so a
            // global captured unshaped cannot have become shaped. If it ever could, the captured dictionary
            // is the whole truth and the shape has to go.
            ((IBuiltinShaped) global).BuiltinDescriptors = null;
            global._type &= ~(InternalTypes.BuiltinShapeMode | InternalTypes.BuiltinShapeIndexAuthoritative);
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

            // Whether the value lives where this repair can reach it has to be decided from the flags as they
            // are NOW as well as as they were captured, and therefore before the flags are put back.
            var valueIsReachable = ValueIsFieldBacked(descriptor, Flags | descriptor._flags);

            // Compare before writing. A built-in constant slot points at a descriptor instance shared by
            // every realm in the process (BuiltinShape.ConstTemplate), so an unconditional store would be a
            // write into state another engine may be reading concurrently, for no gain.
            if (descriptor._flags != Flags)
            {
                descriptor._flags = Flags;
            }

            if (valueIsReachable && !ReferenceEquals(descriptor._value, Value))
            {
                // Raw field again, so a CustomValue setter (which can write host CLR state, or refuse) is
                // never invoked. For a descriptor that was still lazy at capture time this puts back the
                // "not yet materialized" pair — null value, CustomJsValue flag — so the next read resolves
                // it again, which is exactly the captured state.
                descriptor._value = Value;
            }
        }

        /// <summary>
        /// Whether reverting this descriptor's value means writing the inherited <c>_value</c> field.
        /// </summary>
        /// <remarks>
        /// True for every ordinary descriptor, whose value is that field. False for a descriptor carrying
        /// <see cref="PropertyFlag.CustomJsValue"/> that is not one of the engine's own field-backed lazies:
        /// reads then come from the subclass's <see cref="PropertyDescriptor.CustomValue"/> override, which
        /// may keep the value anywhere. Writing the inherited field would not revert such a descriptor — it
        /// would leave the field disagreeing with the value reads resolve to, which is worse than not
        /// reverting, so the descriptor is reinstated by reference and its value left alone.
        /// </remarks>
        private static bool ValueIsFieldBacked(PropertyDescriptor descriptor, PropertyFlag flagsEverSeen)
            => (flagsEverSeen & PropertyFlag.CustomJsValue) == PropertyFlag.None
               || descriptor is IFieldBackedLazyDescriptor;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct NamedState(Key Name, DescriptorState State);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct SymbolState(JsSymbol Symbol, DescriptorState State);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct LexicalBinding(Key Name, Binding Value);
}
