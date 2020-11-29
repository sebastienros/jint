using System.Reflection;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class PropertyInfoDescriptor : ReflectionPropertyDescriptor
    {
        private readonly PropertyInfo _propertyInfo;

        public PropertyInfoDescriptor(Engine engine, PropertyInfo propertyInfo, object target, PropertyInfo indexerToTry = null, string indexerKey = null) 
            : base(engine, propertyInfo.PropertyType, target, propertyInfo.CanWrite, indexerToTry, indexerKey)
        {
            _propertyInfo = propertyInfo;
        }

        protected override object DoGetValue(object target)
        {
            return _propertyInfo.GetValue(target, index: null);
        }

        protected override void DoSetValue(object target, object value)
        {
            _propertyInfo.SetValue(target, value, index: null);
        }
    }
}