using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop;

/// <summary>
/// Instantiated once per exposed CLR type (see ObjectWrapper's array-like resolution cache) so that
/// per-wrapper creation is a plain virtual call and constructor invocation instead of
/// <c>Activator.CreateInstance</c> with argument binding.
/// </summary>
internal abstract class ArrayLikeWrapperFactory
{
    public abstract ArrayLikeWrapper Create(Engine engine, object target, Type type);
}

internal sealed class ArrayWrapperFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapperFactory
{
    public override ArrayLikeWrapper Create(Engine engine, object target, Type type)
        => new ArrayWrapper<T>(engine, (T[]) target, type);
}

internal sealed class GenericListWrapperFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapperFactory
{
    public override ArrayLikeWrapper Create(Engine engine, object target, Type type)
        => new GenericListWrapper<T>(engine, (IList<T>) target, type);
}

internal sealed class ReadOnlyListWrapperFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapperFactory
{
    public override ArrayLikeWrapper Create(Engine engine, object target, Type type)
        => new ReadOnlyListWrapper<T>(engine, (IReadOnlyList<T>) target, type);
}

/// <summary>
/// Enumerates a sequence that has no count and no indexer into the array-backed view
/// <see cref="EnumerableConversionMode.Snapshot"/> exposes. Resolved once per exposed type, like
/// <see cref="ArrayLikeWrapperFactory"/>, and deliberately kept in a cache of its own: that one is consulted
/// for every crossing whatever the engine's options say, while this one exists only for the engines that
/// asked for snapshots.
/// </summary>
internal abstract class EnumerableSnapshotFactory
{
    public abstract ArrayLikeWrapper Create(Engine engine, IEnumerable target);
}

internal sealed class EnumerableSnapshotFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : EnumerableSnapshotFactory
{
    public override ArrayLikeWrapper Create(Engine engine, IEnumerable target)
        => new ArrayWrapper<T>(engine, new List<T>((IEnumerable<T>) target).ToArray(), typeof(T[]));
}

/// <summary>
/// The snapshot of a sequence whose element type is not known — a non-generic <see cref="IEnumerable"/>.
/// </summary>
internal sealed class ObjectEnumerableSnapshotFactory : EnumerableSnapshotFactory
{
    internal static readonly ObjectEnumerableSnapshotFactory Instance = new();

    public override ArrayLikeWrapper Create(Engine engine, IEnumerable target)
    {
        var items = new List<object?>();
        foreach (var item in target)
        {
            items.Add(item);
        }

        return new ArrayWrapper<object?>(engine, items.ToArray(), typeof(object?[]));
    }
}

internal abstract class ArrayLikeWrapper : ObjectWrapper
{
    /// <summary>
    /// Whether indexed element access can execute user CLR code (a custom IList/IReadOnlyList
    /// implementation). Plain memory-backed targets (T[], List&lt;T&gt;) skip the host-boundary
    /// constraint probe on the per-element hot path.
    /// </summary>
    private readonly bool _elementAccessMayRunHostCode;

    /// <summary>
    /// Whether this view has element properties at all: the host's <see cref="TypeResolver.MemberFilter"/>
    /// must admit the indexer the lanes below stand for. Read once per wrapper rather than per element, so a
    /// filtered engine pays one memoized decision per exposed type and an unfiltered one pays a field read
    /// (#3558).
    /// </summary>
    private readonly bool _elementsExposed;

