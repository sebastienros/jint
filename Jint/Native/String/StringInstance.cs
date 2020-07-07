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
            : base(engine, ObjectClass.String)
        {
        }

        Types IPrimitiveInstance.Type => Types.String;

        JsValue IPrimitiveInstance.PrimitiveValue => PrimitiveValue;

        public JsString PrimitiveValue { get; set; }

        private static bool IsInt32(double d, out int intValue)
        {
            if (d >= int.MinValue && d <= int.MaxValue)
            {
                intValue = (int) d;
                return intValue == d;
            }

            intValue = 0;
            return false;
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Infinity)
            {
                return PropertyDescriptor.Undefined;
            }

            if (property == CommonProperties.Length)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }

            var desc = base.GetOwnProperty(property);
            if (desc != PropertyDescriptor.Undefined)
            {
                return desc;
            }

            if ((property._type & (InternalTypes.Number | InternalTypes.Integer | InternalTypes.String)) == 0)
            {
                return PropertyDescriptor.Undefined;
            }

            var str = PrimitiveValue.ToString();
            var number = TypeConverter.ToNumber(property);
            if (!IsInt32(number, out var index) || index < 0 || index >= str.Length)
            {
                return PropertyDescriptor.Undefined;
            }

            return new PropertyDescriptor(str[index], PropertyFlag.OnlyEnumerable);
        }

        public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
        {
            if (_length != null)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, _length);
            }

            foreach (var entry in base.GetOwnProperties())
            {
                yield return entry;
            }
        }

        public override List<JsValue> GetOwnPropertyKeys(Types types)
        {
            var keys = new List<JsValue>(PrimitiveValue.Length + 1);
            for (uint i = 0; i < PrimitiveValue.Length; ++i)
            {
                keys.Add(JsString.Create(i));
            }

            keys.AddRange(base.GetOwnPropertyKeys(types));
            keys.Sort((v1, v2) => TypeConverter.ToNumber(v1).CompareTo(TypeConverter.ToNumber(v2)));

            keys.Add(JsString.LengthString);

            return keys;
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (property == CommonProperties.Length)
            {
                _length = desc;
            }
            else
            {
                base.SetOwnProperty(property, desc);
            }
        }

        public override bool HasOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Length)
            {
                return _length != null;
            }

            return base.HasOwnProperty(property);
        }

        public override void RemoveOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Length)
            {
                _length = null;
            }

            base.RemoveOwnProperty(property);
        }
    }
}