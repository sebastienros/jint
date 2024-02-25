using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class NestedTypeAccessor : ReflectionAccessor
{
    private readonly TypeReference _typeReference;

    public NestedTypeAccessor(TypeReference typeReference) : base(typeof(Type))
    {
        _typeReference = typeReference;
    }

    public override bool Writable => false;

    protected override object? DoGetValue(object target, string memberName) => null;

    protected override void DoSetValue(object target, string memberName, object? value) { }

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        return new(_typeReference, PropertyFlag.AllForbidden);
    }
}
