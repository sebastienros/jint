using System.Reflection;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class FieldAccessor : CompilableMemberAccessor
{
    private readonly FieldInfo _fieldInfo;

    public FieldAccessor(FieldInfo fieldInfo, PropertyInfo? indexer = null)
        : base(fieldInfo, fieldInfo.FieldType, indexer)
    {
        _fieldInfo = fieldInfo;
    }

    public override bool Writable => (_fieldInfo.Attributes & FieldAttributes.InitOnly) == (FieldAttributes) 0;

    protected override object? ReflectionGetValue(object target)
    {
        return _fieldInfo.GetValue(target);
    }

    protected override void ReflectionSetValue(object target, object? value)
    {
        _fieldInfo.SetValue(target, value);
    }
}
