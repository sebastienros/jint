using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class ClrDataDescriptor : PropertyDescriptor
    {
        private readonly PropertyInfo _propertyInfo;
        private readonly object _item;
        private JsValue? _value;

        public ClrDataDescriptor(PropertyInfo propertyInfo, object item)
        {
            _propertyInfo = propertyInfo;
            _item = item;

            _value = JsValue.FromObject(_propertyInfo.GetValue(_item, null));
            Writable = propertyInfo.CanWrite;
        }

        public override JsValue? Value
        {
            get
            {
                return _value;
            }

            set
            {
                _value = value;
                _propertyInfo.SetValue(_item, _value.GetValueOrDefault().ToObject(), null);
            }
        }
    }
}
