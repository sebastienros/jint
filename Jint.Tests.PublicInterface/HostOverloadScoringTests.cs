#nullable enable

using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The three argument shapes overload scoring cannot recognize but the type converter still converts, and
/// the one it must now refuse.
/// </summary>
/// <remarks>
/// Scoring answers "how far is this argument from this parameter" with a dozen structural rules and used to
/// end in a blanket "will rarely succeed", which <c>FindBestMatch</c> treats as a match — so a parameter type
/// the value could not become was selected anyway and the call died converting it. The last rule now asks the
/// installed converter instead, which is the only thing that can tell the three shapes below from the fourth.
/// Each is a single-candidate method, so nothing but that last rule decides it.
/// </remarks>
public class HostOverloadScoringTests
{
    public enum LengthUnit
    {
        Pixel = 42,
    }

    /// <summary>Declares the conversion <em>from</em> <see cref="double"/> on itself, where no rule looking at the argument's own type can see it.</summary>
    public readonly struct Fahrenheit
    {
        private Fahrenheit(double degrees) => Degrees = degrees;

        public double Degrees { get; }

        public static implicit operator Fahrenheit(double degrees) => new Fahrenheit(degrees);
    }

    public readonly struct Vector2D
    {
        public Vector2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }
    }

    public sealed class Host
    {
        public int CountMatches(Predicate<string> predicate)
        {
            var count = 0;
            foreach (var candidate in new[] { "alpha", "beta", "gamma" })
            {
                if (predicate(candidate))
                {
                    count++;
                }
            }

            return count;
        }

        public string DescribeTemperature(Fahrenheit temperature) => $"{temperature.Degrees}F";

        public string DescribeUnit(LengthUnit unit) => $"unit {(int) unit}";

        public string DescribeVector(Vector2D vector) => $"({vector.X}, {vector.Y})";
    }

    public sealed class Boxed
    {
        public Boxed(Vector2D vector) => Vector = vector;

        public Vector2D Vector { get; }
    }

    private static Engine NewEngine()
    {
        var engine = new Engine();
        engine.SetValue("host", new Host());
        engine.SetValue("Boxed", typeof(Boxed));
        return engine;
    }

    [Test]
    public void AJavaScriptFunctionStillBindsToADelegateParameter()
    {
        // JsCallDelegate is not assignable to Predicate<string>, is not IConvertible, and declares no cast
        // operator; only the converter, which builds the delegate, knows this works.
        NewEngine().Evaluate("host.CountMatches(s => s.length === 4)").Should().Be(1);
    }

    [Test]
    public void AConversionOperatorDeclaredOnTheParameterTypeIsStillFound()
    {
        // double declares nothing; Fahrenheit declares the implicit operator. The scoring rule that looks
        // for one only reads the argument's own type, so this pair reaches the last rule.
        NewEngine().Evaluate("host.DescribeTemperature(98.6)").Should().Be("98.6F");
    }

    [Test]
    public void AnEnumParameterStillTakesANumberOutsideItsDefinedMembers()
    {
        // Enum.IsDefined(LengthUnit, 0) is false, so the enum rule declines and the pair reaches the last
        // rule; the converter parses it the way the CLR lets an enum hold any underlying value.
        NewEngine().Evaluate("host.DescribeUnit(0)").Should().Be("unit 0");
    }

    [Test]
    public void AParameterTypeTheValueCannotBecomeIsNotSelected()
    {
        // Nothing converts a string to Vector2D. A method call reported that as a resolution failure
        // already, because MethodInfoFunction.TryCall asks the converter per candidate and moves on when it
        // declines - the scoring rule now asks that same question, so a candidate that cannot bind is no
        // longer proposed in the first place rather than proposed and then declined.
        var engine = NewEngine();

        Invoking(() => engine.Evaluate("host.DescribeVector('text')"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("No public methods with the specified arguments were found.");

        engine.Evaluate("try { host.DescribeVector('text') } catch (e) { e instanceof TypeError }")
            .AsBoolean().Should().BeTrue();
    }

    [Test]
    public void AConstructorParameterTheValueCannotBecomeIsRefusedRatherThanAttempted()
    {
        // Constructor resolution has no such retry: TypeReference calls the first match and stops. So a
        // hopeless candidate was constructed with, and the conversion's InvalidCastException escaped
        // Evaluate as a CLR exception no script catch could see. It is now the resolution failure it is.
        var engine = NewEngine();

        Invoking(() => engine.Evaluate("new Boxed('text')"))
            .Should().Throw<JavaScriptException>()
            .WithMessage("Could not resolve a constructor for the specified arguments.");

        engine.Evaluate("try { new Boxed('text') } catch (e) { e instanceof TypeError }")
            .AsBoolean().Should().BeTrue();
    }
}
