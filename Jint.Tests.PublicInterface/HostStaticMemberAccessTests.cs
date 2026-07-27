#nullable enable

using Jint.Native;
using Jint.Runtime.Interop;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers reading and writing the <b>static</b> properties and fields a host exposes through a
/// <see cref="TypeReference"/>. These share the compiled member lanes with instance members — a static member
/// simply never reads the target parameter — so the assertions below are written against the CLR state
/// itself, which is the only oracle that cannot move with the lane.
/// <para>
/// Two shapes deliberately keep the reflection path and must stay indistinguishable from the compiled ones: a
/// <c>const</c>, which is a literal with no storage a compiled read could load, and any member type outside
/// the small set the JsValue lane covers — an enum in particular, whose rendering has to keep following
/// <see cref="EnumConversionMode"/> for a static field exactly as it does for an instance one.
/// </para>
/// </summary>
public class HostStaticMemberAccessTests
{
    private static Engine CreateEngine(Action<Options>? configure = null)
    {
        var engine = configure is null ? new Engine() : new Engine(configure);
        engine.SetValue("Host", TypeReference.CreateTypeReference(engine, typeof(StaticHost)));
        return engine;
    }

    #region 1. reads

    [Fact]
    public void ReadsStaticPropertiesOfEveryLaneType()
    {
        StaticHost.Reset();
        var engine = CreateEngine();

        engine.Evaluate("Host.Number").AsNumber().Should().Be(1);
        engine.Evaluate("Host.Big").AsNumber().Should().Be(2);
        engine.Evaluate("Host.Fraction").AsNumber().Should().Be(0.5);
        engine.Evaluate("Host.Flag").AsBoolean().Should().BeTrue();
        engine.Evaluate("Host.Text").AsString().Should().Be("text");
        engine.Evaluate("Host.Bridged").AsString().Should().Be("bridged");
    }

