using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Array;

public class ArrayInstance : ObjectInstance, IEnumerable<JsValue>
{
    // The "length" own property. To avoid a per-array PropertyDescriptor allocation in the common case,
    // the value is stored inline in _lengthValue and a descriptor (_lengthDescriptor) is materialized and
    // cached only when a caller needs the object: [[GetOwnProperty]], [[DefineOwnProperty]] (incl. making
    // length non-writable), or reflection. Once _lengthDescriptor is non-null it is authoritative.
    // Inline state is always the default (writable, non-enumerable, non-configurable) because the only ways
    // to alter length's attributes go through DefineLength, which materializes first. Both fields null means
    // length has been removed (only reachable internally; length is normally non-configurable).
    private JsNumber? _lengthValue;
    private protected PropertyDescriptor? _lengthDescriptor;

    /// <summary>Current length value (PositiveZero if length was removed).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JsNumber GetJsNumberLength()
        => _lengthDescriptor is not null ? (JsNumber) _lengthDescriptor._value! : (_lengthValue ?? JsNumber.PositiveZero);

    private bool HasLength => _lengthDescriptor is not null || _lengthValue is not null;

    // Matches `_length is { Writable: true }`: present AND writable. Inline length is always writable
    // (non-writable length only arises via DefineLength, which materializes the descriptor first).
    private bool LengthIsWritable => _lengthDescriptor is not null ? _lengthDescriptor.Writable : _lengthValue is not null;

    /// <summary>Materialize and cache the length descriptor for spec/reflection paths. Null only if removed.</summary>
    private PropertyDescriptor? GetLengthDescriptor()
        => _lengthDescriptor ??= _lengthValue is null ? null : new PropertyDescriptor(_lengthValue, PropertyFlag.OnlyWritable);

    /// <summary>Update the length value (mutates the materialized descriptor when present, else inline).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetLengthValue(JsNumber value)
    {
        if (_lengthDescriptor is not null)
        {
            _lengthDescriptor.Value = value;
        }
        else
        {
            _lengthValue = value;
        }
    }

    internal ulong GetLongLength() => (ulong) GetJsNumberLength()._value;

    private const int MaxDenseArrayLength = 10_000_000;
    internal const int MaxDenseArrayLengthInternal = MaxDenseArrayLength;

    // we have dense and sparse, we usually can start with dense and fall back to sparse when necessary
    // when we have plain JsValues, _denseValues is used - if any operation occurs which requires setting more property flags
    // we convert to _sparse and _denseValues is set to null - it will be a slow array
    internal JsValue?[]? _dense;

    private Dictionary<uint, PropertyDescriptor?>? _sparse;

    private ObjectChangeFlags _objectChangeFlags;

    private ArrayConstructor? _constructor;

    private protected ArrayInstance(Engine engine, InternalTypes type) : base(engine, type: type)
    {
        _dense = [];
    }

    private protected ArrayInstance(Engine engine, uint capacity = 0, uint length = 0) : base(engine, type: InternalTypes.Object | InternalTypes.Array)
    {
        InitializePrototypeAndValidateCapacity(engine, System.Math.Max(capacity, length));

        if (capacity < MaxDenseArrayLength)
        {
            _dense = capacity > 0 ? new JsValue?[capacity] : [];
        }
        else
        {
            _sparse = new Dictionary<uint, PropertyDescriptor?>(1024);
        }

        _lengthValue = JsNumber.Create(length);
    }

    private protected ArrayInstance(Engine engine, JsValue[] items) : base(engine, type: InternalTypes.Object | InternalTypes.Array)
    {
        InitializePrototypeAndValidateCapacity(engine, capacity: 0);

        _dense = items;
        _lengthValue = JsNumber.Create(items.Length);
    }

    private void InitializePrototypeAndValidateCapacity(Engine engine, uint capacity)
    {
        _constructor = engine.Realm.Intrinsics.Array;
        _prototype = _constructor.PrototypeObject;

        // Validates against the larger of the physical backing capacity and the declared length:
        // a `new Array(N)` with capacity 0 but length N must still respect MaxArraySize so callers
        // can't sidestep the constraint via a logical-only allocation followed by `.join()` etc.
        if (capacity > 0 && capacity > engine.Options.Constraints.MaxArraySize)
        {
            ThrowMaximumArraySizeReachedException(engine, capacity);
        }
    }

    internal sealed override bool IsArrayLike => true;

    internal sealed override bool IsArray() => true;

    internal sealed override bool HasOriginalIterator
        => ReferenceEquals(Get(GlobalSymbolRegistry.Iterator), _constructor?.PrototypeObject._originalIteratorFunction);

    /// <summary>
    /// Checks whether there have been changes to object prototype chain which could render fast access patterns impossible.
    /// </summary>
    internal bool CanUseFastAccess
    {
        get
        {
            if ((_objectChangeFlags & ObjectChangeFlags.NonDefaultDataDescriptorUsage) != ObjectChangeFlags.None)
            {
                // could be a mutating property for example, length might change, not safe anymore
                return false;
            }

            if (_prototype is not ArrayPrototype arrayPrototype
                || !ReferenceEquals(_prototype, _constructor?.PrototypeObject))
            {
                // somebody has switched prototype
                return false;
            }

            if ((arrayPrototype._objectChangeFlags & ObjectChangeFlags.ArrayIndex) != ObjectChangeFlags.None)
            {
                // maybe somebody moved integer property to prototype? not safe anymore
                return false;
            }

            if (arrayPrototype.Prototype is not ObjectPrototype arrayPrototypePrototype
                || !ReferenceEquals(arrayPrototypePrototype, _constructor.PrototypeObject.Prototype))
            {
                return false;
            }

            return (arrayPrototypePrototype._objectChangeFlags & ObjectChangeFlags.ArrayIndex) == ObjectChangeFlags.None;
        }
    }

    public sealed override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (CommonProperties.Length.Equals(property))
        {
            return DefineLength(desc);
        }

        var isArrayIndex = IsArrayIndex(property, out var index);
        TrackChanges(property, desc, isArrayIndex);