    protected ArrayLikeWrapper(
        Engine engine,
        object obj,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] Type itemType,
        Type? type,
        bool elementAccessMayRunHostCode) : base(engine, obj, type)
    {
        ItemType = itemType;
        _elementAccessMayRunHostCode = elementAccessMayRunHostCode;
        _elementsExposed = IndexedElementsExposed;
        if (engine.Options.Interop.AttachArrayPrototype)
        {
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }
    }

    /// <summary>
    /// The element type a JS value is coerced to on an element write, which is what
    /// <c>ClrTypeConverter.Convert</c> is handed and therefore what its annotation asks to be preserved:
    /// the type's public constructors, and its public fields for the enum case.
    /// </summary>
    /// <remarks>
    /// The constructor parameter that fills this carries the same annotation, which is what makes the
    /// <c>[DynamicallyAccessedMembers]</c> on <see cref="ArrayWrapperFactory{T}"/>,
    /// <see cref="GenericListWrapperFactory{T}"/>, <see cref="ReadOnlyListWrapperFactory{T}"/> and
    /// <see cref="EnumerableSnapshotFactory{T}"/> — and on the wrappers they build — required by something
    /// rather than decorative: each passes <c>typeof(T)</c> here. The one subclass that cannot satisfy it is
    /// <c>ListWrapper</c>, whose element type is an array's at run time, and its diagnostic is the honest
    /// statement that a trimmer cannot preserve what <see cref="Type.GetElementType"/> returns.
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)]
    private Type ItemType { get; }

    public abstract int Length { get; }

    // Every subclass is a view over something with an indexer, including the IReadOnlyList<T> one the type
    // descriptor does not recognize as integer-indexed — so the only thing left to ask is whether the host's
    // member filter lets script reach it. Answering false here is what routes every Array.prototype generic
    // to ObjectOperations, exactly as a countable-but-not-indexable Queue<T> is routed (#3558).
    internal sealed override bool HasIndexedElements => _elementsExposed;

    /// <summary>
    /// Whether <paramref name="property"/> spells a position this view does not have — index-shaped, and
    /// either outside <c>[0, Length)</c> or never addressable at all (negative, non-canonical, past what the
    /// target can hold).
    /// </summary>
    /// <remarks>
    /// This is the single question every lane that answers <em>whether an index exists</em> has to answer the
    /// same way. <see cref="Get"/>, <see cref="HasProperty"/> and <c>GetOwnPropertyKeys</c> already did;
    /// <c>[[GetOwnProperty]]</c> did not, because it was left to <see cref="ObjectWrapper"/>, which resolves the
    /// reflected indexer and reports a descriptor for <em>any</em> parseable index. So <c>3 in list</c> was
    /// <see langword="false"/> while <c>list.hasOwnProperty(3)</c> was <see langword="true"/> on the same object,
    /// which is not a divergence an implementation may choose:
    /// <see href="https://tc39.es/ecma262/#sec-ordinaryhasproperty">OrdinaryHasProperty</see> is defined in terms
    /// of <c>[[GetOwnProperty]]</c> (#3423).
    /// <para>
    /// A dictionary-shaped target (Newtonsoft's <c>JObject</c> is both <c>IDictionary&lt;string,_&gt;</c> and
    /// <c>IList&lt;_&gt;</c>) answers a string key from its own keys, exactly as <see cref="HasProperty"/>
    /// defers for it.
    /// </para>
    /// <para>
    /// When the host's member filter rejects the indexer, <em>every</em> index-shaped key names an absent
    /// position: the view has no elements to have positions of. That is also the answer the reflected lane
    /// underneath already gave — it resolves no accessor for the hidden member, so <c>hasOwnProperty</c> and
    /// <c>Object.keys</c> said so while <c>in</c> and the element lanes did not (#3558).
    /// </para>
    /// </remarks>
    private bool NamesAbsentPosition(JsValue property)
    {
        if (_typeDescriptor.IsDictionary)
        {
            return false;
        }

        var key = ClassifyElementKey(property, out var index);
        if (!_elementsExposed)
        {
            return key != ElementKey.None;
        }

        return key == ElementKey.OutOfBand
               || (key == ElementKey.Position && (uint) index >= (uint) Length);
    }

    /// <summary>
    /// The descriptor lane, answered from the view's index range before the reflected indexer is consulted.
    /// Reached by <c>Object.getOwnPropertyDescriptor</c>, <c>Reflect.getOwnPropertyDescriptor</c>,
    /// <c>hasOwnProperty</c>, <c>propertyIsEnumerable</c> and — through the default
    /// <see cref="ObjectInstance.ProbeOwnProperty"/> — everything that asks whether a key exists.
    /// </summary>
    public sealed override PropertyDescriptor GetOwnProperty(JsValue property)
        => NamesAbsentPosition(property) ? PropertyDescriptor.Undefined : base.GetOwnProperty(property);

    /// <summary>
    /// The existence probe, kept in step with <see cref="GetOwnProperty"/> by construction rather than by
    /// deriving it: an absent position is answered without resolving an accessor or building a descriptor.
    /// </summary>
    protected internal sealed override OwnPropertyProbe ProbeOwnProperty(JsValue property)
        => NamesAbsentPosition(property) ? OwnPropertyProbe.Missing : base.ProbeOwnProperty(property);

    /// <summary>
    /// A position this view does not have cannot be defined into existence either. Without this,
    /// <c>Object.defineProperty(view, 5, …)</c> would store a descriptor in the wrapper's own property bag for
    /// a key <see cref="GetOwnProperty"/> then denies — a property that both exists and does not. The refusal
    /// is what script already saw (the reflected indexer's descriptor is non-configurable, so the redefinition
    /// was rejected); it is now a property of the view rather than an accident of reflection.
    /// </summary>
    public sealed override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        => !NamesAbsentPosition(property) && base.DefineOwnProperty(property, desc);

    public sealed override JsValue Get(JsValue property, JsValue receiver)
    {
        var key = ClassifyElementKey(property, out var index);
        if (key == ElementKey.Position && _elementsExposed && (uint) index < (uint) Length)
        {
            var result = GetJsValueAt(index);
            if (_elementAccessMayRunHostCode)
            {
                _engine.CheckAmortizedConstraintsAtHostBoundary();
            }
            return result;
        }

        if (key != ElementKey.None)
        {
            // out-of-range, negative and non-canonical indices read like JS array holes — and so does every
            // index of a view whose indexer the host's member filter hides, which is the answer the
            // reflected lane gives for a filtered-out member (#3558)
            return Undefined;
        }

        return base.Get(property, receiver);
    }

    public sealed override bool HasProperty(JsValue property)
    {
        // dictionary-shaped targets (e.g. Newtonsoft's JObject: both IDictionary<string,_> and IList<_>)
        // answer membership by key, not by index range
        if (_typeDescriptor.IsDictionary)
        {
            return base.HasProperty(property);
        }

        // membership of an array-like view is exactly the index range [0, Length); falling through would
        // consult the reflected indexer, which reports presence for any parseable index (so e.g.
        // "-1 in view" would be true)
        var key = ClassifyElementKey(property, out var index);
        if (key == ElementKey.Position)
        {
            return _elementsExposed && (uint) index < (uint) Length;
        }

        if (key == ElementKey.OutOfBand)
        {
            return false;
        }

        return base.HasProperty(property);
    }

    public sealed override bool Delete(JsValue property)
    {
        if (!_elementsExposed && NamesAbsentPosition(property))
        {
            // The host's member filter hides this view's indexer, so there is no element property here to
            // delete and OrdinaryDelete returns true for a property that is not there. Asked before the
            // writability lanes below so the two refusals compose rather than mask each other: containment
            // decides whether there is a property at all, writability only what may be done to one (#3558).
            return true;
        }

        if (!CanWrite || !Extensible)
        {
            if (property is JsString dictionaryKey
                && _typeDescriptor.IsStringKeyedGenericDictionary
                && _typeDescriptor.CanTestDictionaryKey)
            {
                var contains = _typeDescriptor.ContainsDictionaryKey(Target, dictionaryKey.ToString());
                _engine.CheckAmortizedConstraintsAtHostBoundary();
                if (contains)
                {
                    return false;
                }

                if (ArrayInstance.ParseArrayIndex(dictionaryKey.ToString()) != uint.MaxValue)
                {
                    return true;
                }
            }

            if (!_typeDescriptor.IsDictionary)
            {
                var frozenKey = ClassifyElementKey(property, out var frozenIndex);
                if (frozenKey == ElementKey.Position)
                {
                    return (uint) frozenIndex >= (uint) Length;
                }

                if (frozenKey == ElementKey.OutOfBand)
                {
                    return true;
                }
            }

            return ProbeOwnPropertyChecked(property) == OwnPropertyProbe.Missing;
        }

        var key = ClassifyElementKey(property, out var index);
        if (key != ElementKey.None)
        {
            if (key == ElementKey.OutOfBand || (uint) index >= (uint) Length)
            {
                // there is no such position, so there is nothing to delete: OrdinaryDelete returns true
                // for a property that is not there. Reaching the collection with the index instead is
                // what raised the CLR's own ArgumentOutOfRangeException out of Evaluate (#3384).
                return true;
            }

            if (IsFixedSize)
            {
                // elements of a fixed-size CLR array view cannot be removed (and resetting them to a
                // default value on delete would let Array.prototype.pop/shift/splice silently zero
                // slots before their length write throws). Mirror integer-indexed exotic object
                // semantics instead: false for an in-range index, true for anything out of range.
                return false;
            }

            var defaultValue = default(object);
            if (typeof(JsValue).IsAssignableFrom(ItemType))
            {
                defaultValue = JsValue.Undefined;
            }
            else if (ItemType.IsValueType)
            {
                defaultValue = Activator.CreateInstance(ItemType);
            }

            DoSetAt(index, defaultValue);
            return true;
        }

        return base.Delete(property);
    }

    public abstract object? GetAt(int index);

    /// <summary>
    /// Element read producing the JsValue directly; typed subclasses override to convert common
    /// primitive item types without boxing the element through <see cref="GetAt"/>.
    /// </summary>
    internal virtual JsValue GetJsValueAt(int index) => FromObject(_engine, GetAt(index));

    /// <summary>
    /// Boxing-free conversion for the item types <see cref="DefaultObjectConverter"/> maps to primitive
    /// JsValues. The typeof checks are JIT-time constants in value-type instantiations, so each closed
    /// generic compiles down to the single matching branch. Returns null for item types that need the
    /// general converter (which the caller routes through <see cref="GetAt"/>/FromObject as before).
    /// </summary>
    private protected static JsValue? TryConvertCommonItem<TItem>(ref TItem item)
    {
        if (typeof(TItem) == typeof(int))
        {
            return JsNumber.Create(Unsafe.As<TItem, int>(ref item));
        }
        if (typeof(TItem) == typeof(double))
        {
            return JsNumber.Create(Unsafe.As<TItem, double>(ref item));
        }
        if (typeof(TItem) == typeof(long))
        {
            return JsNumber.Create(Unsafe.As<TItem, long>(ref item));
        }
        if (typeof(TItem) == typeof(uint))
        {
            return JsNumber.Create(Unsafe.As<TItem, uint>(ref item));
        }
        if (typeof(TItem) == typeof(ulong))
        {
            return JsNumber.Create(Unsafe.As<TItem, ulong>(ref item));
        }
        if (typeof(TItem) == typeof(short))
        {
            return JsNumber.Create(Unsafe.As<TItem, short>(ref item));
        }
        if (typeof(TItem) == typeof(ushort))
        {
            return JsNumber.Create(Unsafe.As<TItem, ushort>(ref item));
        }
        if (typeof(TItem) == typeof(byte))
        {
            return JsNumber.Create(Unsafe.As<TItem, byte>(ref item));
        }
        if (typeof(TItem) == typeof(sbyte))
        {
            return JsNumber.Create(Unsafe.As<TItem, sbyte>(ref item));
        }
        if (typeof(TItem) == typeof(float))
        {
            // float converts through double exactly like DefaultObjectConverter's mapper does
            return JsNumber.Create((double) Unsafe.As<TItem, float>(ref item));
        }
        if (typeof(TItem) == typeof(bool))
        {
            return Unsafe.As<TItem, bool>(ref item) ? JsBoolean.True : JsBoolean.False;
        }
        if (typeof(TItem) == typeof(char))
        {
            return JsString.Create(Unsafe.As<TItem, char>(ref item));
        }
        if (typeof(TItem) == typeof(string))
        {
            var value = Unsafe.As<TItem, string>(ref item);
            return value is null ? Null : JsString.Create(value);
        }

        return null;
    }

    public sealed override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        // The length and fixed-size index lanes below never reach base.Set, which is where a crossing memo
        // is otherwise evicted per key. Dropping it here keeps that guarantee for the one target shape that
        // can carry both a memo and these lanes — a dictionary-shaped array-like such as JObject, where a
        // length write can invalidate arbitrary keys. Everywhere else the memo is already null.
        DropCrossingMemo();

        if (ReferenceEquals(receiver, this) && CommonProperties.Length.Equals(property))
        {
            // A length write adds and removes elements — it is the element lane spelled by count rather than
            // by position — so a view whose indexer the host's member filter hides has nothing to grow or
            // truncate, and `list.length = 0` must not clear a collection whose elements script cannot see.
            // The "length" forwarder itself keeps answering: it is produced from Count, a member the filter
            // decides about separately (#3558).
            if (!_elementsExposed || !CanWrite || !Extensible)
            {
                return false;
            }

            if (value.IsInteger())
            {
                var length = value.AsInteger();
                if (length < 0)
                {
                    Throw.RangeError(_engine.Realm, "Invalid array length");
                }

                if (length == Length)
                {
                    return true;
                }

                if (length > Length)
                {
                    EnsureCapacity(length);
                }
                else
                {
                    // decrease the length, remove items
                    for (var i = Length - 1; i >= length; i--)
                    {
                        RemoveAt(i);
                    }
                }
                return true;
            }

            Throw.TypeError(_engine.Realm, "Invalid array length");
        }

        if (ReferenceEquals(receiver, this))
        {
            // Every index write is answered here rather than through the reflection indexer base.Set would
            // resolve. That indexer takes the index straight to the collection, so it leaked the CLR's own
            // ArgumentOutOfRangeException for anything outside [0, Count) and, for a T[], found the
            // non-generic IList.Item (object-typed) overload, whose writes bypass item-type coercion (#3384).
            var key = ClassifyElementKey(property, out var index);
            if (key != ElementKey.None)
            {
                // An element write to a read-only or non-extensible (frozen) wrapper is refused as an
                // ordinary [[Set]] false — silent in sloppy mode, a TypeError in strict — rather than by
                // materializing a throwaway descriptor and returning true (#2541). Wrappers don't track
                // per-element writability, so a non-extensible wrapper blocks existing-element writes too
                // — a deliberate, contained interop divergence from the spec.
                //
                // Containment is asked first and gets the same ordinary refusal: a view whose indexer the
                // host's member filter rejects has no element property here, so the fixed-size TypeError
                // below must not fire for it — that message answers "you may not write *there*", which is
                // a question about a lane script was never granted (#3558).
                if (!_elementsExposed || !CanWrite || !Extensible)
                {
                    return false;
                }

                if (key == ElementKey.Position && (uint) index < (uint) Length)
                {
                    SetAt(index, value);
                    return true;
                }

                if (IsFixedSize)
                {
                    Throw.TypeError(_engine.Realm, "Cannot write outside the bounds of a fixed-size CLR array");
                }

                if (key == ElementKey.OutOfBand)
                {
                    // a negative, non-canonical or unaddressable index is a position this view can never
                    // have — Get answers undefined for it and HasProperty answers false — so the write is
                    // the ordinary [[Set]] refusal rather than something the collection has to reject
                    return false;
                }

                // At or past the end of a growable target. The view is an extensible ordinary object and
                // the position can exist, so CreateDataProperty succeeds: make room and write, which is
                // exactly what a "length" write of index + 1 does through the lane above.
                SetAt(index, value);
                return true;
            }
        }

        return base.Set(property, value, receiver);
    }

    /// <summary>
    /// Whether an element of this view may be written at all: the engine must be configured for writes
    /// and the target must not have declared itself read-only. Consulted by <see cref="Set"/>,
    /// <see cref="Delete"/> and by <c>ArrayOperations</c>' array-like lane, so that a refusal is the
    /// ordinary <c>[[Set]]</c>/<c>[[Delete]]</c> <see langword="false"/> — a <c>TypeError</c> in strict
    /// mode, silent in sloppy — rather than a CLR exception from the collection.
    /// </summary>
    internal bool CanWrite => _engine.Options.Interop.AllowWrite && !IsReadOnly;

    /// <summary>
    /// Whether the underlying collection cannot change its length (CLR arrays). Enables the direct
    /// integral-index write lane in <see cref="Set"/>.
    /// </summary>
    protected virtual bool IsFixedSize => false;

    /// <summary>
    /// Whether the underlying collection refuses every mutation, elements included — what
    /// <see cref="ICollection{T}.IsReadOnly"/> and <see cref="IList.IsReadOnly"/> declare. Strictly
    /// stronger than <see cref="IsFixedSize"/>, which forbids only the length change.
    /// </summary>
    protected virtual bool IsReadOnly => false;

    /// <summary>
    /// The refusal every length-changing operation on a fixed-size target owes script: a
    /// <c>TypeError</c> it can catch, never the CLR <see cref="NotSupportedException"/> the underlying
    /// collection would raise. Shared by <see cref="ArrayWrapper{T}"/> and by <see cref="ListWrapper"/>,
    /// which is what a <c>T[]</c> falls back to when the typed wrapper cannot be built (#3299).
    /// </summary>
    [DoesNotReturn]
    protected void ThrowFixedSize() => Throw.TypeError(_engine.Realm, "Cannot resize a fixed-size CLR array");

    /// <summary>
    /// The backstop for a read-only target, and deliberately unreachable: <see cref="CanWrite"/> is false
    /// for one, so every lane refuses before a mutator is called and script gets the ordinary
    /// <c>[[Set]]</c>/<c>[[Delete]]</c> refusal a frozen JavaScript array gives. It exists so that a lane
    /// added later cannot reach <c>Add</c>/<c>RemoveAt</c> and leak the collection's own
    /// <see cref="NotSupportedException"/> past script the way three of them did until #3382.
    /// </summary>
    [DoesNotReturn]
    protected void ThrowReadOnly() => Throw.TypeError(_engine.Realm, "Cannot modify a read-only CLR collection");

    /// <summary>
    /// Guard at the head of every length-changing operation of a wrapper that takes its two facts from
    /// the target rather than from its type argument. Read-only is answered first because it is the
    /// stronger claim: a collection can be both (<c>ArrayList.ReadOnly</c>), and only one message is right.
    /// </summary>
    protected void ThrowIfLengthCannotChange()
    {
        if (IsReadOnly)
        {
            ThrowReadOnly();
        }

        if (IsFixedSize)
        {
            ThrowFixedSize();
        }
    }

    /// <summary>
    /// Writes one element, if this view may be written at all. The <see cref="_elementsExposed"/> half is the
    /// backstop <see cref="ThrowReadOnly"/> is for its own fact: every lane already refuses before reaching
    /// here, and a lane added later that does not must still not write through an indexer the host's member
    /// filter hid.
    /// </summary>
    public void SetAt(int index, JsValue value)
    {
        if (_elementsExposed && CanWrite)
        {
            EnsureCapacity(index + 1);
            DoSetAt(index, ConvertToItemType(value));
        }
    }

    protected abstract void DoSetAt(int index, object? value);

    public abstract void AddDefault();

    public abstract void Add(JsValue value);

    public abstract void RemoveAt(int index);

    public virtual void EnsureCapacity(int capacity)
    {
        while (Length < capacity)
        {
            AddDefault();
        }
    }

    protected object? ConvertToItemType(JsValue value)
    {
        object? converted;
        if (ItemType == typeof(JsValue))
        {
            converted = value;
        }
        else if (!ReflectionExtensions.TryConvertViaTypeCoercion(ItemType, Engine.Options.Interop.ValueCoercion, value, out converted))
        {
            // attempt to convert the JsValue to the target type
            converted = value.ToObject();
            if (converted != null && converted.GetType() != ItemType)
            {
                converted = Engine._typeConverter.Convert(converted, ItemType, CultureInfo.InvariantCulture);
            }
        }

        return converted;
    }
}

