using System.Numerics;
using Jint.Native;
using Jint.Runtime;

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

    [Theory]
    [InlineData("11n ** 711111111n")]
    [InlineData("2n ** 10000000n")]
    [InlineData("100n ** 1000000n")]
    public void ExponentiationShouldThrowForExcessiveSize(string expression)
    {
        var engine = new Engine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
            options.LimitMemory(16_000_000);
        });

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate(expression));
        Assert.Contains("Maximum BigInt size exceeded", ex.Message);
    }

    [Theory]
    [InlineData("11n ** 711111111n")]
    [InlineData("2n ** 10000000n")]
    public void ExponentiationAssignmentShouldThrowForExcessiveSize(string expression)
    {
        var engine = new Engine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
            options.LimitMemory(16_000_000);
        });

        // Convert "a ** b" to "x = a; x **= b" to test assignment path
        var parts = expression.Split(new[] { " ** " }, StringSplitOptions.None);
        var script = $"var x = {parts[0]}; x **= {parts[1]}; x";

        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate(script));
        Assert.Contains("Maximum BigInt size exceeded", ex.Message);
    }

    [Theory]
    [InlineData("2n ** 100n", "1267650600228229401496703205376")]
    [InlineData("3n ** 50n", "717897987691852588770249")]
    [InlineData("0n ** 1000000n", "0")]
    [InlineData("1n ** 1000000n", "1")]
    [InlineData("(-1n) ** 1000001n", "-1")]
    public void ExponentiationShouldWorkForReasonableSizes(string expression, string expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate(expression);
        Assert.Equal(expected, result.ToString());
    }

    [Fact]
    public void NegativeExponentShouldThrowRangeError()
    {
        var engine = new Engine();
        var ex = Assert.Throws<JavaScriptException>(() => engine.Evaluate("2n ** -1n"));
        Assert.Contains("Exponent must be positive", ex.Message);
    }

    [Theory]
    [InlineData("BigInt.asIntN(2147483648, 1n)")]
    [InlineData("BigInt.asUintN(2147483648, 1n)")]
    public void FixedWidthBigIntOperationsRejectUnsupportedBitCounts(string expression)
    {
        var engine = new Engine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate(expression));

        Assert.True(exception.Error.InstanceofOperator(engine.Intrinsics.RangeError));
    }

    [Theory]
    [InlineData("BigInt.asIntN")]
    [InlineData("BigInt.asUintN")]
    public void FixedWidthBigIntOperationsConvertBigIntOperandBeforeRejectingUnsupportedBitCounts(string operation)
    {
        var engine = new Engine();

        var exception = Assert.Throws<JavaScriptException>(() => engine.Evaluate($$"""
            {{operation}}(2147483648, {
                [Symbol.toPrimitive]() {
                    throw new Error('boom');
                }
            });
            """));

        Assert.Equal("boom", exception.Error.AsObject().Get("message").AsString());
    }
}
