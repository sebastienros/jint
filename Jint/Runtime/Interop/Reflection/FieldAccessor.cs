using System.Reflection;

namespace Jint.Runtime.Interop.Reflection
{
    internal sealed class FieldAccessor : ReflectionAccessor
    {
        private readonly FieldInfo _fieldInfo;

        public FieldAccessor(FieldInfo fieldInfo, string? memberName = null, PropertyInfo? indexer = null)
            : base(fieldInfo.FieldType, memberName, indexer)
        {
            _fieldInfo = fieldInfo;
        }

        public override bool Writable => !_fieldInfo.Attributes.HasFlag(FieldAttributes.InitOnly);

        protected override object? DoGetValue(object target)
        {
            return _fieldInfo.GetValue(target);
        }

        protected override void DoSetValue(object target, object? value)
        {
            _fieldInfo.SetValue(target, value);
        }
    }
}