/// <summary>
/// The untyped view over anything that is an <see cref="IList"/>. It is the least specific of the
/// array-like wrappers and also the one a <c>T[]</c> or an <c>IList&lt;T&gt;</c> degrades to when its typed
/// factory cannot be instantiated — Native AOT has no code for
/// <c>ArrayWrapperFactory&lt;int&gt;</c> and friends (#3299) — so it takes the two facts the typed wrapper
/// carried in its type argument from the target instead: the element type, and whether the length can
/// change.
/// </summary>
internal sealed class ListWrapper : ArrayLikeWrapper
{
    private readonly IList? _list;
    private readonly bool _fixedSize;
    private readonly bool _readOnly;

    internal ListWrapper(Engine engine, IList target, Type type)
        : base(engine, target, ElementTypeOf(target), type, elementAccessMayRunHostCode: target is not (Array or ArrayList))
    {
        _list = target;
        _fixedSize = target.IsFixedSize;

        // Two ways this view is read-only: the target says so, or the exposed contract offers no writable
        // indexer. The second half is what a T[] or a List<T> reaching the engine as IReadOnlyList<T>
        // needs. ReadOnlyListWrapper<T> says read-only from its type argument, but an interface is not
        // among its own GetInterfaces(), so ResolveArrayLikeWrapperFactoryType finds no typed factory for
        // that exposure and it degrades to here. Until #3384 the element lane read the target's writability
        // and wrote straight through a contract that had promised script it could only read; the refusal
        // came from the reflected indexer instead, and therefore only for a string-spelled index.
        _readOnly = target.IsReadOnly || !_typeDescriptor.IsIntegerIndexed;
    }

