using System.Collections.Generic;
using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Array
{
    public class ArrayInstance : ObjectInstance
    {
        private readonly Engine _engine;
        private IDictionary<uint, PropertyDescriptor> _array = new MruPropertyCache2<uint, PropertyDescriptor>();
        private PropertyDescriptor _length;

        public ArrayInstance(Engine engine) : base(engine)
        {
            _engine = engine;
        }

        public override string Class
        {
            get
            {
                return "Array";
            }
        }

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
                var valueDesc = new PropertyDescriptor(value: value, writable: null, enumerable: null, configurable: null);
                DefineOwnProperty(propertyName, valueDesc, throwOnError);
                return;
            }

            // property is an accessor or inherited
            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                var setter = desc.Set.Value.TryCast<ICallable>();
                setter.Call(new JsValue(this), new[] { value });
            }
            else
            {
                var newDesc = new PropertyDescriptor(value, true, true, true);
                DefineOwnProperty(propertyName, newDesc, throwOnError);
            }
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            var oldLenDesc = GetOwnProperty("length");
            var oldLen = (uint)TypeConverter.ToNumber(oldLenDesc.Value.Value);
            uint index;

            if (propertyName == "length")
            {
                if (!desc.Value.HasValue)
                {
                    return base.DefineOwnProperty("length", desc, throwOnError);
                }

                var newLenDesc = new PropertyDescriptor(desc);
                uint newLen = TypeConverter.ToUint32(desc.Value.Value);
                if (newLen != TypeConverter.ToNumber(desc.Value.Value))
                {
                    throw new JavaScriptException(_engine.RangeError);
                }

                newLenDesc.Value = newLen;
                if (newLen >= oldLen)
                {
                    return base.DefineOwnProperty("length", _length = newLenDesc, throwOnError);
                }
                if (!oldLenDesc.Writable.Value)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(_engine.TypeError);
                    }

                    return false;
                }
                bool newWritable;
                if (!newLenDesc.Writable.HasValue || newLenDesc.Writable.Value)
                {
                    newWritable = true;
                }
                else
                {
                    newWritable = false;
                    newLenDesc.Writable = true;
                }

                var succeeded = base.DefineOwnProperty("length", _length = newLenDesc, throwOnError);
                if (!succeeded)
                {
                    return false;
                }

                // in the case of sparse arrays, treat each concrete element instead of
                // iterating over all indexes

                if (_array.Count < oldLen - newLen)
                {
                    var keys = _array.Keys.ToArray();
                    foreach (var key in keys)
                    {
                        uint keyIndex;
                        // is it the index of the array
                        if (IsArrayIndex(key, out keyIndex) && keyIndex >= newLen && keyIndex < oldLen)
                        {
                            var deleteSucceeded = Delete(key.ToString(), false);
                            if (!deleteSucceeded)
                            {
                                newLenDesc.Value = new JsValue(keyIndex + 1);
                                if (!newWritable)
                                {
                                    newLenDesc.Writable = false;
                                }
                                base.DefineOwnProperty("length", _length = newLenDesc, false);

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
                            base.DefineOwnProperty("length", _length = newLenDesc, false);

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
                    DefineOwnProperty("length", new PropertyDescriptor(value: null, writable: false, enumerable: null, configurable: null), false);
                }
                return true;
            }
            else if (IsArrayIndex(propertyName, out index))
            {
                if (index >= oldLen && !oldLenDesc.Writable.Value)
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
                    base.DefineOwnProperty("length", _length = oldLenDesc, false);
                }
                return true;
            }

            return base.DefineOwnProperty(propertyName, desc, throwOnError);
        }

        private uint GetLength()
        {
            return TypeConverter.ToUint32(_length.Value.Value);
        }

        public override IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
        {
            foreach(var entry in _array)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(entry.Key.ToString(), entry.Value);
            }

            foreach(var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            uint index;
            if (IsArrayIndex(propertyName, out index))
            {
                PropertyDescriptor result;
                if (_array.TryGetValue(index, out result))
                {
                    return result;
                }
                else
                {
                    return PropertyDescriptor.Undefined;
                } 
            }

            return base.GetOwnProperty(propertyName);
        }

        protected override void SetOwnProperty(string propertyName, PropertyDescriptor desc)
        {
            uint index;
            if (IsArrayIndex(propertyName, out index))
            {
                _array[index] = desc;
            }
            else
            {
                if(propertyName == "length")
                {
                    _length = desc;
                }

                base.SetOwnProperty(propertyName, desc);
            }            
        }

        public override bool HasOwnProperty(string p)
        {
            uint index;
            if (IsArrayIndex(p, out index))
            {
                return index < GetLength() && _array.ContainsKey(index);
            }

            return base.HasOwnProperty(p);
        }

        public override void RemoveOwnProperty(string p)
        {
            uint index;
            if(IsArrayIndex(p, out index))
            {
                _array.Remove(index);
            }

            base.RemoveOwnProperty(p);
        }

        public static bool IsArrayIndex(JsValue p, out uint index)
        {
            index = ParseArrayIndex(TypeConverter.ToString(p));

            return index != uint.MaxValue;

            // 15.4 - Use an optimized version of the specification
            // return TypeConverter.ToString(index) == TypeConverter.ToString(p) && index != uint.MaxValue;
        }

        internal static uint ParseArrayIndex(string p)
        {
            int d = p[0] - '0';

            if (d < 0 || d > 9)
            {
                return uint.MaxValue;
            }

            if(d == 0 && p.Length > 1)
            {
                // If p is a number that start with '0' and is not '0' then
                // its ToString representation can't be the same a p. This is 
                // not a valid array index. '01' !== ToString(ToUInt32('01'))
                // http://www.ecma-international.org/ecma-262/5.1/#sec-15.4

                return uint.MaxValue; 
            }

            ulong result = (uint)d;

            for (int i = 1; i < p.Length; i++)
            {
                d = p[i] - '0';

                if (d < 0 || d > 9)
                {
                    return uint.MaxValue;
                }

                result = result * 10 + (uint)d;
                
                if (result >= uint.MaxValue)
                {
                    return uint.MaxValue;
                }
            }

            return (uint)result;
        }
    }
}
