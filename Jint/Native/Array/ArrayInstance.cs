using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Array
{
    public class ArrayInstance : ObjectInstance, IEnumerable<JsValue>
    {
        internal PropertyDescriptor? _length;

        private const int MaxDenseArrayLength = 10_000_000;

        // we have dense and sparse, we usually can start with dense and fall back to sparse when necessary
        // entries are lazy and can be either of type PropertyDescriptor or plain JsValue while there is no need for extra info
        internal object?[]? _dense;
        private Dictionary<uint, object?>? _sparse;

        private ObjectChangeFlags _objectChangeFlags;

        public ArrayInstance(Engine engine, uint capacity = 0) : base(engine)
        {
            if (capacity > engine.Options.Constraints.MaxArraySize)
            {
                ThrowMaximumArraySizeReachedException(engine, capacity);
            }

            if (capacity < MaxDenseArrayLength)
            {
                _dense = capacity > 0 ? new object?[capacity] : System.Array.Empty<object?>();
            }
            else
            {
                _sparse = new Dictionary<uint, object?>((int) (capacity <= 1024 ? capacity : 1024));
            }
        }

        /// <summary>
        /// Possibility to construct valid array fast, requires that supplied array does not have holes.
        /// </summary>
        public ArrayInstance(Engine engine, JsValue[] items) : base(engine)
        {
            int length;
            if (items == null || items.Length == 0)
            {
                _dense = System.Array.Empty<object>();
                length = 0;
            }
            else
            {
                _dense = items;
                length = items.Length;
            }

            _length = new PropertyDescriptor(length, PropertyFlag.OnlyWritable);
        }

        /// <summary>
        /// Possibility to construct valid array fast, requires that supplied array does not have holes.
        /// </summary>
        public ArrayInstance(Engine engine, PropertyDescriptor[] items) : base(engine)
        {
            int length;
            if (items == null || items.Length == 0)
            {
                _dense = System.Array.Empty<object>();
                length = 0;
            }
            else
            {
                _dense = items;
                length = items.Length;
            }

            _length = new PropertyDescriptor(length, PropertyFlag.OnlyWritable);
        }

        public sealed override bool IsArrayLike => true;

        public sealed override bool IsArray() => true;

        internal sealed override bool HasOriginalIterator
            => ReferenceEquals(Get(GlobalSymbolRegistry.Iterator), _engine.Realm.Intrinsics.Array.PrototypeObject._originalIteratorFunction);

        /// <summary>
        /// Checks whether there have been changes to object prototype chain which could render fast access patterns impossible.
        /// </summary>
        internal bool CanUseFastAccess
        {
            get
            {
                if ((_objectChangeFlags & ObjectChangeFlags.NonDefaultDataDescriptorUsage) != 0)
                {
                    // could be a mutating property for example, length might change, not safe anymore
                    return false;
                }

                if (_prototype is not ArrayPrototype arrayPrototype
                    || !ReferenceEquals(_prototype, _engine.Realm.Intrinsics.Array.PrototypeObject))
                {
                    // somebody has switched prototype
                    return false;
                }

                if ((arrayPrototype._objectChangeFlags & ObjectChangeFlags.ArrayIndex) != 0)
                {
                    // maybe somebody moved integer property to prototype? not safe anymore
                    return false;
                }

                if (arrayPrototype.Prototype is not ObjectPrototype arrayPrototypePrototype
                    || !ReferenceEquals(arrayPrototypePrototype, _engine.Realm.Intrinsics.Array.PrototypeObject.Prototype))
                {
                    return false;
                }

                return (arrayPrototypePrototype._objectChangeFlags & ObjectChangeFlags.ArrayIndex) == 0;
            }
        }

        public sealed override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            var isArrayIndex = IsArrayIndex(property, out var index);
            TrackChanges(property, desc, isArrayIndex);

            if (isArrayIndex)
            {
                return DefineOwnProperty(index, desc);
            }

            if (property == CommonProperties.Length)
            {
                var value = desc.Value;
                if (ReferenceEquals(value, null))
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
                            if (_dense[keyIndex] == null)
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

            return base.DefineOwnProperty(property, desc);
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
        internal uint GetLength()
        {
            if (_length is null)
            {
                return 0;
            }

            return (uint) ((JsNumber) _length._value!)._value;
        }

        protected sealed override void AddProperty(JsValue property, PropertyDescriptor descriptor)
        {
            if (property == CommonProperties.Length)
            {
                _length = descriptor;
                return;
            }

            base.AddProperty(property, descriptor);
        }

        protected sealed override bool TryGetProperty(JsValue property, [NotNullWhen(true)] out PropertyDescriptor? descriptor)
        {
            if (property == CommonProperties.Length)
            {
                descriptor = _length;
                return _length != null;
            }

            return base.TryGetProperty(property, out descriptor);
        }

        public sealed override List<JsValue> GetOwnPropertyKeys(Types types = Types.None | Types.String | Types.Symbol)
        {
            if ((types & Types.String) == 0)
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
                    if (temp[i] != null)
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

        public sealed override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            var temp = _dense;
            if (temp != null)
            {
                var length = System.Math.Min(temp.Length, GetLength());
                for (var i = 0; i < length; i++)
                {
                    var value = temp[i];
                    if (value != null)
                    {
                        if (value is not PropertyDescriptor descriptor)
                        {
                            temp[i] = descriptor = new PropertyDescriptor((JsValue) value, PropertyFlag.ConfigurableEnumerableWritable);
                        }
                        yield return new KeyValuePair<JsValue, PropertyDescriptor>(TypeConverter.ToString(i), descriptor);
                    }
                }
            }
            else
            {
                foreach (var entry in _sparse!)
                {
                    var value = entry.Value;
                    if (value is not null)
                    {
                        if (value is not PropertyDescriptor descriptor)
                        {
                            _sparse[entry.Key] = descriptor = new PropertyDescriptor((JsValue) value, PropertyFlag.ConfigurableEnumerableWritable);
                        }
                        yield return new KeyValuePair<JsValue, PropertyDescriptor>(TypeConverter.ToString(entry.Key), descriptor);
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
            if (property == CommonProperties.Length)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }

            if (IsArrayIndex(property, out var index))
            {
                if (TryGetDescriptor(index, out var result))
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
                value = UnwrapJsValue(Prototype?.GetProperty(JsString.Create(index)) ?? PropertyDescriptor.Undefined);
            }

            return value;
        }

        public sealed override JsValue Get(JsValue property, JsValue receiver)
        {
            if (IsSafeSelfTarget(receiver) && IsArrayIndex(property, out var index) && TryGetValue(index, out var value))
            {
                return value;
            }

            return base.Get(property, receiver);
        }

        public sealed override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            var isSafeSelfTarget = IsSafeSelfTarget(receiver);
            if (isSafeSelfTarget && IsArrayIndex(property, out var index))
            {
                if (TryGetDescriptor(index, out var descriptor))
                {
                    if (descriptor.IsDefaultArrayValueDescriptor())
                    {
                        // fast path with direct write without allocations
                        descriptor.Value = value;
                        return true;
                    }
                }
                else if (CanUseFastAccess)
                {
                    // we know it's to be written to own array backing field as new value
                    SetIndexValue(index, value, true);
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

        protected internal sealed override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            var isArrayIndex = IsArrayIndex(property, out var index);
            TrackChanges(property, desc, isArrayIndex);
            if (isArrayIndex)
            {
                WriteArrayValue(index, desc);
            }
            else if (property == CommonProperties.Length)
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
                if (!desc.IsDefaultArrayValueDescriptor())
                {
                    _objectChangeFlags |= ObjectChangeFlags.NonDefaultDataDescriptorUsage;
                }
                _objectChangeFlags |= ObjectChangeFlags.ArrayIndex;
            }
            else
            {
                _objectChangeFlags |= property.IsSymbol() ? ObjectChangeFlags.Symbol : ObjectChangeFlags.Property;
            }
        }

        public sealed override void RemoveOwnProperty(JsValue p)
        {
            if (IsArrayIndex(p, out var index))
            {
                Delete(index);
            }

            if (p == CommonProperties.Length)
            {
                _length = null;
            }

            base.RemoveOwnProperty(p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsArrayIndex(JsValue p, out uint index)
        {
            if (p is JsNumber number)
            {
                var value = number._value;
                var intValue = (uint) value;
                index = intValue;
                return value == intValue && intValue != uint.MaxValue;
            }

            index = ParseArrayIndex(p.ToString());
            return index != uint.MaxValue;

            // 15.4 - Use an optimized version of the specification
            // return TypeConverter.ToString(index) == TypeConverter.ToString(p) && index != uint.MaxValue;
        }

        internal static uint ParseArrayIndex(string p)
        {
            if (p.Length == 0)
            {
                return uint.MaxValue;
            }

            if (p.Length > 1 && p[0] == '0')
            {
                // If p is a number that start with '0' and is not '0' then
                // its ToString representation can't be the same a p. This is
                // not a valid array index. '01' !== ToString(ToUInt32('01'))
                // http://www.ecma-international.org/ecma-262/5.1/#sec-15.4

                return uint.MaxValue;
            }

            if (!uint.TryParse(p, out var d))
            {
                return uint.MaxValue;
            }

            return d;
        }

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
        internal void SetLength(ulong length)
        {
            var number = JsNumber.Create(length);
            if (Extensible && _length!._flags == PropertyFlag.OnlyWritable)
            {
                _length!.Value = number;
            }
            else
            {
                // slow path
                Set(CommonProperties.Length, number, true);
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

        internal bool Delete(uint index)
        {
            // check fast path
            var temp = _dense;
            if (temp != null)
            {
                if (index < (uint) temp.Length)
                {
                    var value = temp[index];
                    if (value is JsValue || value is PropertyDescriptor { Configurable: true })
                    {
                        temp[index] = null;
                        return true;
                    }
                }
            }

            if (!TryGetDescriptor(index, out var desc))
            {
                return true;
            }

            if (desc.Configurable)
            {
                DeleteAt(index);
                return true;
            }

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

        private bool TryGetDescriptor(uint index, [NotNullWhen(true)] out PropertyDescriptor? descriptor)
        {
            descriptor = null;
            var temp = _dense;
            if (temp != null)
            {
                if (index < (uint) temp.Length)
                {
                    var value = temp[index];
                    if (value is JsValue jsValue)
                    {
                        temp[index] = descriptor = new PropertyDescriptor(jsValue, PropertyFlag.ConfigurableEnumerableWritable);
                    }
                    else if (value is PropertyDescriptor propertyDescriptor)
                    {
                        descriptor = propertyDescriptor;
                    }
                }
                return descriptor != null;
            }

            if (_sparse!.TryGetValue(index, out var sparseValue))
            {
                if (sparseValue is JsValue jsValue)
                {
                    _sparse[index] = descriptor = new PropertyDescriptor(jsValue, PropertyFlag.ConfigurableEnumerableWritable);
                }
                else if (sparseValue is PropertyDescriptor propertyDescriptor)
                {
                    descriptor = propertyDescriptor;
                }
            }

            return descriptor is not null;
        }

        internal bool TryGetValue(uint index, out JsValue value)
        {
            value = GetValue(index, unwrapFromNonDataDescriptor: true)!;

            if (value is not null)
            {
                return true;
            }

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

        private JsValue? GetValue(uint index, bool unwrapFromNonDataDescriptor)
        {
            object? value = null;
            var temp = _dense;
            if (temp != null)
            {
                if (index < (uint) temp.Length)
                {
                    value = temp[index];
                }
            }
            else
            {
                _sparse!.TryGetValue(index, out value);
            }

            if (value is JsValue jsValue)
            {
                return jsValue;
            }

            if (value is PropertyDescriptor propertyDescriptor)
            {
                return propertyDescriptor.IsDataDescriptor() || unwrapFromNonDataDescriptor ? UnwrapJsValue(propertyDescriptor) : null;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteArrayValue(uint index, object? value)
        {
            var dense = _dense;
            if (dense != null && index < (uint) dense.Length)
            {
                dense[index] = value;
            }
            else
            {
                WriteArrayValueUnlikely(index, value);
            }
        }

        private void WriteArrayValueUnlikely(uint index, object? value)
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
                if (dense != null)
                {
                    ConvertToSparse();
                }

                _sparse![index] = value;
            }
        }

        private void ConvertToSparse()
        {
            _sparse = new Dictionary<uint, object?>(_dense!.Length <= 1024 ? _dense.Length : 0);
            // need to move data
            for (uint i = 0; i < (uint) _dense.Length; ++i)
            {
                if (_dense[i] != null)
                {
                    _sparse[i] = _dense[i];
                }
            }

            _dense = null;
        }

        internal void EnsureCapacity(uint capacity)
        {
            if (capacity > MaxDenseArrayLength || _dense is null || capacity <= (uint) _dense.Length)
            {
                return;
            }

            if (capacity > _engine.Options.Constraints.MaxArraySize)
            {
                ThrowMaximumArraySizeReachedException(_engine, capacity);
            }

            // need to grow
            var newArray = new object[capacity];
            System.Array.Copy(_dense, newArray, _dense.Length);
            _dense = newArray;
        }

        public IEnumerator<JsValue> GetEnumerator()
        {
            var length = GetLength();
            for (uint i = 0; i < length; i++)
            {
                TryGetValue(i, out var outValue);
                yield return outValue ?? Undefined;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Push(JsValue value)
        {
            var initialLength = GetLength();
            var newLength = initialLength + 1;

            var temp = _dense;
            var canUseDirectIndexSet = temp != null && newLength <= temp.Length;

            double n = initialLength;
            var desc = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
            if (canUseDirectIndexSet)
            {
                temp![(uint) n] = desc;
            }
            else
            {
                WriteValueSlow(n, desc);
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

        internal uint Push(JsValue[] arguments)
        {
            var initialLength = GetLength();
            var newLength = initialLength + arguments.Length;

            // if we see that we are bringing more than normal growth algorithm handles, ensure capacity eagerly
            if (_dense != null
                && initialLength != 0
                && arguments.Length > initialLength * 2
                && newLength <= MaxDenseArrayLength)
            {
                EnsureCapacity((uint) newLength);
            }

            var temp = _dense;
            ulong n = initialLength;
            foreach (var argument in arguments)
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
        private void WriteValueSlow(double n, PropertyDescriptor desc)
        {
            if (n < uint.MaxValue)
            {
                WriteArrayValue((uint) n, desc);
            }
            else
            {
                DefinePropertyOrThrow((uint) n, desc);
            }
        }

        internal ArrayInstance Map(JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var len = GetLength();

            var callable = GetCallable(callbackfn);
            var a = Engine.Realm.Intrinsics.Array.ArrayCreate(len);
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
            JsValue[] arguments,
            out uint index,
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

        public sealed override uint Length => GetLength();

        internal sealed override bool IsIntegerIndexedArray => true;

        public JsValue this[uint index]
        {
            get
            {
                TryGetValue(index, out var kValue);
                return kValue ?? Undefined;
            }
        }

        /// <summary>
        /// Fast path for concatenating sane-sized arrays, we assume size has been calculated.
        /// </summary>
        internal void CopyValues(ArrayInstance source, uint sourceStartIndex, uint targetStartIndex, uint length)
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
            if (dense != null && sourceDense != null
                               && (uint) dense.Length >= targetStartIndex + length
                               && dense[targetStartIndex] is null)
            {
                uint j = 0;
                for (var i = sourceStartIndex; i < sourceStartIndex + length; ++i, j++)
                {
                    object? sourceValue;
                    if (i < (uint) sourceDense.Length && sourceDense[i] != null)
                    {
                        sourceValue = sourceDense[i];
                        if (sourceValue is PropertyDescriptor propertyDescriptor)
                        {
                            sourceValue = UnwrapJsValue(propertyDescriptor);
                        }
                    }
                    else
                    {
                        if (!source.TryGetValue(i, out var temp))
                        {
                            sourceValue = source.Prototype?.Get(JsString.Create(i));
                        }
                        else
                        {
                            sourceValue = temp;
                        }
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
}
