using System.Reflection;

namespace Jint.Runtime.Interop.Reflection
{
    internal sealed class PropertyAccessor : ReflectionAccessor
    {
        private readonly PropertyInfo _propertyInfo;

        public PropertyAccessor(
            string memberName,
            PropertyInfo propertyInfo,
            PropertyInfo? indexerToTry = null)
            : base(propertyInfo.PropertyType, memberName, indexerToTry)
        {
            _propertyInfo = propertyInfo;
        }

        public override bool Writable => _propertyInfo.CanWrite;

        protected override object? DoGetValue(object target)
        {
            return _propertyInfo.GetValue(target, index: null);
        }

        protected override void DoSetValue(object target, object? value)
        {
            _propertyInfo.SetValue(target, value, index: null);
        }
    }
}
