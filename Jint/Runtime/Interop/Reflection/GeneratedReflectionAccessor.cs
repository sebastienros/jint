using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// Base class for accessors emitted by the [JsAccessible] source generator. Skips reflection on the
/// hot path: <see cref="GetJsValue"/> and <see cref="SetFromJsValue"/> are emitted with a typed cast
/// and direct member touch, avoiding the PropertyInfo.GetValue/SetValue boxing round-trip entirely.
/// </summary>
public abstract class GeneratedReflectionAccessor : ReflectionAccessor
{
    protected GeneratedReflectionAccessor(Type memberType) : base(memberType)
    {
    }

    public abstract JsValue GetJsValue(Engine engine, object target);

    public abstract void SetFromJsValue(Engine engine, object target, JsValue value);

    // ObjectWrapper.Set bypasses the descriptor and calls accessor.SetValue directly. Override
    // to route through SetFromJsValue and skip the base class's TypeCoercion + boxing dance.
    public override void SetValue(Engine engine, object target, string memberName, JsValue value)
        => SetFromJsValue(engine, target, value);

    // GetValue is reached only when a path bypasses the descriptor; route through GetJsValue and
    // box back to object on the cold edge for compatibility with whatever called us.
    public override object? GetValue(Engine engine, object target, string memberName)
        => GetJsValue(engine, target).ToObject();

    protected override object? DoGetValue(object target, string memberName)
        => throw new InvalidOperationException("GeneratedReflectionAccessor does not use the boxed object path; call GetJsValue directly.");

    protected override void DoSetValue(object target, string memberName, object? value)
        => throw new InvalidOperationException("GeneratedReflectionAccessor does not use the boxed object path; call SetFromJsValue directly.");

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        return new GeneratedReflectionDescriptor(engine, this, target, enumerable);
    }
}
