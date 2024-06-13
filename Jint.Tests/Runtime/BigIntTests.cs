using Jint.Native;

namespace Jint.Tests.Runtime;

public class BigIntTests
{
    [Theory]
    [InlineData("a = a + b;", "146")]
    [InlineData("a = a - b;", "100")]
    [InlineData("a = a * b;", "2829")]
    [InlineData("a = a / b;", "5")]
    [InlineData("a += b;", "146")]
    [InlineData("a -= b;", "100")]
    [InlineData("a *= b;", "2829")]
    [InlineData("a /= b;", "5")]
    public void BasicOperations(string statement, string expected)
    {
        var outputValues = new List<JsValue>();
        var engine = new Engine()
            .SetValue("log", outputValues.Add);
        engine.Evaluate("let a = 123n; let b = 23n;");

        engine.Evaluate(statement);

        engine.Evaluate("log(a)");
        Assert.True(outputValues[0].IsBigInt(), "The type of the value is expected to stay BigInt");
        Assert.Equal(expected, outputValues[0].ToString());
    }
}
