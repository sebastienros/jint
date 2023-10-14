using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class NestedTypeAccessor : ReflectionAccessor
{
    private readonly TypeReference _typeReference;

    public NestedTypeAccessor(TypeReference typeReference, string name) : base(typeof(Type), name)
    {
        _typeReference = typeReference;
    }

    public override bool Writable => false;

    protected override object? DoGetValue(object target)
    {
        return null;
    }

    protected override void DoSetValue(object target, object? value)
    {
    }

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, bool enumerable = true)
    {
        return new(_typeReference, PropertyFlag.AllForbidden);
    }
}
