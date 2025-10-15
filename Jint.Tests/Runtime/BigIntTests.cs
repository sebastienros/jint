using System.Numerics;
using Jint.Native;

namespace Jint.Tests.Runtime;

public class BigIntTests
{
    [Theory]
    [InlineData("a = a + b;", "146")]
    [InlineData("a = a - b;", "100")]
    [InlineData("a = a * b;", "2829")]
    [InlineData("a = a / b;", "5")]
    [InlineData("a = a % b;", "8")]
    [InlineData("a += b;", "146")]
    [InlineData("a -= b;", "100")]
    [InlineData("a *= b;", "2829")]
    [InlineData("a /= b;", "5")]
    [InlineData("a %= b;", "8")]
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

    [Theory]
    [InlineData("let right = 123n;")]
    [InlineData("let right = BigInt(123);")]
    [InlineData("let right = BigInt('123');")]
    public void ObjectIsReturnsTrueForSameBigInts(string statement)
    {
        // Arrange
        var engine = new Engine();
        engine.SetValue("left", new JsBigInt(new BigInteger(123)));
        engine.Evaluate(statement);

        // Act
        var result = engine.Evaluate("Object.is(left, right);");

        // Assert
        Assert.True(result.AsBoolean());
    }

    [Theory]
    [InlineData("let right = false;")]
    [InlineData("let right = 'test';")]
    [InlineData("let right = 123;")]
    [InlineData("let right = 321n;")]
    [InlineData("let right = BigInt('321');")]
    public void ObjectIsReturnsFalseDifferentComparisonsWithBigInt(string statement)
    {
        // Arrange
        var engine = new Engine();
        engine.SetValue("left", new JsBigInt(new BigInteger(123)));
        engine.Evaluate(statement);

        // Act
        var result = engine.Evaluate("Object.is(left, right);");

        // Assert
        Assert.False(result.AsBoolean());
    }
}
