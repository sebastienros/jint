using System.Reflection;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class PropertyAccessor : CompilableMemberAccessor
{
    private readonly PropertyInfo _propertyInfo;

    public PropertyAccessor(
        PropertyInfo propertyInfo,
        PropertyInfo? indexerToTry = null)
        : base(propertyInfo, propertyInfo.PropertyType, indexerToTry)
    {
        _propertyInfo = propertyInfo;
    }

    public override bool Readable => _propertyInfo.CanRead;

    public override bool Writable => _propertyInfo.CanWrite;

    protected override object? ReflectionGetValue(object target)
    {
        return _propertyInfo.GetValue(target, index: null);
    }

    protected override void ReflectionSetValue(object target, object? value)
    {
        _propertyInfo.SetValue(target, value, index: null);
    }
}
