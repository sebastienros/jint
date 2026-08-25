using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Jint.Diagnostics;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint;

public partial class Engine
{
    private DiagnosticOperations? _diagnostics;

    /// <summary>
    /// Gets what this engine can be asked about itself, and the profiler and coverage counters that
    /// produce the rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing here changes what a script computes, which is the line between this facet and
    /// <see cref="Advanced"/>.
    /// </para>
    /// <para>
    /// Three of its members are declared non-contracts and say so to the compiler as <c>JINT0001</c>; see
    /// <see cref="JintDiagnosticIds"/>. Created on first access, so an engine nobody asks never allocates
    /// one.
    /// </para>
    /// </remarks>
    public DiagnosticOperations Diagnostics => _diagnostics ??= new DiagnosticOperations(this);

    /// <summary>
    /// The one classification that <see cref="DiagnosticOperations.GetObjectRepresentation"/> and
    /// <see cref="AdvancedOperations.HasSharedShape"/> both answer from, so the diagnostic and the
    /// contract predicate can never disagree about an object. Both arms are internal type-flag tests, so
    /// it allocates nothing, and it observes without perturbing: a lazily initialized object reports its
    /// settled representation only once something has touched it.
    /// </summary>
    private static ObjectRepresentation ClassifyRepresentation(Engine engine, ObjectInstance value)
    {
        if (value is null)
        {
            Throw.ArgumentNullException(nameof(value));
        }

        if (!ReferenceEquals(value.Engine, engine))
        {
            // Hidden classes are interned per engine and the fallback thresholds are per-engine counters,
            // so a representation question is only meaningful about the engine that built the object.
            // Rejecting a foreign object turns the mixed-up-engine mistake — easy to make in exactly the
            // multi-engine tests these APIs are written for — into a failure instead of a misleading pass.
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
    /// What an <see cref="Engine"/> can be asked about itself, and the instruments that produce it.
    /// </summary>
    public sealed partial class DiagnosticOperations
    {
        private readonly Engine _engine;

        internal DiagnosticOperations(Engine engine)
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
            return ClassifyRepresentation(_engine, value);
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
        /// <see cref="AdvancedOperations.RestoreGlobalSnapshot"/> deliberately does not revert, timers a previous cycle
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
        /// var before = engine.Diagnostics.GetMemoryReport();
        /// RunRequest(engine);
        /// engine.Advanced.RestoreGlobalSnapshot(snapshot);
        /// var after = engine.Diagnostics.GetMemoryReport();
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
    }
}

/// <summary>
/// The internal representation an object uses for its own string-keyed properties, as reported by
/// <see cref="Engine.DiagnosticOperations.GetObjectRepresentation"/>.
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
/// <see cref="Engine.DiagnosticOperations.GetInteropConversionDiagnostics"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>These counts describe internal events and are not part of Jint's compatibility contract.</b> Which
/// events are counted may be refined in any release, and this type may gain members. Neither is a breaking
/// change, and neither will be treated as one — which is why the constructor is internal: a host reads the
/// properties it knows about and is unaffected when another appears.
/// </para>
/// <para>
/// See <see cref="Engine.DiagnosticOperations.GetInteropConversionDiagnostics"/> for what falls inside and
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
