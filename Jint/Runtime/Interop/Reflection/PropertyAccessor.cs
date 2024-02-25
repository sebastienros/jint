using System.Reflection;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class PropertyAccessor : ReflectionAccessor
{
    private readonly PropertyInfo _propertyInfo;

    public PropertyAccessor(
        PropertyInfo propertyInfo,
        PropertyInfo? indexerToTry = null)
        : base(propertyInfo.PropertyType, indexerToTry)
    {
        _propertyInfo = propertyInfo;
    }

    public override bool Readable => _propertyInfo.CanRead;

    public override bool Writable => _propertyInfo.CanWrite;

    protected override object? DoGetValue(object target, string memberName)
    {
        return _propertyInfo.GetValue(target, index: null);
    }

    protected override void DoSetValue(object target, string memberName, object? value)
    {
        _propertyInfo.SetValue(target, value, index: null);
    }
}
