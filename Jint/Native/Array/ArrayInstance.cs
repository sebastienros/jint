using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

using PropertyDescriptor = Jint.Runtime.Descriptors.PropertyDescriptor;
using TypeConverter = Jint.Runtime.TypeConverter;

namespace Jint.Native.Array
{
    public class ArrayInstance : ObjectInstance, IEnumerable<JsValue>
    {
        private readonly Engine _engine;

        private const string PropertyNameLength = "length";
        private const int PropertyNameLengthLength = 6;

        private PropertyDescriptor _length;

        private const int MaxDenseArrayLength = 1024 * 10;

        // we have dense and sparse, we usually can start with dense and fall back to sparse when necessary
        private PropertyDescriptor[] _dense;
        private Dictionary<uint, PropertyDescriptor> _sparse;

        public ArrayInstance(Engine engine, uint capacity = 0) : base(engine)
        {
            _engine = engine;
            if (capacity < MaxDenseArrayLength)
            {
                _dense = capacity > 0 ? new PropertyDescriptor[capacity] : System.Array.Empty<PropertyDescriptor>();
            }
            else
            {
                _sparse = new Dictionary<uint, PropertyDescriptor>((int) (capacity <= 1024 ? capacity : 1024));
            }
        }

        public ArrayInstance(Engine engine, PropertyDescriptor[] items) : base(engine)
        {
            _engine = engine;
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
            
            SetOwnProperty(PropertyNameLength, new PropertyDescriptor(length, PropertyFlag.OnlyWritable));
        }

        public ArrayInstance(Engine engine, Dictionary<uint, PropertyDescriptor> items) : base(engine)
        {
            _engine = engine;
            _sparse = items;
            var length = items?.Count ?? 0;
            SetOwnProperty(PropertyNameLength, new PropertyDescriptor(length, PropertyFlag.OnlyWritable));
        }

        public override string Class => "Array";

        /// Implementation from ObjectInstance official specs as the one
        /// in ObjectInstance is optimized for the general case and wouldn't work
        /// for arrays
        public override void Put(string propertyName, JsValue value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc.IsDataDescriptor())
            {
                var valueDesc = new PropertyDescriptor(value, PropertyFlag.None);
                DefineOwnProperty(propertyName, valueDesc, throwOnError);
                return;
            }

            // property is an accessor or inherited
            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                var setter = desc.Set.TryCast<ICallable>();
                setter.Call(this, new[] {value});
            }
            else
            {
                var newDesc = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                DefineOwnProperty(propertyName, newDesc, throwOnError);
            }
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            var oldLenDesc = _length;
            var oldLen = (uint) TypeConverter.ToNumber(oldLenDesc.Value);

            if (propertyName.Length == 6 && propertyName == "length")
            {
                var value = desc.Value;
                if (ReferenceEquals(value, null))
                {
                    return base.DefineOwnProperty("length", desc, throwOnError);
                }

                var newLenDesc = new PropertyDescriptor(desc);
                uint newLen = TypeConverter.ToUint32(value);
                if (newLen != TypeConverter.ToNumber(value))
                {
                    throw new JavaScriptException(_engine.RangeError);
                }

                newLenDesc.Value = newLen;
                if (newLen >= oldLen)
                {
                    return base.DefineOwnProperty("length", newLenDesc, throwOnError);
                }

                if (!oldLenDesc.Writable)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(_engine.TypeError);
                    }

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

                var succeeded = base.DefineOwnProperty("length", newLenDesc, throwOnError);
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
                                var deleteSucceeded = DeleteAt(keyIndex);
                                if (!deleteSucceeded)
                                {
                                    newLenDesc.Value = keyIndex + 1;
                                    if (!newWritable)
                                    {
                                        newLenDesc.Writable = false;
                                    }

                                    base.DefineOwnProperty("length", newLenDesc, false);

                                    if (throwOnError)
                                    {
                                        throw new JavaScriptException(_engine.TypeError);
                                    }

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
                                var deleteSucceeded = Delete(TypeConverter.ToString(keyIndex), false);
                                if (!deleteSucceeded)
                                {
                                    newLenDesc.Value = JsNumber.Create(keyIndex + 1);
                                    if (!newWritable)
                                    {
                                        newLenDesc.Writable = false;
                                    }

                                    base.DefineOwnProperty("length", newLenDesc, false);

                                    if (throwOnError)
                                    {
                                        throw new JavaScriptException(_engine.TypeError);
                                    }

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
                        var deleteSucceeded = Delete(TypeConverter.ToString(oldLen), false);
                        if (!deleteSucceeded)
                        {
                            newLenDesc.Value = oldLen + 1;
                            if (!newWritable)
                            {
                                newLenDesc.Writable = false;
                            }

                            base.DefineOwnProperty("length", newLenDesc, false);

                            if (throwOnError)
                            {
                                throw new JavaScriptException(_engine.TypeError);
                            }

                            return false;
                        }
                    }
                }

                if (!newWritable)
                {
                    DefineOwnProperty("length", new PropertyDescriptor(value: null, PropertyFlag.WritableSet), false);
                }

