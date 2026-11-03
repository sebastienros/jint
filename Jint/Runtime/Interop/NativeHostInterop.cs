using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Interop;

// Stateless and shared. Engine ownership and explicit ObjectConverters are handled
// by the caller before this closed scalar conversion boundary is reached.
internal sealed class NativeHostInterop : HostInterop
{
    // Every rejection names the switch: the host that sees one is not necessarily the one that set it.
    private const string Disabled = "Jint.Runtime.Interop.ReflectionEnabled is false in this publication: ";

    internal const string BroadInteropMessage =
        Disabled + "broad CLR interop (Interop.Enabled, AllowClr, operator overloading) is unavailable.";

    internal const string ExtensionMethodsMessage =
        Disabled + "reflected CLR extension methods are unavailable; register native JsValue functions instead.";

    internal const string WrapperMessage =
        Disabled + "reflection-based CLR wrappers are unavailable; use a native JsValue adapter.";

    internal const string ProjectionMessage =
        Disabled + "reflection-based CLR projection is unavailable; register an ObjectConverter or pass a JsValue.";

    internal const string ConversionMessage =
        Disabled + "reflection-based CLR conversion is unavailable; use a native JsValue adapter.";

    internal override ClrTypeConverter CreateTypeConverter(Engine engine) => new ReflectionFreeTypeConverter();

    internal override JsValue FromObject(Engine engine, object value, Type? type) => value switch
    {
        bool v => v ? JsBoolean.True : JsBoolean.False,
        byte v => JsNumber.Create(v),
        sbyte v => JsNumber.Create(v),
        short v => JsNumber.Create(v),
        ushort v => JsNumber.Create(v),
        int v => JsNumber.Create(v),
        uint v => JsNumber.Create(v),
        long v => (JsValue) v,
        ulong v => (JsValue) v,
        float v => (JsValue) v,
        double v => (JsValue) v,
        decimal v => (JsValue) (double) v,
        char v => JsString.Create(v),
        string v => JsString.Create(v),
        _ => UnsupportedProjection()
    };

    internal override void ApplyOptions(Options options, Engine engine)
    {
        if (options.Interop.Enabled || options.Interop.AllowOperatorOverloading)
        {
            Throw.NotSupportedException(BroadInteropMessage);
        }
    }

    internal override void AttachExtensionMethods(Engine engine) =>
        Throw.NotSupportedException(ExtensionMethodsMessage);

    internal override ObjectInstance CreateObjectWrapper(Engine engine, object target, Type? type)
    {
        Throw.NotSupportedException(WrapperMessage);
        return null!;
    }

    private static JsValue UnsupportedProjection()
    {
        Throw.NotSupportedException(ProjectionMessage);
        return JsValue.Undefined;
    }
}

internal sealed class ReflectionFreeTypeConverter : ClrTypeConverter
{
    public override object? Convert(object? value,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicFields)] Type type, IFormatProvider formatProvider)
    {
        Throw.NotSupportedException(NativeHostInterop.ConversionMessage);
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
