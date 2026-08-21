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

    public sealed override JsValue Get(JsValue property, JsValue receiver)
    {
        if (property.IsInteger())
        {
            var index = property.AsInteger();
            if ((uint) index < (uint) Length)
            {
                var result = GetJsValueAt(index);
                if (_elementAccessMayRunHostCode)
                {
                    _engine.CheckAmortizedConstraintsAtHostBoundary();
                }
                return result;
            }

            // out-of-range and negative indices read like JS array holes
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

        if (property.IsNumber())
        {
            var value = ((JsNumber) property)._value;
            if (TypeConverter.IsIntegralNumber(value))
            {
                // numeric membership of an array-like view is exactly the index range [0, Length);
                // falling through would consult the reflected indexer, which reports presence for
                // any parseable index (so e.g. "-1 in view" would be true). Compare as double so a
                // negative or out-of-int-range index can never alias into range.
                return value >= 0 && value < Length;
            }
        }
        else if (property is JsString jsString)
        {
            var str = jsString.ToString();
            var index = ArrayInstance.ParseArrayIndex(str);
            if (index != uint.MaxValue)
            {
                return index < (uint) Length;
            }
            // an integer-shaped but non-canonical key ("-1", "08") is not a member of the view
            // either, and must not fall through to the reflected indexer's presence-only answer
            if (long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
        }

        return base.HasProperty(property);
    }

    public sealed override bool Delete(JsValue property)
    {
        if (!_engine.Options.Interop.AllowWrite || !Extensible)
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

            if (!_typeDescriptor.IsDictionary && property.IsNumber())
            {
                var value = ((JsNumber) property)._value;
                if (TypeConverter.IsIntegralNumber(value))
                {
                    return value < 0 || value >= Length;
                }
            }
            else if (!_typeDescriptor.IsDictionary && property is JsString jsString)
            {
                var index = ArrayInstance.ParseArrayIndex(jsString.ToString());
                if (index != uint.MaxValue)
                {
                    return index >= (uint) Length;
                }
            }

            return ProbeOwnPropertyChecked(property) == OwnPropertyProbe.Missing;
        }

        if (property.IsNumber())
        {
            var value = ((JsNumber) property)._value;
            if (TypeConverter.IsIntegralNumber(value))
            {
                if (IsFixedSize)
                {
                    // elements of a fixed-size CLR array view cannot be removed (and resetting them to a
                    // default value on delete would let Array.prototype.pop/shift/splice silently zero
                    // slots before their length write throws). Mirror integer-indexed exotic object
                    // semantics instead: false for an in-range index, true for anything out of range.
                    return value < 0 || value >= Length;
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

                DoSetAt((int) value, defaultValue);
                return true;
            }
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

        if (ReferenceEquals(receiver, this) && property.IsNumber())
        {
            // An element write to a read-only or non-extensible (frozen) wrapper must be refused:
            // base.SetSlow would otherwise materialize a throwaway descriptor and return true, silently
            // "succeeding" (#2541), or reach an indexer with an out-of-range growth write. Everything else —
            // writable in-range writes, growth, negative and non-integral indices — defers to base.Set, which
            // writes through and already
            // rejects runtime read-only collections (e.g. ReadOnlyCollection<T>) cleanly. Wrappers don't
            // track per-element writability, so a non-extensible wrapper blocks existing-element writes too
            // — a deliberate, contained interop divergence from the spec.
            var numValue = ((JsNumber) property)._value;
            if (TypeConverter.IsIntegralNumber(numValue) && numValue >= 0
                && (!CanWrite || !Extensible))
            {
                return false;
            }

            // Fixed-size targets (CLR arrays) handle integral index writes here: in-range writes go
            // straight to the backing store and anything else is a TypeError. The base.Set slow path
            // below resolves the element writer through the reflection indexer, which for T[] finds
            // the non-generic IList.Item (object-typed) indexer — element values would bypass item
            // type coercion and out-of-range writes would leak CLR exceptions.
            if (IsFixedSize && TypeConverter.IsIntegralNumber(numValue))
            {
                if (!CanWrite || !Extensible)
                {
                    return false;
                }

                if (numValue >= 0 && numValue < Length)
                {
                    SetAt((int) numValue, value);
                    return true;
                }

                Throw.TypeError(_engine.Realm, "Cannot write outside the bounds of a fixed-size CLR array");
            }
        }

        return base.Set(property, value, receiver);
    }

    protected virtual bool CanWrite => _engine.Options.Interop.AllowWrite;

    /// <summary>
    /// Whether the underlying collection cannot change its length (CLR arrays). Enables the direct
    /// integral-index write lane in <see cref="Set"/>.
    /// </summary>
    protected virtual bool IsFixedSize => false;

    public void SetAt(int index, JsValue value)
    {
        if (_engine.Options.Interop.AllowWrite)
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
                converted = Engine.TypeConverter.Convert(converted, ItemType, CultureInfo.InvariantCulture);
            }
        }

        return converted;
    }
}

internal sealed class ListWrapper : ArrayLikeWrapper
{
    private readonly IList? _list;

    internal ListWrapper(Engine engine, IList target, Type type)
        : base(engine, target, typeof(object), type, elementAccessMayRunHostCode: target is not (Array or ArrayList))
    {
        _list = target;
    }

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

    public override void AddDefault() => _list?.Add(null);

    public override void Add(JsValue value) => _list?.Add(ConvertToItemType(value));

    public override void RemoveAt(int index) => _list?.RemoveAt(index);
}

internal class GenericListWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapper
{
    private readonly IList<T> _list;

    public GenericListWrapper(Engine engine, IList<T> target, Type? type)
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

    protected override void DoSetAt(int index, object? value) => _list[index] = (T) value!;

    public override void AddDefault() => _list.Add(default!);

    public override void Add(JsValue value)
    {
        var converted = ConvertToItemType(value);
        _list.Add((T) converted!);
    }

    public override void RemoveAt(int index) => _list.RemoveAt(index);
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

    [DoesNotReturn]
    private void ThrowFixedSize() => Throw.TypeError(_engine.Realm, "Cannot resize a fixed-size CLR array");
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

    protected override bool CanWrite => false;

    public override void AddDefault() => Throw.NotSupportedException();

    protected override void DoSetAt(int index, object? value) => Throw.NotSupportedException();

    public override void Add(JsValue value) => Throw.NotSupportedException();

    public override void RemoveAt(int index) => Throw.NotSupportedException();

    public override void EnsureCapacity(int capacity) => Throw.NotSupportedException();
}
