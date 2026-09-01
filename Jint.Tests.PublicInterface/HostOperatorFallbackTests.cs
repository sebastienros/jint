#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// An operator overload the specification would never have selected is not selected.
/// </summary>
/// <remarks>
/// <c>'s' + v</c>, <c>v + 's'</c>, <c>1 + v</c> and <c>true + v</c> are ordinary JavaScript:
/// <c>ApplyStringOrNumericBinaryOperator</c> takes <c>ToPrimitive</c> of both operands and, one of them
/// being a string, concatenates. Turning <see cref="Options.InteropOptions.AllowOperatorOverloading"/> on
/// does not change that for a host type whose only <c>op_Addition</c> is <c>(T, T)</c> — there is no
/// overload for those pairs, so there is nothing to select. Overload scoring nevertheless handed back
/// <c>(T, T)</c> and the argument conversion then threw, so the option turned four spec-defined expressions
/// into failures.
/// </remarks>
public class HostOperatorFallbackTests
{
    /// <summary>The shape that has no answer for any of the four: one operator, both parameters its own type.</summary>
    public readonly struct Vector2D
    {
        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public static Vector2D operator +(Vector2D left, Vector2D right) => new Vector2D(left.X + right.X, left.Y + right.Y);

        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>The same shape with the mixed overloads declared, which must keep winning.</summary>
    public readonly struct Metres
    {
        public Metres(double value) => Value = value;

        public double Value { get; }

        public static Metres operator +(Metres left, Metres right) => new Metres(left.Value + right.Value);

        public static Metres operator +(Metres left, double right) => new Metres(left.Value + right);

        public static Metres operator +(double left, Metres right) => new Metres(left + right.Value);

        public static Metres operator +(string left, Metres right) => new Metres(right.Value);

        public override string ToString() => $"{Value}m";
    }

    private static Engine NewEngine()
    {
        var engine = new Engine(options => options.Interop.AllowOperatorOverloading = true);
        engine.SetValue("v", new Vector2D(1, 2));
        engine.SetValue("m", new Metres(3));
        return engine;
    }

    [Theory]
    [InlineData("'text ' + v", "text (1, 2)")]
    [InlineData("v + ' text'", "(1, 2) text")]
    [InlineData("1 + v", "1(1, 2)")]
    [InlineData("true + v", "true(1, 2)")]
    [InlineData("'### ' + v + ' ###'", "### (1, 2) ###")]
    public void AnOperandPairNoOverloadAcceptsFollowsOrdinaryJavaScript(string source, string expected)
    {
        NewEngine().Evaluate(source).Should().Be(expected);
    }

    [Fact]
    public void TheOverloadThatDoesAcceptThePairIsStillSelected()
    {
        var engine = NewEngine();

        // (T, T), (T, double), (double, T) and (string, T) are all declared, so all four are operator calls
        // and none of them concatenates.
        engine.Evaluate("(m + m).Value").Should().Be(6);
        engine.Evaluate("(m + 4).Value").Should().Be(7);
        engine.Evaluate("(4 + m).Value").Should().Be(7);
        engine.Evaluate("('anything' + m).Value").Should().Be(3);
    }

    [Fact]
    public void ANumericPairIsUnaffectedByTheOptionBeingOn()
    {
        NewEngine().Evaluate("1 + 2").Should().Be(3);
        NewEngine().Evaluate("'a' + 'b'").Should().Be("ab");
    }

    [Fact]
    public void TheHostValueStillReadsAsTheStringItsToStringProduces()
    {
        var engine = NewEngine();

        engine.Evaluate("String(v)").Should().Be("(1, 2)");
        engine.Evaluate("`${v}`").Should().Be("(1, 2)");
        engine.Evaluate("[v].join('')").Should().Be("(1, 2)");
    }

    [Fact]
    public void AnOperatorWithNoTextualFallbackReportsThatNoOverloadApplies()
    {
        // '-' has no string form to fall back to, so the pair is a number pair: ToNumber of an object with
        // no numeric valueOf is NaN. That is what the specification says, and it is what an engine with the
        // option off already did.
        NewEngine().Evaluate("v - 1").AsNumber().Should().Be(double.NaN);
    }
}
