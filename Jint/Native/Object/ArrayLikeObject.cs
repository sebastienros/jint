using System.Diagnostics;
using System.Runtime.CompilerServices;
using Jint.Native.Array;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Base class for host-defined, read-only, array-like objects — live views over native indexed state (a DOM
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
/// <c>TryGetOwnPropertyValue</c>, <c>ProbeOwnProperty</c>, the key enumerations, <c>Delete</c> and
/// <c>DefineOwnProperty</c> mutually consistent. Both members are re-consulted on every operation, so a
/// collection that grows or shrinks between reads is observed live, exactly like a DOM collection.
/// </para>
/// <para>
/// <b>The JS-visible model.</b> Canonical array indices below <see cref="Length"/> for which
/// <see cref="TryGetIndex"/> answers are own data properties
/// <c>{ writable: false, enumerable: true, configurable: true }</c>; <c>length</c> is
/// <c>{ writable: false, enumerable: false, configurable: true }</c>. Writes to either are ignored in sloppy
/// mode and raise <c>TypeError</c> in strict mode, and <c>delete</c> / <c>Object.defineProperty</c> against an
/// index key or <c>length</c> are refused — the WebIDL platform-object shape. Named properties are ordinary:
/// the inherited property bag and the prototype work as usual, and expandos may be added.
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
/// <b>Extending it.</b> <see cref="ObjectInstance.Get(JsValue, JsValue)"/> is sealed, so a collection needing to
/// intercept every <i>named</i> read through <c>Get</c> cannot derive from this class and should stay on plain
/// <see cref="ObjectInstance"/>. Live named getters are still available by overriding
/// <see cref="ObjectInstance.GetOwnProperty"/> (and <see cref="GetOwnPropertyKeys"/> /
/// <see cref="GetOwnProperties"/> to advertise them): such an override <b>must</b> delegate canonical array-index
/// keys and <c>length</c> to <c>base</c>, which a Debug build of Jint verifies on every read.
/// </para>
/// <para>
/// <b>Read-only by design.</b> There is no write hook in this version; every mutating <c>Array.prototype</c>
/// generic (<c>sort</c>, <c>fill</c>, <c>reverse</c>, …) fails with the spec-shaped <c>TypeError</c> an ordinary
/// non-writable property produces. If the collection is a static snapshot rather than a live view, copying it
/// into a <see cref="JsArray"/> is cheaper and gives full array semantics; this class exists for the live case,
/// which cannot be copied.
/// </para>
/// </remarks>
public abstract class ArrayLikeObject : ObjectInstance
{
    // { writable: false, enumerable: true, configurable: true } — what browsers report for the indices of a
    // platform array-like. configurable:true (plus the [[Delete]]/[[DefineOwnProperty]] refusals below) is the
    // WebIDL shape and is what lets a shrinking collection stay within the [[GetOwnProperty]] invariants: a
    // non-configurable property may never afterwards report as absent.
    private const PropertyFlag IndexFlags = PropertyFlag.Configurable | PropertyFlag.Enumerable | PropertyFlag.WritableSet;

    // { writable: false, enumerable: false, configurable: true } — configurable for the same invariant reason,
    // the value changing from call to call.
    private const PropertyFlag LengthFlags = PropertyFlag.Configurable | PropertyFlag.EnumerableSet | PropertyFlag.WritableSet;

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
    /// would report at the same instant — a Debug build of Jint verifies that on every read. Never hand back a
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
    /// The single funnel every engine-side index read goes through, so the host contract is enforced in one
    /// place: a <see langword="false"/> answer always leaves <paramref name="value"/> as <c>undefined</c> rather
    /// than whatever the host left in the <c>out</c> slot, and a <see langword="true"/> answer is checked for the
    /// documented "never null" obligation in Debug.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ReadIndex(uint index, out JsValue value)
    {
        if (TryGetIndex(index, out value))
        {
            Debug.Assert(value is not null, $"{GetType()}.TryGetIndex answered index {index} with a CLR null; return a JsValue or answer false.");
            return true;
        }

        value = Undefined;
        return false;
    }

    /// <summary>
    /// Sealed so no subclass can go exotic underneath the lanes that resolve an indexed read without calling
    /// <c>Get</c> at all (the <c>ArrayOperations</c> dispatcher and the interpreter's computed-read branch).
    /// The body is the ordinary implementation, which is what the constructor's
    /// <see cref="PropertyAccessSemantics.Ordinary"/> declaration asserts.
    /// </summary>
    public sealed override JsValue Get(JsValue property, JsValue receiver) => base.Get(property, receiver);

    /// <summary>
    /// Index and <c>length</c> reads resolve straight out of the host with no descriptor at all; every other key
    /// falls through to the base implementation, which is the descriptor-driven answer through the (possibly
    /// overridden) <see cref="ObjectInstance.GetOwnProperty"/> — so a named-getter subclass stays correct without
    /// touching this hook.
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

        return base.TryGetOwnPropertyValue(property, receiver, out value);
    }

    /// <inheritdoc />
    public override PropertyDescriptor GetOwnProperty(JsValue property)
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

        return base.GetOwnProperty(property);
    }

    /// <summary>
    /// Existence and enumerability for <c>in</c>, <c>hasOwnProperty</c>, <c>propertyIsEnumerable</c>,
    /// <c>Object.keys</c>/<c>values</c>/<c>entries</c>, <c>Object.assign</c>, object spread and
    /// <c>JSON.stringify</c> — answered from the same two primitives as <see cref="GetOwnProperty"/>, with no
    /// descriptor materialized.
    /// </summary>
    protected internal sealed override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if (ArrayInstance.IsArrayIndex(property, out var index))
        {
            return ReadIndex(index, out _) ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.Missing;
        }

        if (CommonProperties.Length.Equals(property))
        {
            return OwnPropertyProbe.NonEnumerable;
        }

        return base.ProbeOwnProperty(property);
    }

    /// <summary>
    /// Ordinary <c>[[OwnPropertyKeys]]</c> order: the present indices ascending, then <c>length</c>, then the
    /// property bag's string keys in insertion order, then symbols. Override only to add host-defined named keys
    /// (paired with a <see cref="GetOwnProperty"/> override), and always append to <c>base</c>'s result.
    /// </summary>
    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
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

            if (ReadIndex(i, out _))
            {
                keys.Add(JsString.Create(i));
            }
        }

        keys.Add(CommonProperties.Length);
        keys.AddRange(base.GetOwnPropertyKeys(types));

        return keys;
    }

    /// <summary>
    /// The same order as <see cref="GetOwnPropertyKeys"/>, with the descriptors materialized. See that member for
    /// the override contract.
    /// </summary>
    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
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

        foreach (var entry in base.GetOwnProperties())
        {
            yield return entry;
        }
    }

    /// <summary>
    /// Refuses <c>delete</c> of an index the collection currently has and of <c>length</c> (sloppy mode: the
    /// expression evaluates to <c>false</c>; strict mode: <c>TypeError</c>) — the WebIDL platform-object shape.
    /// An index the collection does not have deletes vacuously, like any absent property.
    /// </summary>
    public sealed override bool Delete(JsValue property)
    {
        if (ArrayInstance.IsArrayIndex(property, out var index))
        {
            return !ReadIndex(index, out _);
        }

        if (CommonProperties.Length.Equals(property))
        {
            return false;
        }

        return base.Delete(property);
    }

    /// <summary>
    /// Refuses <c>[[DefineOwnProperty]]</c> on <c>length</c> and on <b>every</b> canonical array-index key,
    /// in range or not.
    /// </summary>
    /// <remarks>
    /// Refusing out-of-range indices as well is stricter than WebIDL, which lets an ordinary expando live at an
    /// index at or beyond <c>length</c>. The strict form avoids the projection-versus-bag incoherence that would
    /// appear the moment the live collection grew over such an expando. Named keys are unaffected and define
    /// ordinarily.
    /// </remarks>
    public sealed override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (ArrayInstance.IsArrayIndex(property, out _) || CommonProperties.Length.Equals(property))
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
}
