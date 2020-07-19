using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Array
{
    public class ArrayInstance : ObjectInstance
    {
        internal PropertyDescriptor _length;

        private const int MaxDenseArrayLength = 1024 * 10;
        private const ulong MaxArrayLength = 4294967295;

        // we have dense and sparse, we usually can start with dense and fall back to sparse when necessary
        internal PropertyDescriptor[] _dense;
        private Dictionary<uint, PropertyDescriptor> _sparse;

        public ArrayInstance(Engine engine, uint capacity = 0) : base(engine, ObjectClass.Array)
        {
            if (capacity < MaxDenseArrayLength)
            {
                _dense = capacity > 0 ? new PropertyDescriptor[capacity] : System.Array.Empty<PropertyDescriptor>();
            }
            else
            {
                _sparse = new Dictionary<uint, PropertyDescriptor>((int) (capacity <= 1024 ? capacity : 1024));
            }
        }

        /// <summary>
        /// Possibility to construct valid array fast, requires that supplied array does not have holes.
        /// </summary>
        public ArrayInstance(Engine engine, PropertyDescriptor[] items) : base(engine, ObjectClass.Array)
        {
            int length = 0;
            if (items == null || items.Length == 0)
            {
                _dense = System.Array.Empty<PropertyDescriptor>();
                length = 0;
            }
            else
            {
                _dense = items;
                length = items.Length;
            }

            _length = new PropertyDescriptor(length, PropertyFlag.OnlyWritable);
        }

        public ArrayInstance(Engine engine, Dictionary<uint, PropertyDescriptor> items) : base(engine, ObjectClass.Array)
        {
            _sparse = items;
            var length = items?.Count ?? 0;
            _length = new PropertyDescriptor(length, PropertyFlag.OnlyWritable);
        }

        public override bool IsArrayLike => true;

        public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            var oldLenDesc = _length;
            var oldLen = (uint) TypeConverter.ToNumber(oldLenDesc.Value);

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
                    ExceptionHelper.ThrowRangeError(_engine);
                }

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

                var count = _dense?.Length ?? _sparse.Count;
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
                        var keys = new List<uint>(_sparse.Keys);
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
            else if (IsArrayIndex(property, out var index))
            {
                if (index >= oldLen && !oldLenDesc.Writable)
                {
                    return false;
                }

                var succeeded = base.DefineOwnProperty(property, desc);
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

            return base.DefineOwnProperty(property, desc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint GetLength()
        {
            if (_length is null)
            {
                return 0;
            }
            
            return (uint) ((JsNumber) _length._value)._value;
        }

        protected override void AddProperty(JsValue property, PropertyDescriptor descriptor)
        {
            if (property == CommonProperties.Length)
            {
                _length = descriptor;
                return;
            }

            base.AddProperty(property, descriptor);
        }

        protected override bool TryGetProperty(JsValue property, out PropertyDescriptor descriptor)
        {
            if (property == CommonProperties.Length)
            {
                descriptor = _length;
                return _length != null;
            }

            return base.TryGetProperty(property, out descriptor);
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types)
        {
            var properties = new List<JsValue>(_dense?.Length ?? 0 + 1);
            if (_dense != null)
            {
                var length = System.Math.Min(_dense.Length, GetLength());
                for (var i = 0; i < length; i++)
                {
                    if (_dense[i] != null)
                    {
                        properties.Add(JsString.Create(i));
                    }
                }
            }
            else
            {
                foreach (var entry in _sparse)
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

        public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            if (_dense != null)
            {
                var length = System.Math.Min(_dense.Length, GetLength());
                for (var i = 0; i < length; i++)
                {
                    if (_dense[i] != null)
                    {
                        yield return new KeyValuePair<JsValue, PropertyDescriptor>(TypeConverter.ToString(i), _dense[i]);
                    }
                }
            }
            else
            {
                foreach (var entry in _sparse)
                {
                    yield return new KeyValuePair<JsValue, PropertyDescriptor>(TypeConverter.ToString(entry.Key), entry.Value);
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

        public override PropertyDescriptor GetOwnProperty(JsValue property)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PropertyDescriptor GetOwnProperty(uint index)
        {
            return TryGetDescriptor(index, out var result)
                ? result
                : PropertyDescriptor.Undefined;
        }

        internal JsValue Get(uint index)
        {
            var prop = GetOwnProperty(index);
            if (prop == PropertyDescriptor.Undefined)
            {
                prop = Prototype?.GetProperty(index) ?? PropertyDescriptor.Undefined;
            }

            return UnwrapJsValue(prop);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PropertyDescriptor GetProperty(uint index)
        {
            var prop = GetOwnProperty(index);
            if (prop != PropertyDescriptor.Undefined)
            {
                return prop;
            }
            return Prototype?.GetProperty(index) ?? PropertyDescriptor.Undefined;
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (IsArrayIndex(property, out var index))
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

        public override bool HasOwnProperty(JsValue p)
        {
            if (IsArrayIndex(p, out var index))
            {
                return index < GetLength()
                       && (_sparse == null || _sparse.ContainsKey(index))
                       && (_dense == null || (index < (uint) _dense.Length && _dense[index] != null));
            }

            if (p == CommonProperties.Length)
            {
                return _length != null;
            }

            return base.HasOwnProperty(p);
        }

        public override void RemoveOwnProperty(JsValue p)
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
        private static bool IsArrayIndex(JsValue p, out uint index)
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

        private static uint ParseArrayIndex(string p)
        {
            if (p.Length == 0)
            {
                return uint.MaxValue;
            }

            int d = p[0] - '0';

            if (d < 0 || d > 9)
            {
                return uint.MaxValue;
            }

            if (d == 0 && p.Length > 1)
            {
                // If p is a number that start with '0' and is not '0' then
                // its ToString representation can't be the same a p. This is
                // not a valid array index. '01' !== ToString(ToUInt32('01'))
                // http://www.ecma-international.org/ecma-262/5.1/#sec-15.4

                return uint.MaxValue;
            }

            if (p.Length > 1)
            {
                return StringAsIndex(d, p);
            }

            return (uint) d;
        }

        private static uint StringAsIndex(int d, string p)
        {
            ulong result = (uint) d;
            for (int i = 1; i < p.Length; i++)
            {
                d = p[i] - '0';

                if (d < 0 || d > 9)
                {
                    return uint.MaxValue;
                }

                result = result * 10 + (uint) d;

                if (result >= uint.MaxValue)
                {
                    return uint.MaxValue;
                }
            }

            return (uint) result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetIndexValue(uint index, JsValue value, bool updateLength)
        {
            if (updateLength)
            {
                var length = GetLength();
                if (index >= length)
                {
                    SetLength(index + 1);
                }
            }
            WriteArrayValue(index, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetLength(uint length)
        {
            _length.Value = length;
        }

        internal uint GetSmallestIndex()
        {
            if (_dense != null)
            {
                return 0;
            }

            uint smallest = 0;
            // only try to help if collection reasonable small
            if (_sparse.Count > 0 && _sparse.Count < 100 && !_sparse.ContainsKey(0))
            {
                smallest = uint.MaxValue;
                foreach (var key in _sparse.Keys)
                {
                    smallest = System.Math.Min(key, smallest);
                }
            }

            return smallest;
        }

        public bool TryGetValue(uint index, out JsValue value)
        {
            value = Undefined;

            if (!TryGetDescriptor(index, out var desc))
            {
                desc = GetProperty(index);
            }

            return desc.TryGetValue(this, out value);
        }

        internal bool DeletePropertyOrThrow(uint index)
        {
            if (!Delete(index))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }
            return true;
        }

        internal bool Delete(uint index)
        {
            var desc = GetOwnProperty(index);

            if (desc == PropertyDescriptor.Undefined)
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
                return _sparse.Remove(index);
            }

            return false;
        }
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetDescriptor(uint index, out PropertyDescriptor descriptor)
        {
            var temp = _dense;
            if (temp != null)
            {
                descriptor = null;
                if (index < (uint) temp.Length)
                {
                    descriptor = temp[index];
                }
                return descriptor != null;
            }

            return _sparse.TryGetValue(index, out descriptor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteArrayValue(uint index, PropertyDescriptor desc)
        {
            // calculate eagerly so we know if we outgrow
            var newSize = _dense != null && index >= (uint) _dense.Length
                ? System.Math.Max(index, System.Math.Max(_dense.Length, 2)) * 2
                : 0;

            bool canUseDense = _dense != null
                               && index < MaxDenseArrayLength
                               && newSize < MaxDenseArrayLength
                               && index < _dense.Length + 50; // looks sparse

            if (canUseDense)
            {
                if (index >= (uint) _dense.Length)
                {
                    EnsureCapacity((uint) newSize);
                }

                _dense[index] = desc;
            }
            else
            {
                if (_dense != null)
                {
                    ConvertToSparse();
                }
                _sparse[index] = desc;
            }
        }

        private void ConvertToSparse()
        {
            _sparse = new Dictionary<uint, PropertyDescriptor>(_dense.Length <= 1024 ? _dense.Length : 0);
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

            // need to grow
            var newArray = new PropertyDescriptor[capacity];
            System.Array.Copy(_dense, newArray, _dense.Length);
            _dense = newArray;
        }

        public IEnumerator<JsValue> GetEnumerator()
        {
            var length = GetLength();
            for (uint i = 0; i < length; i++)
            {
                if (TryGetValue(i, out JsValue outValue))
                {
                    yield return outValue;
                }
            };
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

            var canUseDirectIndexSet = _dense != null && newLength <= _dense.Length;

            double n = initialLength;
            foreach (var argument in arguments)
            {
                var desc = new PropertyDescriptor(argument, PropertyFlag.ConfigurableEnumerableWritable);
                if (canUseDirectIndexSet)
                {
                    _dense[(uint) n] = desc;
                }
                else
                {
                    WriteValueSlow(n, desc);
                }

                n++;
            }

            // check if we can set length fast without breaking ECMA specification
            if (n < uint.MaxValue && CanSetLength())
            {
                _length.Value = (uint) n;
            }
            else
            {
                if (!Set(CommonProperties.Length, newLength, this))
                {
                    ExceptionHelper.ThrowTypeError(_engine);
                }
            }

            return (uint) n;
        }

        private bool CanSetLength()
        {
            if (!_length.IsAccessorDescriptor())
            {
                return _length.Writable;
            }
            var set = _length.Set;
            return !(set is null) && !set.IsUndefined();
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
            var a = Engine.Array.ConstructFast(len);
            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = this;
            for (uint k = 0; k < len; k++)
            {
                if (TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    var mappedValue = callable.Call(thisArg, args);
                    var desc = new PropertyDescriptor(mappedValue, PropertyFlag.ConfigurableEnumerableWritable);
                    if (a._dense != null && k < (uint) a._dense.Length)
                    {
                        a._dense[k] = desc;
                    }
                    else
                    {
                        a.WriteArrayValue(k, desc);
                    }
                }
            }

            _engine._jsValueArrayPool.ReturnArray(args);
            return a;
        }

        /// <inheritdoc />
        internal override bool FindWithCallback(
            JsValue[] arguments,
            out uint index,
            out JsValue value,
            bool visitUnassigned)
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
            for (uint k = 0; k < len; k++)
            {
                if (TryGetValue(k, out var kvalue) || visitUnassigned)
                {
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

            _engine._jsValueArrayPool.ReturnArray(args);

            index = 0;
            value = Undefined;
            return false;
        }

        public override uint Length => GetLength();

        public JsValue this[uint index]
        {
            get
            {
                TryGetValue(index, out var kValue);
                return kValue;
            }
        }

        internal ArrayInstance ToArray(Engine engine)
        {
            var length = GetLength();
            var array = _engine.Array.ConstructFast(length);
            for (uint i = 0; i < length; i++)
            {
                if (TryGetValue(i, out var kValue))
                {
                    array.SetIndexValue(i, kValue, updateLength: false);
                }
            }
            return array;
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

            var dense = _dense;
            var sourceDense = source._dense;

            if (dense != null && sourceDense != null
                               && (uint) dense.Length >= targetStartIndex + length
                               && dense[targetStartIndex] is null)
            {
                uint j = 0;
                for (uint i = sourceStartIndex; i < sourceStartIndex + length; ++i, j++)
                {
                    var sourcePropertyDescriptor = i < (uint) sourceDense.Length && sourceDense[i] != null
                        ? sourceDense[i]
                        : source.GetProperty(i);

                    dense[targetStartIndex + j] = sourcePropertyDescriptor?._value != null
                        ? new PropertyDescriptor(sourcePropertyDescriptor._value, PropertyFlag.ConfigurableEnumerableWritable)
                        : null;
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
                }
            }
        }
        
        public override string ToString()
        {
            // debugger can make things hard when evaluates computed values
            return "(" + (_length?._value.AsNumber() ?? 0) + ")[]";
        }
    }
}
