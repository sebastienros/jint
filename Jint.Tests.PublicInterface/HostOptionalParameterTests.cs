#nullable enable

using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers calling a host method that declares optional parameters, with and without supplying them. Eliding
/// one used to force the call onto the raw reflection binder; the declared default is now filled in by the
/// engine instead, so every case below has to keep producing the value the binder produced.
/// <para>
/// Each behaviour is asserted twice where it can be: once with exact-typed arguments, which take the compiled
/// invoker, and once with arguments that decline it (a fractional number bound to an int, a boolean where a
/// string is expected) and therefore run the full binding path. The two must agree, and both must agree with
/// the same call made directly from C#.
/// </para>
/// </summary>
public class HostOptionalParameterTests
{
    private static Engine CreateEngine()
    {
        var engine = new Engine();
        engine.SetValue("host", new Host());
        return engine;
    }

    #region 1. the default is filled in

    [Fact]
    public void ElidedStringDefaultIsSupplied()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Greet('world')").AsString().Should().Be("Hello, world");
        engine.Evaluate("host.Greet('world', 'Hi')").AsString().Should().Be("Hi, world");

        new Host().Greet("world").Should().Be("Hello, world");
    }

    [Fact]
    public void ElidedNumericDefaultsAreSupplied()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Add(1)").AsNumber().Should().Be(6);
        engine.Evaluate("host.Add(1, 2)").AsNumber().Should().Be(3);

        engine.Evaluate("host.Scale(2)").AsNumber().Should().Be(1);
        engine.Evaluate("host.Scale(2, 3)").AsNumber().Should().Be(6);

        engine.Evaluate("host.Sum(1)").AsNumber().Should().Be(11);
    }

    [Fact]
    public void ElidedBooleanDefaultIsSupplied()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Render('x')").AsString().Should().Be("X");
        engine.Evaluate("host.Render('x', false)").AsString().Should().Be("x");
    }

    [Fact]
    public void ElidedNullDefaultIsSupplied()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Describe(1)").AsString().Should().Be("1/<null>");
        engine.Evaluate("host.Describe(1, 'tag')").AsString().Should().Be("1/tag");
    }

    [Fact]
    public void SeveralElidedDefaultsAreSupplied()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Join('a')").AsString().Should().Be("a-b-c");
        engine.Evaluate("host.Join('a', 'x')").AsString().Should().Be("a-x-c");
        engine.Evaluate("host.Join('a', 'x', 'y')").AsString().Should().Be("a-x-y");
    }

    #endregion

    #region 2. the declining lane agrees

    [Fact]
    public void NonExactArgumentsProduceTheSameResults()
    {
        var engine = CreateEngine();

        // a fractional number cannot bind exactly to an int, so this call runs the full binding path
        engine.Evaluate("host.Add(1.5)").AsNumber().Should().Be(7);
        engine.Evaluate("host.Add(1.5, 2)").AsNumber().Should().Be(4);

        // and so does a non-string where a string is expected
        engine.Evaluate("host.Greet(1)").AsString().Should().Be("Hello, 1");
    }

    [Fact]
    public void AnExplicitUndefinedIsNotTheSameAsAnElidedArgument()
    {
        var engine = CreateEngine();

        // a supplied undefined is an argument, not an omission: it coerces (to null for a string) instead of
        // reviving the declared default. Filling defaults for elided arguments must not change that - and it
        // cannot, because a non-JsString argument declines the compiled lane and takes the same binding path
        // it always did.
        engine.Evaluate("host.Greet('world', undefined)").AsString().Should().Be(", world");
    }

    [Fact]
    public void AnExplicitNullReachesTheParameter()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Describe(1, null)").AsString().Should().Be("1/<null>");
    }

    #endregion

    #region 3. shapes that keep the reflection path

    [Fact]
    public void AJsValueParameterKeepsReceivingNullWhenElided()
    {
        // TryCall assigns a JsValue-typed parameter the raw argument, which is null when absent - the
        // declared default is never consulted for one, whichever lane runs
        var engine = CreateEngine();

        engine.Evaluate("host.Bridge()").AsString().Should().Be("<none>");
        engine.Evaluate("host.Bridge(1)").AsString().Should().Be("1");
        engine.Evaluate("host.Bridge(undefined)").AsString().Should().Be("undefined");
    }

    [Fact]
    public void ADefaultOfATypeTheLaneDoesNotCoverStillWorks()
    {
        // decimal is outside the compiled invoker's parameter set, so the whole method keeps the
        // reflection path - including its default
        var engine = CreateEngine();

        engine.Evaluate("host.Price()").AsNumber().Should().Be(1.5);
        engine.Evaluate("host.Price(2)").AsNumber().Should().Be(2);
    }

    [Fact]
    public void AnEnumDefaultStillWorks()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Pick()").AsString().Should().Be("One");
        engine.Evaluate("host.Pick(0)").AsString().Should().Be("Zero");
    }

    [Fact]
    public void ParamsMethodsAreUnaffected()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Concat('a')").AsString().Should().Be("a");
        engine.Evaluate("host.Concat('a', 'b', 'c')").AsString().Should().Be("abc");
    }

    [Fact]
    public void OverloadsStillResolveWhenOneHasOptionalParameters()
    {
        var engine = CreateEngine();

        engine.Evaluate("host.Pad('x')").AsString().Should().Be("x..");
        engine.Evaluate("host.Pad('x', 3)").AsString().Should().Be("x...");
        engine.Evaluate("host.Pad(1)").AsString().Should().Be("number:1");
    }

    #endregion

    #region hosts

    public enum Level
    {
        Zero = 0,
        One = 1,
    }

    public sealed class Host
    {
        public string Greet(string name, string greeting = "Hello") => greeting + ", " + name;

        public int Add(int a, int b = 5) => a + b;

        public double Scale(double a, double factor = 0.5) => a * factor;

        public long Sum(long a, long b = 10) => a + b;

        public string Render(string text, bool upper = true) => upper ? text.ToUpperInvariant() : text;

        public string Describe(int id, string? tag = null) => id + "/" + (tag ?? "<null>");

        public string Join(string a, string b = "b", string c = "c") => a + "-" + b + "-" + c;

        public string Bridge(JsValue? value = null) => value is null ? "<none>" : value.ToString();

        public decimal Price(decimal amount = 1.5m) => amount;

        public string Pick(Level level = Level.One) => level.ToString();

        public string Concat(params string[] parts) => string.Concat(parts);

        public string Pad(string text, int width = 2) => text.PadRight(text.Length + width, '.');

        public string Pad(int number) => "number:" + number;
    }

    #endregion
}