                return true;
            }
            else if (IsArrayIndex(propertyName, out var index))
            {
                if (index >= oldLen && !oldLenDesc.Writable)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(_engine.TypeError);
                    }

                    return false;
                }

                var succeeded = base.DefineOwnProperty(propertyName, desc, false);
                if (!succeeded)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(_engine.TypeError);
                    }

                    return false;
                }

                if (index >= oldLen)
                {
                    oldLenDesc.Value = index + 1;
                    base.DefineOwnProperty("length", oldLenDesc, false);
                }

                return true;
            }

            return base.DefineOwnProperty(propertyName, desc, throwOnError);
        }

        public uint GetLength()
        {
            return (uint) ((JsNumber) _length.Value)._value;
        }

        protected override void AddProperty(string propertyName, PropertyDescriptor descriptor)
        {
            if (propertyName.Length == PropertyNameLengthLength && propertyName == PropertyNameLength)
            {
                _length = descriptor;
                return;
            }

            base.AddProperty(propertyName, descriptor);
        }

        protected override bool TryGetProperty(string propertyName, out PropertyDescriptor descriptor)
        {
            if (propertyName.Length == PropertyNameLengthLength && propertyName == PropertyNameLength)
            {
                descriptor = _length;
                return _length != null;
            }

            return base.TryGetProperty(propertyName, out descriptor);
        }

        public override IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
        {
            if (_length != null)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(PropertyNameLength, _length);
            }

            if (_dense != null)
            {
                var length = System.Math.Min(_dense.Length, GetLength());
                for (var i = 0; i < length; i++)
                {
                    if (_dense[i] != null)
                    {
                        yield return new KeyValuePair<string, PropertyDescriptor>(TypeConverter.ToString(i), _dense[i]);
                    }
                }
            }
            else
            {
                foreach (var entry in _sparse)
                {
                    yield return new KeyValuePair<string, PropertyDescriptor>(TypeConverter.ToString(entry.Key), entry.Value);
                }
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            if (IsArrayIndex(propertyName, out var index))
            {
                if (TryGetDescriptor(index, out var result))
                {
                    return result;
                }

                return PropertyDescriptor.Undefined;
            }

            if (propertyName.Length == PropertyNameLengthLength && propertyName == PropertyNameLength)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }

            return base.GetOwnProperty(propertyName);
        }

        protected internal override void SetOwnProperty(string propertyName, PropertyDescriptor desc)
        {
            if (IsArrayIndex(propertyName, out var index))
            {
                WriteArrayValue(index, desc);
            }
            else if (propertyName.Length == PropertyNameLengthLength && propertyName == PropertyNameLength)
            {
                _length = desc;
            }
            else
            {
                base.SetOwnProperty(propertyName, desc);
            }
        }

        public override bool HasOwnProperty(string p)
        {
            if (IsArrayIndex(p, out var index))
            {
                return index < GetLength()
                       && (_sparse == null || _sparse.ContainsKey(index))
                       && (_dense == null || (index < _dense.Length && _dense[index] != null));
            }

            if (p == PropertyNameLength)
            {
                return _length != null;
            }

            return base.HasOwnProperty(p);
        }

        public override void RemoveOwnProperty(string p)
        {
            if (IsArrayIndex(p, out var index))
            {
                DeleteAt(index);
            }

            if (p == PropertyNameLength)
            {
                _length = null;
            }

            base.RemoveOwnProperty(p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsArrayIndex(string p, out uint index)
        {
            index = ParseArrayIndex(p);
            return index != uint.MaxValue;

            // 15.4 - Use an optimized version of the specification
            // return TypeConverter.ToString(index) == TypeConverter.ToString(p) && index != uint.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ParseArrayIndex(string p)
        {
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

        internal void SetIndexValue(uint index, JsValue value, bool throwOnError)
        {
            var length = GetLength();
            if (index >= length)
            {
                _length.Value = index + 1;
            }

            WriteArrayValue(index, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));
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

            if (!TryGetDescriptor(index, out var desc)
                || desc == null
                || desc == PropertyDescriptor.Undefined
                || (ReferenceEquals(desc.Value, null) && ReferenceEquals(desc.Get, null)))
            {
                desc = GetProperty(TypeConverter.ToString(index));
            }

            if (desc != null && desc != PropertyDescriptor.Undefined)
            {
                bool success = desc.TryGetValue(this, out value);
                return success;
            }

            return false;
        }

        internal bool DeleteAt(uint index)
        {
            if (_dense != null)
            {
                if (index < _dense.Length)
                {
                    _dense[index] = null;
                    return true;
                }
            }
            else
            {
                return _sparse.Remove(index);
            }

            return false;
        }

        private bool TryGetDescriptor(uint index, out PropertyDescriptor descriptor)
        {
            if (_dense != null)
            {
                if (index >= _dense.Length)
                {
                    descriptor = null;
                    return false;
                }

                descriptor = _dense[index];
                return descriptor != null;
            }

            return _sparse.TryGetValue(index, out descriptor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayValue(uint index, PropertyDescriptor desc)
        {
            // calculate eagerly so we know if we outgrow
            var newSize = _dense != null && index >= _dense.Length
                ? System.Math.Max(index, System.Math.Max(_dense.Length, 2)) * 2
                : 0;

            bool canUseDense = _dense != null
                               && index < MaxDenseArrayLength
                               && newSize < MaxDenseArrayLength
                               && index < _dense.Length + 50; // looks sparse

            if (canUseDense)
            {
                if (index >= _dense.Length)
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
            for (uint i = 0; i < _dense.Length; ++i)
            {
                if (_dense[i] != null)
                {
                    _sparse[i] = _dense[i];
                }
            }

            _dense = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureCapacity(uint capacity)
        {
            if (capacity > _dense.Length)
            {
                // need to grow
                var newArray = new PropertyDescriptor[capacity];
                System.Array.Copy(_dense, newArray, _dense.Length);
                _dense = newArray;
            }
        }

        public IEnumerator<JsValue> GetEnumerator()
        {
            for (uint i = 0; i < GetLength(); i++)
            {
                if (TryGetValue(i, out JsValue outValue))
                {
                    yield return outValue;
                }
            };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
