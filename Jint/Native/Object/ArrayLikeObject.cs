using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native.Array;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Base class for host-defined, array-like objects — live views over native indexed state (a DOM
/// <c>NodeList</c>, a result window, a host list projection). Deriving from it gives the object array-like
/// semantics the engine recognizes end to end: indexed reads without descriptor or key allocation (including
/// the interpreter's computed-read lane), <c>Array.prototype</c> generics and the array iterator driven by a
/// single per-element callback, index/<c>length</c> enumeration (<c>Object.keys</c>, <c>for-in</c>,
/// spread-into-object, <c>JSON.stringify</c>), and the destructuring fast path.
/// </summary>
/// <remarks>
/// <para>
/// A subclass supplies exactly two members — <see cref="Length"/> and <see cref="TryGetIndex"/> — and this class
/// derives the whole JS-visible property model from them, keeping <c>GetOwnProperty</c>,
/// <c>TryGetOwnPropertyValue</c>, <c>ProbeOwnProperty</c>, the key enumerations, <c>Set</c>, <c>Delete</c> and
/// <c>DefineOwnProperty</c> mutually consistent. Both members are re-consulted on every operation, so a
/// collection that grows or shrinks between reads is observed live, exactly like a DOM collection.
/// </para>
/// <para>
/// There is a third, optional member: <see cref="HasIndex"/>. Existence questions (<c>index in list</c>,
/// <c>hasOwnProperty</c>, <c>Object.keys</c>, the per-element hole test the <c>Array.prototype</c> generics run)
/// need a yes/no, not a value, and by default answer by producing an element and discarding it. A backing store
/// that can test containment more cheaply than it can project an element should override
/// <see cref="HasIndex"/>; a host that does not is never asked and pays exactly what it did before.
/// </para>
/// <para>
/// <b>The JS-visible model.</b> Canonical array indices below <see cref="Length"/> for which
/// <see cref="TryGetIndex"/> answers are own data properties
/// <c>{ writable: false, enumerable: true, configurable: true }</c>; <c>length</c> is
/// <c>{ writable: false, enumerable: false, configurable: true }</c>. Writes to either are ignored in sloppy
/// mode and raise <c>TypeError</c> in strict mode, and <c>delete</c> / <c>Object.defineProperty</c> against an
/// index key or <c>length</c> are refused — the WebIDL platform-object shape.
/// </para>
/// <para>
/// <b>Where <c>length</c> lives</b> is a known deviation. Here it is an <em>own</em> property, so
/// <c>list.hasOwnProperty('length')</c> is <c>true</c> and <c>Object.getOwnPropertyNames(list)</c> contains
/// <c>"length"</c>; a browser puts it on <c>NodeList.prototype</c> as a WebIDL attribute and answers
/// <c>false</c>. There is deliberately no opt-out in this version: the engine reads the length of an array-like
/// through <c>[[Get]]("length")</c> in places that do not go through this type's operations
/// (<c>JSON.stringify</c>'s array serialization, and every write-mode <c>Array.prototype</c> generic), so a
/// collection that stopped owning the property would silently behave as empty unless it also installed an
/// accessor on its prototype. Owning it keeps that impossible to get wrong, and moving it later is an additive
/// change.
/// </para>
/// <para>
/// <b>What it deliberately is NOT: an Array.</b> <c>Array.isArray</c> answers <c>false</c>,
/// <c>[].concat(collection)</c> appends the object (define <c>Symbol.isConcatSpreadable</c> on the instance or
/// its prototype to opt in), and <c>Object.prototype.toString</c> reports <c>[object Object]</c> unless
/// <c>Symbol.toStringTag</c> is set — the same answers a browser gives for a <c>NodeList</c>. It does deviate
/// from a browser in one place, following Jint's own host convention instead (an <c>ObjectWrapper</c> over a
/// CLR collection already does this): <c>JSON.stringify</c> serializes it as a JSON <b>array</b>, not as an
/// object with numeric keys.
/// </para>
/// <para>
/// <b>Iteration.</b> The class does not install <c>Symbol.iterator</c>. Wire it on the instance or its prototype
/// to <c>engine.Intrinsics.Array.PrototypeObject.Get(GlobalSymbolRegistry.Iterator)</c> (what WHATWG specs do
/// for <c>NodeList</c>) and <c>for-of</c>, spread, <c>Array.from</c> and array destructuring all route through
/// the engine's array-like iterator against <see cref="TryGetIndex"/>.
/// </para>
/// <para>
/// <b>Named members.</b> A collection that also projects <em>named</em> state — WebIDL's named properties, a
/// <c>NodeList</c>-shaped view with a live <c>last</c>, a result window with a <c>total</c> — declares them
/// through the same hooks <see cref="NamedPropertyObject"/> publishes, and this class derives the named half of
/// the model from them the same way it derives the indexed half: <see cref="NameCount"/>,
/// <see cref="NameAt"/> and <see cref="TryGetNamedValue"/>, refined by <see cref="HasName"/>,
/// <see cref="IsNameEnumerable"/>, <see cref="IsNameWritable"/>, <see cref="TrySetNamedValue"/> and
/// <see cref="TryDeleteName"/>. All eight default to "the projection carries nothing", so a collection with no
/// named state declares nothing and is not asked. The indexed collection owns every canonical array index and
/// <c>length</c>, which are answered before the projection is ever consulted, so a projected name may not spell
/// one of those.
/// </para>
/// <para>
/// <b>Extending it.</b> Every member this class derives is sealed, including
/// <see cref="ObjectInstance.Get(JsValue, JsValue)"/>: a collection needing to intercept every <i>named</i>
/// read through <c>Get</c> cannot derive from this class and should stay on plain
/// <see cref="ObjectInstance"/>. Live named members are the hooks above, not a <c>GetOwnProperty</c> override —
/// which is what the sealing is for, because that override had to be kept consistent with
/// <c>GetOwnPropertyKeys</c> and <c>GetOwnProperties</c> by hand and could not reach the probe lane at all.
/// </para>
/// <para>
/// <b>Turning verification on.</b> Every obligation below is trusted on the hot path and checked only
/// when host-contract verification is enabled. A Debug build of Jint has it on; the shipped <em>Release</em>
/// package — which is the only one on NuGet — needs the AppContext switch set before the first use of any
/// Jint type: <c>AppContext.SetSwitch("Jint.EnableHostContractVerification", true)</c>. Running a host's own
/// integration suite that way is how these contracts get checked; it is not something to leave on in
/// production, because each verifier redoes exactly the work the lane it guards exists to avoid.
/// </para>
/// <para>
/// <b>Read-only indices by design.</b> There is no write hook for an element; every mutating
/// <c>Array.prototype</c> generic (<c>sort</c>, <c>fill</c>, <c>reverse</c>, …) fails with the spec-shaped
/// <c>TypeError</c> an ordinary non-writable property produces. If the collection is a static snapshot rather
/// than a live view, copying it into a <see cref="JsArray"/> is cheaper and gives full array semantics; this
/// class exists for the live case, which cannot be copied.
/// </para>
/// </remarks>
public abstract class ArrayLikeObject : ObjectInstance, INamedProjection
{
    // { writable: false, enumerable: true, configurable: true } — what browsers report for the indices of a
    // platform array-like. configurable:true (plus the [[Delete]]/[[DefineOwnProperty]] refusals below) is the
    // WebIDL shape and is what lets a shrinking collection stay within the [[GetOwnProperty]] invariants: a
    // non-configurable property may never afterwards report as absent.
    private const PropertyFlag IndexFlags = PropertyFlag.NonWritable;

    // { writable: false, enumerable: false, configurable: true } — configurable for the same invariant reason,
    // the value changing from call to call.
    private const PropertyFlag LengthFlags = PropertyFlag.OnlyConfigurable;

    // Whether the runtime type declared any of the named hooks. Derived from the type once and cached
    // process-wide, the way the access-semantics flags are: it is a pure *routing* decision — a collection that
    // declares no named state must not pay a virtual call per named read to be told so — and an inconclusive
    // derivation answers "yes", which costs those calls and never changes an answer.
    private readonly bool _hasNamedProjection;

    /// <summary>
    /// Creates the object against <paramref name="engine"/>. Set <see cref="ObjectInstance.Prototype"/> afterwards
    /// to whatever the host wants inherited — nothing is attached automatically.
    /// </summary>
    protected ArrayLikeObject(Engine engine) : base(engine)
    {
        // Sealing Get below makes the type derivation see an override and answer Exotic; correct it, because the
        // sealed body IS the ordinary implementation by construction. This is exactly the case
        // SetPropertyAccessSemantics documents, and it leaves the OwnValueHook bit (derived from the
        // TryGetOwnPropertyValue override) untouched.
        SetPropertyAccessSemantics(PropertyAccessSemantics.Ordinary);

        _hasNamedProjection = (NamedProjection.DeclaredHooks(GetType()) & NamedProjectionHooks.Any) != NamedProjectionHooks.None;
    }

    /// <summary>
    /// Current element count. Re-read on every operation that needs it — a live collection may change between
    /// reads, and each JavaScript operation observes the value at its own start, exactly like a live DOM
    /// collection. Implementations should be O(1) and allocation-free.
    /// </summary>
    public abstract uint Length { get; }

    /// <summary>
    /// Reads the element at <paramref name="index"/>. Return <see langword="true"/> with the value; return
    /// <see langword="false"/> exactly when the object has no own element there — <paramref name="index"/> is at
    /// or beyond <see cref="Length"/>, or the position is a hole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="false"/> is <b>authoritative</b>: the engine trusts it, does not re-probe, and resolves the
    /// read on the prototype chain instead. It must agree with what <see cref="ObjectInstance.GetOwnProperty"/>
    /// would report at the same instant — a build with host-contract verification on verifies that on every
    /// read. Never hand back a
    /// CLR <see langword="null"/> value.
    /// </para>
    /// <para>
    /// May be invoked from every read path, including tight interpreter loops, so implementations should be O(1)
    /// and allocation-free. Because <see cref="Length"/> is re-read live, this can legitimately be called with an
    /// <paramref name="index"/> that has just gone out of range through concurrent host mutation: answer
    /// <see langword="false"/>, never throw.
    /// </para>
    /// </remarks>
    public abstract bool TryGetIndex(uint index, out JsValue value);

    /// <summary>
    /// Whether the object has an own element at <paramref name="index"/> — the <em>existence-only</em> question,
    /// with no value produced. Override it when the backing store can answer containment more cheaply than it can
    /// produce an element; the default asks <see cref="TryGetIndex"/> and discards the value, which is what every
    /// host got before this hook existed, so not overriding it changes nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It backs every question that needs a yes/no rather than a value: <c>index in list</c>,
    /// <c>hasOwnProperty</c>, <c>propertyIsEnumerable</c>, the key enumerations
    /// (<c>Object.keys</c>/<c>values</c>/<c>entries</c>, <c>Object.getOwnPropertyNames</c>, <c>for-in</c>,
    /// <c>Object.assign</c>, object spread), <c>delete</c>, and the hole test the <c>Array.prototype</c> generics
    /// run per element (<c>map</c>, <c>filter</c>, <c>forEach</c>, …). Each of those otherwise materializes an
    /// element purely to throw it away. Reads still go through <see cref="TryGetIndex"/>: a question that needs
    /// the value never asks this one first, so an override is never an extra call on a read path.
    /// </para>
    /// <para>
    /// <b>Contract:</b> the answer must equal what <see cref="TryGetIndex"/> would return at the same instant.
    /// The engine trusts it and does not re-verify: a wrong <see langword="false"/> silently drops the element
    /// from every enumeration and existence check above while <c>list[index]</c> still reads it, and a wrong
    /// <see langword="true"/> advertises a key whose read yields <c>undefined</c> or resolves on the prototype.
    /// A build with host-contract verification on verifies both directions on every probe, so running a host
    /// suite that way is the
    /// checker. Like <see cref="TryGetIndex"/> it must be O(1), allocation-free, free of observable side effects,
    /// and must answer <see langword="false"/> rather than throw for an index that has just gone out of range.
    /// </para>
    /// </remarks>
    protected virtual bool HasIndex(uint index) => TryGetIndex(index, out _);

    /// <summary>
    /// How many <em>named</em> members the collection projects beside its elements; the default is <c>0</c>, so
    /// a collection with no named state declares nothing and is never asked for one. See the type's remarks for
    /// the whole named hook set, which is the one <see cref="NamedPropertyObject"/> publishes.
    /// </summary>
    protected virtual int NameCount => 0;

    /// <summary>
    /// The name at <paramref name="index"/>, which is below <see cref="NameCount"/>. Overriding
    /// <see cref="NameCount"/> without overriding this is the one way to get an exception out of the default
    /// projection.
    /// </summary>
    /// <remarks>
    /// Every name reported here must be one <see cref="TryGetNamedValue"/> answers at the same instant, no name
    /// may repeat, and none may be a canonical array index or <c>length</c> — the indexed collection owns those
    /// keys and answers them before the projection is consulted, so such a name would be advertised for a read
    /// that never reaches it. The order is the enumeration order script sees, so it must be stable. A build
    /// with host-contract verification on checks all three obligations on every enumeration.
    /// </remarks>
    protected virtual string NameAt(int index)
    {
        Throw.ArgumentOutOfRangeException(nameof(index), $"{GetType()} reports {NameCount} projected names but does not override NameAt.");
        return null!;
    }

    /// <summary>
    /// Reads the projected member called <paramref name="name"/>. Return <see langword="true"/> with the value;
    /// return <see langword="false"/> exactly when the projection does not carry that name, which is
    /// <b>authoritative</b> — the engine resolves the read on the property bag and then the prototype chain
    /// without asking again. The default carries nothing.
    /// </summary>
    /// <remarks>
    /// Never reached for a canonical array index or for <c>length</c>: those are answered from
    /// <see cref="Length"/> and <see cref="TryGetIndex"/> first. Never hand back a CLR <see langword="null"/>
    /// value.
    /// </remarks>
    protected virtual bool TryGetNamedValue(string name, out JsValue value)
    {
        value = Undefined;
        return false;
    }

    /// <summary>
    /// Whether the projection carries <paramref name="name"/> — the existence-only question, with no value
    /// produced. The default asks <see cref="TryGetNamedValue"/> and discards the value; override it when the
    /// backing store can answer containment more cheaply. The answer must equal what
    /// <see cref="TryGetNamedValue"/> would return at the same instant, which a build with host-contract
    /// verification on checks in both directions on every probe.
    /// </summary>
    protected virtual bool HasName(string name) => TryGetNamedValue(name, out _);

    /// <summary>
    /// Whether <paramref name="name"/> is enumerable; the default is <see langword="true"/>. A non-enumerable
    /// name still answers <c>in</c> and <c>hasOwnProperty</c> and still appears in
    /// <c>Object.getOwnPropertyNames</c>, but is skipped by <c>Object.keys</c>, <c>for..in</c>, spread,
    /// <c>Object.assign</c> and <c>JSON.stringify</c>.
    /// </summary>
    protected virtual bool IsNameEnumerable(string name) => true;

    /// <summary>
    /// Whether <paramref name="name"/> is assignable; the default is <see langword="false"/>, which keeps the
    /// named projection as read-only as the elements are. A <see langword="true"/> answer makes the name report
    /// <c>writable: true</c> and routes an assignment to it to <see cref="TrySetNamedValue"/>, ahead of the
    /// prototype chain and only when the assignment's receiver is this object — the WebIDL named-property-setter
    /// shape. It applies to <em>names</em> only: an index and <c>length</c> stay non-writable whatever this
    /// answers.
    /// </summary>
    protected virtual bool IsNameWritable(string name) => false;

    /// <summary>
    /// Accepts an assignment to a name <see cref="IsNameWritable"/> claims. <see langword="false"/> refuses the
    /// write, which raises a <c>TypeError</c> in strict mode and is a silent no-op in sloppy mode; the default
    /// refuses everything.
    /// </summary>
    protected virtual bool TrySetNamedValue(string name, JsValue value) => false;

    /// <summary>
    /// Accepts <c>delete</c> of a projected name. <see langword="false"/> refuses, which evaluates to
    /// <c>false</c> in sloppy mode and raises a <c>TypeError</c> in strict mode; the default refuses
    /// everything. Answering <see langword="true"/> obliges the projection to stop carrying the name
    /// immediately, which a build with host-contract verification on re-reads and checks.
    /// </summary>
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
    /// The single funnel every engine-side index read goes through, so the host contract is enforced in one
    /// place: a <see langword="false"/> answer always leaves <paramref name="value"/> as <c>undefined</c> rather
    /// than whatever the host left in the <c>out</c> slot, and a <see langword="true"/> answer is checked for the
    /// documented "never null" obligation when host-contract verification is on.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ReadIndex(uint index, out JsValue value)
    {
        if (TryGetIndex(index, out value))
        {
            if (HostContractVerification.Enabled && value is null)
            {
                HostContractVerification.Fail($"{GetType()}.TryGetIndex answered index {index} with a CLR null; return a JsValue or answer false.");
            }

            return true;
        }

        value = Undefined;
        return false;
    }

    /// <summary>
    /// The existence-only counterpart of <see cref="ReadIndex"/>, and the only way engine code outside this class
    /// reaches <see cref="HasIndex"/> (which is <c>protected</c>). Every index existence question funnels through
    /// here so the Debug agreement check sits in one place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ProbeIndex(uint index)
    {
        var has = HasIndex(index);
        if (HostContractVerification.Enabled)
        {
            AssertHasIndexAgreesWithTryGetIndex(this, index, has);
        }

        return has;
    }

    /// <summary>
    /// Verifier for the <see cref="HasIndex"/> contract, checking <b>both</b> directions against
    /// <see cref="TryGetIndex"/> for the same index. Gated on <see cref="HostContractVerification.Enabled"/>,
    /// so a host's own suite run against a Debug Jint — or against the shipped Release package with the
    /// <c>Jint.EnableHostContractVerification</c> switch set — becomes the checker, and every other process
    /// pays nothing. The same arrangement <c>ObjectInstance.AssertOwnValueAgreesWithDescriptor</c> uses for the
    /// value hook.
    /// </summary>
    private static void AssertHasIndexAgreesWithTryGetIndex(ArrayLikeObject target, uint index, bool answered)
    {
        var produced = target.TryGetIndex(index, out _);
        if (produced == answered)
        {
            return;
        }

        HostContractVerification.Fail(answered
            ? $"{target.GetType()}.HasIndex answered true for index {index} but its TryGetIndex answers false. The engine trusts HasIndex, so this advertises a key whose read yields undefined or resolves on the prototype."
            : $"{target.GetType()}.HasIndex answered false for index {index} but its TryGetIndex produces a value. The engine trusts HasIndex, so this silently drops the element from `in`, hasOwnProperty, Object.keys and the Array.prototype hole tests while list[{index}] still reads it.");
    }

    /// <summary>
    /// The key as a projected name, or <see langword="false"/> when the projection cannot own it — the type
    /// declared none, the key is a symbol or an object key, or the key belongs to the indexed collection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetProjectedName(JsValue property, [NotNullWhen(true)] out string? name)
    {
        if (!_hasNamedProjection)
        {
            name = null;
            return false;
        }

        return NamedProjection.TryGetName(property, out name);
    }

    /// <summary>
    /// Sealed so no subclass can go exotic underneath the lanes that resolve an indexed read without calling
    /// <c>Get</c> at all (the <c>ArrayOperations</c> dispatcher and the interpreter's computed-read branch).
    /// The body is the ordinary implementation, which is what the constructor's
    /// <see cref="PropertyAccessSemantics.Ordinary"/> declaration asserts.
    /// </summary>
    public sealed override JsValue Get(JsValue property, JsValue receiver) => base.Get(property, receiver);

    /// <summary>
    /// Index, <c>length</c> and projected-name reads resolve straight out of the host with no descriptor at
    /// all; every other key falls through to the ordinary property bag.
    /// </summary>
    protected internal sealed override bool TryGetOwnPropertyValue(JsValue property, JsValue receiver, out JsValue value)
    {
        if (ArrayInstance.IsArrayIndex(property, out var index))
        {
            return ReadIndex(index, out value);
        }

        if (CommonProperties.Length.Equals(property))
        {
            value = JsNumber.Create(Length);
            return true;
        }

        if (TryGetProjectedName(property, out var name) && NamedProjection.Read(this, name, out value))
        {
            return true;
        }

        return base.TryGetOwnPropertyValue(property, receiver, out value);
    }

    /// <inheritdoc />
    public sealed override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (ArrayInstance.IsArrayIndex(property, out var index))
        {
            return ReadIndex(index, out var value)
                ? new PropertyDescriptor(value, IndexFlags)
                : PropertyDescriptor.Undefined;
        }

        if (CommonProperties.Length.Equals(property))
        {
            return new PropertyDescriptor(JsNumber.Create(Length), LengthFlags);
        }

        if (TryGetProjectedName(property, out var name) && NamedProjection.Read(this, name, out var named))
        {
            return NamedProjection.DescriptorFor(this, name, named);
        }

        return base.GetOwnProperty(property);
    }

    /// <summary>
    /// Existence and enumerability for <c>in</c>, <c>hasOwnProperty</c>, <c>propertyIsEnumerable</c>,
    /// <c>Object.keys</c>/<c>values</c>/<c>entries</c>, <c>Object.assign</c>, object spread and
    /// <c>JSON.stringify</c> — answered from the same primitives as <see cref="GetOwnProperty"/>, with no
    /// descriptor materialized. This is the lane a hand-rolled named getter could never reach, because the
    /// member was sealed before there was a hook to answer it from.
    /// </summary>
    protected internal sealed override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if (ArrayInstance.IsArrayIndex(property, out var index))
        {
            return ProbeIndex(index) ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.Missing;
        }

        if (CommonProperties.Length.Equals(property))
        {
            return OwnPropertyProbe.NonEnumerable;
        }

        if (TryGetProjectedName(property, out var name) && NamedProjection.Probe(this, name))
        {
            return NamedProjection.ProbeResultFor(this, name);
        }

        return base.ProbeOwnProperty(property);
    }

    /// <summary>
    /// Ordinary <c>[[OwnPropertyKeys]]</c> order: the present indices ascending, then <c>length</c>, then the
    /// projected names in <see cref="NameAt"/> order, then the property bag's string keys in insertion order,
    /// then symbols.
    /// </summary>
    public sealed override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        if ((types & Types.String) == Types.Empty)
        {
            return base.GetOwnPropertyKeys(types);
        }

        var length = CheckedLength();
        var keys = new List<JsValue>((int) length + 1);
        for (uint i = 0; i < length; i++)
        {
            if (i > 0 && i % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            if (ProbeIndex(i))
            {
                keys.Add(JsString.Create(i));
            }
        }

        keys.Add(CommonProperties.Length);

        var names = CollectNames();
        keys.AddRange(names);

        foreach (var stored in base.GetOwnPropertyKeys(types))
        {
            if (NamedProjection.ShadowsBagKey(this, stored, names.Count))
            {
                continue;
            }

            keys.Add(stored);
        }

        return keys;
    }

    /// <summary>
    /// The same order as <see cref="GetOwnPropertyKeys"/>, with the descriptors materialized.
    /// </summary>
    public sealed override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        var length = CheckedLength();
        for (uint i = 0; i < length; i++)
        {
            if (i > 0 && i % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            if (ReadIndex(i, out var value))
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(JsString.Create(i), new PropertyDescriptor(value, IndexFlags));
            }
        }

        yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, new PropertyDescriptor(JsNumber.Create(length), LengthFlags));

        var names = CollectNames();
        foreach (var key in names)
        {
            var name = key.ToString();
            if (NamedProjection.Read(this, name, out var value))
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(key, NamedProjection.DescriptorFor(this, name, value));
            }
        }

        foreach (var entry in base.GetOwnProperties())
        {
            if (NamedProjection.ShadowsBagKey(this, entry.Key, names.Count))
            {
                continue;
            }

            yield return entry;
        }
    }

    /// <summary>
    /// Routes an assignment to a name <see cref="IsNameWritable"/> claims to <see cref="TrySetNamedValue"/>.
    /// Indices and <c>length</c> never reach it — they are non-writable, so the ordinary path refuses them with
    /// the spec-shaped answer (sloppy mode: ignored; strict mode: <c>TypeError</c>).
    /// </summary>
    public sealed override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        // The collection owns every index and `length`, and both are non-writable, so neither ever reaches the
        // projection: base.Set finds the non-writable descriptor this class reports and refuses in the ordinary
        // way. What is left is a name, offered to the projection ahead of the prototype chain and only when the
        // receiver is this object — the WebIDL named-property-setter shape.
        if (ReferenceEquals(this, receiver)
            && !ArrayInstance.IsArrayIndex(property, out _)
            && !CommonProperties.Length.Equals(property)
            && TryGetProjectedName(property, out var name)
            && IsNameWritable(name))
        {
            return NamedProjection.Write(this, name, value);
        }

        return base.Set(property, value, receiver);
    }

    /// <summary>
    /// Refuses <c>delete</c> of an index the collection currently has and of <c>length</c> (sloppy mode: the
    /// expression evaluates to <c>false</c>; strict mode: <c>TypeError</c>) — the WebIDL platform-object shape.
    /// An index the collection does not have deletes vacuously, like any absent property. A projected name is
    /// offered to <see cref="TryDeleteName"/>, whose default refuses.
    /// </summary>
    public sealed override bool Delete(JsValue property)
    {
        if (ArrayInstance.IsArrayIndex(property, out var index))
        {
            return !ProbeIndex(index);
        }

        if (CommonProperties.Length.Equals(property))
        {
            return false;
        }

        if (TryGetProjectedName(property, out var name) && NamedProjection.Probe(this, name))
        {
            return NamedProjection.Remove(this, name);
        }

        return base.Delete(property);
    }

    /// <summary>
    /// Refuses <c>[[DefineOwnProperty]]</c> on <c>length</c>, on <b>every</b> canonical array-index key,
    /// in range or not, and on a projected name.
    /// </summary>
    /// <remarks>
    /// Refusing out-of-range indices as well is stricter than WebIDL, which lets an ordinary expando live at an
    /// index at or beyond <c>length</c>. The strict form avoids the projection-versus-bag incoherence that would
    /// appear the moment the live collection grew over such an expando. A projected name is refused for the
    /// reason <see cref="NamedPropertyObject"/> gives: the projection owns all three attributes, so a
    /// redefinition has nothing to change. Every other named key defines ordinarily.
    /// </remarks>
    public sealed override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (ArrayInstance.IsArrayIndex(property, out _) || CommonProperties.Length.Equals(property))
        {
            return false;
        }

        if (TryGetProjectedName(property, out var name) && NamedProjection.Probe(this, name))
        {
            return false;
        }

        return base.DefineOwnProperty(property, desc);
    }

    internal sealed override bool IsArrayLike => true;

    internal sealed override uint GetLength() => Length;

    // Identity-compare the resolved @@iterator against the realm's captured %Array.prototype.values%, exactly as
    // ArrayInstance does: a host that wired the original array iterator gets the destructuring fast path, one
    // that installed its own (or none) keeps the full iterator protocol.
    internal sealed override bool HasOriginalIterator
    {
        get
        {
            var iterator = Get(GlobalSymbolRegistry.Iterator);
            return !iterator.IsUndefined()
                   && ReferenceEquals(iterator, _engine.Realm.Intrinsics.Array.PrototypeObject._originalIteratorFunction);
        }
    }

    // Enumerating a collection materializes one key per element, so a hostile or buggy Length must not be able to
    // ask for an allocation the CLR cannot serve. Mirrors ArrayOperations.GetAll's guard.
    private uint CheckedLength()
    {
        var length = Length;
        if (length > ClrLimits.MaxArrayLength)
        {
            Throw.RangeError(_engine.Realm, "Invalid array-like length");
        }

        return length;
    }

    private List<JsValue> CollectNames()
        => _hasNamedProjection
            ? NamedProjection.CollectNames(this, _engine, NamedProjection.NameOrder.BesideIndices)
            : [];
}
