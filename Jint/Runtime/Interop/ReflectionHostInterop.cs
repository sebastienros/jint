using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interop;

// Retains ordinary Jint behavior. None of these roots is referenced by the native
// implementation; the factory is the sole place that can instantiate this type.
internal sealed class ReflectionHostInterop : HostInterop
{
    internal override ClrTypeConverter CreateTypeConverter(Engine engine) => new DefaultTypeConverter(engine);

    internal override ClrTypeConverter WrapTypeConverter(Engine engine, ClrTypeConverter converter,
        TypeConverterTargetFilter? filter) => HostContractVerification.Enabled && filter is { IsRestricted: true }
            ? new VerifyingClrTypeConverter(converter, filter, new DefaultTypeConverter(engine))
            : converter;

    internal override JsValue FromObject(Engine engine, object value, Type? type) =>
        JsValue.FromObjectWithReflection(engine, value, type);

    internal override void ApplyOptions(Options options, Engine engine) => options.ApplyReflectionInterop(engine);
    internal override void AttachExtensionMethods(Engine engine) => Options.AttachReflectedExtensionMethodsToPrototypes(engine);

    internal override ObjectInstance CreateObjectWrapper(Engine engine, object target, Type? type) =>
        ObjectWrapper.CreateWithReflection(engine, target, type);

    internal override bool TryBinaryOperator(EvaluationContext context, JsValue left, JsValue right,
        string? name, [NotNullWhen(true)] out object? result) =>
        JintBinaryExpression.TryOperatorOverloadingWithReflection(context, left, right, name, out result);

    internal override bool TryUnaryOperator(EvaluationContext context, JsValue value, string name,
        [NotNullWhen(true)] out JsValue? result) =>
        JintUnaryExpression.TryOperatorOverloadingWithReflection(context, value, name, out result);
}
