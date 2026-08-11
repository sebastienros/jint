using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection;

internal sealed class MethodAccessor : ReflectionAccessor
{
    private readonly Type _targetType;
    private readonly MethodDescriptor[] _methods;

    public MethodAccessor(Type targetType, MethodDescriptor[] methods) : base(null!)
    {
        _targetType = targetType;
        _methods = methods;

        var allExtensionMethods = methods.Length > 0;
        foreach (var method in methods)
        {
            if (!method.IsExtensionMethod)
            {
                allExtensionMethods = false;
                break;
            }
        }
        AllExtensionMethods = allExtensionMethods;
    }

    /// <summary>
    /// Whether every candidate is a registered extension method, i.e. the member is not a real
    /// member of the target type. Derived from the descriptors alone, so instances served from
    /// the shared accessor cache answer identically for every engine.
    /// </summary>
    public bool AllExtensionMethods { get; }

    public override bool Writable => false;

    protected override object? DoGetValue(object target, string memberName) => null;

    protected override void DoSetValue(object target, string memberName, object? value)
    {
    }

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        return new(new MethodInfoFunction(engine, _targetType, target, memberName, _methods), PropertyFlag.Configurable | PropertyFlag.NonData);
    }
}