        if (isArrayIndex)
        {
            ConvertToSparse();
            return DefineOwnProperty(index, desc);
        }

        return base.DefineOwnProperty(property, desc);
    }

    private bool DefineLength(PropertyDescriptor desc)
    {
        var value = desc.Value;
        if (value is null)
        {
            return base.DefineOwnProperty(CommonProperties.Length, desc);
        }

        var newLenDesc = new PropertyDescriptor(desc);
        uint newLen = TypeConverter.ToUint32(value);
        if (newLen != TypeConverter.ToNumber(value))
        {
            Throw.RangeError(_engine.Realm);
        }

        var oldLenDesc = GetLengthDescriptor();
        var oldLen = (uint) TypeConverter.ToNumber(oldLenDesc!.Value);

        newLenDesc.Value = newLen;
        if (newLen >= oldLen)
        {
            return base.DefineOwnProperty(CommonProperties.Length, newLenDesc);
        }

        if (!oldLenDesc.Writable)
        {
            return false;
        }

        bool newWritable;
        if (!newLenDesc.WritableSet || newLenDesc.Writable)
        {
            newWritable = true;
        }
        else
        {
            newWritable = false;
            newLenDesc.Writable = true;
        }

        var succeeded = base.DefineOwnProperty(CommonProperties.Length, newLenDesc);
        if (!succeeded)
        {
            return false;
        }

        var count = _dense?.Length ?? _sparse!.Count;
        if (count < oldLen - newLen)
        {
            if (_dense != null)
            {
                for (uint keyIndex = 0; keyIndex < _dense.Length; ++keyIndex)
                {
                    if (_dense[keyIndex] is null)
                    {
                        continue;
                    }

                    // is it the index of the array
                    if (keyIndex >= newLen && keyIndex < oldLen)
                    {
                        var deleteSucceeded = Delete(keyIndex);
                        if (!deleteSucceeded)
                        {
                            newLenDesc.Value = keyIndex + 1;
                            if (!newWritable)
                            {
                                newLenDesc.Writable = false;
                            }

                            base.DefineOwnProperty(CommonProperties.Length, newLenDesc);
                            return false;
                        }
                    }
                }
            }
            else
            {
                // in the case of sparse arrays, treat each concrete element instead of
                // iterating over all indexes
                var keys = new List<uint>(_sparse!.Keys);
                var keysCount = keys.Count;
                for (var i = 0; i < keysCount; i++)
                {
                    var keyIndex = keys[i];

                    // is it the index of the array
                    if (keyIndex >= newLen && keyIndex < oldLen)
                    {
                        var deleteSucceeded = Delete(TypeConverter.ToString(keyIndex));
                        if (!deleteSucceeded)
                        {
                            newLenDesc.Value = JsNumber.Create(keyIndex + 1);
                            if (!newWritable)
                            {
                                newLenDesc.Writable = false;
                            }

                            base.DefineOwnProperty(CommonProperties.Length, newLenDesc);
                            return false;
                        }
                    }
                }
            }
        }
        else
        {
            while (newLen < oldLen)
            {
                // algorithm as per the spec
                oldLen--;
                var deleteSucceeded = Delete(oldLen);
                if (!deleteSucceeded)
                {
                    newLenDesc.Value = oldLen + 1;
                    if (!newWritable)
                    {
                        newLenDesc.Writable = false;
                    }

                    base.DefineOwnProperty(CommonProperties.Length, newLenDesc);
                    return false;
                }
            }
        }

        if (!newWritable)
        {
            base.DefineOwnProperty(CommonProperties.Length, new PropertyDescriptor(value: null, PropertyFlag.WritableSet));
        }

        return true;
    }

    private bool DefineOwnProperty(uint index, PropertyDescriptor desc)
    {
        var oldLenDesc = GetLengthDescriptor();
        var oldLen = (uint) TypeConverter.ToNumber(oldLenDesc!.Value);

        if (index >= oldLen && !oldLenDesc.Writable)
        {
            return false;
        }

        var succeeded = base.DefineOwnProperty(index, desc);
        if (!succeeded)
        {
            return false;
        }

        if (index >= oldLen)
        {
            oldLenDesc.Value = index + 1;
            base.DefineOwnProperty(CommonProperties.Length, oldLenDesc);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal override uint GetLength() => (uint) GetJsNumberLength()._value;

    protected sealed override bool TryGetProperty(JsValue property, [NotNullWhen(true)] out PropertyDescriptor? descriptor)
    {
        if (CommonProperties.Length.Equals(property))
        {
            descriptor = GetLengthDescriptor();
            return descriptor != null;
        }

        return base.TryGetProperty(property, out descriptor);
    }

    public sealed override List<JsValue> GetOwnPropertyKeys(Types types = Types.Empty | Types.String | Types.Symbol)
    {
        if ((types & Types.String) == Types.Empty)
        {
            return base.GetOwnPropertyKeys(types);
        }

        var temp = _dense;
        var properties = new List<JsValue>((temp?.Length ?? _sparse!.Count) + 1);
        if (temp != null)
        {
            var length = System.Math.Min(temp.Length, GetLength());
            for (var i = 0; i < length; i++)
            {
                if (temp[i] is not null)
                {
                    properties.Add(JsString.Create(i));
                }
            }
        }
        else
        {
            foreach (var entry in _sparse!)
            {
                properties.Add(JsString.Create(entry.Key));
            }
        }

        if (HasLength)
        {
            properties.Add(CommonProperties.Length);
        }

        properties.AddRange(base.GetOwnPropertyKeys(types));

        return properties;
    }

    /// <summary>
    /// Returns key and value pairs for actual array entries, excludes parent and optionally length.
    /// </summary>
    /// <param name="includeLength">Whether to return length and it's value.</param>
    public IEnumerable<KeyValuePair<string, JsValue>> GetEntries(bool includeLength = false)
    {
        foreach (var (index, value) in this.Enumerate())
        {
            yield return new KeyValuePair<string, JsValue>(TypeConverter.ToString(index), value);
        }

        if (includeLength && HasLength)
        {
            yield return new KeyValuePair<string, JsValue>(CommonProperties.Length._value, GetJsNumberLength());
        }
    }

    public sealed override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        var temp = _dense;
        if (temp != null)
        {
            var length = System.Math.Min(temp.Length, GetLength());
            for (uint i = 0; i < length; i++)
            {
                var value = temp[i];
                if (value is not null)
                {
                    if (_sparse is null || !_sparse.TryGetValue(i, out var descriptor) || descriptor is null)
                    {
                        _sparse ??= new Dictionary<uint, PropertyDescriptor?>();
                        _sparse[i] = descriptor = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                    }
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(TypeConverter.ToString(i), descriptor);
                }
            }
        }
        else if (_sparse != null)
        {
            foreach (var entry in _sparse)
            {
                var value = entry.Value;
                if (value is not null)
                {
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(TypeConverter.ToString(entry.Key), value);
                }
            }
        }

        if (GetLengthDescriptor() is { } lengthDescriptor)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, lengthDescriptor);
        }

        foreach (var entry in base.GetOwnProperties())
        {
            yield return entry;
        }
    }

    public sealed override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (CommonProperties.Length.Equals(property))
        {
            return GetLengthDescriptor() ?? PropertyDescriptor.Undefined;
        }

        if (IsArrayIndex(property, out var index))
        {
            if (TryGetDescriptor(index, createIfMissing: true, out var result))
            {
                return result;
            }

            return PropertyDescriptor.Undefined;
        }

        return base.GetOwnProperty(property);
    }

    /// <summary>
    /// Element probe without the <see cref="TryGetDescriptor"/> materialization: enumerating a
    /// dense array (for-in, Object.keys/values/entries, spread, assign) previously allocated one
    /// CEW descriptor per touched index AND grew a permanent <c>_sparse</c> shadow dictionary
    /// beside <c>_dense</c>. Presence and flags answer here allocation-free; a previously
    /// materialized descriptor (if any) stays authoritative for its flags.
    /// </summary>
    internal override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if (CommonProperties.Length.Equals(property))
        {
            // the length property is never enumerable
            return HasLength ? OwnPropertyProbe.NonEnumerable : base.ProbeOwnProperty(property);
        }

        if (IsArrayIndex(property, out var index))
        {
            var temp = _dense;
            if (temp is not null)
            {
                if (index < (uint) temp.Length && temp[index] is not null)
                {
                    if (_sparse is not null && _sparse.TryGetValue(index, out var materialized) && materialized is not null)
                    {
                        return materialized.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
                    }

                    return OwnPropertyProbe.Enumerable;
                }

                return OwnPropertyProbe.Missing;
            }

            if (_sparse is not null && _sparse.TryGetValue(index, out var descriptor) && descriptor is not null)
            {
                return descriptor.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
            }

            return OwnPropertyProbe.Missing;
        }

        return base.ProbeOwnProperty(property);
    }

    /// <summary>
    /// True when any own index-keyed element exists: a present dense element or a materialized
    /// per-index descriptor. O(1) for the pristine case that matters (Array.prototype's empty
    /// backing store) — the dense scan only runs over slots the object actually allocated.
    /// </summary>
    internal bool HasAnyOwnIndex()
    {
        if (_sparse is { Count: > 0 })
        {
            return true;
        }

        var dense = _dense;
        if (dense is not null)
        {
            foreach (var element in dense)
            {
                if (element is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Any present element is treated as potentially enumerable (a _sparse shadow could in theory
    // hold only non-enumerable descriptors, but scanning for that buys nothing — false only means
    // the caller takes the exact snapshot path). ArrayPrototype reaches Core with an empty _dense.
    internal override bool HasNoEnumerableOwnStringKeys()
        => !HasAnyOwnIndex() && HasNoEnumerableOwnStringKeysCore();

    /// <summary>
    /// Gate for the for-in index-enumeration fast path: a dense-backed array with no materialized
    /// per-index descriptors and no named own string properties has exactly its present indices
    /// (plus non-enumerable length) as own string keys, in ascending order. <paramref name="bound"/>
    /// is the index stepping limit — everything at or beyond it is provably a hole.
    /// </summary>
    internal bool TryStartForInIndexMode(out uint bound)
    {
        var dense = _dense;
        if (dense is null || _sparse is not null || _properties is { Count: > 0 })
        {
            bound = 0;
            return false;
        }

        bound = (uint) System.Math.Min((ulong) dense.Length, GetLongLength());
        return true;
    }

    /// <summary>
    /// One for-in step over the index range: advances <paramref name="index"/> past holes and hands
    /// out the key of the next present element, or returns false when the range is exhausted.
    /// Re-reads the representation on every call so mid-loop mutations (dense growth/shrink,
    /// dense→sparse conversion, materialized descriptors) are picked up: the pristine
    /// representation steps allocation-free, anything else falls back to a per-index probe that
    /// honors current enumerability. Indices at or past <paramref name="bound"/> are never yielded
    /// (elements appended mid-enumeration are not visited, matching the snapshot the exact path
    /// takes).
    /// </summary>
    internal bool TryFastForInStep(ref uint index, uint bound, [NotNullWhen(true)] out JsValue? key)
    {
        var dense = _dense;
        if (dense is not null && _sparse is null)
        {
            while (index < bound)
            {
                var i = index++;
                if (i < (uint) dense.Length && dense[i] is not null)
                {
                    key = JsString.Create(i);
                    return true;
                }
            }

            key = null;
            return false;
        }

        // deopted mid-loop; probe each remaining index against the live representation
        while (index < bound)
        {
            var candidate = JsString.Create(index++);
            if (ProbeOwnProperty(candidate) == OwnPropertyProbe.Enumerable)
            {
                key = candidate;
                return true;
            }
        }

        key = null;
        return false;
    }

    internal JsValue Get(uint index)
    {
        if (!TryGetValue(index, out var value))
        {
            // Reading a hole walks the prototype chain, but when nothing relevant has changed
            // (no exotic descriptors here, prototypes pristine — the same invariant the write
            // fast path trusts) the walk provably yields undefined: skip it and the per-hole
            // JsString the probe would allocate.
            if (CanUseFastAccess)
            {
                return Undefined;
            }

            value = Prototype?.Get(JsString.Create(index)) ?? Undefined;
        }

        return value;
    }

    public sealed override JsValue Get(JsValue property, JsValue receiver)
    {
        if (IsSafeSelfTarget(receiver) && IsArrayIndex(property, out var index))
        {
            if (TryGetValue(index, out var value))
            {
                return value;
            }

            // hole/out-of-range read with pristine prototypes: see Get(uint)
            if (CanUseFastAccess)
            {
                return Undefined;
            }
        }

        if (CommonProperties.Length.Equals(property))
        {
            if (HasLength)
            {
                return GetJsNumberLength();
            }
        }

        return base.Get(property, receiver);
    }

    public sealed override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        var isSafeSelfTarget = IsSafeSelfTarget(receiver);
        if (isSafeSelfTarget && CanUseFastAccess)
        {
            if (!ReferenceEquals(property, CommonProperties.Length) && IsArrayIndex(property, out var index))
            {
                SetIndexValue(index, value, updateLength: true);
                return true;
            }

            if (CommonProperties.Length.Equals(property)
                && LengthIsWritable
                && value is JsNumber jsNumber
                && jsNumber.IsInteger()
                && jsNumber._value <= MaxDenseArrayLength
                && jsNumber._value >= GetLength())
            {
                // we don't need explicit resize
                SetLengthValue(jsNumber);
                return true;
            }
        }

        // slow path
        return base.Set(property, value, receiver);
    }

    private bool IsSafeSelfTarget(JsValue receiver) => ReferenceEquals(receiver, this) && Extensible;

    public sealed override bool HasProperty(JsValue property)
    {
        if (IsArrayIndex(property, out var index))
        {
            if (GetValue(index, unwrapFromNonDataDescriptor: false) is not null)
            {
                return true;
            }

            // absent index with pristine prototypes: the chain walk provably finds nothing
            // (same invariant as Get; exotic own descriptors clear CanUseFastAccess)
            if (CanUseFastAccess)
            {
                return false;
            }
        }

        return base.HasProperty(property);
    }

    internal bool HasProperty(ulong index)
    {
        if (index < uint.MaxValue)
        {
            var temp = _dense;
            if (temp != null)
            {
                if (index < (uint) temp.Length && temp[index] is not null)
                {
                    return true;
                }
            }
            else if (_sparse!.ContainsKey((uint) index))
            {
                return true;
            }
        }

        return base.HasProperty(index);
    }

    protected internal sealed override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        var isArrayIndex = IsArrayIndex(property, out var index);
        TrackChanges(property, desc, isArrayIndex);
        if (isArrayIndex)
        {
            WriteArrayValue(index, desc);
        }
        else if (CommonProperties.Length.Equals(property))
        {
            _lengthDescriptor = desc;
            _lengthValue = null;
        }
        else
        {
            base.SetOwnProperty(property, desc);
        }
    }

    private void TrackChanges(JsValue property, PropertyDescriptor desc, bool isArrayIndex)
    {
        EnsureInitialized();

        if (isArrayIndex)
        {
            if (!desc.IsDefaultArrayValueDescriptor() && desc.Flags != PropertyFlag.None)
            {
                _objectChangeFlags |= ObjectChangeFlags.NonDefaultDataDescriptorUsage;
            }

            if (GetType() != typeof(JsArray))
            {
                _objectChangeFlags |= ObjectChangeFlags.ArrayIndex;
            }
        }
        else
        {
            _objectChangeFlags |= property.IsSymbol() ? ObjectChangeFlags.Symbol : ObjectChangeFlags.Property;
        }
    }

    public sealed override void RemoveOwnProperty(JsValue property)
    {
        if (IsArrayIndex(property, out var index))
        {
            Delete(index);
        }

        if (CommonProperties.Length.Equals(property))
        {
            _lengthDescriptor = null;
            _lengthValue = null;
        }

        base.RemoveOwnProperty(property);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsArrayIndex(JsValue p, out uint index)
    {
        if (p.IsNumber())
        {
            var value = ((JsNumber) p)._value;
            var intValue = (uint) value;
            index = intValue;
            return value == intValue && intValue != uint.MaxValue;
        }

        index = !p.IsSymbol() ? ParseArrayIndex(p.ToString()) : uint.MaxValue;
        return index != uint.MaxValue;

        // 15.4 - Use an optimized version of the specification
        // return TypeConverter.ToString(index) == TypeConverter.ToString(p) && index != uint.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ParseArrayIndex(string p)
    {
        if (p.Length == 0 || p.Length > 1 && !IsInRange(p[0], '1', '9') || !uint.TryParse(p, out var d))
        {
            return uint.MaxValue;
        }

        return d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInRange(char c, char min, char max) => c - (uint) min <= max - (uint) min;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetIndexValue(uint index, JsValue? value, bool updateLength)
    {
        if (updateLength)
        {
            EnsureCorrectLength(index);
        }

        WriteArrayValue(index, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCorrectLength(uint index)
    {
        var length = GetLength();
        if (index >= length)
        {
            SetLength(index + 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetLength(ulong length) => SetLength(JsNumber.Create(length));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetLength(JsNumber length)
    {
        // Fast path requires length present with the default (writable, non-enum, non-config) attributes —
        // which is exactly the inline state, or a materialized descriptor still carrying OnlyWritable.
        if (Extensible && (_lengthDescriptor is null ? _lengthValue is not null : _lengthDescriptor._flags == PropertyFlag.OnlyWritable))
        {
            SetLengthValue(length);
        }
        else
        {
            // slow path
            Set(CommonProperties.Length, length, true);
        }
    }

    internal uint GetSmallestIndex()
    {
        if (_dense != null)
        {
            return 0;
        }

        uint smallest = 0;
        // only try to help if collection reasonable small
        if (_sparse!.Count > 0 && _sparse.Count < 100 && !_sparse.ContainsKey(0))
        {
            smallest = uint.MaxValue;
            foreach (var key in _sparse.Keys)
            {
                smallest = System.Math.Min(key, smallest);
            }
        }

        return smallest;
    }

    internal bool DeletePropertyOrThrow(uint index)
    {
        if (!Delete(index))
        {
            Throw.TypeError(_engine.Realm);
        }
        return true;
    }

    private bool Delete(uint index) => Delete(index, unwrapFromNonDataDescriptor: false, out _);

    private bool Delete(uint index, bool unwrapFromNonDataDescriptor, out JsValue? deletedValue)
    {
        TryGetDescriptor(index, createIfMissing: false, out var desc);

        // check fast path
        var temp = _dense;
        if (temp != null)
        {
            if (index < (uint) temp.Length)
            {
                if (desc is null || desc.Configurable)
                {
                    deletedValue = temp[index];
                    temp[index] = null;
                    return true;
                }
            }
        }

        if (desc is null)
        {
            deletedValue = null;
            return true;
        }

        if (desc.Configurable)
        {
            _sparse!.Remove(index);
            deletedValue = desc.IsDataDescriptor() || unwrapFromNonDataDescriptor
                ? UnwrapJsValue(desc)
                : null;

            return true;
        }

        deletedValue = null;
        return false;
    }

    internal bool DeleteAt(uint index)
    {
        var temp = _dense;
        if (temp != null)
        {
            if (index < (uint) temp.Length)
            {
                temp[index] = null;
                return true;
            }
        }
        else
        {
            return _sparse!.Remove(index);
        }

        return false;
    }

    private bool TryGetDescriptor(uint index, bool createIfMissing, [NotNullWhen(true)] out PropertyDescriptor? descriptor)
    {
        if (!createIfMissing && _sparse is null)
        {
            descriptor = null;
            return false;
        }

        descriptor = null;
        var temp = _dense;
        if (temp != null)
        {
            if (index < (uint) temp.Length)
            {
                var value = temp[index];
                if (value is not null)
                {
                    if (_sparse is null || !_sparse.TryGetValue(index, out descriptor) || descriptor is null)
                    {
                        _sparse ??= new Dictionary<uint, PropertyDescriptor?>();
                        _sparse[index] = descriptor = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                    }

                    descriptor.Value = value;
                    return true;
                }
            }
            return false;
        }

        _sparse?.TryGetValue(index, out descriptor);
        return descriptor is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetValue(uint index, out JsValue value)
    {
        value = GetValue(index, unwrapFromNonDataDescriptor: true)!;

        if (value is not null)
        {
            return true;
        }

        return TryGetValueUnlikely(index, out value);
    }

    /// <summary>
    /// Fast read of a present dense element: returns true only when <paramref name="index"/> is in
    /// range and the slot is non-null (a real element, not a hole). Holes and out-of-range indices
    /// return false so the caller falls back to the full lookup (prototype chain etc.). The caller
    /// must have already verified <see cref="CanUseFastAccess"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetValueFast(uint index, out JsValue value)
    {
        var temp = _dense;
        if (temp is not null && index < (uint) temp.Length)
        {
            var v = temp[index];
            if (v is not null)
            {
                value = v;
                return true;
            }
        }

        value = Undefined;
        return false;
    }

    /// <summary>
    /// Fast overwrite of an existing dense element: returns true only when <paramref name="index"/>
    /// is in range and the slot is already non-null. This never grows the array, fills a hole, or
    /// changes length, so growing/hole-filling writes return false and defer to the full set path.
    /// The caller must have already verified <see cref="CanUseFastAccess"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryWriteExistingDense(uint index, JsValue value)
    {
        var temp = _dense;
        if (temp is not null && index < (uint) temp.Length && temp[index] is not null)
        {
            // A non-writable element descriptor can coexist with the dense backing when descriptors have
            // been materialized (GetOwnProperties / for-in lazily populate _sparse alongside _dense) and
            // one is then turned read-only in place. Bail to the full Set/PutValue path so writability is
            // enforced, but only when the array is also non-extensible: an extensible array's fallback
            // ArrayInstance.Set re-enters this same dense fast path and would write through regardless, so
            // the lookup would be wasted work there. The cheap _sparse null check comes first so the common
            // (non-materialized) write stays free; the dictionary probe is reached only for the rare
            // materialized + non-extensible array.
            if (_sparse is not null && !Extensible && _sparse.TryGetValue(index, out var descriptor) && descriptor is not null && !descriptor.Writable)
            {
                return false;
            }

            temp[index] = value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Fast append of the next dense element (<paramref name="index"/> == length): grows capacity
    /// with the standard doubling policy and bumps length in place. Returns false for anything
    /// else — non-append indices, non-writable or materialized-non-default length, non-extensible
    /// arrays, sparse mode, dense cap — so the caller falls back to the full set path (which
    /// enforces the corresponding spec errors). The caller must have already verified
    /// <see cref="CanUseFastAccess"/>. An index below the backing length writes into spare
    /// capacity: by the dense invariant slots at or beyond length are null holes.
    /// </summary>
    internal bool TryAppendDense(uint index, JsValue value)
    {
        var temp = _dense;
        if (temp is null || !Extensible || !LengthIsWritable)
        {
            return false;
        }

        if (index != (uint) GetJsNumberLength()._value || index >= MaxDenseArrayLength)
        {
            return false;
        }

        if (index < (uint) temp.Length)
        {
            temp[index] = value;
        }
        else
        {
            // same growth policy as WriteArrayValueUnlikely's dense arm
            var newSize = System.Math.Max(index, System.Math.Max((uint) temp.Length, 2)) * 2;
            if (newSize >= MaxDenseArrayLength)
            {
                return false;
            }

            EnsureCapacity(newSize);
            _dense![index] = value;
        }

        SetLengthValue(JsNumber.Create(index + 1));
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryGetValueUnlikely(uint index, out JsValue value)
    {
        if (!CanUseFastAccess)
        {
            // slow path must be checked for prototype
            var prototype = Prototype;
            JsValue key = index;
            while (prototype is not null)
            {
                var desc = prototype.GetOwnProperty(key);
                if (desc != PropertyDescriptor.Undefined)
                {
                    value = UnwrapJsValue(desc);
                    return true;
                }

                prototype = prototype.Prototype;
            }
        }

        value = Undefined;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JsValue? GetValue(uint index, bool unwrapFromNonDataDescriptor)
    {
        var temp = _dense;
        if (temp != null)
        {
            if (index < (uint) temp.Length)
            {
                return temp[index];
            }
            return null;
        }

        return GetValueUnlikely(index, unwrapFromNonDataDescriptor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue? GetValueUnlikely(uint index, bool unwrapFromNonDataDescriptor)
    {
        JsValue? value = null;
        if (_sparse!.TryGetValue(index, out var descriptor) && descriptor != null)
        {
            value = descriptor.IsDataDescriptor() || unwrapFromNonDataDescriptor
                ? UnwrapJsValue(descriptor)
                : null;
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteArrayValue(uint index, PropertyDescriptor descriptor)
    {
        var temp = _dense;
        if (temp != null && descriptor.IsDefaultArrayValueDescriptor())
        {
            if (index < (uint) temp.Length)
            {
                temp[index] = descriptor.Value;
            }
            else
            {
                WriteArrayValueUnlikely(index, descriptor.Value);
            }
        }
        else
        {
            WriteArrayValueUnlikely(index, descriptor);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteArrayValue(uint index, JsValue? value)
    {
        var temp = _dense;
        if (temp != null)
        {
            if (index < (uint) temp.Length)
            {
                temp[index] = value;
                return;
            }
        }

        WriteArrayValueUnlikely(index, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteArrayValueUnlikely(uint index, JsValue? value)
    {
        // calculate eagerly so we know if we outgrow
        var dense = _dense;
        var newSize = dense != null && index >= (uint) dense.Length
            ? System.Math.Max(index, System.Math.Max(dense.Length, 2)) * 2
            : 0;

        // Lazy-backed arrays from `new Array(N)` start with _dense.Length=0 but _length=N;
        // a write to an index within N is not sparse intent, just realising the lazy capacity.
        // Compare against the larger of the physical backing length and the declared length.
        var declaredLength = (uint) GetJsNumberLength()._value;
        var denseHeadroom = System.Math.Max((uint) (dense?.Length ?? 0), declaredLength);

        var canUseDense = dense != null
                          && index < MaxDenseArrayLength
                          && newSize < MaxDenseArrayLength
                          && index < denseHeadroom + 50; // looks sparse

        if (canUseDense)
        {
            EnsureCapacity((uint) newSize);
            _dense![index] = value;
        }
        else
        {
            ConvertToSparse();
            _sparse![index] = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
        }
    }

    private void WriteArrayValueUnlikely(uint index, PropertyDescriptor? value)
    {
        if (_sparse == null)
        {
            ConvertToSparse();
        }

        _sparse![index] = value;
    }

    private void ConvertToSparse()
    {
        // need to move data
        var temp = _dense;

        if (temp is null)
        {
            return;
        }

        if (_sparse is null)
        {
            // Fast path: first conversion. Single dictionary touch per slot, no Remove calls
            // (a brand-new dictionary has nothing to remove).
            _sparse = new Dictionary<uint, PropertyDescriptor?>(temp.Length);
            for (uint i = 0; i < (uint) temp.Length; ++i)
            {
                var value = temp[i];
                if (value is not null)
                {
                    _sparse[i] = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                }
            }
        }
        else
        {
            for (uint i = 0; i < (uint) temp.Length; ++i)
            {
                var value = temp[i];
                if (value is not null)
                {
                    if (_sparse.TryGetValue(i, out var descriptor) && descriptor is not null)
                    {
                        descriptor.Value = value;
                    }
                    else
                    {
                        _sparse[i] = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                    }
                }
                else
                {
                    _sparse.Remove(i);
                }
            }
        }

        _dense = null;
    }

    /// <summary>
    /// Bulk-moves a contiguous range of dense slots: equivalent to
    /// <c>Array.Copy(_dense, sourceIndex, _dense, destIndex, count)</c>, growing the backing
    /// if needed. Returns <c>false</c> if the array is sparse, the prototype chain can observe
    /// element access (so per-element semantics differ from a memmove), or the move would
    /// overflow <see cref="MaxDenseArrayLength"/>. Caller is responsible for clearing any
    /// vacated tail slots and updating <c>_length</c>.
    /// </summary>
    internal bool TryMoveDenseRange(uint sourceIndex, uint destIndex, uint count)
    {
        if (count == 0)
        {
            return true;
        }

        var dense = _dense;
        if (dense is null || !CanUseFastAccess)
        {
            return false;
        }

        var requiredCapacity = System.Math.Max(sourceIndex + count, destIndex + count);
        if (requiredCapacity > MaxDenseArrayLength)
        {
            return false;
        }
        if (requiredCapacity > (uint) dense.Length)
        {
            EnsureCapacityAmortized(requiredCapacity);
            dense = _dense!;
        }

        System.Array.Copy(dense, sourceIndex, dense, destIndex, count);
        return true;
    }

    /// <summary>
    /// Variant of <see cref="EnsureCapacity"/> that grows geometrically when a grow is needed
    /// (doubles current size, falling back to <paramref name="capacity"/> if that overflows the
    /// dense bound). Use for hot paths that call this in a loop (e.g. repeated push/unshift) so
    /// the cumulative copy cost stays amortized O(N) instead of O(N²).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnsureCapacityAmortized(uint capacity)
    {
        var dense = _dense;
        if (dense is null || capacity <= (uint) dense.Length)
        {
            return;
        }
        var doubled = (uint) dense.Length * 2;
        if (doubled < 4)
        {
            doubled = 4;
        }
        var grown = System.Math.Max(doubled, capacity);
        if (grown > MaxDenseArrayLength)
        {
            grown = capacity;
        }
        EnsureCapacity(grown);
    }

    /// <summary>
    /// Sets <c>_dense[startIndex .. startIndex + count]</c> to null, clamped to the current
    /// dense length. No-op if the array is sparse.
    /// </summary>
    internal void ClearDenseRange(uint startIndex, uint count)
    {
        var dense = _dense;
        if (dense is null || count == 0)
        {
            return;
        }
        var end = System.Math.Min(startIndex + count, (uint) dense.Length);
        if (startIndex < end)
        {
            System.Array.Clear(dense, (int) startIndex, (int) (end - startIndex));
        }
    }

    internal void EnsureCapacity(uint capacity, bool force = false)
    {
        var dense = _dense;

        if (dense is null)
        {
            return;
        }

        if (!force && (capacity > MaxDenseArrayLength || capacity <= (uint) dense.Length))
        {
            return;
        }

        if (capacity > _engine.Options.Constraints.MaxArraySize)
        {
            ThrowMaximumArraySizeReachedException(_engine, capacity);
        }

        // need to grow
        System.Array.Resize(ref _dense, (int) capacity);
    }

    public JsValue[] ToArray()
    {
        var length = GetLength();
        var array = new JsValue[length];
        for (uint i = 0; i < length; i++)
        {
            TryGetValue(i, out var outValue);
            array[i] = outValue;
        }

        return array;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<JsValue> GetEnumerator()
    {
        foreach (var (_, value) in this.Enumerate())
        {
            yield return value;
        }
    }

    private readonly record struct IndexedEntry(int Index, JsValue Value);

    private IEnumerable<IndexedEntry> Enumerate()
    {
        if (!CanUseFastAccess)
        {
            // slow path where prototype is also checked
            var length = GetLength();
            for (uint i = 0; i < length; i++)
            {
                TryGetValue(i, out var outValue);
                yield return new IndexedEntry((int) i, outValue);
            }

            yield break;
        }

        var temp = _dense;
        if (temp != null)
        {
            var length = System.Math.Min(temp.Length, GetLength());
            for (var i = 0; i < length; i++)
            {
                var value = temp[i];
                if (value is not null)
                {
                    yield return new IndexedEntry(i, value);
                }
            }
        }
        else
        {
            foreach (var entry in _sparse!)
            {
                var descriptor = entry.Value;
                if (descriptor is not null)
                {
                    yield return new IndexedEntry((int) entry.Key, descriptor.Value);
                }
            }
        }
    }

    /// <summary>
    /// Pushes the value to the end of the array instance.
    /// </summary>
    public void Push(JsValue value)
    {
        var initialLength = GetLength();
        var newLength = initialLength + 1;

        var temp = _dense;
        var canUseDirectIndexSet = temp != null && newLength <= temp.Length;

        double n = initialLength;
        if (canUseDirectIndexSet)
        {
            temp![(uint) n] = value;
        }
        else
        {
            WriteValueSlow(n, value);
        }

        // check if we can set length fast without breaking ECMA specification
        if (n < uint.MaxValue && CanSetLength())
        {
            SetLengthValue(JsNumber.Create(newLength));
        }
        else
        {
            if (!Set(CommonProperties.Length, newLength))
            {
                Throw.TypeError(_engine.Realm);
            }
        }
    }

    /// <summary>
    /// Pushes the given values to the end of the array.
    /// </summary>
    public uint Push(JsValue[] values)
    {
        var initialLength = GetLength();
        var newLength = initialLength + values.Length;

        // if we see that we are bringing more than normal growth algorithm handles, ensure capacity eagerly
        if (_dense != null
            && initialLength != 0
            && values.Length > initialLength * 2
            && newLength <= MaxDenseArrayLength)
        {
            EnsureCapacity((uint) newLength);
        }

        var temp = _dense;
        ulong n = initialLength;
        foreach (var argument in values)
        {
            if (n < ArrayOperations.MaxArrayLength)
            {
                WriteArrayValue((uint) n, argument);
            }
            else
            {
                DefineOwnProperty(n, new PropertyDescriptor(argument, PropertyFlag.ConfigurableEnumerableWritable));
            }

            n++;
        }

        // check if we can set length fast without breaking ECMA specification
        if (n < ArrayOperations.MaxArrayLength && CanSetLength())
        {
            SetLengthValue(JsNumber.Create(n));
        }
        else
        {
            if (!Set(CommonProperties.Length, newLength))
            {
                Throw.TypeError(_engine.Realm);
            }
        }

        return (uint) n;
    }

    public JsValue Pop()
    {
        var len = GetJsNumberLength();
        if (JsNumber.PositiveZero.Equals(len))
        {
            SetLength(len);
            return Undefined;
        }

        var newLength = (uint) len._value - 1;

        if (!Delete(newLength, unwrapFromNonDataDescriptor: true, out var element))
        {
            Throw.TypeError(_engine.Realm);
        }

        SetLength(newLength);

        return element ?? Undefined;
    }

    private bool CanSetLength()
    {
        var desc = _lengthDescriptor;
        if (desc is null)
        {
            // inline length is always writable
            return true;
        }
        if (!desc.IsAccessorDescriptor())
        {
            return desc.Writable;
        }
        var set = desc.Set;
        return set is not null && !set.IsUndefined();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteValueSlow(double n, JsValue value)
    {
        if (n < ArrayOperations.MaxArrayLength)
        {
            WriteArrayValue((uint) n, value);
        }
        else
        {
            DefinePropertyOrThrow((uint) n, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));
        }
    }

    internal JsArray Map(JsCallArguments arguments)
    {
        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);

        var len = GetLength();

        var callable = GetCallable(callbackfn);
        var a = _engine.Realm.Intrinsics.Array.ArrayCreate(len);
        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = this;
        for (uint k = 0; k < len; k++)
        {
            if (TryGetValue(k, out var kvalue))
            {
                args[0] = kvalue;
                args[1] = k;
                var mappedValue = callable.Call(thisArg, args);
                if (a._dense != null && k < (uint) a._dense.Length)
                {
                    a._dense[k] = mappedValue;
                }
                else
                {
                    a.WriteArrayValue(k, mappedValue);
                }
            }
        }

        _engine._jsValueArrayPool.ReturnArray(args);
        return a;
    }

    internal JsArray Filter(JsCallArguments arguments)
    {
        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);

        var len = GetLength();

        var callable = GetCallable(callbackfn);

        // Output size is unknown (only bounded by len); accumulate into a pooled buffer and
        // materialize an exact-size result instead of growing the result's dense backing by
        // doubling. Capped initial rent so a large low-selectivity source doesn't rent ~len slots.
        var builder = new JsValueListBuilder((int) System.Math.Min(len, 1024));
        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = this;

        // try/finally so the rented args array and the pooled builder buffer are released
        // when the callback or a periodic Check() throws.
        try
        {
            for (uint k = 0; k < len; k++)
            {
                if (k > 0 && k % Engine.ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }

                if (TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    var selected = callable.Call(thisArg, args);
                    if (TypeConverter.ToBoolean(selected))
                    {
                        builder.Add(kvalue);
                    }
                }
            }

            return _engine.Realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
        }
        finally
        {
            builder.Dispose();
            _engine._jsValueArrayPool.ReturnArray(args);
        }
    }

    /// <inheritdoc />
    internal sealed override bool FindWithCallback(
        JsCallArguments arguments,
        out ulong index,
        out JsValue value,
        bool visitUnassigned,
        bool fromEnd = false)
    {
        var thisArg = arguments.At(1);
        var callbackfn = arguments.At(0);
        var callable = GetCallable(callbackfn);

        var len = GetLength();
        if (len == 0)
        {
            index = 0;
            value = Undefined;
            return false;
        }

        var args = _engine._jsValueArrayPool.RentArray(3);
        args[2] = this;

        // try/finally so the rented pool array is returned on every exit path: the early
        // return-on-match below and a periodic Check() throw both previously leaked it.
        try
        {
            if (!fromEnd)
            {
                for (uint k = 0; k < len; k++)
                {
                    if (k > 0 && k % Engine.ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    if (TryGetValue(k, out var kvalue) || visitUnassigned)
                    {
                        kvalue ??= Undefined;
                        args[0] = kvalue;
                        args[1] = k;
                        var testResult = callable.Call(thisArg, args);
                        if (TypeConverter.ToBoolean(testResult))
                        {
                            index = k;
                            value = kvalue;
                            return true;
                        }
                    }
                }
            }
            else
            {
                for (long k = len - 1; k >= 0; k--)
                {
                    if (k % Engine.ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    var idx = (uint) k;
                    if (TryGetValue(idx, out var kvalue) || visitUnassigned)
                    {
                        kvalue ??= Undefined;
                        args[0] = kvalue;
                        args[1] = idx;
                        var testResult = callable.Call(thisArg, args);
                        if (TypeConverter.ToBoolean(testResult))
                        {
                            index = idx;
                            value = kvalue;
                            return true;
                        }
                    }
                }
            }
        }
        finally
        {
            _engine._jsValueArrayPool.ReturnArray(args);
        }

        index = 0;
        value = Undefined;
        return false;
    }

    internal sealed override bool IsIntegerIndexedArray => true;

    public JsValue this[uint index]
    {
        get
        {
            TryGetValue(index, out var kValue);
            return kValue;
        }
        set
        {
            SetIndexValue(index, value, updateLength: true);
        }
    }

    public JsValue this[int index]
    {
        get
        {
            JsValue? kValue;
            if (index >= 0)
            {
                TryGetValue((uint) index, out kValue);
            }
            else
            {
                // slow path
                TryGetValue(JsNumber.Create(index), out kValue);
            }
            return kValue;
        }
        set
        {
            if (index >= 0)
            {
                SetIndexValue((uint) index, value, updateLength: true);
            }
            else
            {
                Set(index, value);
            }
        }
    }

    /// <summary>
    /// Fast path for concatenating sane-sized arrays, we assume size has been calculated.
    /// </summary>
    internal void CopyValues(JsArray source, uint sourceStartIndex, uint targetStartIndex, uint length)
    {
        if (length == 0)
        {
            return;
        }

        var sourceDense = source._dense;

        if (sourceDense is not null)
        {
            EnsureCapacity((uint) (targetStartIndex + sourceDense.LongLength));
        }

        var dense = _dense;
        if (dense != null
            && sourceDense != null
            && (uint) dense.Length >= targetStartIndex + length
            && dense[targetStartIndex] is null)
        {
            uint j = 0;
            for (var i = sourceStartIndex; i < sourceStartIndex + length; ++i, j++)
            {
                JsValue? sourceValue;
                if (i < (uint) sourceDense.Length)
                {
                    sourceValue = sourceDense[i];
                }
                else
                {
                    source.TryGetValue(i, out sourceValue);
                }

                dense[targetStartIndex + j] = sourceValue;
            }
        }
        else
        {
            // slower version
            for (uint k = sourceStartIndex; k < length; k++)
            {
                if (source.TryGetValue(k, out var subElement))
                {
                    SetIndexValue(targetStartIndex, subElement, updateLength: false);
                }

                targetStartIndex++;
            }
        }
    }

    public sealed override string ToString()
    {
        // debugger can make things hard when evaluates computed values
        return "(" + GetJsNumberLength()._value + ")[]";
    }

    internal static void ThrowMaximumArraySizeReachedException(Engine engine, uint capacity)
    {
        Throw.MemoryLimitExceededException(
            $"The array size {capacity} is larger than maximum allowed ({engine.Options.Constraints.MaxArraySize})"
        );
    }
}

internal static class ArrayPropertyDescriptorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDefaultArrayValueDescriptor(this PropertyDescriptor propertyDescriptor)
        => propertyDescriptor.Flags == PropertyFlag.ConfigurableEnumerableWritable && propertyDescriptor.IsDataDescriptor();
}
