using System.Reflection;

namespace Jint.Runtime.Interop.Reflection;

internal class ReadonlyAccessor : ReflectionAccessor
{
    private readonly ReflectionAccessor _inner;

    public ReadonlyAccessor(ReflectionAccessor inner)
        : base(inner.MemberType, null)
    {
        _inner = inner;
    }

    public override bool Writable => false;

    public override object? GetValue(Engine engine, object target, string memberName)
    {
        return _inner.GetValue(engine, target, memberName);
    }

    protected override object? DoGetValue(object target, string memberName)
    {
        return null;
    }

    protected override void DoSetValue(object target, string memberName, object? value)
    {
        throw new NotSupportedException("Member is readonly.");
    }
}
