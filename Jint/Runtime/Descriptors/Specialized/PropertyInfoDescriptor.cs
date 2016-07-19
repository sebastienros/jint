using System;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class PropertyInfoDescriptor : PropertyDescriptor, ISharedDescriptor
    {
        private readonly Engine _engine;
        private readonly PropertyProxy _propertyProxy;
        private object _item;

        public PropertyInfoDescriptor(Engine engine, PropertyProxy propertyProxy, object item = null)
        {
            _engine = engine;
            _propertyProxy = propertyProxy;
            _item = item;

            Writable = propertyProxy.CanWrite;
        }

        public override JsValue Value
        {
            get
            {
                return _propertyProxy.GetValue(_engine, _item);
            }

            set
            {
                var currentValue = value;
                object obj;
                if (_propertyProxy.PropertyType == typeof (JsValue))
                {
                    obj = currentValue;
                }
                else
                {
                    // attempt to convert the JsValue to the target type
                    obj = currentValue.ToObject();
                    if (obj != null && obj.GetType() != _propertyProxy.PropertyType)
                    {
                        obj = _engine.ClrTypeConverter.Convert(obj, _propertyProxy.PropertyType, CultureInfo.InvariantCulture);
                    }
                }
                
                _propertyProxy.SetValue(_engine, _item, obj);
            }
        }

        public void SetTarget(object target)
        {
            _item = target;
        }
    }
}
