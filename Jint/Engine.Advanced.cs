using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint;

public partial class Engine
{
    private AdvancedOperations? _advanced;

    /// <summary>
    /// Gets the operations that have no counterpart in the ECMAScript object model: the live call stack,
    /// property layouts, .NET proxy traps, and global snapshots.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name means "past the model", not "difficult". The host loop is <see cref="Tasks"/>, the reports
    /// are <see cref="Diagnostics"/>, and the web-platform bridge is <c>WebApi</c>.
    /// </para>
    /// <para>
    /// Created on first access, so an engine that never asks for it never allocates one.
    /// </para>
    /// </remarks>
    public AdvancedOperations Advanced => _advanced ??= new AdvancedOperations(this);

    /// <summary>
    /// The operations on an <see cref="Engine"/> that have no counterpart in the ECMAScript object model.
    /// </summary>
    public sealed partial class AdvancedOperations
    {
        private readonly Engine _engine;

        internal AdvancedOperations(Engine engine)
        {
            _engine = engine;
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
        /// Gets the names of the principal realm's global lexical bindings, in declaration order.
        /// </summary>
        /// <remarks>
        /// <para>
        /// These are the <c>let</c>, <c>const</c> and <c>class</c> declarations top-level script made, which
        /// the specification keeps in the global environment record rather than on the global object — so
        /// <c>globalThis</c> carries none of them, and neither <see cref="Engine.SetValue(string, JsValue)"/>
        /// nor a <c>var</c> nor a function declaration adds one.
        /// </para>
        /// <para>
        /// Names only: read a value with <see cref="Engine.Evaluate(string, string, ScriptParsingOptions)"/> over the name. A binding whose
        /// declaration has been hoisted but not yet evaluated is named too, and evaluating it throws a
        /// <c>ReferenceError</c>.
        /// </para>
        /// <para>
        /// Each call answers a fresh list, so a declaration made afterwards does not change one already
        /// handed out. <see cref="AdvancedOperations.RestoreGlobalSnapshot(GlobalSnapshot)"/> clears the
        /// bindings, and <see cref="Diagnostics.EngineMemoryReport.LexicalGlobalBindingCount"/> counts them.
        /// </para>
        /// </remarks>
        /// <returns>The binding names, in the order they were declared.</returns>
        public IReadOnlyList<string> GetGlobalLexicalNames()
        {
            using var ownership = _engine.EnterHostCall();

            // The principal realm, not the current one: inside a ShadowRealm evaluation `engine.Realm` names
            // the shadow realm, and this answers a question about the engine. Same reading as the memory
            // report's LexicalGlobalBindingCount, so the count and the names can never disagree.
            return _engine._mainRealm.GlobalEnv._declarativeRecord.GetAllBindingNames();
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
        /// Reports whether <paramref name="value"/>'s own string-keyed properties are described by a
        /// class shared with every other object of the same layout, rather than stored as individual
        /// descriptors in a per-object dictionary.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Unlike <see cref="DiagnosticOperations.GetObjectRepresentation"/>, this is part of Jint's compatibility contract.</b>
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
        /// <see cref="DiagnosticOperations.GetObjectRepresentation"/> remains available as the finer-grained diagnostic naming
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
            using var ownership = _engine.EnterHostCall();

            // Answered from the same classification the diagnostic answers from, so the two can never
            // disagree about an object: this predicate is exactly the shared-layout half of that enum. Listed
            // positively, so a representation added later is excluded until someone decides it belongs — a
            // new member is far more likely to name a specialized storage than a shared layout, and the safe
            // default for a contract is to keep answering what it always did. Both arms are internal
            // type-flag tests, so the call allocates nothing.
            var representation = ClassifyRepresentation(_engine, value);
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
    }
}
