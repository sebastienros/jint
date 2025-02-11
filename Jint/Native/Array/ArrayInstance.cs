using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Array;

public class ArrayInstance : ObjectInstance, IEnumerable<JsValue>
{
    internal PropertyDescriptor? _length;

    private const int MaxDenseArrayLength = 10_000_000;

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
        InitializePrototypeAndValidateCapacity(engine, capacity);

        if (capacity < MaxDenseArrayLength)
        {
            _dense = capacity > 0 ? new JsValue?[capacity] : [];
        }
        else
        {
            _sparse = new Dictionary<uint, PropertyDescriptor?>(1024);
        }

        _length = new PropertyDescriptor(length, PropertyFlag.OnlyWritable);
    }

    private protected ArrayInstance(Engine engine, JsValue[] items) : base(engine, type: InternalTypes.Object | InternalTypes.Array)
    {
        InitializePrototypeAndValidateCapacity(engine, capacity: 0);

        _dense = items;
        _length = new PropertyDescriptor(items.Length, PropertyFlag.OnlyWritable);
    }

    private void InitializePrototypeAndValidateCapacity(Engine engine, uint capacity)
    {
        _constructor = engine.Realm.Intrinsics.Array;
        _prototype = _constructor.PrototypeObject;

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
            ExceptionHelper.ThrowRangeError(_engine.Realm);
        }

        var oldLenDesc = _length;
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
        var oldLenDesc = _length;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private JsNumber GetJsNumberLength() => _length is null ? JsNumber.PositiveZero : (JsNumber) _length._value!;

    protected sealed override bool TryGetProperty(JsValue property, [NotNullWhen(true)] out PropertyDescriptor? descriptor)
    {
        if (CommonProperties.Length.Equals(property))
        {
            descriptor = _length;
            return _length != null;
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
        var properties = new List<JsValue>(temp?.Length ?? 0 + 1);
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

        if (_length != null)
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

        if (includeLength && _length != null)
        {
            yield return new KeyValuePair<string, JsValue>(CommonProperties.Length._value, _length.Value);
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

        if (_length != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, _length);
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
            return _length ?? PropertyDescriptor.Undefined;
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

    internal JsValue Get(uint index)
    {
        if (!TryGetValue(index, out var value))
        {
            value = Prototype?.Get(JsString.Create(index)) ?? Undefined;
        }

        return value;
    }

    public sealed override JsValue Get(JsValue property, JsValue receiver)
    {
        if (IsSafeSelfTarget(receiver) && IsArrayIndex(property, out var index) && TryGetValue(index, out var value))
        {
            return value;
        }

        if (CommonProperties.Length.Equals(property))
        {
            var length = _length?._value;
            if (length is not null)
            {
                return length;
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
                && _length is { Writable: true }
                && value is JsNumber jsNumber
                && jsNumber.IsInteger()
                && jsNumber._value <= MaxDenseArrayLength
                && jsNumber._value >= GetLength())
            {
                // we don't need explicit resize
                _length.Value = jsNumber;
                return true;
            }
        }

        // slow path
        return base.Set(property, value, receiver);
    }

    private bool IsSafeSelfTarget(JsValue receiver) => ReferenceEquals(receiver, this) && Extensible;

    public sealed override bool HasProperty(JsValue property)
    {
        if (IsArrayIndex(property, out var index) && GetValue(index, unwrapFromNonDataDescriptor: false) is not null)
        {
            return true;
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
            _length = desc;
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
            _length = null;
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
        if (Extensible && _length!._flags == PropertyFlag.OnlyWritable)
        {
            _length!.Value = length;
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
            ExceptionHelper.ThrowTypeError(_engine.Realm);
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

        var canUseDense = dense != null
                          && index < MaxDenseArrayLength
                          && newSize < MaxDenseArrayLength
                          && index < dense.Length + 50; // looks sparse

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

        _sparse ??= new Dictionary<uint, PropertyDescriptor?>();
        for (uint i = 0; i < (uint) temp.Length; ++i)
        {
            var value = temp[i];
            if (value is not null)
            {
                _sparse.TryGetValue(i, out var descriptor);
                descriptor ??= new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                descriptor.Value = value;
                _sparse[i] = descriptor;
            }
            else
            {
                _sparse.Remove(i);
            }
        }

        _dense = null;
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
            _length!.Value = newLength;
        }
        else
        {
            if (!Set(CommonProperties.Length, newLength))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
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
            _length!.Value = n;
        }
        else
        {
            if (!Set(CommonProperties.Length, newLength))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
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
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        SetLength(newLength);

        return element ?? Undefined;
    }

    private bool CanSetLength()
    {
        if (!_length!.IsAccessorDescriptor())
        {
            return _length.Writable;
        }
        var set = _length.Set;
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

        if (!fromEnd)
        {
            for (uint k = 0; k < len; k++)
            {
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

        _engine._jsValueArrayPool.ReturnArray(args);

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
        return "(" + (_length?._value!.AsNumber() ?? 0) + ")[]";
    }

    private static void ThrowMaximumArraySizeReachedException(Engine engine, uint capacity)
    {
        ExceptionHelper.ThrowMemoryLimitExceededException(
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
