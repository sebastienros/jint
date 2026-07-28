using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint;

public partial class Engine
{
    public AdvancedOperations Advanced { get; }

    public partial class AdvancedOperations
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
            _engine.ResetCallStack();
        }

        /// <summary>
        /// Forcefully processes the current task queues (micro and regular), this API may break and change behavior!
        /// </summary>
        public void ProcessTasks()
        {
            _engine.RunAvailableContinuations();
        }

        /// <summary>
        /// EXPERIMENTAL! Subject to change.
        ///
        /// Registers a promise within the currently running EventLoop (has to be called within "ExecuteWithEventLoop" call).
        /// Note that ExecuteWithEventLoop will not trigger "onFinished" callback until ALL manual promises are settled.
        ///
        /// NOTE: that resolve and reject need to be called withing the same thread as "ExecuteWithEventLoop".
        /// The API assumes that the Engine is called from a single thread.
        /// </summary>
        /// <returns>a Promise instance and functions to either resolve or reject it</returns>
        public ManualPromise RegisterPromise()
        {
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
        public ObjectRepresentation GetObjectRepresentation(ObjectInstance value)
        {
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
        /// The motivating case: <see cref="ArrayConversionMode.LiveView"/> has been the default since 4.14, and
        /// the two modes differ in behavior a script can observe — a live view writes through to the CLR array
        /// and tracks its contents, a copy does neither. A host that does not own all of its Jint consumers has
        /// no way to find out whether any CLR array crosses into script at all, let alone under which
        /// semantics, short of auditing every transitive caller by hand. Reading the counters around a run
        /// answers it directly.
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
        public InteropConversionDiagnostics GetInteropConversionDiagnostics()
            => new(_engine._arrayLiveViewConversions, _engine._arrayCopyConversions);

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
/// enum therefore exists for diagnostics and tests — so that host code can assert an optimization it asked
/// for actually happened — and host code must not branch on it in production.
/// </para>
/// </remarks>
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
/// </remarks>
[StructLayout(LayoutKind.Auto)]
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
