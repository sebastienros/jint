using System.Numerics;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Runtime;

public class BigIntTests
{
    [TestCase("a = a + b;", "146")]
    [TestCase("a = a - b;", "100")]
    [TestCase("a = a * b;", "2829")]
    [TestCase("a = a / b;", "5")]
    [TestCase("a = a % b;", "8")]
    [TestCase("a += b;", "146")]
    [TestCase("a -= b;", "100")]
    [TestCase("a *= b;", "2829")]
    [TestCase("a /= b;", "5")]
    [TestCase("a %= b;", "8")]
    public void BasicOperations(string statement, string expected)
    {
        var outputValues = new List<JsValue>();
        var engine = new Engine()
            .SetValue("log", outputValues.Add);
        engine.Evaluate("let a = 123n; let b = 23n;");

        engine.Evaluate(statement);

        engine.Evaluate("log(a)");
        outputValues[0].IsBigInt().Should().BeTrue("The type of the value is expected to stay BigInt");
        outputValues[0].ToString().Should().Be(expected);
    }

    [TestCase("let right = 123n;")]
    [TestCase("let right = BigInt(123);")]
    [TestCase("let right = BigInt('123');")]
    public void ObjectIsReturnsTrueForSameBigInts(string statement)
    {
        // Arrange
        var engine = new Engine();
        engine.SetValue("left", new JsBigInt(new BigInteger(123)));
        engine.Evaluate(statement);

        // Act
        var result = engine.Evaluate("Object.is(left, right);");

        // Assert
        result.AsBoolean().Should().BeTrue();
    }

    [TestCase("let right = false;")]
    [TestCase("let right = 'test';")]
    [TestCase("let right = 123;")]
    [TestCase("let right = 321n;")]
    [TestCase("let right = BigInt('321');")]
    public void ObjectIsReturnsFalseDifferentComparisonsWithBigInt(string statement)
    {
        // Arrange
        var engine = new Engine();
        engine.SetValue("left", new JsBigInt(new BigInteger(123)));
        engine.Evaluate(statement);

        // Act
        var result = engine.Evaluate("Object.is(left, right);");

        // Assert
        result.AsBoolean().Should().BeFalse();
    }

    [TestCase("11n ** 711111111n")]
    [TestCase("2n ** 10000000n")]
    [TestCase("100n ** 1000000n")]
    public void ExponentiationShouldThrowForExcessiveSize(string expression)
    {
        var engine = new Engine(options =>
        {
            options.LimitExecutionTime(TimeSpan.FromSeconds(5));
            options.LimitMemory(16_000_000);
        });

        var ex = Invoking(() => engine.Evaluate(expression)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Maximum BigInt size exceeded");
    }

    [TestCase("11n ** 711111111n")]
    [TestCase("2n ** 10000000n")]
    public void ExponentiationAssignmentShouldThrowForExcessiveSize(string expression)
    {
        var engine = new Engine(options =>
        {
            options.LimitExecutionTime(TimeSpan.FromSeconds(5));
            options.LimitMemory(16_000_000);
        });

        // Convert "a ** b" to "x = a; x **= b" to test assignment path
        var parts = expression.Split(new[] { " ** " }, StringSplitOptions.None);
        var script = $"var x = {parts[0]}; x **= {parts[1]}; x";

        var ex = Invoking(() => engine.Evaluate(script)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Maximum BigInt size exceeded");
    }

    [TestCase("2n ** 100n", "1267650600228229401496703205376")]
    [TestCase("3n ** 50n", "717897987691852588770249")]
    [TestCase("0n ** 1000000n", "0")]
    [TestCase("1n ** 1000000n", "1")]
    [TestCase("(-1n) ** 1000001n", "-1")]
    public void ExponentiationShouldWorkForReasonableSizes(string expression, string expected)
    {
        var engine = new Engine();
        var result = engine.Evaluate(expression);
        result.ToString().Should().Be(expected);
    }

    [Test]
    public void NegativeExponentShouldThrowRangeError()
    {
        var engine = new Engine();
        var ex = Invoking(() => engine.Evaluate("2n ** -1n")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Exponent must be positive");
    }

    [TestCase("BigInt.asIntN(2147483648, 1n)")]
    [TestCase("BigInt.asUintN(2147483648, 1n)")]
    public void FixedWidthBigIntOperationsRejectUnsupportedBitCounts(string expression)
    {
        var engine = new Engine();

        var exception = Invoking(() => engine.Evaluate(expression)).Should().ThrowExactly<JavaScriptException>().Which;

        exception.Error.InstanceofOperator(engine.Intrinsics.RangeError).Should().BeTrue();
    }

    [TestCase("BigInt.asIntN")]
    [TestCase("BigInt.asUintN")]
    public void FixedWidthBigIntOperationsConvertBigIntOperandBeforeRejectingUnsupportedBitCounts(string operation)
    {
        var engine = new Engine();

        var exception = Invoking(() => engine.Evaluate($$"""
            {{operation}}(2147483648, {
                [Symbol.toPrimitive]() {
                    throw new Error('boom');
                }
            });
            """)).Should().ThrowExactly<JavaScriptException>().Which;

        exception.Error.AsObject().Get("message").AsString().Should().Be("boom");
    }
}