    /// <summary>
    /// A <see cref="ListWrapper"/> over an array must coerce element writes to the array's element type,
    /// exactly as <see cref="ArrayWrapper{T}"/> does from its type argument: <c>object</c> would hand
    /// <c>IList.this[int]</c> the boxed <c>double</c> a JS number converts to and raise
    /// <see cref="InvalidCastException"/> out of the assignment. Everything else keeps <c>object</c>,
    /// which is all a non-generic <see cref="IList"/> promises.
    /// </summary>
    private static Type ElementTypeOf(IList target)
        => target is Array array ? array.GetType().GetElementType() ?? typeof(object) : typeof(object);

    /// <summary>
    /// Read from the target rather than declared, because this wrapper serves both the growable case and
    /// the fixed-size one: <c>System.Array</c> reaches it through <see cref="IList"/> whenever
    /// <see cref="ArrayWrapper{T}"/> could not be built, and <c>ArrayList.FixedSize</c> reaches it always.
    /// Without this every length-changing operation reached <c>IList.Add</c> and leaked that collection's
    /// <see cref="NotSupportedException"/> past script instead of raising a catchable <c>TypeError</c>.
    /// </summary>
    protected override bool IsFixedSize => _fixedSize;

    /// <summary>
    /// Read from the target for the same reason as <see cref="IsFixedSize"/>, and separately from it
    /// because the non-generic <see cref="IList"/> asks the two questions separately:
    /// <c>ArrayList.FixedSize</c> is fixed-size and writable, <c>ArrayList.ReadOnly</c> is both, and an
    /// embedder's own read-only <see cref="IList"/> may be neither fixed-size nor writable.
    /// </summary>
    protected override bool IsReadOnly => _readOnly;

