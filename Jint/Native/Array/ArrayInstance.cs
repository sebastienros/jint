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
            var oldLenDesc = GetOwnProperty("length").As<DataDescriptor>();
            var oldLen = TypeConverter.ToNumber(oldLenDesc.Value);
            if (propertyName == "length")
            {
                var descData = desc as DataDescriptor;
                if (descData == null)
                {
                    return base.DefineOwnProperty("length", desc, throwOnError);
                }
                
                var newLenDesc = new DataDescriptor(desc);
                uint newLen = TypeConverter.ToUint32(descData.Value);
                if (newLen != TypeConverter.ToNumber(descData.Value))
                {
                    throw new JavaScriptException(_engine.RangeError);
                }
                newLenDesc.Value = newLen;
                if (newLen >= oldLen)
                {
                    return base.DefineOwnProperty("length", newLenDesc, throwOnError);
                }
                if (!oldLenDesc.WritableIsSet)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(_engine.TypeError);
                    }

                    return false;
                }
                var newWritable = true;
                if (!newLenDesc.WritableIsSet)
                {
                    newWritable = false;
                    newLenDesc.Writable = true;
                }
                var succeeded = base.DefineOwnProperty("length", newLenDesc, throwOnError);
                if (!succeeded)
                {
                    return false;
                }
                while (newLen < oldLen)
                {
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
                if (!newWritable)
                {
                    DefineOwnProperty("length", new DataDescriptor(null) {Writable = false}, false);
                }
                return true;
            }
            else if (IsArrayIndex(propertyName))
            {
                var index = TypeConverter.ToUint32(propertyName);
                if (index >= oldLen && !oldLenDesc.WritableIsSet)
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

            return base.DefineOwnProperty(propertyName, desc, false);
        }

        public static bool IsArrayIndex(object p)
        {
            return TypeConverter.ToString(TypeConverter.ToUint32(p)) == TypeConverter.ToString(p) && TypeConverter.ToUint32(p) != uint.MaxValue;
        }

    }
}
