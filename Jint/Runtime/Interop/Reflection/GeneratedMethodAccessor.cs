using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// Method-side counterpart to <see cref="GeneratedReflectionAccessor"/>. The generator emits a
/// concrete subclass per <c>[JsAccessible]</c>-annotated method that overrides
/// <see cref="CreateFunction"/> to allocate a typed <see cref="Function"/> with a <c>Call</c> body
/// that casts <c>thisObject</c> and invokes the method directly, eliminating MethodInfo.Invoke
/// and argument boxing.
/// </summary>
public abstract class GeneratedMethodAccessor : ReflectionAccessor
{
    protected GeneratedMethodAccessor() : base(null)
    {
    }

    public override bool Readable => true;
    public override bool Writable => false;

    protected override object? DoGetValue(object target, string memberName) => null;

    protected override void DoSetValue(object target, string memberName, object? value)
    {
    }

    protected abstract Function CreateFunction(Engine engine, object target, string memberName);

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        return new PropertyDescriptor(CreateFunction(engine, target, memberName), PropertyFlag.Configurable | PropertyFlag.NonData);
    }
}
