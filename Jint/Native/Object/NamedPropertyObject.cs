using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Base class for a host-defined object whose <em>named</em> properties are projected from native state — a
/// record, a settings bag, a document, a live view over host data. The indexed counterpart is
/// <see cref="ArrayLikeObject"/>, which publishes the same named hooks beside its indexed ones.
/// </summary>
/// <remarks>
/// <para>
/// A subclass supplies three members — <see cref="NameCount"/>, <see cref="NameAt"/> and
/// <see cref="TryGetNamedValue"/> — and this class derives the whole JS-visible property model from them,
/// keeping <c>GetOwnProperty</c>, <c>TryGetOwnPropertyValue</c>, <c>ProbeOwnProperty</c>, both key
/// enumerations, <c>HasProperty</c>, <c>Set</c>, <c>Delete</c> and <c>DefineOwnProperty</c> mutually
/// consistent. All three are re-consulted on every operation, so a projection that gains or loses names
/// between reads is observed live.
/// </para>
/// <para>
/// <b>Five optional hooks refine it</b>, each defaulting to what a projection with no native support for that
/// question answers, so adding one is pure opt-in and adding none leaves a read-only record:
/// </para>
/// <list type="table">
/// <listheader><term>hook</term><description>what it decides, and its default</description></listheader>
/// <item>
///   <term><see cref="HasName"/></term>
///   <description>existence without producing a value; defaults to asking
///   <see cref="TryGetNamedValue"/> and discarding it.</description>
/// </item>
/// <item>
///   <term><see cref="IsNameEnumerable"/></term>
///   <description>whether the name is enumerable; defaults to <see langword="true"/>.</description>
/// </item>
/// <item>
///   <term><see cref="IsNameWritable"/></term>
///   <description>whether the name reports <c>writable: true</c> and therefore routes assignment to the host;
///   defaults to <see langword="false"/>.</description>
/// </item>
/// <item>
///   <term><see cref="TrySetNamedValue"/></term>
///   <description>accepts one assignment; defaults to refusing.</description>
/// </item>
/// <item>
///   <term><see cref="TryDeleteName"/></term>
///   <description>accepts one <c>delete</c>; defaults to refusing.</description>
/// </item>
/// </list>
/// <para>
/// <b>The JS-visible model.</b> A projected name is an own data property
/// <c>{ writable: IsNameWritable(name), enumerable: IsNameEnumerable(name), configurable: true }</c>. Every
/// other key is ordinary: symbols, and names the projection does not carry, use the inherited property bag and
/// the prototype chain as usual, so <c>Symbol.toStringTag</c>, <c>Symbol.iterator</c> and expandos all work. A
/// projected name always wins over a bag entry of the same name.
/// </para>
/// <para>
/// <c>configurable: true</c> is not a choice: a live projection may stop carrying a name, and a
/// non-configurable property may never afterwards report as absent. That is also why
/// <c>Object.defineProperty</c> against a projected name is refused whether or not the name is writable — the
/// projection owns all three attributes, so there is nothing a redefinition could change that the hooks have
/// not already decided. Assignment, not <c>defineProperty</c>, is the write path.
/// </para>
/// <para>
/// <b>Enumeration order</b> is the ordinary one: projected names that are canonical array indices first,
/// ascending, then the remaining projected names in <see cref="NameAt"/> order, then the property bag's own
/// keys and symbols. A bag expando whose name is a canonical array index therefore sorts after the projected
/// names rather than with them — the same deviation <see cref="ArrayLikeObject"/> has.
/// </para>
/// <para>
/// <b>Extending it.</b> Every derived member is sealed, so the coherence this class exists to guarantee cannot
/// be broken from underneath it. <see cref="ObjectInstance.Get(JsValue, JsValue)"/> is sealed too, which is
/// what lets the constructor declare <see cref="PropertyAccessSemantics.Ordinary"/>: an object needing to
/// observe reads that resolve on its <em>prototype</em> cannot derive from this class and should stay on plain
/// <see cref="ObjectInstance"/>. Every <em>own</em> read is already observed — that is
/// <see cref="TryGetNamedValue"/>.
/// </para>
/// <para>
/// <b>Turning verification on.</b> Every obligation below is trusted on the hot path and checked only when
/// host-contract verification is enabled. A Debug build of Jint has it on; the shipped <em>Release</em>
/// package needs the AppContext switch set before the first use of any Jint type:
/// <c>AppContext.SetSwitch("Jint.EnableHostContractVerification", true)</c>. Running a host's own integration
/// suite that way is how these contracts get checked.
/// </para>
/// </remarks>
public abstract class NamedPropertyObject : ObjectInstance, INamedProjection
{
    /// <summary>
    /// Creates the object against <paramref name="engine"/>. Set <see cref="ObjectInstance.Prototype"/>
    /// afterwards to whatever the host wants inherited — nothing is attached automatically.
    /// </summary>
    protected NamedPropertyObject(Engine engine) : base(engine)
    {
        // Sealing Get below makes the type derivation see an override and answer Exotic; correct it, because
        // the sealed body IS the ordinary implementation by construction. This is exactly the case
        // SetPropertyAccessSemantics documents, and it leaves the OwnValueHook bit (derived from the
        // TryGetOwnPropertyValue override) untouched.
        SetPropertyAccessSemantics(PropertyAccessSemantics.Ordinary);
    }

