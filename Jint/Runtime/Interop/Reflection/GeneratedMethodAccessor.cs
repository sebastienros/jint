using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// The accessor a <see cref="JsAccessibleAttribute"/>-annotated method resolves to. Where
/// <see cref="MethodAccessor"/> hands the call to a <see cref="MethodInfoFunction"/> that scores overloads,
/// converts each argument and finishes at <see cref="System.Reflection.MethodBase.Invoke(object, object[])"/>,
/// this one hands it to a <see cref="ClrFunction"/> over a generated <see langword="static"/> invoker: the
/// call site is a delegate invocation and a direct C# call.
/// </summary>
/// <remarks>
/// Everything around that call is kept identical to the reflected path on purpose — the receiver is unwrapped
/// by the same rule, an incompatible receiver raises the same <c>TypeError</c>, and the host boundary is
/// checked for amortized constraints after the call returns. Only method shapes whose reflected argument
/// binding is a pass-through are registered (see the generator), so there is no conversion left to reproduce.
/// </remarks>
internal sealed class GeneratedMethodAccessor : ReflectionAccessor
{
    private readonly Type _targetType;
    private readonly string _name;
    private readonly int _length;
    private readonly Func<Engine, object, JsValue[], JsValue> _invoke;

    internal GeneratedMethodAccessor(Type targetType, string name, int length, Func<Engine, object, JsValue[], JsValue> invoke)
        : base(null)
    {
        _targetType = targetType;
        _name = name;
        _length = length;
        _invoke = invoke;
    }

    public override bool Writable => false;

    protected override object? DoGetValue(object target, string memberName) => null;

    protected override void DoSetValue(object target, string memberName, object? value)
    {
    }

    public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        return new PropertyDescriptor(CreateFunction(engine, target), PropertyFlag.Configurable | PropertyFlag.NonData);
    }

    private ClrFunction CreateFunction(Engine engine, object target)
    {
        var invoke = _invoke;
        var targetType = _targetType;
        var name = _name;
        var length = _length;

        return new ClrFunction(
            engine,
            engine.Realm,
            name,
            (thisObject, arguments) =>
            {
                // Reflection binds a single candidate only for an exact argument count (no optional
                // parameters survive the generator's filter), and reports anything else as an unresolved
                // member rather than as a call. Same answer, same error object.
                if (arguments.Length != length)
                {
                    ThrowUnresolved(engine, targetType, name, arguments);
                }

                var receiver = MethodInfoFunction.ResolveClrReceiver(engine, thisObject, target, name);

                // Classified here rather than left to the invoker's cast, for the reason MethodInfoFunction
                // gives: an InvalidCastException out of the generated body would be indistinguishable from
                // one the host method itself threw.
                if (!targetType.IsInstanceOfType(receiver))
                {
                    engine.CheckAmortizedConstraintsAtHostBoundary();
                    Throw.TypeError(engine.Realm, $"Method '{name}' called on incompatible receiver");
                }

                var result = invoke(engine, receiver!, arguments);
                engine.CheckAmortizedConstraintsAtHostBoundary();
                return result;
            },
            _length);
    }

    /// <summary>
    /// The answer <see cref="MethodInfoFunction"/> gives when no candidate binds. The candidate list it
    /// would attach to a detailed message is the one thing not reproduced: a generated method has no
    /// <see cref="MethodDescriptor"/> to describe, which is the whole point of it.
    /// </summary>
    [DoesNotReturn]
    private static void ThrowUnresolved(Engine engine, Type targetType, string name, JsCallArguments arguments)
    {
        engine.CheckAmortizedConstraintsAtHostBoundary();

        var message = engine.Options.Interop.ExposeDetailedResolutionErrors
            ? InteropErrorHelper.CreateNoMatchingMethodMessage(targetType, name, arguments, [])
            : "No public methods with the specified arguments were found.";

        Throw.InteropResolutionError(engine.Realm, message, targetType, name, arguments, candidates: null);
    }
}
