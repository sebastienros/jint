using System.Globalization;
using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class PropertyInfoDescriptor : PropertyDescriptor
    {
        private readonly Engine _engine;
        private readonly PropertyInfo _propertyInfo;
        private readonly object _item;

        public PropertyInfoDescriptor(Engine engine, PropertyInfo propertyInfo, object item) : base(PropertyFlag.CustomJsValue)
        {
            _engine = engine;
            _propertyInfo = propertyInfo;
            _item = item;

            Writable = propertyInfo.CanWrite && engine.Options._IsClrWriteAllowed;
        }

        protected internal override JsValue CustomValue
        {
            get
            {
                object v;
                try
                {
                    v = _propertyInfo.GetValue(_item, null);
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                    throw;
                }

                return JsValue.FromObject(_engine, v);
            }
            set
            {
                object obj;
                if (_propertyInfo.PropertyType == typeof(JsValue))
                {
                    obj = value;
                }
                else
                {
                    // attempt to convert the JsValue to the target type
                    obj = value.ToObject();
                    if (obj != null && obj.GetType() != _propertyInfo.PropertyType)
                    {
                        obj = _engine.ClrTypeConverter.Convert(obj, _propertyInfo.PropertyType, CultureInfo.InvariantCulture);
                    }
                }

                try
                {
                    _propertyInfo.SetValue(_item, obj, null);
                }
                catch (TargetInvocationException exception)
                {
                    ExceptionHelper.ThrowMeaningfulException(_engine, exception);
                }
            }
        }
    }
}