    /// <summary>
    /// How many names the projection currently carries. Re-read on every enumeration, so a projection that
    /// grows or shrinks is observed live.
    /// </summary>
    public abstract int NameCount { get; }

    /// <summary>
    /// The name at <paramref name="index"/>, which is below <see cref="NameCount"/>.
    /// </summary>
    /// <remarks>
    /// Every name reported here must be one <see cref="TryGetNamedValue"/> answers at the same instant, and no
    /// name may repeat: a name advertised but not readable is listed by <c>Object.keys</c> and
    /// <c>Object.getOwnPropertyNames</c> and then reads as <c>undefined</c> or off the prototype. The order is
    /// the enumeration order script sees, so it must be stable — a bare <c>Dictionary</c>'s key order is not.
    /// A build with host-contract verification on checks both obligations on every enumeration.
    /// </remarks>
    public abstract string NameAt(int index);

    /// <summary>
    /// Reads the property called <paramref name="name"/>. Return <see langword="true"/> with the value; return
    /// <see langword="false"/> exactly when the projection does not carry that name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="false"/> is <b>authoritative</b>: the engine trusts it, does not probe again, and
    /// resolves the read on the prototype chain instead. It must never mean "I could not produce the value",
    /// which would make an existing property read as <c>undefined</c> or resolve on the prototype. A build
    /// with host-contract verification on checks both directions on every read. Never hand back a CLR
    /// <see langword="null"/> value.
    /// </para>
    /// <para>
    /// This is the whole read path — every own read reaches it and nothing else, including computed keys,
    /// <c>Reflect.get</c> and the base of a member call — so it should be O(1), allocation-free and free of
    /// observable side effects.
    /// </para>
    /// </remarks>
    public abstract bool TryGetNamedValue(string name, out JsValue value);

    /// <summary>
    /// Whether the projection carries <paramref name="name"/> — the existence-only question, with no value
    /// produced. Override it when the backing store can answer containment more cheaply than it can project a
    /// value; the default asks <see cref="TryGetNamedValue"/> and discards the value.
    /// </summary>
    /// <remarks>
    /// It backs <c>name in obj</c>, <c>hasOwnProperty</c>, <c>propertyIsEnumerable</c>, <c>delete</c> and the
    /// enumeration-shaped operations (<c>Object.keys</c>/<c>values</c>/<c>entries</c>, <c>for..in</c>,
    /// <c>Object.assign</c>, object spread, <c>JSON.stringify</c>). The answer must equal what
    /// <see cref="TryGetNamedValue"/> would return at the same instant — the engine trusts it and does not
    /// re-verify, so a wrong <see langword="false"/> silently drops the key from all of them while
    /// <c>obj.name</c> still reads it. A build with host-contract verification on checks both directions on
    /// every probe.
    /// </remarks>
    protected virtual bool HasName(string name) => TryGetNamedValue(name, out _);

    /// <summary>
    /// Whether <paramref name="name"/> is enumerable; the default is <see langword="true"/>. A non-enumerable
    /// name still answers <c>in</c> and <c>hasOwnProperty</c> and still appears in
    /// <c>Object.getOwnPropertyNames</c>, but is skipped by <c>Object.keys</c>, <c>for..in</c>, spread,
    /// <c>Object.assign</c> and <c>JSON.stringify</c>.
    /// </summary>
    /// <remarks>
    /// Consulted only where a descriptor or a probe is actually built, never on the value read path.
    /// </remarks>
    protected virtual bool IsNameEnumerable(string name) => true;

