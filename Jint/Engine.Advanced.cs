using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Jint.Diagnostics;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint;

public partial class Engine
{
    public AdvancedOperations Advanced { get; }

    public sealed partial class AdvancedOperations
    {
        private readonly Engine _engine;

        internal AdvancedOperations(Engine engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// Gets current stack trace that is active in engine.
        /// </summary>
        public string StackTrace
        {
            get
            {
                using var ownership = _engine.EnterHostCall();
                var lastSyntaxElement = _engine._lastSyntaxElement;
                if (lastSyntaxElement is null)
                {
                    return string.Empty;
                }

                return _engine.CallStack.BuildCallStackString(_engine, lastSyntaxElement.Location);
            }
        }

        /// <summary>
        /// Clears the engine's call stack. The frames are zeroed, so everything an interrupted execution
        /// left them holding — callee functions, receivers, arguments — is released.
        /// </summary>
        /// <remarks>
        /// This does <b>not</b> clear the interpreter's per-node inline caches, and there is no API that
        /// does. In particular, a member-read site that resolved a name on its receiver's prototype keeps
        /// a strong reference to the last receiver it served (along with that prototype and the resolved
        /// descriptor) so the next read can be validated by identity; the entry is only replaced when that
        /// same site later caches a <i>different</i> receiver. Handler trees are engine-owned and survive
        /// between evaluations on an engine that re-runs a script, so a pooled engine can keep one host
        /// object alive per warmed call site until its next run. A host whose receivers wrap large native
        /// state that must not outlive a run should drop the engine rather than pool it.
        /// </remarks>
        public void ResetCallStack()
        {
            using var ownership = _engine.EnterHostCall();
            _engine.ResetCallStack();
        }

        /// <summary>
        /// Gives the engine a turn: runs everything the event loop currently has to run — queued jobs,
        /// promise reactions, a due timer, a completion that arrived from a background thread — and returns.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is the canonical host loop, together with
        /// <see cref="TimeUntilNextScheduledWork"/>, which answers <i>when</i> to call it.</b> Jint never
        /// starts a thread to run script, so an engine nobody pumps runs no <c>setTimeout</c> callback,
        /// settles no <c>Atomics.waitAsync</c> and delivers no worker message. Any host using timers,
        /// promises or workers has to call this, or one of the blocking waits built on it
        /// (<see cref="WaitForScheduledWork(TimeSpan, System.Threading.CancellationToken)"/>).
        /// </para>
        /// <para>
        /// It runs what is available and does not block: it is not a drain for a budget, and there is
        /// deliberately no such method — see the loop shapes on <see cref="TimeUntilNextScheduledWork"/>.
        /// A job belonging to an evaluation cycle <see cref="RestoreGlobalSnapshot"/> has ended is discarded
        /// rather than run.
        /// </para>
        /// </remarks>
        public void ProcessTasks()
        {
            using var ownership = _engine.EnterHostCall();
            _engine.RunAvailableContinuations();
        }

        /// <summary>
        /// Converts a script result to a detached CLR graph while enforcing depth, size, and output limits.
        /// </summary>
        /// <remarks>
        /// Arrays, typed arrays, array buffers, maps, sets, and enumerable own object properties are copied.
        /// Cycles are rejected, as are functions and symbols that cannot form a detached data result. CLR
        /// wrappers return their existing host-owned target without walking it. Property access may invoke
        /// getters and proxy traps, so the conversion runs under the engine's execution constraints; those
        /// constraints and an outer request deadline remain required for untrusted code.
        /// </remarks>
        public object? ConvertResult(JsValue value, ResultLimits? limits = null)
        {
            if (value is null)
            {
                Throw.ArgumentNullException(nameof(value));
            }

            if (value is ObjectInstance instance && !ReferenceEquals(instance.Engine, _engine))
            {
                Throw.ArgumentException("The value belongs to a different engine.", nameof(value));
            }

            var effectiveLimits = limits ?? _engine.Options.ResultLimits;
            return _engine.ExecuteWithConstraints(
                _engine.Options.Strict,
                () => ResultConverter.Convert(_engine, value, effectiveLimits));
        }

        /// <summary>
        /// Inspects the effective security configuration after engine-construction callbacks have run.
        /// </summary>
        /// <remarks>
        /// The callbacks are not replayed. Constraint factories are inspected through the instances this engine
        /// already created, so the report neither invokes user code nor changes runtime policy.
        /// </remarks>
        public SecurityConfigurationReport ValidateSecurityConfiguration(
            SecurityConfigurationPolicy policy = SecurityConfigurationPolicy.UntrustedScripts)
        {
            using var ownership = _engine.EnterHostCall();
            return OptionsSecurityExtensions.CreateEngineReport(_engine, policy);
        }

        /// <summary>
        /// Hands script a promise this host settles itself, returning the promise together with its resolve
        /// and reject functions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Resolve and reject may be called from another thread, and take a CLR value.</b> Settlement is
        /// enqueued safely and drains inline only when the calling thread can claim exclusive engine
        /// ownership; otherwise the owning host turn or a later <see cref="ProcessTasks"/> call drains it.
        /// The conversion of the value happens in that enqueued job — on the engine's thread — which is the
        /// reason the parameter is <see cref="object"/> and not <see cref="JsValue"/>: a host settling from a
        /// <see cref="System.Threading.Tasks.Task"/> continuation holds a CLR value and has nowhere safe to
        /// convert it, because a <see cref="JsValue"/> belongs to the engine that made it and that engine may
        /// be busy. Passing a <see cref="JsValue"/> is still correct and costs nothing extra, and passing
        /// <see langword="null"/> settles with <see cref="JsValue.Null"/>.
        /// </para>
        /// <para>
        /// A promise registered before a <see cref="RestoreGlobalSnapshot"/> is dropped when it settles
        /// rather than resuming into the restored globals; register one that must outlive a restore after it.
        /// </para>
        /// <para>
        /// This is a low-level primitive — the supported way for host code to hand script a promise it
        /// settles itself — and it is an ordinary part of the public surface: unlike the diagnostics marked
        /// <c>JINT0001</c> (see <see cref="JintDiagnosticIds"/>), what it returns is a real capability rather
        /// than a report about an internal representation, so a change to it is a migration-guide row like
        /// any other. It carried an "EXPERIMENTAL! Subject to change" banner from before that distinction
        /// existed; the banner was removed rather than promoted, so that the word means one thing here.
        /// </para>
        /// </remarks>
        /// <returns>a Promise instance and functions to either resolve or reject it</returns>
        public ManualPromise RegisterPromise()
        {
            using var ownership = _engine.EnterHostCall();
            return _engine.RegisterPromise();
        }

        /// <summary>
        /// Creates an ECMAScript Proxy exotic object whose traps are implemented in .NET by
        /// <paramref name="handler"/>. This is the CLR-side equivalent of <c>new Proxy(target, handlerObject)</c>
        /// in script, which remains the way to create proxies with JavaScript handler objects.
        /// </summary>
        /// <remarks>
        /// A trap method returning CLR <see langword="null"/> forwards the operation to
        /// <paramref name="target"/>, exactly like an absent trap on a JavaScript handler object.
        /// All ECMAScript proxy invariants are enforced on trap results. Note that <c>proxy.method(x)</c>
        /// fires the <see cref="ProxyHandler.Get"/> trap (returning the method) followed by a plain call
        /// on the result — the <see cref="ProxyHandler.Apply"/> trap only fires when the proxy itself is
        /// invoked, so intercepting method calls means returning a wrapping function from
        /// <see cref="ProxyHandler.Get"/>.
        /// </remarks>
        /// <param name="target">The proxy target object.</param>
        /// <param name="handler">The .NET trap implementation.</param>
        /// <returns>The proxy object, ready to be passed into script.</returns>
        public ObjectInstance CreateProxy(ObjectInstance target, ProxyHandler handler)
        {
            using var ownership = _engine.EnterHostCall();
            if (target is null)
            {
                Throw.ArgumentNullException(nameof(target));
            }

            if (handler is null)
            {
                Throw.ArgumentNullException(nameof(handler));
            }

            return new JsProxy(_engine, target, handler);
        }

        /// <summary>
        /// Creates a revocable ECMAScript Proxy exotic object whose traps are implemented in .NET by
        /// <paramref name="handler"/>, mirroring JavaScript's <c>Proxy.revocable()</c>. See
        /// <see cref="CreateProxy"/> for trap forwarding and invariant semantics.
        /// </summary>
        /// <param name="target">The proxy target object.</param>
        /// <param name="handler">The .NET trap implementation.</param>
        /// <returns>The proxy paired with its revoke operation.</returns>
        public RevocableProxy CreateRevocableProxy(ObjectInstance target, ProxyHandler handler)
        {
            using var ownership = _engine.EnterHostCall();
            if (target is null)
            {
                Throw.ArgumentNullException(nameof(target));
            }

            if (handler is null)
            {
                Throw.ArgumentNullException(nameof(handler));
            }

            return new RevocableProxy(new JsProxy(_engine, target, handler));
        }

        /// <summary>
        /// Reports which internal representation <paramref name="value"/> is currently using for its own
        /// string-keyed properties.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is a diagnostic, and it is not part of Jint's compatibility contract.</b>
        /// <see cref="ObjectRepresentation"/> names an internal implementation detail: which representation
        /// any given object ends up in may change in any release, and the enum may gain members. Neither is
        /// a breaking change, and neither will be treated as one.
        /// </para>
        /// <para>
        /// Use it in diagnostics, assertions and regression tests; do not branch on it in production code.
        /// The representations are indistinguishable from script — they differ only in speed and allocation,
        /// never in behavior — so a host that takes different code paths per representation is depending on
        /// something Jint is free to change underneath it.
        /// </para>
        /// <para>
        /// The motivating case: <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/> and <see cref="JsObject.CreateFromEntries(Engine, IEnumerable{KeyValuePair{string, JsValue}})"/>
        /// fall back to the dictionary representation silently and correctly when they cannot shape an
        /// object, and two of the triggers are not knowable at the call site (they depend on what the engine
        /// has already built). Without this method a host cannot write a test proving its objects are still
        /// being shaped, and a later change could deoptimize them unnoticed.
        /// </para>
        /// <para>
        /// This method observes without perturbing: it does not force a lazily initialized built-in to
        /// populate its properties, so such an object reports its settled representation only once something
        /// has touched it.
        /// </para>
        /// </remarks>
        /// <param name="value">The object to inspect. It must belong to this engine.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> belongs to a different engine.</exception>
        [Experimental(JintDiagnosticIds.NonContractDiagnostic)]
        public ObjectRepresentation GetObjectRepresentation(ObjectInstance value)
        {
            using var ownership = _engine.EnterHostCall();
            if (value is null)
            {
                Throw.ArgumentNullException(nameof(value));
            }

            if (!ReferenceEquals(value.Engine, _engine))
            {
                // Hidden classes are interned per engine and the fallback thresholds are per-engine counters,
                // so a representation question is only meaningful about the engine that built the object.
                // Rejecting a foreign object turns the mixed-up-engine mistake — easy to make in exactly the
                // multi-engine tests this API is written for — into a failure instead of a misleading pass.
                Throw.ArgumentException("The object belongs to a different engine.", nameof(value));
            }

            if ((value._type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                return ObjectRepresentation.HiddenClass;
            }

            if ((value._type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
            {
                return ObjectRepresentation.SharedBuiltinLayout;
            }

            // JsObject is the only type that can be in the hidden-class representation, and it is sealed, so
            // a JsObject that is not shaped is in the ordinary property-dictionary representation and nothing
            // else. Any other subclass may resolve own properties itself, which neither of those two names
            // would describe honestly.
            return value is JsObject ? ObjectRepresentation.Dictionary : ObjectRepresentation.Specialized;
        }

        /// <summary>
        /// Reports whether <paramref name="value"/>'s own string-keyed properties are described by a
        /// class shared with every other object of the same layout, rather than stored as individual
        /// descriptors in a per-object dictionary.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Unlike <see cref="GetObjectRepresentation"/>, this is part of Jint's compatibility contract.</b>
        /// The question it answers has a stable meaning — "does this object share its property layout with its
        /// siblings, so a script reading a batch of them keeps a monomorphic inline cache" — and a host may pin
        /// the answer in its own test suite. What may still change from release to release is which
        /// <i>construction paths</i> reach a shared layout: that set can only be expected to grow, and a host
        /// pins the documented success case of the API it called, not an incidental one. The three documented
        /// cases are <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/>,
        /// <see cref="JsObject.CreateFromEntries(Engine, IEnumerable{KeyValuePair{string, JsValue}})"/> and
        /// <see cref="JsObjectShape.Instantiate(Engine, ObjectInstance)"/>.
        /// </para>
        /// <para>
        /// The motivating case is a one-line adoption assertion. Those factories fall back to the per-object
        /// dictionary silently and correctly when they cannot shape an object, and two of the triggers are not
        /// knowable at the call site, so the only way to know the optimization was actually obtained is to ask:
        /// <code>
        /// var row = JsObject.CreateFromEntries(engine, fields);
        /// Assert.True(engine.Advanced.HasSharedShape(row));
        /// </code>
        /// </para>
        /// <para>
        /// A <see langword="false"/> answer is never a correctness problem. The representations are
        /// indistinguishable from script — they differ only in speed and allocation — so this is a performance
        /// assertion and nothing more, and production code must not branch on it.
        /// <see cref="GetObjectRepresentation"/> remains available as the finer-grained diagnostic naming
        /// <i>which</i> representation an object landed in; it is deliberately not a contract, and this
        /// predicate is the part of its answer that is.
        /// </para>
        /// <para>
        /// Like that diagnostic, this method observes without perturbing: it does not force a lazily
        /// initialized object to populate its properties. A built-in, and an object from
        /// <see cref="JsObjectShape.Instantiate(Engine, ObjectInstance)"/>, installs its shared layout on the
        /// first property access of any kind, so ask after something has touched the object.
        /// </para>
        /// </remarks>
        /// <param name="value">The object to inspect. It must belong to this engine.</param>
        /// <returns>
        /// <see langword="true"/> when the object's own string-keyed properties are served from a shared
        /// layout; <see langword="false"/> for the per-object property dictionary, and for any specialized
        /// object type — an array, a proxy, a CLR wrapper, a host-defined <see cref="ObjectInstance"/>
        /// subclass — whose own properties are that type's own business and share no layout with anything.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> belongs to a different engine.</exception>
        public bool HasSharedShape(ObjectInstance value)
        {
            // Answered through the diagnostic rather than beside it, so the two can never disagree about an
            // object: this predicate is exactly the shared-layout half of that enum. Listed positively, so a
            // representation added later is excluded until someone decides it belongs — a new member is far
            // more likely to name a specialized storage than a shared layout, and the safe default for a
            // contract is to keep answering what it always did. Both arms are internal type-flag tests, so
            // the call allocates nothing.
            var representation = GetObjectRepresentation(value);
            return representation is ObjectRepresentation.HiddenClass or ObjectRepresentation.SharedBuiltinLayout;
        }

        /// <summary>
        /// Reports the <see cref="PropertyAccessSemantics"/> the engine resolved for <paramref name="value"/> —
        /// derived from its runtime type, or declared through
        /// <see cref="ObjectInstance.SetPropertyAccessSemantics"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is a diagnostic, and it is not part of Jint's compatibility contract.</b> Use it in
        /// diagnostics, assertions and regression tests; do not branch on it in production code. For an object
        /// of a <i>host-defined</i> type the answer is exactly the semantics the engine's read lanes honor for
        /// that instance, and it is stable as long as the type is. For the engine's own types the answer names
        /// an internal classification which may be refined in any release without notice, and the enum may gain
        /// members; neither is a breaking change.
        /// </para>
        /// <para>
        /// The motivating case: the derivation is deliberately silent. A host type is
        /// <see cref="PropertyAccessSemantics.Exotic"/> because it overrides
        /// <see cref="ObjectInstance.Get(JsValue, JsValue)"/> and
        /// <see cref="PropertyAccessSemantics.Ordinary"/> because it does not, so an edit that removes the last
        /// <c>Get</c> override — or a refactor that moves it to a base class the probe cannot see as an
        /// override — flips the semantics of every read against that type with nothing to fail. Without this
        /// method a host can only pin the derivation indirectly, by instrumenting its own overrides and
        /// asserting probe counts.
        /// </para>
        /// <para>
        /// It reports the resolved <see cref="PropertyAccessSemantics"/> and nothing else. In particular it
        /// does not report whether the type overrides <c>TryGetOwnPropertyValue</c>, which is an orthogonal
        /// routing decision a host observes directly by instrumenting that override.
        /// </para>
        /// </remarks>
        /// <param name="value">The object to inspect. It must belong to this engine.</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="value"/> belongs to a different engine.</exception>
        public PropertyAccessSemantics GetPropertyAccessSemantics(ObjectInstance value)
        {
            using var ownership = _engine.EnterHostCall();
            if (value is null)
            {
                Throw.ArgumentNullException(nameof(value));
            }

            if (!ReferenceEquals(value.Engine, _engine))
            {
                // The resolved semantics happen not to depend on the engine, but rejecting a foreign object
                // keeps this diagnostic consistent with GetObjectRepresentation and turns the mixed-up-engine
                // mistake — easy to make in exactly the multi-engine tests these APIs are written for — into a
                // failure instead of a misleading pass.
                Throw.ArgumentException("The object belongs to a different engine.", nameof(value));
            }

            // Ordinary is the absence of the exotic claim, not a flag of its own: an in-box object built through
            // the internal constructor carries neither OrdinaryGet nor ExoticGet, and ordinary is what such an
            // object has. Only ExoticGet — set by the built-ins that deviate, derived for a host type that
            // overrides Get, and settable either way through SetPropertyAccessSemantics — can move the answer.
            return (value._type & InternalTypes.ExoticGet) != InternalTypes.Empty
                ? PropertyAccessSemantics.Exotic
                : PropertyAccessSemantics.Ordinary;
        }

        /// <summary>
        /// Reports how many CLR arrays this engine has converted for script, and under which semantics.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is a diagnostic, and it is not part of Jint's compatibility contract.</b> Exactly which
        /// internal events are counted may be refined in any release, and
        /// <see cref="InteropConversionDiagnostics"/> may gain members; neither is a breaking change. Use it
        /// for audits, assertions and regression tests, never for production branching.
        /// </para>
        /// <para>
        /// The motivating case: <see cref="ArrayConversionMode.Copy"/> is the default, while
        /// <see cref="ArrayConversionMode.LiveView"/> is an explicit performance opt-in, and the two modes differ
        /// in behavior a script can observe — a live view writes through to the CLR array and tracks its contents,
        /// while a copy does neither. A host that does not own all of its Jint consumers has no way to find out
        /// whether any CLR array crosses into script at all, let alone under which semantics, short of auditing
        /// every transitive caller by hand. Reading the counters around a run answers it directly.
        /// </para>
        /// <para>
        /// Scope, stated honestly. What is counted is a conversion that actually went through the
        /// array-conversion lane, which is not quite the same as "a CLR array reached script". Under
        /// <see cref="ArrayConversionMode.LiveView"/> an array crossing under a non-array <i>declared</i>
        /// type — <c>IReadOnlyList&lt;T&gt;</c>, <c>IEnumerable&lt;T&gt;</c> — honors that declared contract
        /// through the ordinary wrapper lane instead, converts no array, and is not counted; under
        /// <see cref="ArrayConversionMode.Copy"/> the same value does take the copy lane and is counted. A
        /// re-crossing served from an identity cache is never counted, since a cache hit performs no
        /// conversion and implies an earlier one that was — which is what "did arrays cross, and how" needs
        /// to know.
        /// </para>
        /// <para>
        /// The counters are cumulative for the life of the engine and never reset. Two engines count
        /// independently, so a host pooling engines should read them per engine.
        /// </para>
        /// </remarks>
        [Experimental(JintDiagnosticIds.NonContractDiagnostic)]
        public InteropConversionDiagnostics GetInteropConversionDiagnostics()
        {
            using var ownership = _engine.EnterHostCall();
            return new(_engine._arrayLiveViewConversions, _engine._arrayCopyConversions);
        }

        /// <summary>
        /// Counts what this engine is currently holding on to — global bindings, queued and scheduled work,
        /// the handler-tree and interop caches, the object pools, and a bounded census of the object graph
        /// reachable from <c>globalThis</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>This is a diagnostic, and it is not part of Jint's compatibility contract.</b> Which internal
        /// collections are counted, and how, may be refined in any release, and
        /// <see cref="EngineMemoryReport"/> and the report types nested in it may gain members. Neither is a
        /// breaking change, and neither will be treated as one. Use it for audits, assertions, logging and
        /// regression tests; do not branch on it in production code.
        /// </para>
        /// <para>
        /// <b>The motivating case is the pooled engine.</b> An engine reused across requests keeps things a
        /// fresh one never had: warmed handler trees (and through them one retained receiver per warmed
        /// member-read site and one retained callee per warmed call site), a module registry that
        /// <see cref="RestoreGlobalSnapshot"/> deliberately does not revert, timers a previous cycle
        /// scheduled, globals a script installed. Each of those is documented, and none of them was
        /// <em>observable</em> — a host that suspected its pool was accumulating had no way to ask, short of
        /// attaching a heap profiler to production. This is the ask.
        /// </para>
        /// <para>
        /// <b>Counts, not bytes.</b> Jint does not track the allocation size of what it builds, and this
        /// report will not invent one; see <see cref="EngineMemoryReport"/> for the reasoning and for what
        /// each figure means exactly. The one number that comes close to a size,
        /// <see cref="PoolReport.PooledJsValueArraySlots"/>, is a slot count and says so.
        /// </para>
        /// <para>
        /// <b>It observes without perturbing.</b> No getter is invoked, no lazy property factory is run, and
        /// no built-in's function object is created in order to be counted — so an engine that has never
        /// touched <c>Array</c> still reports it as untouched afterwards, and two consecutive calls on an
        /// idle engine produce two equal reports. The report types are records, so that is directly
        /// assertable. The engine carries no state for any of this: the report is derived on demand from
        /// collections that already exist, and an engine nobody asks pays nothing.
        /// </para>
        /// <para>
        /// <b>Threading.</b> Call it from the thread that owns the engine, like everything else on
        /// <see cref="Engine"/>, and not from inside a running evaluation — it walks collections a script
        /// would be mutating underneath it. <see cref="EngineMemoryReport.EventLoopQueueDepth"/> is the one
        /// figure another thread can change while it is read, since a
        /// <see cref="System.Threading.Tasks.Task"/> completing on the thread pool enqueues its settle from
        /// there.
        /// </para>
        /// <para>
        /// <b>Cost.</b> Everything but the census is a handful of field reads. The census is a breadth-first
        /// walk bounded by <paramref name="objectCensusBound"/>, so it is linear in that bound and allocates
        /// a visited set of at most that size; the default is meant to be cheap enough to call between
        /// requests, and a host logging on every request should still measure it against its own budget.
        /// </para>
        /// </remarks>
        /// <example>
        /// The shape a pooling host uses it in — compare a pooled engine against itself over time, not
        /// against an absolute figure:
        /// <code>
        /// var before = engine.Advanced.GetMemoryReport();
        /// RunRequest(engine);
        /// engine.Advanced.RestoreGlobalSnapshot(snapshot);
        /// var after = engine.Advanced.GetMemoryReport();
        /// // A module registry or a global-property count that climbs every request is the leak.
        /// </code>
        /// </example>
        /// <param name="objectCensusBound">
        /// How many distinct objects the census may visit before it stops.
        /// <see cref="ObjectCensusReport.BoundReached"/> reports whether it did stop, in which case the
        /// category counts are a lower bound. Zero or less skips the census entirely, which is what a host
        /// that only wants the cheap counts should pass.
        /// </param>
        /// <returns>The counts, as of the moment of the call.</returns>
        [Experimental(JintDiagnosticIds.NonContractDiagnostic)]
        public EngineMemoryReport GetMemoryReport(int objectCensusBound = 10_000)
            => EngineMemoryReportBuilder.Build(_engine, objectCensusBound);

        /// <summary>
        /// Installs a global on <em>this</em> engine whose value is produced the first time script reads it,
        /// instead of now. The per-engine counterpart of
        /// <see cref="OptionsExtensions.AddLazyGlobal(Options, string, Func{Engine, JsValue}, PropertyFlag)"/>,
        /// for the values a host cannot know until after the engine exists — per-request data, a scoped
        /// service provider, a workflow context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The property is installed eagerly, so existence checks and enumeration — <c>in</c>,
        /// <c>hasOwnProperty</c>, <c>Object.keys(globalThis)</c>, <c>Object.getOwnPropertyNames</c> — see it
        /// immediately without materializing anything. Only its value is left unresolved:
        /// <paramref name="valueFactory"/> runs on the first read of that value, and the produced value is
        /// stored in the descriptor so subsequent reads are ordinary property reads.
        /// <c>typeof</c> counts as a read, since it has to inspect the value to name its type. Once per
        /// engine, then — with the single exception of
        /// <see cref="RestoreGlobalSnapshot"/>, which returns an unread global to its unmaterialized state
        /// on purpose, so the factory runs again on the next read. What that second run produces is the
        /// factory's business and not the restore's: one that constructs gives the next cycle a fresh value,
        /// one that hands back something it is holding gives the next cycle what the previous one mutated.
        /// </para>
        /// <para>
        /// A script that <b>deletes</b> the global before ever reading it (<c>delete globalThis.name</c>,
        /// requires <see cref="PropertyFlag.Configurable"/>) removes the descriptor outright, and the factory
        /// never runs. A script that <b>overwrites or redefines</b> it first does still run the factory once
        /// and then discard the result: <c>[[Set]]</c> on the global object funnels into
        /// <c>[[DefineOwnProperty]]</c>, whose ValidateAndApplyPropertyDescriptor step reads the current
        /// value before replacing it. The end state is correct in every case — the script's value wins — the
        /// laziness is simply not preserved through that particular sequence.
        /// </para>
        /// <para>
        /// <b>Differences from the <see cref="Options"/> registration.</b> That one is recorded on a
        /// configuration object which may be shared by any number of engines, so its factories must not
        /// capture anything engine-affine. This one belongs to one engine and receives it, so its factory
        /// <em>may</em> close over that engine's <see cref="JsValue"/>s and over per-request host state — the
        /// case the options-time API cannot express. It also replaces any existing global of that name,
        /// including a built-in, rather than being applied once during construction.
        /// </para>
        /// <para>
        /// <b>When to call it.</b> Any time between evaluations, under the engine's usual single-thread
        /// contract; an <see cref="Engine"/> is not thread-safe and this is an ordinary mutation of its
        /// global object. Installing during an evaluation — from a host callback the script invoked — is not
        /// prevented, but the read that is already in flight may have resolved the old binding.
        /// </para>
        /// <para>
        /// <b>Interaction with <see cref="CaptureGlobalSnapshot"/>.</b> A restore returns the binding to its
        /// state at capture, which cuts both ways: a global installed after the capture is gone after the
        /// restore, and one installed before it whose factory had <em>not</em> yet run at capture time is
        /// returned to that unmaterialized state, so the factory <b>runs again</b> on the next read. That is
        /// a contract rather than an artifact — it is what lets a pooled engine keep the laziness across
        /// evaluations, and a host that reuses engines depends on it to stop one request's value being
        /// served to the next. A factory whose result must survive a restore has to be installed after it.
        /// </para>
        /// </remarks>
        /// <example>
        /// The per-request shape the <see cref="Options"/> registration cannot express, since the values are
        /// only known once the request is in hand:
        /// <code>
        /// engine.Advanced.AddLazyGlobal("user", _ => JsValue.FromObject(engine, request.User));
        /// engine.Advanced.AddLazyGlobal("db", e => ObjectWrapper.Create(e, scope.ServiceProvider.GetRequiredService&lt;IDb&gt;()));
        /// </code>
        /// Neither the user projection nor the database wrapper is built for a script that never mentions
        /// the name.
        /// </example>
        /// <param name="name">The global property name.</param>
        /// <param name="valueFactory">
        /// Produces the value, given this engine. Invoked lazily, so it may use anything the engine exposes.
        /// A <see langword="null"/> return is replaced by <see cref="JsValue.Undefined"/>, so that it cannot
        /// silently turn into a factory that re-runs on every read.
        /// </param>
        /// <param name="flags">
        /// Property attributes; defaults to the configurable/enumerable/writable combination that
        /// <see cref="Engine.SetValue(string, JsValue)"/> produces — <b>not</b> the
        /// <see cref="PropertyFlag.NonEnumerable"/> that <see cref="Engine.SetValue(string, Delegate)"/> uses.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> or
        /// <paramref name="valueFactory"/> is <see langword="null"/>.</exception>
        public void AddLazyGlobal(
            string name,
            Func<Engine, JsValue> valueFactory,
            PropertyFlag flags = PropertyFlag.ConfigurableEnumerableWritable)
        {
            using var ownership = _engine.EnterHostCall();
            if (name is null)
            {
                Throw.ArgumentNullException(nameof(name));
            }

            if (valueFactory is null)
            {
                Throw.ArgumentNullException(nameof(valueFactory));
            }

            var engine = _engine;

            // SetProperty, exactly as the options-time registration uses, and for a reason that only bites
            // here: unlike a registration applied during construction, this one lands on an engine whose
            // handler trees may already hold a resolved binding for this very name. Every storage path
            // SetProperty can take — replacing a slot of the global's shared built-in layout, adding to the
            // hybrid side dictionary, plain dictionary store, and the deopt fallback — bumps
            // _propertiesVersion, which is the sole thing the global-identifier and member-read inline
            // caches revalidate against. Installing through anything that skipped that bump would leave a
            // warmed read site serving the previous binding forever.
            engine.Realm.GlobalObject.SetProperty(name, new LazyPropertyDescriptor<Engine>(engine, valueFactory, flags));
        }

        /// <summary>
        /// Declares a lazy global whose factory is handed a value the caller supplies, so that the factory can
        /// be a <see langword="static"/> lambda instead of a closure. Behaves in every other way exactly like
        /// <see cref="AddLazyGlobal(string, Func{Engine, JsValue}, PropertyFlag)"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This overload exists for allocation, not for expressiveness — anything it can express, a closure
        /// could. The case it serves is a host that installs its globals <em>per engine</em>, which is the
        /// whole reason the per-engine API exists: the values depend on request state, so the registration
        /// cannot be hoisted onto <see cref="Options"/> and runs again for every engine. A capturing lambda
        /// then costs a display class and a delegate per global per engine, which for a host exposing a few
        /// dozen globals is a per-request cost with nothing to show for it. Passing the state through instead
        /// leaves the descriptor as the only allocation.
        /// </para>
        /// <para>
        /// Use a value tuple to pass more than one thing:
        /// <c>AddLazyGlobal("db", (services, key), static (e, s) =&gt; Wrap(e, s.services, s.key))</c>. Keep
        /// the factory <see langword="static"/>; a lambda that still captures gives the allocation straight
        /// back and the overload buys nothing.
        /// </para>
        /// <para>
        /// There is deliberately no <see cref="Options"/> counterpart. A registration made there is recorded
        /// once and merely replayed per engine, so its closure is a one-off for the process and the
        /// per-engine cost is already just the descriptor.
        /// </para>
        /// </remarks>
        /// <typeparam name="TState">The type of the value handed back to the factory.</typeparam>
        /// <param name="name">The global property name.</param>
        /// <param name="state">Passed to <paramref name="valueFactory"/> unchanged when it runs.</param>
        /// <param name="valueFactory">
        /// Produces the value, given this engine and <paramref name="state"/>. Invoked lazily, once per
        /// materialization — so once, unless <see cref="RestoreGlobalSnapshot"/> re-arms the global. A
        /// <see langword="null"/> return is replaced by <see cref="JsValue.Undefined"/>.
        /// </param>
        /// <param name="flags">Property attributes; see the non-generic overload.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> or
        /// <paramref name="valueFactory"/> is <see langword="null"/>.</exception>
        public void AddLazyGlobal<TState>(
            string name,
            TState state,
            Func<Engine, TState, JsValue> valueFactory,
            PropertyFlag flags = PropertyFlag.ConfigurableEnumerableWritable)
        {
            using var ownership = _engine.EnterHostCall();
            if (name is null)
            {
                Throw.ArgumentNullException(nameof(name));
            }

            if (valueFactory is null)
            {
                Throw.ArgumentNullException(nameof(valueFactory));
            }

            var engine = _engine;

            // The resolver is static, so it is cached once per TState instantiation rather than allocated
            // here, and the state rides inside the descriptor's own field. See the SetProperty note above for
            // why the installation has to go through it.
            engine.Realm.GlobalObject.SetProperty(
                name,
                new LazyPropertyDescriptor<EngineAndState<TState>>(
                    new EngineAndState<TState>(engine, state, valueFactory),
                    static s => s.Factory(s.Engine, s.State),
                    flags));
        }

        /// <summary>
        /// The <c>[[HostDefined]]</c> field of this engine's principal Realm Record — the realm
        /// <c>InitializeHostDefinedRealm</c> created when the engine was constructed. The specification
        /// reserves it for "hosts that need to associate additional information with a Realm Record", and the
        /// engine never reads or interprets what is put there.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The motivating case.</b> Every host-facing factory in this API receives the engine and nothing
        /// else. <see cref="AddLazyGlobal(string, Func{Engine, JsValue}, PropertyFlag)"/> can close over
        /// per-request state because it runs on a live engine, but its
        /// <see cref="OptionsExtensions.AddLazyGlobal(Options, string, Func{Engine, JsValue}, PropertyFlag)"/>
        /// counterpart cannot: one <see cref="Options"/> instance is shared by every engine built from it, so a
        /// factory recorded there must not capture anything engine-affine. A host can sidestep that by building
        /// a fresh <see cref="Options"/> per scope and letting its factories close over it, which is supported
        /// and cheap — but it costs an <see cref="Options"/> per request and gives up the process-wide instance
        /// most embedders actually have. Keeping the shared instance used to leave only an out-of-band side
        /// table keyed by engine: real embedders keep a
        /// <c>static ConditionalWeakTable&lt;Engine, IServiceProvider&gt;</c> for precisely this, and pay its
        /// internal write lock on every evaluation of every tenant in the process.
        /// </para>
        /// <para>
        /// <b>Which realm.</b> The <em>principal</em> one, not the current one. <see cref="Engine.Intrinsics"/>
        /// and <see cref="Engine.Global"/> follow the running execution context's realm and therefore change
        /// identity inside a <c>ShadowRealm</c> evaluation, which is correct for them — they name the current
        /// Realm Record. This does not move: it answers the same value from inside a shadow-realm callback as
        /// outside, which is what a host attaching "the request this engine is serving" means.
        /// </para>
        /// <para>
        /// A shadow realm is a distinct Realm Record and gets its own <c>[[HostDefined]]</c>, empty by
        /// specification — <c>ShadowRealm</c> step 4 performs <c>InitializeHostDefinedRealm()</c>, whose new
        /// record defaults the field to <c>undefined</c>. That is deliberate isolation rather than an
        /// oversight: propagating the outer request's services into sandboxed code would be exactly the
        /// ambient authority a shadow realm exists to withhold. A host that does want to populate it has the
        /// hook the specification provides for it, <see cref="Runtime.Host.InitializeShadowRealm"/>.
        /// </para>
        /// <para>
        /// <b>Lifetime.</b> The principal realm is reachable only from the base execution context, which is
        /// pushed once and never popped, so the value dies with the engine — the difference from a
        /// process-wide side table, which keeps whatever bookkeeping its own entries need. Nothing in the
        /// engine clears it: <see cref="RestoreGlobalSnapshot"/> reverts global bindings, lexical declarations
        /// and transient per-evaluation state, and does not touch it, so an engine pooled across requests keeps
        /// its state through a restore and the host decides when to replace it — typically right after
        /// restoring. <see cref="Engine.Dispose"/> does not clear it either; it releases interop caches the
        /// engine built, not state the host attached.
        /// </para>
        /// <para>
        /// An <see cref="Engine"/> is not thread-safe and this is an ordinary reference field: set it from the
        /// thread that owns the engine, like everything else.
        /// </para>
        /// </remarks>
        /// <example>
        /// The shape the options-time registration cannot otherwise express — a process-wide
        /// <see cref="Options"/> whose factories capture nothing, with the per-request part supplied per engine:
        /// <code>
        /// // once per process
        /// var options = new Options().AddLazyGlobal("db", static e =>
        ///     JsValue.FromObject(e, ((IServiceProvider) e.Advanced.HostDefined!).GetRequiredService&lt;IDb&gt;()));
        ///
        /// // once per request
        /// var engine = new Engine(options);
        /// engine.Advanced.HostDefined = scope.ServiceProvider;
        /// </code>
        /// The ordering works because the factory is lazy: it runs on the first script read of <c>db</c>, which
        /// is necessarily after the constructor returned and the state was attached. An
        /// <see cref="OptionsExtensions.Configure"/> callback runs <em>during</em> construction, and can set
        /// this but must not expect a later request's value to be visible to it.
        /// <para>
        /// The engine stores the value verbatim, so the natural retrieval is a type pattern:
        /// <c>if (engine.Advanced.HostDefined is RequestContext ctx)</c>.
        /// </para>
        /// </example>
        public object? HostDefined
        {
            get
            {
                using var ownership = _engine.EnterHostCall();
                return _engine._mainRealm.HostDefined;
            }
            set
            {
                using var ownership = _engine.EnterHostCall();
                _engine._mainRealm.HostDefined = value;
            }
        }

        /// <summary>
        /// Event raised when a promise is rejected without a handler (operation = Reject),
        /// or when a handler is added to a previously unhandled rejected promise (operation = Handle).
        /// This implements the HostPromiseRejectionTracker abstract operation from the TC39 spec.
        /// </summary>
        /// <remarks>
        /// https://tc39.es/ecma262/#sec-hostpromiserejectiontracker
        /// </remarks>
        public event EventHandler<PromiseRejectionTrackerEventArgs>? PromiseRejectionTracker;

        internal void RaisePromiseRejectionTracker(JsPromise promise, PromiseRejectionOperation operation)
        {
            PromiseRejectionTracker?.Invoke(_engine, new PromiseRejectionTrackerEventArgs(promise, operation));
        }
    }
}

/// <summary>
/// The internal representation an object uses for its own string-keyed properties, as reported by
/// <see cref="Engine.AdvancedOperations.GetObjectRepresentation"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>These values describe an internal representation and are not part of Jint's compatibility
/// contract.</b> Which representation a given object ends up in may change in any release, and this enum
/// may gain members. Neither is a breaking change, and neither will be treated as one.
/// </para>
/// <para>
/// The representations are indistinguishable from script; they differ only in speed and allocation. This
/// enum therefore exists for diagnostics and tests, and host code must not branch on it in production. To
/// <i>assert</i> that an optimization asked for actually happened, use
/// <see cref="Engine.AdvancedOperations.HasSharedShape"/> instead: it answers the one question a host cares
/// about — is this object's layout shared with its siblings — and unlike this enum that answer is part of
/// the compatibility contract and may be pinned in a test suite. What remains here is the finer-grained
/// diagnostic naming <i>which</i> representation an object landed in, for the times a false answer has to be
/// explained.
/// </para>
/// <para>
/// That non-contract status is declared to the compiler as <c>JINT0001</c>; see
/// <see cref="JintDiagnosticIds"/> for how a host acknowledges it.
/// </para>
/// </remarks>
[Experimental(JintDiagnosticIds.NonContractDiagnostic)]
public enum ObjectRepresentation
{
    /// <summary>
    /// A plain object whose own string-keyed properties are stored as individual descriptors in a
    /// per-object property dictionary. This is the ordinary representation, and the one
    /// <see cref="HiddenClass"/> degrades to whenever it meets something it cannot express. The dictionary
    /// is allocated lazily, so a plain object with no properties yet is already in this representation.
    /// </summary>
    Dictionary = 0,

    /// <summary>
    /// A plain object whose own string-keyed properties are described by a hidden class shared with every
    /// other object of the same layout, leaving only the values stored per object. This is what
    /// <c>JsObject.Create</c> and <c>JsObject.CreateFromEntries</c> aim for, and what an
    /// ordinary object literal reaches.
    /// </summary>
    HiddenClass = 1,

    /// <summary>
    /// A built-in whose own string-keyed properties come from a layout shared by every instance of its
    /// type, with the values resolved per realm. Built-ins install this lazily on first use, and a built-in
    /// mutated in a way the shared layout cannot express stops using it.
    /// </summary>
    SharedBuiltinLayout = 2,

    /// <summary>
    /// The object is not a plain object: it is an instance of a specialized object type — an array, a typed
    /// array, a proxy, a CLR object wrapper, a built-in, or a host-defined
    /// <see cref="Jint.Native.Object.ObjectInstance"/> subclass. Where such a type keeps its own properties
    /// is that type's business — it may resolve them itself, use the ordinary property dictionary, or both —
    /// so none of the other members would describe it honestly.
    /// </summary>
    Specialized = 3,
}

/// <summary>
/// Counts of the CLR-array interop conversions an engine has performed, as reported by
/// <see cref="Engine.AdvancedOperations.GetInteropConversionDiagnostics"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>These counts describe internal events and are not part of Jint's compatibility contract.</b> Which
/// events are counted may be refined in any release, and this type may gain members. Neither is a breaking
/// change, and neither will be treated as one — which is why the constructor is internal: a host reads the
/// properties it knows about and is unaffected when another appears.
/// </para>
/// <para>
/// See <see cref="Engine.AdvancedOperations.GetInteropConversionDiagnostics"/> for what falls inside and
/// outside the counted set.
/// </para>
/// <para>
/// That non-contract status is declared to the compiler as <c>JINT0001</c>; see
/// <see cref="JintDiagnosticIds"/> for how a host acknowledges it.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[Experimental(JintDiagnosticIds.NonContractDiagnostic)]
public readonly record struct InteropConversionDiagnostics
{
    internal InteropConversionDiagnostics(long arrayLiveViewConversions, long arrayCopyConversions)
    {
        ArrayLiveViewConversions = arrayLiveViewConversions;
        ArrayCopyConversions = arrayCopyConversions;
    }

    /// <summary>
    /// Live wrapper views created over a CLR array under <see cref="ArrayConversionMode.LiveView"/>. Such a
    /// view reads and writes the CLR array itself, so script sees later changes to it and the host sees
    /// script's writes.
    /// </summary>
    public long ArrayLiveViewConversions { get; }

    /// <summary>
    /// Snapshot copies of a CLR array created under <see cref="ArrayConversionMode.Copy"/>, or under
    /// <see cref="ArrayConversionMode.LiveView"/> where no view could be built — a custom
    /// <c>WrapObjectHandler</c> declining the value. The resulting JavaScript array is disconnected from the
    /// CLR array in both directions.
    /// </summary>
    public long ArrayCopyConversions { get; }
}

/// <summary>
/// Event arguments for the PromiseRejectionTracker event.
/// </summary>
public sealed class PromiseRejectionTrackerEventArgs : EventArgs
{
    /// <summary>
    /// The promise that triggered the rejection tracking.
    /// </summary>
    public JsValue Promise { get; }

    /// <summary>
    /// The rejection reason (only meaningful when Operation is Reject).
    /// </summary>
    public JsValue? Value { get; }

    /// <summary>
    /// Whether this is a new unhandled rejection ("Reject") or a previously
    /// unhandled rejection that now has a handler ("Handle").
    /// </summary>
    public PromiseRejectionOperation Operation { get; }

    internal PromiseRejectionTrackerEventArgs(JsPromise promise, PromiseRejectionOperation operation)
    {
        Promise = promise;
        Value = promise.State == PromiseState.Rejected ? promise.Value : null;
        Operation = operation;
    }
}
