using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;

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

    protected ArrayLikeWrapper(
        Engine engine,
        object obj,
        Type itemType,
        Type? type,
        bool elementAccessMayRunHostCode) : base(engine, obj, type)
    {
        ItemType = itemType;
        _elementAccessMayRunHostCode = elementAccessMayRunHostCode;
        if (engine.Options.Interop.AttachArrayPrototype)
        {
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)]
    private Type ItemType { get; }

    public abstract int Length { get; }

    // every subclass is a view over something with an indexer, including the IReadOnlyList<T> one the
    // type descriptor does not recognize as integer-indexed
    internal sealed override bool HasIndexedElements => true;

    /// <summary>
    /// What a property key names on this view. Every element lane below is keyed on the <em>index</em>
    /// rather than on how script spelled it: <c>x[3]</c> and <c>x["3"]</c> are one property key, because
    /// <see href="https://tc39.es/ecma262/#sec-topropertykey">ToPropertyKey</see> turns both into the
    /// String <c>"3"</c>. Answering the two spellings from different lanes is answering one question
    /// twice, and the lane that used to serve the string spelling was the reflected indexer, which reaches
    /// the collection with whatever index it parses out of the key (#3384).
    /// </summary>
    private enum ElementKey
    {
        /// <summary>Not a position of this view: a member name, a symbol, a fractional number, or any string key of a dictionary-shaped target.</summary>
        None,

        /// <summary>A position the target could address. May be at or past <see cref="Length"/>.</summary>
        Position,

        /// <summary>Index-shaped but never a position of this view: negative, non-canonical (<c>"08"</c>, <c>"+3"</c>), or past what the target can address.</summary>
        OutOfBand,
    }

    /// <summary>
    /// The highest position an element lane will address, one below <see cref="int.MaxValue"/> so that
    /// growing to <c>index + 1</c> cannot overflow.
    /// </summary>
    private const int MaxPosition = int.MaxValue - 1;

    private ElementKey ClassifyElementKey(JsValue property, out int index)
    {
        index = 0;

        if (property is JsNumber number)
        {
            var value = number._value;
            if (!TypeConverter.IsIntegralNumber(value))
            {
                return ElementKey.None;
            }

            if (value < 0 || value > MaxPosition)
            {
                return ElementKey.OutOfBand;
            }

            index = (int) value;
            return ElementKey.Position;
        }

        // a symbol names no position, and a dictionary-shaped target (e.g. Newtonsoft's JObject: both
        // IDictionary<string,_> and IList<_>) answers a string key from its own keys rather than by index
        if (property is not JsString jsString || _typeDescriptor.IsDictionary)
        {
            return ElementKey.None;
        }

        var member = jsString.ToString();
        var parsed = ArrayInstance.ParseArrayIndex(member);
        if (parsed != uint.MaxValue)
        {
            if (parsed > MaxPosition)
            {
                return ElementKey.OutOfBand;
            }

            index = (int) parsed;
            return ElementKey.Position;
        }

        // One character rules out every ordinary member name that reaches this view — "length", "Count",
        // "push" — which would otherwise pay a whole long.TryParse only to be rejected by it.
        if (member.Length == 0 || !IsIntegerShapedStart(member[0]))
        {
            return ElementKey.None;
        }

        // an integer-shaped but non-canonical key ("-1", "08") is not a position of the view either, and
        // must not fall through to the reflected indexer, which parses one out of it and reaches the
        // collection with an index the collection rejects
        return long.TryParse(member, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? ElementKey.OutOfBand
            : ElementKey.None;
    }

    /// <summary>
    /// Whether <paramref name="c"/> can begin something <see cref="NumberStyles.Integer"/> parses: a digit,
    /// a sign, or the leading white space it tolerates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIntegerShapedStart(char c)
        => (uint) (c - '0') <= 9 || c == '-' || c == '+' || char.IsWhiteSpace(c);

    public sealed override JsValue Get(JsValue property, JsValue receiver)
    {
        var key = ClassifyElementKey(property, out var index);
        if (key == ElementKey.Position && (uint) index < (uint) Length)
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
            // out-of-range, negative and non-canonical indices read like JS array holes
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
            return (uint) index < (uint) Length;
        }

        if (key == ElementKey.OutOfBand)
        {
            return false;
        }

        return base.HasProperty(property);
    }

    public sealed override bool Delete(JsValue property)
    {
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
            if (!CanWrite || !Extensible)
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
                if (!CanWrite || !Extensible)
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

    public void SetAt(int index, JsValue value)
    {
        if (CanWrite)
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
