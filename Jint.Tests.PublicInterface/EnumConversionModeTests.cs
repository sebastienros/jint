#nullable enable

using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers <see cref="Options.InteropOptions.EnumConversion"/>, the built-in replacement for the hand-written
/// "render enums as their name" object converter that embedders otherwise have to register — the very
/// registration that used to cost every unrelated member its compiled read lane. These live in the
/// public-interface suite on purpose: the project references Jint without any internals access, so the whole
/// knob is proven reachable by a third-party host.
/// <para>
/// The numeric default itself is additionally pinned across every underlying-type route by
/// <c>Jint.Tests.Runtime.InteropEnumConversionTests</c>.
/// </para>
/// </summary>
public class EnumConversionModeTests
{
    public enum Level
    {
        Zero = 0,
        One = 1,
        Two = 2,
    }

    [Flags]
    public enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    public enum LongLevel : long
    {
        Big = 5_000_000_000L,
    }

    public sealed class Host
    {
        public Level Level { get; set; } = Level.One;
        public Level? NullableLevel { get; set; }
        public Access Access { get; set; } = Access.Read | Access.Write;
        public LongLevel LongLevel { get; set; } = LongLevel.Big;
        public Level Undefined { get; set; } = (Level) 42;
        public bool Flag { get; set; } = true;
        public int Number { get; set; } = 7;

        public Level Echo(Level level) => level;
    }

    private static Engine CreateEngine(Host host, EnumConversionMode? mode = null)
    {
        var engine = new Engine(options =>
        {
            if (mode is not null)
            {
                options.Interop.EnumConversion = mode.Value;
            }
        });
        engine.SetValue("host", host);
        return engine;
    }

    [Fact]
    public void DefaultsToTheNumericValue()
    {
        var engine = CreateEngine(new Host());

        engine.Evaluate("host.Level").Should().Be(1);
        engine.Evaluate("host.Access").Should().Be(3);
        engine.Evaluate("host.Undefined").Should().Be(42);
        engine.Evaluate("host.Echo(2)").Should().Be(2);
    }

    [Fact]
    public void ExplicitNumberModeMatchesTheDefault()
    {
        var engine = CreateEngine(new Host(), EnumConversionMode.Number);

        engine.Evaluate("host.Level").Should().Be(1);
    }

    [Fact]
    public void StringModeExposesTheMemberName()
    {
        var engine = CreateEngine(new Host(), EnumConversionMode.String);

        engine.Evaluate("host.Level").Should().Be("One");
        engine.Evaluate("typeof host.Level").Should().Be("string");
    }

    [Fact]
    public void StringModeCombinesFlagNames()
    {
        var engine = CreateEngine(new Host(), EnumConversionMode.String);

        engine.Evaluate("host.Access").Should().Be("Read, Write");
    }

    [Fact]
    public void StringModeFallsBackToTheNumberForANamelessValue()
    {
        var engine = CreateEngine(new Host(), EnumConversionMode.String);

        engine.Evaluate("host.Undefined").Should().Be("42");
    }

    [Fact]
    public void StringModeCoversWideUnderlyingTypes()
    {
        var engine = CreateEngine(new Host(), EnumConversionMode.String);

        engine.Evaluate("host.LongLevel").Should().Be("Big");
    }

    [Fact]
    public void StringModeCoversNullableMembers()
    {
        var engine = CreateEngine(new Host { NullableLevel = Level.Two }, EnumConversionMode.String);

        engine.Evaluate("host.NullableLevel").Should().Be("Two");
        engine.Evaluate("host.Level === 'One'").Should().Be(true);
    }

    [Fact]
    public void NullNullableMemberStaysNull()
    {
        var engine = CreateEngine(new Host(), EnumConversionMode.String);

        engine.Evaluate("host.NullableLevel").Should().Be(JsValue.Null);
    }

    [Fact]
    public void StringModeCoversMethodReturnValues()
    {
        var engine = CreateEngine(new Host(), EnumConversionMode.String);

        engine.Evaluate("host.Echo(2)").Should().Be("Two");
    }

    [Fact]
    public void StringModeLeavesOtherMembersAlone()
    {
        var engine = CreateEngine(new Host(), EnumConversionMode.String);

        engine.Evaluate("host.Flag").Should().Be(true);
        engine.Evaluate("host.Number").Should().Be(7);
    }

    [Fact]
    public void WritesStillAcceptBothNamesAndNumbers()
    {
        var host = new Host();
        var engine = CreateEngine(host, EnumConversionMode.String);

        engine.Evaluate("host.Level = 'Two'");
        host.Level.Should().Be(Level.Two);

        engine.Evaluate("host.Level = 0");
        host.Level.Should().Be(Level.Zero);

        engine.Evaluate("host.Echo('Two')").Should().Be("Two");
    }

    [Fact]
    public void RoundTripsThroughScript()
    {
        var host = new Host();
        var engine = CreateEngine(host, EnumConversionMode.String);

        engine.Evaluate("host.Level = host.Echo(host.Level)");
        host.Level.Should().Be(Level.One);
    }

    [Fact]
    public void ModeIsPerEngine()
    {
        var host = new Host();
        CreateEngine(host, EnumConversionMode.String).Evaluate("host.Level").Should().Be("One");
        CreateEngine(host).Evaluate("host.Level").Should().Be(1);
    }
}
