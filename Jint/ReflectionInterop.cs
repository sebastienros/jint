using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint;

internal static class ReflectionInterop
{
    [FeatureSwitchDefinition("Jint.Runtime.Interop.ReflectionEnabled")]
    internal static bool Enabled { get; } = !AppContext.TryGetSwitch(
        "Jint.Runtime.Interop.ReflectionEnabled", out var enabled) || enabled;

    internal static JsValue UnsupportedProjection()
    {
        Throw.NotSupportedException("Reflection-based CLR projection is disabled; register an ObjectConverter or pass JsValue.");
        return JsValue.Undefined;
    }
}

internal sealed class ReflectionFreeTypeConverter : ClrTypeConverter
{
    public override object? Convert(object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicFields)] Type type, IFormatProvider formatProvider)
    {
        Throw.NotSupportedException("Reflection-based CLR conversion is disabled; use a native JsValue adapter.");
        return null;
    }

    public override bool TryConvert(object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicFields)] Type type, IFormatProvider formatProvider,
        [NotNullWhen(true)] out object? converted)
    {
        converted = null;
        return false;
    }
}
