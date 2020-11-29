using System.Reflection;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class FieldInfoDescriptor : ReflectionPropertyDescriptor
    {
        private readonly FieldInfo _fieldInfo;

        public FieldInfoDescriptor(Engine engine, FieldInfo fieldInfo, object target, PropertyInfo indexerToTry = null, string indexerKey = null)
            : base(engine, fieldInfo.FieldType, target, !fieldInfo.Attributes.HasFlag(FieldAttributes.InitOnly), indexerToTry, indexerKey)
        {
            _fieldInfo = fieldInfo;
        }

        protected override object DoGetValue(object target)
        {
            return _fieldInfo.GetValue(target);
        }

        protected override void DoSetValue(object target, object value)
        {
            _fieldInfo.SetValue(target, value);
        }
    }
}