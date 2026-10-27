using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Interop;

// Stateless and shared. Engine ownership and explicit ObjectConverters are handled
// by the caller before this closed scalar conversion boundary is reached.
internal sealed class NativeHostInterop : HostInterop
{
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
            Throw.NotSupportedException("Broad CLR interop is disabled in this publication.");
        }
    }

    internal override void AttachExtensionMethods(Engine engine) =>
        Throw.NotSupportedException("Reflected CLR extension methods are disabled; use native JsValue functions.");

    internal override ObjectInstance CreateObjectWrapper(Engine engine, object target, Type? type) =>
        throw new NotSupportedException("Reflection-based CLR wrappers are disabled; use a native JsValue adapter.");

    private static JsValue UnsupportedProjection()
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
