using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Interpreter;

namespace Jint.Runtime.Interop;

// The only composition decision. A trimmed native host never instantiates the
// reflection implementation, so its virtual methods and their callees can die.
internal abstract class HostInterop
{
    [FeatureSwitchDefinition("Jint.Runtime.Interop.ReflectionEnabled")]
    private static bool ReflectionEnabled { get; } = !AppContext.TryGetSwitch(
        "Jint.Runtime.Interop.ReflectionEnabled", out var enabled) || enabled;

    internal static HostInterop Instance { get; } = ReflectionEnabled
        ? new ReflectionHostInterop()
        : new NativeHostInterop();

    internal abstract ClrTypeConverter CreateTypeConverter(Engine engine);
    internal virtual ClrTypeConverter WrapTypeConverter(Engine engine, ClrTypeConverter converter,
        TypeConverterTargetFilter? filter) => converter;
    internal abstract JsValue FromObject(Engine engine, object value, Type? type);
    internal abstract void ApplyOptions(Options options, Engine engine);
    internal abstract void AttachExtensionMethods(Engine engine);
    internal abstract ObjectInstance CreateObjectWrapper(Engine engine, object target, Type? type);

    internal virtual bool TryBinaryOperator(EvaluationContext context, JsValue left, JsValue right,
        string? name, [NotNullWhen(true)] out object? result)
    {
        result = null;
        return false;
    }

    internal virtual bool TryUnaryOperator(EvaluationContext context, JsValue value, string name,
        [NotNullWhen(true)] out JsValue? result)
    {
        result = null;
        return false;
    }
}