    /// <summary>
    /// Whether <paramref name="name"/> is assignable; the default is <see langword="false"/>, which is what
    /// makes a projection read-only until the host says otherwise. It is the twin of
    /// <see cref="IsNameEnumerable"/>: one attribute of the descriptor a projected name reports, declared
    /// per name so a record with computed or read-only fields beside writable ones stays honest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see langword="true"/> answer does two things: the name reports <c>writable: true</c>, and an
    /// assignment to it is routed to <see cref="TrySetNamedValue"/> instead of being refused. That routing is
    /// the WebIDL named-property-setter shape and happens <em>before</em> the prototype chain is consulted, so
    /// a name the projection does not yet carry can be answered <see langword="true"/> here to let an
    /// assignment create it. A name answered <see langword="false"/> takes the ordinary path: the projection
    /// refuses the write if it carries the name, and an unknown name falls through to the prototype chain and
    /// then to an ordinary expando.
    /// </para>
    /// <para>
    /// Declaring a name writable and then having no <see cref="TrySetNamedValue"/> override to accept the
    /// write is a contract violation — the descriptor advertises an assignment that always fails — and a build
    /// with host-contract verification on reports it. So is overriding <see cref="TrySetNamedValue"/> without
    /// ever overriding this hook, which leaves it dead code. Overriding <see cref="TryDeleteName"/> alone is
    /// not a mistake: deletion is governed by <c>configurable</c>, which a projected name always reports
    /// <see langword="true"/>, so a read-only but removable projection is an ordinary shape.
    /// </para>
    /// </remarks>
    protected virtual bool IsNameWritable(string name) => false;

    /// <summary>
    /// Accepts an assignment to <paramref name="name"/>. Return <see langword="true"/> when the projection
    /// took the value; return <see langword="false"/> to <b>refuse</b> the write. The default refuses
    /// everything, so a projection that does not override it is read-only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A refusal is the same answer an ordinary non-writable data property gives: the assignment raises a
    /// <c>TypeError</c> in strict mode and is a silent no-op in sloppy mode. Refuse — do not throw — for a
    /// value the projection will not take; a thrown CLR exception crosses into script as a host error instead
    /// of the language's own <c>TypeError</c>.
    /// </para>
    /// <para>
    /// Only reached for a name <see cref="IsNameWritable"/> answered <see langword="true"/> for, and only when
    /// the assignment's receiver is this object — <c>Reflect.set(obj, k, v, other)</c> defines on
    /// <c>other</c>, exactly as it does for an ordinary object.
    /// </para>
    /// </remarks>
    protected virtual bool TrySetNamedValue(string name, JsValue value) => false;

    /// <summary>
    /// Accepts <c>delete obj[name]</c> for a name the projection carries. Return <see langword="true"/> when
    /// the name is gone; return <see langword="false"/> to <b>refuse</b> the delete. The default refuses
    /// everything, so a projection that does not override it keeps every name it advertises.
    /// </summary>
    /// <remarks>
    /// A refusal makes <c>delete</c> evaluate to <c>false</c> in sloppy mode and raise a <c>TypeError</c> in
    /// strict mode — what a non-configurable property does, even though a projected name reports
    /// <c>configurable: true</c> for the invariant reason in the type's remarks. Answering
    /// <see langword="true"/> obliges the projection to stop carrying the name immediately: a build with
    /// host-contract verification on re-reads it and fails if it is still there.
    /// </remarks>
    protected virtual bool TryDeleteName(string name) => false;

    int INamedProjection.NameCount => NameCount;
    string INamedProjection.NameAt(int index) => NameAt(index);
    bool INamedProjection.TryGetNamedValue(string name, out JsValue value) => TryGetNamedValue(name, out value);
    bool INamedProjection.HasName(string name) => HasName(name);
    bool INamedProjection.IsNameEnumerable(string name) => IsNameEnumerable(name);
    bool INamedProjection.IsNameWritable(string name) => IsNameWritable(name);
    bool INamedProjection.TrySetNamedValue(string name, JsValue value) => TrySetNamedValue(name, value);
    bool INamedProjection.TryDeleteName(string name) => TryDeleteName(name);

    /// <summary>
    /// Sealed so no subclass can go exotic underneath the <see cref="PropertyAccessSemantics.Ordinary"/> claim
    /// the constructor makes. The body is the ordinary implementation, which is what that claim asserts.
    /// </summary>
    public sealed override JsValue Get(JsValue property, JsValue receiver) => base.Get(property, receiver);