    public override int Length => _list?.Count ?? 0;

    public override object? GetAt(int index)
    {
        if (_list is not null && index >= 0 && index < _list.Count)
        {
            return _list[index];
        }

        return null;
    }

    protected override void DoSetAt(int index, object? value)
    {
        if (_list is not null)
        {
            _list[index] = value;
        }
    }

    public override void AddDefault()
    {
        ThrowIfLengthCannotChange();
        _list?.Add(null);
    }

    public override void Add(JsValue value)
    {
        ThrowIfLengthCannotChange();
        _list?.Add(ConvertToItemType(value));
    }

    public override void RemoveAt(int index)
    {
        ThrowIfLengthCannotChange();
        _list?.RemoveAt(index);
    }

    public override void EnsureCapacity(int capacity)
    {
        if (_fixedSize || _readOnly)
        {
            if (capacity > Length)
            {
                ThrowIfLengthCannotChange();
            }

            return;
        }

        base.EnsureCapacity(capacity);
    }
}

internal class GenericListWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapper
{
    private readonly IList<T> _list;
    private readonly bool _fixedSize;
    private readonly bool _readOnly;

    public GenericListWrapper(Engine engine, IList<T> target, Type? type)
        : base(engine, target, typeof(T), type, elementAccessMayRunHostCode: target is not (T[] or List<T>))
    {
        _list = target;
        _fixedSize = IsFixedSizeArray(target);
        _readOnly = !_fixedSize && target.IsReadOnly;
    }

