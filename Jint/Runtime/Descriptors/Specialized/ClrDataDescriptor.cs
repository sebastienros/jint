using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class ClrDataDescriptor : PropertyDescriptor
    {
        private readonly Engine _engine;
        private readonly PropertyInfo _propertyInfo;
        private readonly object _item;

        public ClrDataDescriptor(Engine engine, PropertyInfo propertyInfo, object item)
        {
            _engine = engine;
            _propertyInfo = propertyInfo;
            _item = item;

            Writable = propertyInfo.CanWrite;
        }

        public override JsValue? Value
        {
            get
            {
                return JsValue.FromObject(_engine, _propertyInfo.GetValue(_item, null));
            }

            set
            {
                _propertyInfo.SetValue(_item, value.GetValueOrDefault().ToObject(), null);
            }
        }
    }
}