    [Fact]
    public void ReadsStaticFieldsOfEveryLaneType()
    {
        StaticHost.Reset();
        var engine = CreateEngine();

        engine.Evaluate("Host.NumberField").AsNumber().Should().Be(1);
        engine.Evaluate("Host.TextField").AsString().Should().Be("field");
        engine.Evaluate("Host.FlagField").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReadsAStaticNullString()
    {
        StaticHost.Reset();
        var engine = CreateEngine();

        engine.Evaluate("Host.NullText === null").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ReadsAConstantAndAReadOnlyField()
    {
        // a const is a literal: it has no storage for a compiled read to load, so it keeps the reflection
        // path and must produce the same value a readonly field does
        var engine = CreateEngine();

        engine.Evaluate("Host.Constant").AsString().Should().Be("constant");
        engine.Evaluate("Host.ReadOnlyField").AsString().Should().Be("readonly");
    }

    [Fact]
    public void ReadsStaticMembersOfAValueType()
    {
        StaticValueTypeHost.Reset();
        var engine = new Engine();
        engine.SetValue("Host", TypeReference.CreateTypeReference(engine, typeof(StaticValueTypeHost)));

        engine.Evaluate("Host.Number").AsNumber().Should().Be(3);
        engine.Evaluate("Host.Text").AsString().Should().Be("struct");
    }

    #endregion

    #region 2. writes

    [Fact]
    public void WritesStaticProperties()
    {
        StaticHost.Reset();
        var engine = CreateEngine();

        engine.Execute("Host.Number = 42; Host.Text = 'written'; Host.Flag = false; Host.Fraction = 1.5;");

        StaticHost.Number.Should().Be(42);
        StaticHost.Text.Should().Be("written");
        StaticHost.Flag.Should().BeFalse();
        StaticHost.Fraction.Should().Be(1.5);

        engine.Evaluate("Host.Number").AsNumber().Should().Be(42);
    }

    [Fact]
    public void WritesStaticFields()
    {
        StaticHost.Reset();
        var engine = CreateEngine();

        engine.Execute("Host.NumberField = 7; Host.TextField = 'written'; Host.FlagField = false;");

        StaticHost.NumberField.Should().Be(7);
        StaticHost.TextField.Should().Be("written");
        StaticHost.FlagField.Should().BeFalse();
    }

    [Fact]
    public void WriteOfANonExactValueStillConverts()
    {
        // a fractional number bound to an int member declines the exact-type lane and takes the conversion
        // path, which is the pre-existing behaviour for both instance and static members
        StaticHost.Reset();
        var engine = CreateEngine();

        engine.Execute("Host.Number = 3.5;");

        // banker's rounding through the type converter, which is what the declined lane has always done
        StaticHost.Number.Should().Be(4);
    }

    #endregion

    #region 3. shapes that must keep their existing behaviour

    [Fact]
    public void AStaticEnumFieldStillFollowsTheEnumConversionMode()
    {
        StaticHost.Reset();

        var asNumber = CreateEngine(options => options.Interop.EnumConversion = EnumConversionMode.Number);
        asNumber.Evaluate("Host.EnumField").AsNumber().Should().Be(1);
        asNumber.Evaluate("Host.EnumProperty").AsNumber().Should().Be(1);

        var asString = CreateEngine(options => options.Interop.EnumConversion = EnumConversionMode.String);
        asString.Evaluate("Host.EnumField").AsString().Should().Be("One");
        asString.Evaluate("Host.EnumProperty").AsString().Should().Be("One");
    }

    [Fact]
    public void AStaticMemberOfANonLaneTypeStillReadsThrough()
    {
        StaticHost.Reset();
        var engine = CreateEngine();

        engine.Evaluate("Host.Nested.Value").AsNumber().Should().Be(5);
        engine.Evaluate("Host.Moment.getUTCFullYear()").AsNumber().Should().Be(2020);
    }

    [Fact]
    public void AThrowingStaticGetterSurfacesTheSameWayAnInstanceOneDoes()
    {
        var engine = new Engine();
        engine.SetValue("Host", TypeReference.CreateTypeReference(engine, typeof(ThrowingHost)));
        engine.SetValue("host", new ThrowingHost());

        var fromStatic = Invoking(() => engine.Evaluate("Host.Boom")).Should().Throw<Exception>().Which;
        var fromInstance = Invoking(() => engine.Evaluate("host.InstanceBoom")).Should().Throw<Exception>().Which;

        fromStatic.GetType().Should().Be(fromInstance.GetType());
        fromStatic.Message.Should().Be(fromInstance.Message);
    }

    #endregion

    #region hosts

    public enum Level
    {
        Zero = 0,
        One = 1,
    }

    public sealed class Nested
    {
        public int Value => 5;
    }

    public static class StaticHost
    {
        public static int Number { get; set; }
        public static long Big { get; set; }
        public static double Fraction { get; set; }
        public static bool Flag { get; set; }
        public static string Text { get; set; } = "text";
        public static string? NullText { get; set; }
        public static JsValue Bridged { get; set; } = JsValue.Undefined;
        public static Level EnumProperty { get; set; }
        public static Nested Nested { get; set; } = new();
        public static DateTimeOffset Moment { get; set; }

        public static int NumberField;
        public static string TextField = "field";
        public static bool FlagField;
        public static Level EnumField;

        public static readonly string ReadOnlyField = "readonly";
        public const string Constant = "constant";

        public static void Reset()
        {
            Number = 1;
            Big = 2;
            Fraction = 0.5;
            Flag = true;
            Text = "text";
            NullText = null;
            Bridged = new JsString("bridged");
            EnumProperty = Level.One;
            Nested = new Nested();
            Moment = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);

            NumberField = 1;
            TextField = "field";
            FlagField = true;
            EnumField = Level.One;
        }
    }

    public struct StaticValueTypeHost
    {
        public static int Number { get; set; }
        public static string Text = "struct";

        public static void Reset()
        {
            Number = 3;
            Text = "struct";
        }
    }

    public sealed class ThrowingHost
    {
        public static string Boom => throw new InvalidOperationException("boom");

        public string InstanceBoom => throw new InvalidOperationException("boom");
    }

    #endregion
}
