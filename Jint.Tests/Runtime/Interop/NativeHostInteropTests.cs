#nullable enable

using System.Globalization;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter;

namespace Jint.Tests.Runtime.Interop;

/// <summary>
/// <see cref="NativeHostInterop"/> exercised directly, beside the <see cref="ReflectionHostInterop"/> it replaces.
/// </summary>
/// <remarks>
/// The feature switch is read once per process and this test host keeps the default, so every other test runs the
/// reflection implementation. These cases are what keep the native one from being compiled but never executed:
/// the trimmed example only runs where its own publication is built.
/// </remarks>
[Parallelizable(ParallelScope.All)]
public class NativeHostInteropTests
{
    private const string Switch = "Jint.Runtime.Interop.ReflectionEnabled";

    private static readonly NativeHostInterop Native = new();
    private static readonly ReflectionHostInterop Reflection = new();

    private static IEnumerable<object> Scalars()
    {
        yield return true;
        yield return false;
        yield return (byte) 200;
        yield return (sbyte) -100;
        yield return (short) -30_000;
        yield return (ushort) 60_000;
        yield return 0;
        yield return -1;
        yield return int.MaxValue;
        yield return int.MinValue;
        yield return uint.MaxValue;
        yield return long.MaxValue;
        yield return long.MinValue;
        yield return ulong.MaxValue;
        yield return 1.5f;
        yield return float.NaN;
        yield return 0.1d;
        yield return -0d;
        yield return double.NaN;
        yield return double.PositiveInfinity;
        yield return 1.25m;
        yield return decimal.MaxValue;
        yield return 'x';
        yield return "";
        yield return "text";
    }

    [Test]
    public void TheDefaultPublicationComposesTheReflectionImplementation()
    {
        HostInterop.Instance.Should().BeOfType<ReflectionHostInterop>(
            "the switch defaults to true, so a host that never set it keeps ordinary CLR interop");
    }

    [TestCaseSource(nameof(Scalars))]
    public void AScalarConvertsExactlyAsTheReflectionPathConvertsIt(object value)
    {
        using var engine = new Engine();

        var native = Native.FromObject(engine, value, null);
        var reflected = Reflection.FromObject(engine, value, null);

        native.Type.Should().Be(reflected.Type);
        JsValue.SameValue(native, reflected).Should().BeTrue(
            $"{value.GetType().Name} {value} became {native} natively and {reflected} through reflection");
    }

    [Test]
    public void AnUnknownClrObjectIsRejectedRatherThanProjected()
    {
        using var engine = new Engine();

        var act = () => Native.FromObject(engine, new object(), null);

        act.Should().Throw<NotSupportedException>().WithMessage($"*{Switch}*");
    }

    [Test]
    public void AWrapperIsRejected()
    {
        using var engine = new Engine();

        var act = () => Native.CreateObjectWrapper(engine, new object(), null);

        act.Should().Throw<NotSupportedException>().WithMessage($"*{Switch}*");
    }

    [Test]
    public void ReflectedExtensionMethodsAreRejected()
    {
        using var engine = new Engine();

        var act = () => Native.AttachExtensionMethods(engine);

        act.Should().Throw<NotSupportedException>().WithMessage($"*{Switch}*");
    }

    [Test]
    public void TheTypeConverterDeclinesCoercionAndThenRefusesIt()
    {
        using var engine = new Engine();
        var converter = Native.CreateTypeConverter(engine);

        converter.TryConvert(1d, typeof(int), CultureInfo.InvariantCulture, out var converted).Should().BeFalse();
        converted.Should().BeNull();
        converter.TryConvert(null, typeof(string), CultureInfo.InvariantCulture, out converted).Should().BeFalse();
        converted.Should().BeNull();

        var act = () => converter.Convert(1d, typeof(int), CultureInfo.InvariantCulture);
        act.Should().Throw<NotSupportedException>().WithMessage($"*{Switch}*");
    }

    [TestCase(1.5d, typeof(double))]
    [TestCase(true, typeof(bool))]
    [TestCase("text", typeof(string))]
    [TestCase(1.5d, typeof(object))]
    public void AValueThatAlreadyIsTheTargetTypePassesThrough(object value, Type type)
    {
        using var engine = new Engine();
        var converter = Native.CreateTypeConverter(engine);

        converter.TryConvert(value, type, CultureInfo.InvariantCulture, out var converted).Should().BeTrue();
        converted.Should().BeSameAs(value);
        converter.Convert(value, type, CultureInfo.InvariantCulture).Should().BeSameAs(value);
    }

    [Test]
    public void AnInstalledConverterIsNotWrapped()
    {
        using var engine = new Engine();
        var converter = Native.CreateTypeConverter(engine);

        Native.WrapTypeConverter(engine, converter, filter: null).Should().BeSameAs(converter);
    }

    [Test]
    public void NoOperatorIsResolved()
    {
        using var engine = new Engine();
        var context = new EvaluationContext(engine);

        Native.TryBinaryOperator(context, JsNumber.Create(1), JsNumber.Create(2), "op_Addition", out var binary)
            .Should().BeFalse();
        binary.Should().BeNull();
        Native.TryUnaryOperator(context, JsNumber.Create(1), "op_UnaryNegation", out var unary).Should().BeFalse();
        unary.Should().BeNull();
    }

    [Test]
    public void OptionsThatNeedNoReflectionAreAccepted()
    {
        using var engine = new Engine();

        var act = () => Native.ApplyOptions(new Options(), engine);

        act.Should().NotThrow();
    }

    [Test]
    public void BroadInteropIsRejected()
    {
        using var engine = new Engine();
        var options = new Options();
        options.Interop.Enabled = true;

        var act = () => Native.ApplyOptions(options, engine);

        act.Should().Throw<NotSupportedException>().WithMessage($"*{Switch}*");
    }

    [Test]
    public void OperatorOverloadingIsRejected()
    {
        using var engine = new Engine();
        var options = new Options();
        options.Interop.AllowOperatorOverloading = true;

        var act = () => Native.ApplyOptions(options, engine);

        act.Should().Throw<NotSupportedException>().WithMessage($"*{Switch}*");
    }
}