    /// <summary>
    /// The two shapes whose <see cref="ICollection{T}.IsReadOnly"/> means <em>cannot grow</em> rather
    /// than <em>cannot be written</em>, which is the one place the generic interface's single flag
    /// disagrees with the non-generic <see cref="IList"/>'s two. Both accept element writes and refuse
    /// every length change, so they are fixed-size here and not read-only. A <c>T[]</c> reaches this
    /// wrapper when the exposed type is a declared <see cref="IList{T}"/> rather than the array type,
    /// which <see cref="ObjectWrapper.Create(Engine, object, Type?)"/> and <c>clrHelper</c> both allow.
    /// </summary>
    private static bool IsFixedSizeArray(IList<T> target) => target is T[] or ArraySegment<T>;

    protected override bool IsFixedSize => _fixedSize;

    /// <summary>
    /// Taken from the target's own <see cref="ICollection{T}.IsReadOnly"/>: a
    /// <c>ReadOnlyCollection&lt;T&gt;</c>, an immutable collection and a host list that declares itself
    /// read-only all raise <see cref="NotSupportedException"/> from <c>Add</c>/<c>RemoveAt</c> and from
    /// the indexer setter, which script cannot catch (#3382).
    /// </summary>
    protected override bool IsReadOnly => _readOnly;

