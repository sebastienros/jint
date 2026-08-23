using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native.Array;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Base class for a host-defined, read-only object whose <em>named</em> properties are projected from native
/// state — a record, a settings bag, a live view over host data. The indexed counterpart is
/// <see cref="ArrayLikeObject"/>.
/// </summary>
/// <remarks>
/// <para>
/// A subclass supplies three members — <see cref="NameCount"/>, <see cref="NameAt"/> and
/// <see cref="TryGetNamedValue"/> — and this class derives the whole JS-visible property model from them,
/// keeping <c>GetOwnProperty</c>, <c>TryGetOwnPropertyValue</c>, <c>ProbeOwnProperty</c>, both key
/// enumerations, <c>HasProperty</c>, <c>Delete</c> and <c>DefineOwnProperty</c> mutually consistent. All three
/// are re-consulted on every operation, so a projection that gains or loses names between reads is observed
/// live. Two further members are optional: <see cref="HasName"/> answers existence without producing a value,
/// and <see cref="IsNameEnumerable"/> hides a name from enumeration.
/// </para>
/// <para>
/// <b>The JS-visible model.</b> A projected name is an own data property
/// <c>{ writable: false, enumerable: IsNameEnumerable(name), configurable: true }</c>. Writing one is ignored
/// in sloppy mode and raises <c>TypeError</c> in strict mode, and <c>delete</c> and
/// <c>Object.defineProperty</c> against one are refused. Every other key is ordinary: symbols, and names the
/// projection does not carry, use the inherited property bag and the prototype chain as usual, so
/// <c>Symbol.toStringTag</c>, <c>Symbol.iterator</c> and expandos all work. A projected name always wins over
/// a bag entry of the same name.
/// </para>
/// <para>
/// <c>configurable: true</c> is not a choice: a live projection may stop carrying a name, and a
/// non-configurable property may never afterwards report as absent. Nor is <c>writable: false</c>, because
/// there is no write hook to route a write to — adding one later is additive.
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
/// </remarks>
public abstract class NamedPropertyObject : ObjectInstance
{
    // { writable: false, enumerable: true, configurable: true } and its non-enumerable twin. configurable:true
    // is what keeps a projection that loses a name inside the [[GetOwnProperty]] invariants.
    private const PropertyFlag EnumerableFlags = PropertyFlag.NonWritable;
    private const PropertyFlag NonEnumerableFlags = PropertyFlag.OnlyConfigurable;

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
    /// Enumerability is the only attribute a host may vary; see the type's remarks for why writability and
    /// configurability are fixed. It is consulted only where a descriptor or a probe is actually built, never
    /// on the value read path.
    /// </remarks>
    protected virtual bool IsNameEnumerable(string name) => true;

    /// <summary>
    /// The single funnel every engine-side named read goes through, so the host contract is enforced in one
    /// place: a <see langword="false"/> answer always leaves <paramref name="value"/> as <c>undefined</c>
    /// rather than whatever the host left in the <c>out</c> slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadName(string name, out JsValue value)
    {
        if (TryGetNamedValue(name, out value))
        {
            if (HostContractVerification.Enabled && value is null)
            {
                HostContractVerification.Fail($"{GetType()}.TryGetNamedValue answered '{name}' with a CLR null; return a JsValue or answer false.");
            }

            return true;
        }

        value = Undefined;
        return false;
    }

    /// <summary>
    /// The existence-only counterpart of <see cref="ReadName"/>, and the only way this class reaches
    /// <see cref="HasName"/>, so the agreement check sits in one place.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ProbeName(string name)
    {
        var has = HasName(name);
        if (HostContractVerification.Enabled)
        {
            AssertHasNameAgreesWithTryGetNamedValue(this, name, has);
        }

        return has;
    }

    /// <summary>
    /// Verifier for the <see cref="HasName"/> contract, checking <b>both</b> directions against
    /// <see cref="TryGetNamedValue"/> for the same name. Gated on
    /// <see cref="HostContractVerification.Enabled"/>, so a host's own suite run with the switch on is the
    /// checker and every other process pays nothing.
    /// </summary>
    private static void AssertHasNameAgreesWithTryGetNamedValue(NamedPropertyObject target, string name, bool answered)
    {
        var produced = target.TryGetNamedValue(name, out _);
        if (produced == answered)
        {
            return;
        }

        HostContractVerification.Fail(answered
            ? $"{target.GetType()}.HasName answered true for '{name}' but its TryGetNamedValue answers false. The engine trusts HasName, so this advertises a key whose read yields undefined or resolves on the prototype."
            : $"{target.GetType()}.HasName answered false for '{name}' but its TryGetNamedValue produces a value. The engine trusts HasName, so this silently drops the property from `in`, hasOwnProperty, Object.keys, spread and JSON.stringify while obj['{name}'] still reads it.");
    }