    /// <summary>
    /// A projected name resolves straight out of the host with no descriptor at all; every other key falls
    /// through to the ordinary property bag.
    /// </summary>
    protected internal sealed override bool TryGetOwnPropertyValue(JsValue property, JsValue receiver, out JsValue value)
    {
        if (NamedProjection.TryGetName(property, out var name) && NamedProjection.Read(this, name, out value))
        {
            return true;
        }

        // Deliberately base.GetOwnProperty rather than base.TryGetOwnPropertyValue: that one routes through
        // the *virtual* GetOwnProperty, which is this class's own, and would ask the projection a second time
        // for every name it does not carry — the miss and the prototype-hit rows of the probe-count pins.
        var descriptor = base.GetOwnProperty(property);
        if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
        {
            value = Undefined;
            return false;
        }

        value = UnwrapJsValue(descriptor, receiver);
        return true;
    }

    /// <inheritdoc />
    public sealed override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (NamedProjection.TryGetName(property, out var name) && NamedProjection.Read(this, name, out var value))
        {
            return NamedProjection.DescriptorFor(this, name, value);
        }

        return base.GetOwnProperty(property);
    }

    /// <summary>
    /// Existence and enumerability answered from <see cref="HasName"/> and <see cref="IsNameEnumerable"/>,
    /// with no descriptor materialized.
    /// </summary>
    protected internal sealed override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if (NamedProjection.TryGetName(property, out var name) && NamedProjection.Probe(this, name))
        {
            return NamedProjection.ProbeResultFor(this, name);
        }

        // base.GetOwnProperty rather than base.ProbeOwnProperty, for the reason above: the base probe ends in
        // a virtual GetOwnProperty call, which is this class's own. A host subclass is never in either shape
        // mode, so the storage fast paths the base probe would have taken do not apply to it anyway.
        var descriptor = base.GetOwnProperty(property);
        if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
        {
            return OwnPropertyProbe.Missing;
        }

        return descriptor.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
    }

    /// <inheritdoc />
    public sealed override bool HasProperty(JsValue property) => base.HasProperty(property);

    /// <summary>
    /// Ordinary <c>[[OwnPropertyKeys]]</c> order: projected names that are canonical array indices ascending,
    /// then the remaining projected names in <see cref="NameAt"/> order, then the property bag's string keys
    /// and symbols.
    /// </summary>
    public sealed override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        if ((types & Types.String) == Types.Empty)
        {
            return base.GetOwnPropertyKeys(types);
        }

        var keys = NamedProjection.CollectNames(this, _engine, NamedProjection.NameOrder.IndexNamesFirst);
        var projected = keys.Count;
        foreach (var stored in base.GetOwnPropertyKeys(types))
        {
            if (NamedProjection.ShadowsBagKey(this, stored, projected))
            {
                continue;
            }

            keys.Add(stored);
        }

        return keys;
    }

    /// <summary>
    /// Routes an assignment to a name <see cref="IsNameWritable"/> claims to
    /// <see cref="TrySetNamedValue"/>, and leaves every other key entirely ordinary. A refused write raises a
    /// <c>TypeError</c> in strict mode and is a silent no-op in sloppy mode, which is also what a name the
    /// projection carries but does not declare writable gets.
    /// </summary>
    public sealed override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        // WebIDL's named property setter runs ahead of the prototype chain and only when the receiver is the
        // object itself. `Reflect.set(obj, k, v, other)` therefore defines on `other`, exactly as it would for
        // an ordinary object, and a non-writable name falls to base.Set, which finds the non-writable
        // descriptor this class reports and refuses in the ordinary way.
        if (ReferenceEquals(this, receiver)
            && NamedProjection.TryGetName(property, out var name)
            && IsNameWritable(name))
        {
            return NamedProjection.Write(this, name, value);
        }

        return base.Set(property, value, receiver);
    }

    /// <summary>
    /// Routes <c>delete</c> of a projected name to <see cref="TryDeleteName"/>, whose default refuses (sloppy
    /// mode: the expression evaluates to <c>false</c>; strict mode: <c>TypeError</c>). Every other key deletes
    /// ordinarily.
    /// </summary>
    public sealed override bool Delete(JsValue property)
    {
        if (NamedProjection.TryGetName(property, out var name) && NamedProjection.Probe(this, name))
        {
            return NamedProjection.Remove(this, name);
        }

        return base.Delete(property);
    }

    /// <summary>
    /// Refuses <c>[[DefineOwnProperty]]</c> on a projected name — the projection owns all three attributes, so
    /// a redefinition has nothing to change, and assignment rather than <c>defineProperty</c> is the write
    /// path. Every other key defines ordinarily.
    /// </summary>
    public sealed override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (NamedProjection.TryGetName(property, out var name) && NamedProjection.Probe(this, name))
        {
            return false;
        }

        return base.DefineOwnProperty(property, desc);
    }
}
