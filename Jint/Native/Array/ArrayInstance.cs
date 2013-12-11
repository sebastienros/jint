using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Array
{
    public class ArrayInstance : ObjectInstance
    {
        private readonly Engine _engine;
 
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

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            var oldLenDesc = GetOwnProperty("length");
            var oldLen = TypeConverter.ToNumber(oldLenDesc.Value.Value);
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
                    return base.DefineOwnProperty("length", newLenDesc, throwOnError);
                }
                if (!oldLenDesc.Writable.Value.AsBoolean())
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(_engine.TypeError);
                    }

                    return false;
                }
                bool newWritable;
                if (!newLenDesc.Writable.HasValue || newLenDesc.Writable.Value.AsBoolean())
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
                // in the case of sparse arrays, treat each concrete element instead of
                // iterating over all indexes

                if (Properties.Count < oldLen - newLen)
                {
                    var keys = Properties.Keys.ToArray();
                    foreach (var key in keys)
                    {
                        uint index;
                        // is it the index of the array
                        if (uint.TryParse(key, out index) && index >= newLen && index < oldLen)
                        {
                            var deleteSucceeded = Delete(key, false);
                            if (!deleteSucceeded)
                            {
                                newLenDesc.Value = new JsValue(index + 1);
                                if (!newWritable)
                                {
                                    newLenDesc.Writable = JsValue.False;
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
                    DefineOwnProperty("length", new PropertyDescriptor(value: null, writable: false, enumerable: null, configurable: null), false);
                }
                return true;
            }
            else if (IsArrayIndex(propertyName))
            {
                var index = TypeConverter.ToUint32(propertyName);
                if (index >= oldLen && !oldLenDesc.Writable.Value.AsBoolean())
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

        public static bool IsArrayIndex(JsValue p)
        {
            return TypeConverter.ToString(TypeConverter.ToUint32(p)) == TypeConverter.ToString(p) && TypeConverter.ToUint32(p) != uint.MaxValue;
        }

    }
}
