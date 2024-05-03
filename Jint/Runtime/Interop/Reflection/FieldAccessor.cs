using System.Reflection;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class FieldAccessor : ReflectionAccessor
{
    private readonly FieldInfo _fieldInfo;

    public FieldAccessor(FieldInfo fieldInfo, PropertyInfo? indexer = null)
        : base(fieldInfo.FieldType, indexer)
    {
        _fieldInfo = fieldInfo;
    }

    public override bool Writable => (_fieldInfo.Attributes & FieldAttributes.InitOnly) == (FieldAttributes) 0;

    protected override object? DoGetValue(object target, string memberName)
    {
        return _fieldInfo.GetValue(target);
    }

    protected override void DoSetValue(object target, string memberName, object? value)
    {
        _fieldInfo.SetValue(target, value);
    }
}
