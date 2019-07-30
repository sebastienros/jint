using System.Collections.Generic;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.String
{
    public class StringInstance : ObjectInstance, IPrimitiveInstance
    {
        internal PropertyDescriptor _length;

        public StringInstance(Engine engine)
            : base(engine, objectClass: "String")
        {
        }

        Types IPrimitiveInstance.Type => Types.String;

        JsValue IPrimitiveInstance.PrimitiveValue => PrimitiveValue;

        public JsString PrimitiveValue { get; set; }

        private static bool IsInt(double d)
        {
            if (d >= long.MinValue && d <= long.MaxValue)
            {
                var l = (long) d;
                return l >= int.MinValue && l <= int.MaxValue;
            }

            return false;
        }

        public override PropertyDescriptor GetOwnProperty(in Key propertyName)
        {
            if (propertyName == KnownKeys.Infinity)
            {
                return PropertyDescriptor.Undefined;
            }

            if (propertyName == KnownKeys.Length)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }

            var desc = base.GetOwnProperty(propertyName);
            if (desc != PropertyDescriptor.Undefined)
            {
                return desc;
            }

            if (!TypeConverter.CanBeIndex(propertyName))
            {
                return PropertyDescriptor.Undefined;
            }

            var integer = TypeConverter.ToInteger(propertyName);
            var str = PrimitiveValue;
            var dIndex = integer;
            if (!IsInt(dIndex))
                return PropertyDescriptor.Undefined;

            var index = (int) dIndex;
            var len = str.AsStringWithoutTypeCheck().Length;
            if (len <= index || index < 0)
            {
                return PropertyDescriptor.Undefined;
            }

            var resultStr = TypeConverter.ToString(str.AsStringWithoutTypeCheck()[index]);
            return new PropertyDescriptor(resultStr, PropertyFlag.OnlyEnumerable);
        }

        public override IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
        {
            if (_length != null)
            {
                yield return new KeyValuePair<string, PropertyDescriptor>(KnownKeys.Length, _length);
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        protected internal override void SetOwnProperty(in Key propertyName, PropertyDescriptor desc)
        {
            if (propertyName == KnownKeys.Length)
            {
                _length = desc;
            }
            else
            {
                base.SetOwnProperty(propertyName, desc);
            }
        }

        public override bool HasOwnProperty(in Key propertyName)
        {
            if (propertyName == KnownKeys.Length)
            {
                return _length != null;
            }

            return base.HasOwnProperty(propertyName);
        }

        public override void RemoveOwnProperty(in Key propertyName)
        {
            if (propertyName == KnownKeys.Length)
            {
                _length = null;
            }

            base.RemoveOwnProperty(propertyName);
        }
    }
}