    /// <summary>
    /// Verifier for the <see cref="NameAt"/> contract: every advertised name must be readable, and no name may
    /// repeat.
    /// </summary>
    private static void AssertAdvertisedNamesExist(NamedPropertyObject target, List<JsValue> keys, int projected)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < projected; i++)
        {
            var name = keys[i].ToString();
            if (!seen.Add(name))
            {
                HostContractVerification.Fail($"{target.GetType()}.NameAt reported '{name}' more than once. A duplicate key makes Object.keys and Object.getOwnPropertyNames report the property twice.");
            }

            if (!target.TryGetNamedValue(name, out _))
            {
                HostContractVerification.Fail($"{target.GetType()}.NameAt advertised '{name}' but its TryGetNamedValue answers false for it. Object.keys and Object.getOwnPropertyNames would list a key that reads as undefined or resolves on the prototype.");
            }
        }
    }

    /// <summary>
    /// The key as a name, or <see langword="false"/> for a key the projection cannot own — a symbol, a private
    /// name, or an object key that would need an observable <c>ToPrimitive</c>. Those fall through to the
    /// ordinary property bag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetName(JsValue property, [NotNullWhen(true)] out string? name)
    {
        if (property is JsString jsString)
        {
            name = jsString.ToString();
            return true;
        }

        if (property is JsSymbol or PrivateName or ObjectInstance)
        {
            name = null;
            return false;
        }

        name = TypeConverter.ToString(property);
        return true;
    }

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
        if (TryGetName(property, out var name) && ReadName(name, out value))
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
        if (TryGetName(property, out var name) && ReadName(name, out var value))
        {
            return new PropertyDescriptor(value, IsNameEnumerable(name) ? EnumerableFlags : NonEnumerableFlags);
        }

        return base.GetOwnProperty(property);
    }

    /// <summary>
    /// Existence and enumerability answered from <see cref="HasName"/> and <see cref="IsNameEnumerable"/>,
    /// with no descriptor materialized.
    /// </summary>
    protected internal sealed override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if (TryGetName(property, out var name) && ProbeName(name))
        {
            return IsNameEnumerable(name) ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
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

        var keys = CollectNames();
        var projected = keys.Count;
        foreach (var stored in base.GetOwnPropertyKeys(types))
        {
            // A bag entry the projection has since started carrying is shadowed by it — an expando written
            // before the name appeared. Listing both would put a duplicate into [[OwnPropertyKeys]]. Nothing
            // is asked at all in the usual case, where the bag is empty.
            if (projected > 0 && stored is JsString storedName && ProbeName(storedName.ToString()))
            {
                continue;
            }

            keys.Add(stored);
        }

        if (HostContractVerification.Enabled)
        {
            AssertAdvertisedNamesExist(this, keys, projected);
        }

        return keys;
    }

    /// <summary>
    /// The same order as <see cref="GetOwnPropertyKeys"/>, with the descriptors materialized.
    /// </summary>
    public sealed override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        var names = CollectNames();
        foreach (var key in names)
        {
            var name = key.ToString();
            if (ReadName(name, out var value))
            {
                var flags = IsNameEnumerable(name) ? EnumerableFlags : NonEnumerableFlags;
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(key, new PropertyDescriptor(value, flags));
            }
        }

        foreach (var entry in base.GetOwnProperties())
        {
            // Shadowed bag entries are skipped, exactly as in GetOwnPropertyKeys.
            if (names.Count > 0 && entry.Key is JsString storedName && ProbeName(storedName.ToString()))
            {
                continue;
            }

            yield return entry;
        }
    }

    /// <summary>
    /// Refuses <c>delete</c> of a projected name (sloppy mode: the expression evaluates to <c>false</c>;
    /// strict mode: <c>TypeError</c>). Every other key deletes ordinarily.
    /// </summary>
    public sealed override bool Delete(JsValue property)
    {
        if (TryGetName(property, out var name) && ProbeName(name))
        {
            return false;
        }

        return base.Delete(property);
    }

    /// <summary>
    /// Refuses <c>[[DefineOwnProperty]]</c> on a projected name — the projection is read-only and would shadow
    /// the definition anyway. Every other key defines ordinarily.
    /// </summary>
    public sealed override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (TryGetName(property, out var name) && ProbeName(name))
        {
            return false;
        }

        return base.DefineOwnProperty(property, desc);
    }

    /// <summary>
    /// The projected names as keys, in <c>[[OwnPropertyKeys]]</c> order. The index list is allocated only when
    /// the projection actually carries a canonical-array-index name, which is the uncommon case.
    /// </summary>
    private List<JsValue> CollectNames()
    {
        var count = NameCount;
        var keys = new List<JsValue>(count < 0 ? 0 : count);
        List<uint>? indices = null;

        for (var i = 0; i < count; i++)
        {
            if (i > 0 && i % Engine.ConstraintCheckInterval == 0)
            {
                _engine.Constraints.Check();
            }

            var name = NameAt(i);
            if (name is null)
            {
                Throw.InvalidOperationException($"{GetType()}.NameAt({i}) returned null; every name below NameCount must be a string.");
            }

            var arrayIndex = ArrayInstance.ParseArrayIndex(name);
            if (arrayIndex < ArrayOperations.MaxArrayLength)
            {
                (indices ??= new List<uint>()).Add(arrayIndex);
            }
            else
            {
                keys.Add(JsString.Create(name));
            }
        }

        if (indices is null)
        {
            return keys;
        }

        indices.Sort();
        var ordered = new List<JsValue>(indices.Count + keys.Count);
        foreach (var arrayIndex in indices)
        {
            ordered.Add(JsString.Create(arrayIndex));
        }

        ordered.AddRange(keys);
        return ordered;
    }
}
