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

    // InitOnly is a readonly field, Literal is a compile-time constant. Reflection refuses to write
    // either one: omitting Literal here let a constant look writable and surfaced the resulting
    // FieldAccessException to the host instead of the ordinary non-writable [[Set]] answer.
    public override bool Writable => (_fieldInfo.Attributes & (FieldAttributes.InitOnly | FieldAttributes.Literal)) == (FieldAttributes) 0;

    protected override object? ReflectionGetValue(object target)
    {
        return _fieldInfo.GetValue(target);
    }

    protected override void ReflectionSetValue(object target, object? value)
    {
        _fieldInfo.SetValue(target, value);
    }
}
