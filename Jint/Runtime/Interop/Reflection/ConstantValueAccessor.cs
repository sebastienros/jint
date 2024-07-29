using Jint.Native;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class ConstantValueAccessor : ReflectionAccessor
{
    public static readonly ConstantValueAccessor NullAccessor = new(null);

    public ConstantValueAccessor(JsValue? value) : base(null!, null!)
    {
        ConstantValue = value;
    }

    public override bool Writable => false;

    protected override JsValue? ConstantValue { get; }

    protected override object? DoGetValue(object target, string memberName)
    {
        return ConstantValue;
    }

    protected override void DoSetValue(object target, string memberName, object? value)
    {
        throw new InvalidOperationException();
    }

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        return ConstantValue is null
            ? PropertyDescriptor.Undefined
            : new(ConstantValue, PropertyFlag.AllForbidden);
    }
}