    public override int Length => _list.Count;

    public override object? GetAt(int index)
    {
        if (index >= 0 && index < _list.Count)
        {
            return _list[index];
        }

        return null;
    }

    internal override JsValue GetJsValueAt(int index)
    {
        if (index >= 0 && index < _list.Count)
        {
            var item = _list[index];
            return TryConvertCommonItem(ref item) ?? FromObject(_engine, item);
        }

        // defensive: callers bounds-check; out-of-range reads like a JS array hole
        return Undefined;
    }

    protected override void DoSetAt(int index, object? value) => _list[index] = (T) value!;

    public override void AddDefault()
    {
        ThrowIfLengthCannotChange();
        _list.Add(default!);
    }

    public override void Add(JsValue value)
    {
        ThrowIfLengthCannotChange();
        var converted = ConvertToItemType(value);
        _list.Add((T) converted!);
    }

    public override void RemoveAt(int index)
    {
        ThrowIfLengthCannotChange();
        _list.RemoveAt(index);
    }

    public override void EnsureCapacity(int capacity)
    {
        if (capacity > Length)
        {
            ThrowIfLengthCannotChange();
        }

        base.EnsureCapacity(capacity);
    }
}

/// <summary>
/// Live view over a single-rank CLR array (<c>T[]</c>) used by <see cref="ArrayConversionMode.LiveView"/>.
/// Element reads and writes go straight to the underlying array; because CLR arrays are fixed-size,
/// every length-changing operation surfaces as a JavaScript <c>TypeError</c> instead of leaking a CLR
/// <see cref="NotSupportedException"/> (which is what routing <c>T[]</c> through
/// <see cref="GenericListWrapper{T}"/> would do via <c>IList&lt;T&gt;.Add</c>).
/// </summary>
internal sealed class ArrayWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapper
{
    private readonly T[] _array;

