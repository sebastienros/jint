using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class NumberTests
{
    private readonly Engine _engine;

    public NumberTests()
    {
        _engine = new Engine()
            .SetValue("log", new Action<object>(Console.WriteLine))
            .SetValue("assert", new Action<bool>(Assert.True))
            .SetValue("equal", new Action<object, object>(Assert.Equal));
    }

    private void RunTest(string source)
    {
        _engine.Execute(source);
    }

    [Theory]
    [InlineData(1, "3.0e+0")]
    [InlineData(50, "3.00000000000000000000000000000000000000000000000000e+0")]
    [InlineData(100, "3.0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000e+0")]
    public void ToExponential(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toExponential({fractionDigits}).toString()").AsString();
        Assert.Equal(result, value);
    }

    [Theory]
    [InlineData(1, "3.0")]
    [InlineData(50, "3.00000000000000000000000000000000000000000000000000")]
    [InlineData(99, "3.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    public void ToFixed(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toFixed({fractionDigits}).toString()").AsString();
        Assert.Equal(result, value);
    }

    [Fact]
    public void ToFixedWith100FractionDigitsThrows()
    {
        var ex = Assert.Throws<JavaScriptException>(() => _engine.Evaluate($"(3).toFixed(100)"));
        Assert.Equal("100 fraction digits is not supported due to .NET format specifier limitation", ex.Message);
    }

    [Theory]
    [InlineData(1, "3")]
    [InlineData(50, "3.0000000000000000000000000000000000000000000000000")]
    [InlineData(100, "3.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    public void ToPrecision(int fractionDigits, string result)
    {
        var value = _engine.Evaluate($"(3).toPrecision({fractionDigits}).toString()").AsString();
        Assert.Equal(result, value);
    }

    [Theory]
    [InlineData("1.7976931348623157e+308", double.MaxValue)]
    public void ParseFloat(string input, double result)
    {
        var value = _engine.Evaluate($"parseFloat('{input}')").AsNumber();
        Assert.Equal(result, value);
    }

    // Results from node -v v18.18.0.
    [Theory]
    // Thousand separators.
    [InlineData("1000000", "en-US", "1,000,000")]
    [InlineData("1000000", "en-GB", "1,000,000")]
    [InlineData("1000000", "de-DE", "1.000.000")]
    // TODO. Fails in Win CI due to U+2009
    // Check https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu
    // [InlineData("1000000", "fr-FR", "1 000 000")] 
    [InlineData("1000000", "es-ES", "1.000.000")]
    [InlineData("1000000", "es-LA", "1.000.000")]
    [InlineData("1000000", "es-MX", "1,000,000")]
    [InlineData("1000000", "es-AR", "1.000.000")]
    [InlineData("1000000", "es-CL", "1.000.000")]
    // Comma separator.
    [InlineData("1,23", "en-US", "23")]
    [InlineData("1,23", "en-GB", "23")]
    [InlineData("1,23", "de-DE", "23")]
    [InlineData("1,23", "fr-FR", "23")]
    [InlineData("1,23", "es-ES", "23")]
    [InlineData("1,23", "es-LA", "23")]
    [InlineData("1,23", "es-MX", "23")]
    [InlineData("1,23", "es-AR", "23")]
    [InlineData("1,23", "es-CL", "23")]
    // Dot deicimal separator.
    [InlineData("1.23", "en-US", "1.23")]
    [InlineData("1.23", "en-GB", "1.23")]
    [InlineData("1.23", "de-DE", "1,23")]
    [InlineData("1.23", "fr-FR", "1,23")]
    [InlineData("1.23", "es-ES", "1,23")]
    [InlineData("1.23", "es-LA", "1,23")]
    [InlineData("1.23", "es-MX", "1.23")]
    [InlineData("1.23", "es-AR", "1,23")]
    [InlineData("1.23", "es-CL", "1,23")]
    // Scientific notation.
    [InlineData("1e6", "en-US", "1,000,000")]
    [InlineData("1e6", "en-GB", "1,000,000")]
    [InlineData("1e6", "de-DE", "1.000.000")]
    // TODO. Fails in Win CI due to U+2009
    // Check https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu
    // [InlineData("1000000", "fr-FR", "1 000 000")]
    [InlineData("1e6", "es-ES", "1.000.000")]
    [InlineData("1e6", "es-LA", "1.000.000")]
    [InlineData("1e6", "es-MX", "1,000,000")]
    [InlineData("1e6", "es-AR", "1.000.000")]
    [InlineData("1e6", "es-CL", "1.000.000")]
    // Returns the correct max decimal degits for the respective cultures, rounded down.
    [InlineData("1.234444449", "en-US", "1.234")]
    [InlineData("1.234444449", "en-GB", "1.234")]
    [InlineData("1.234444449", "de-DE", "1,234")]
    [InlineData("1.234444449", "fr-FR", "1,234")]
    [InlineData("1.234444449", "es-ES", "1,234")]
    [InlineData("1.234444449", "es-LA", "1,234")]
    [InlineData("1.234444449", "es-MX", "1.234")]
    [InlineData("1.234444449", "es-AR", "1,234")]
    [InlineData("1.234444449", "es-CL", "1,234")]
    // Returns the correct max decimal degits for the respective cultures, rounded up.
    [InlineData("1.234500001", "en-US", "1.235")]
    [InlineData("1.234500001", "en-GB", "1.235")]
    [InlineData("1.234500001", "de-DE", "1,235")]
    [InlineData("1.234500001", "fr-FR", "1,235")]
    [InlineData("1.234500001", "es-ES", "1,235")]
    [InlineData("1.234500001", "es-LA", "1,235")]
    [InlineData("1.234500001", "es-MX", "1.235")]
    [InlineData("1.234500001", "es-AR", "1,235")]
    [InlineData("1.234500001", "es-CL", "1,235")]
    public void ToLocaleString(string parseNumber, string culture, string result)
    {
        var value = _engine.Evaluate($"({parseNumber}).toLocaleString('{culture}')").AsString();
        Assert.Equal(result, value);
    }

    [Theory]
    // Does not add extra zeros of there is no culture argument.
    [InlineData("123456")]
    public void ToLocaleStringNoArg(string parseNumber)
    {
        var value = _engine.Evaluate($"({parseNumber}).toLocaleString()").AsString();
        Assert.DoesNotContain(".0", value);
    }

    [Fact]
    public void CoercingOverflowFromString()
    {
        var engine = new Engine();

        Assert.Equal(double.PositiveInfinity, engine.Evaluate("Number(1e1000)").ToObject());
        Assert.Equal(double.PositiveInfinity, engine.Evaluate("+1e1000").ToObject());
        Assert.Equal("Infinity", engine.Evaluate("(+1e1000).toString()").ToObject());

        Assert.Equal(double.PositiveInfinity, engine.Evaluate("Number('1e1000')").ToObject());
        Assert.Equal(double.PositiveInfinity, engine.Evaluate("+'1e1000'").ToObject());
        Assert.Equal("Infinity", engine.Evaluate("(+'1e1000').toString()").ToObject());

        Assert.Equal(double.NegativeInfinity, engine.Evaluate("Number(-1e1000)").ToObject());
        Assert.Equal(double.NegativeInfinity, engine.Evaluate("-1e1000").ToObject());
        Assert.Equal("-Infinity", engine.Evaluate("(-1e1000).toString()").ToObject());

        Assert.Equal(double.NegativeInfinity, engine.Evaluate("Number('-1e1000')").ToObject());
        Assert.Equal(double.NegativeInfinity, engine.Evaluate("-'1e1000'").ToObject());
        Assert.Equal("-Infinity", engine.Evaluate("(-'1e1000').toString()").ToObject());
    }
}