    public ArrayWrapper(Engine engine, T[] target, Type? type)
        : base(engine, target, typeof(T), type, elementAccessMayRunHostCode: false)
    {
        _array = target;
    }

    public override int Length => _array.Length;

    protected override bool IsFixedSize => true;

    public override object? GetAt(int index)
    {
        var array = _array;
        if ((uint) index < (uint) array.Length)
        {
            return array[index];
        }

        return null;
    }

    internal override JsValue GetJsValueAt(int index)
    {
        var array = _array;
        if ((uint) index < (uint) array.Length)
        {
            var item = array[index];
            return TryConvertCommonItem(ref item) ?? FromObject(_engine, item);
        }

        // defensive: callers bounds-check; out-of-range reads like a JS array hole
        return Undefined;
    }

    protected override void DoSetAt(int index, object? value)
    {
        // defensive bounds guard: growth attempts are rejected by EnsureCapacity/Set before this
        // method is reached, and the fixed-size Delete lane never calls it
        var array = _array;
        if ((uint) index < (uint) array.Length)
        {
            array[index] = (T) value!;
        }
    }

    public override void AddDefault() => ThrowFixedSize();

    public override void Add(JsValue value) => ThrowFixedSize();

    public override void RemoveAt(int index) => ThrowFixedSize();

    public override void EnsureCapacity(int capacity)
    {
        if (capacity > _array.Length)
        {
            ThrowFixedSize();
        }
    }
}

internal sealed class ReadOnlyListWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapper
{
    private readonly IReadOnlyList<T> _list;

    public ReadOnlyListWrapper(Engine engine, IReadOnlyList<T> target, Type type)
        : base(engine, target, typeof(T), type, elementAccessMayRunHostCode: target is not (T[] or List<T>))
    {
        _list = target;
    }

    public override int Length => _list.Count;

    public override object? GetAt(int index)
    {
        if (index >= 0 && index < _list.Count)
        {
            return _list[index];
        }

        return null;
    }

    internal override JsValue GetJsValueAt(int index)
    {
        if (index >= 0 && index < _list.Count)
        {
            var item = _list[index];
            return TryConvertCommonItem(ref item) ?? FromObject(_engine, item);
        }

        // defensive: callers bounds-check; out-of-range reads like a JS array hole
        return Undefined;
    }

    /// <summary>
    /// An <see cref="IReadOnlyList{T}"/> exposes no write of any kind, so this view is read-only in the
    /// full sense. It replaces the <c>CanWrite</c> override this class used to carry, which said the same
    /// thing about element writes alone and left <c>Array.prototype</c>'s length-changing generics
    /// reaching the mutators below — where they raised the CLR's own
    /// <see cref="NotSupportedException"/> past script (#3382).
    /// </summary>
    protected override bool IsReadOnly => true;

    public override void AddDefault() => ThrowReadOnly();

    protected override void DoSetAt(int index, object? value) => ThrowReadOnly();

    public override void Add(JsValue value) => ThrowReadOnly();

    public override void RemoveAt(int index) => ThrowReadOnly();
}
