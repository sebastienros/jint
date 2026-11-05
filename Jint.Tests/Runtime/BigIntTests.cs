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
        outputValues[0].IsBigInt().Should().BeTrue("The type of the value is expected to stay BigInt");
        outputValues[0].ToString().Should().Be(expected);
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
        result.AsBoolean().Should().BeTrue();
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
        result.AsBoolean().Should().BeFalse();
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

        var ex = Invoking(() => engine.Evaluate(expression)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Maximum BigInt size exceeded");
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

        var ex = Invoking(() => engine.Evaluate(script)).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Maximum BigInt size exceeded");
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
        result.ToString().Should().Be(expected);
    }

    [Fact]
    public void NegativeExponentShouldThrowRangeError()
    {
        var engine = new Engine();
        var ex = Invoking(() => engine.Evaluate("2n ** -1n")).Should().ThrowExactly<JavaScriptException>().Which;
        ex.Message.Should().Contain("Exponent must be positive");
    }

    /// <summary>
    /// A value that already fits in the requested width is its own result, however large the bit count, so none
    /// of these may build 2^bits (issue #4432).
    /// </summary>
    [Theory]
    [InlineData("BigInt.asIntN(2147483648, 1n)", "1")]
    [InlineData("BigInt.asUintN(2147483648, 1n)", "1")]
    [InlineData("BigInt.asIntN(2 ** 31 - 1, 5n)", "5")]
    [InlineData("BigInt.asIntN(2 ** 31 - 1, -5n)", "-5")]
    [InlineData("BigInt.asUintN(2 ** 31 - 1, 5n)", "5")]
    [InlineData("BigInt.asUintN(2 ** 31 - 2, 5n)", "5")]
    [InlineData("BigInt.asIntN(2 ** 53 - 1, -5n)", "-5")]
    [InlineData("BigInt.asUintN(2 ** 53 - 1, 5n)", "5")]
    [InlineData("BigInt.asUintN(2 ** 30, 2n ** 64n)", "18446744073709551616")]
    public void FixedWidthBigIntOperationsReturnAValueThatFitsUnchanged(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The ordinary widths, against the values the spec's modulo arithmetic gives, around each sign boundary.
    /// </summary>
    [Theory]
    [InlineData("BigInt.asIntN(8, 127n)", "127")]
    [InlineData("BigInt.asIntN(8, 128n)", "-128")]
    [InlineData("BigInt.asIntN(8, 255n)", "-1")]
    [InlineData("BigInt.asIntN(8, 256n)", "0")]
    [InlineData("BigInt.asIntN(8, -128n)", "-128")]
    [InlineData("BigInt.asIntN(8, -129n)", "127")]
    [InlineData("BigInt.asIntN(1, 1n)", "-1")]
    [InlineData("BigInt.asIntN(1, -1n)", "-1")]
    [InlineData("BigInt.asIntN(0, 5n)", "0")]
    [InlineData("BigInt.asIntN(0, -5n)", "0")]
    [InlineData("BigInt.asIntN(64, 2n ** 63n)", "-9223372036854775808")]
    [InlineData("BigInt.asIntN(64, -(2n ** 63n) - 1n)", "9223372036854775807")]
    [InlineData("BigInt.asUintN(8, 255n)", "255")]
    [InlineData("BigInt.asUintN(8, 256n)", "0")]
    [InlineData("BigInt.asUintN(8, 257n)", "1")]
    [InlineData("BigInt.asUintN(8, -1n)", "255")]
    [InlineData("BigInt.asUintN(8, -256n)", "0")]
    [InlineData("BigInt.asUintN(8, -257n)", "255")]
    [InlineData("BigInt.asUintN(0, 5n)", "0")]
    [InlineData("BigInt.asUintN(0, -5n)", "0")]
    [InlineData("BigInt.asUintN(64, -1n)", "18446744073709551615")]
    [InlineData("BigInt.asUintN(1000000, -1n).toString(16).length", "250000")]
    public void FixedWidthBigIntOperationsWrapAroundTheWidth(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// A negative value is the one case whose result is as wide as the bit count, so a count past the BigInt size
    /// <c>**</c> enforces is a RangeError the script can catch, never a CLR exception.
    /// </summary>
    [Theory]
    [InlineData("BigInt.asUintN(1000001, -1n)")]
    [InlineData("BigInt.asUintN(2 ** 31 - 1, -1n)")]
    [InlineData("BigInt.asUintN(2147483648, -1n)")]
    [InlineData("BigInt.asUintN(2 ** 53 - 1, -1n)")]
    public void FixedWidthBigIntOperationsRejectAResultTooWideToBuild(string expression)
    {
        ThrownErrorName(expression).Should().Be("RangeError");
    }

    /// <summary>
    /// BigInt::leftShift and BigInt::signedRightShift are defined for any count. A count outside Int32 that moves
    /// right leaves only the sign; the CLR cast used to turn every one of them into an OverflowException.
    /// </summary>
    [Theory]
    [InlineData("1n >> (2n ** 32n)", "0")]
    [InlineData("-1n >> (2n ** 32n)", "-1")]
    [InlineData("-5n >> (2n ** 64n)", "-1")]
    [InlineData("1n << -(2n ** 32n)", "0")]
    [InlineData("-1n << -(2n ** 32n)", "-1")]
    [InlineData("0n << (2n ** 32n)", "0")]
    [InlineData("0n >> -(2n ** 32n)", "0")]
    [InlineData("1n >> 2147483647n", "0")]
    [InlineData("-1n >> 2147483647n", "-1")]
    [InlineData("5n << -2147483648n", "0")]
    [InlineData("-5n << -2147483648n", "-1")]
    [InlineData("let x = 1n; x >>= 2n ** 32n; x", "0")]
    [InlineData("let x = -1n; x <<= -(2n ** 32n); x", "-1")]
    public void BigIntShiftsByACountOutsideInt32(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The same counts moving a non-zero value left cannot be represented.
    /// </summary>
    [Theory]
    [InlineData("1n << (2n ** 32n)")]
    [InlineData("-1n << (2n ** 32n)")]
    [InlineData("1n >> -(2n ** 32n)")]
    [InlineData("1n >> -2147483648n")]
    [InlineData("let x = 1n; x <<= 2n ** 32n")]
    [InlineData("let x = 1n; x >>= -(2n ** 32n)")]
    public void BigIntLeftShiftsByACountOutsideInt32AreARangeError(string expression)
    {
        ThrownErrorName(expression).Should().Be("RangeError");
    }

    /// <summary>
    /// A count inside Int32 that would build a result wider than <see cref="JsBigInt.MaxBitLength"/> bits is the
    /// same RangeError <c>**</c> gives, raised before anything is allocated: <c>1n &lt;&lt; 2147483600n</c> was a CLR
    /// OverflowException on .NET 10 and a 256 MB allocation on the older runtimes.
    /// </summary>
    [Theory]
    [InlineData("1n << 2147483600n")]
    [InlineData("1n << 2147483647n")]
    [InlineData("-1n << 2147483647n")]
    [InlineData("1n << 1000000n")]
    [InlineData("(2n ** 64n) << 999937n")]
    [InlineData("1n >> -2147483647n")]
    [InlineData("let x = 1n; x <<= 2147483600n")]
    public void BigIntLeftShiftsPastTheMaximumBigIntSizeAreARangeError(string expression)
    {
        ThrownErrorName(expression).Should().Be("RangeError");
    }

    /// <summary>
    /// Up to that size a left shift is exact, and a zero is a zero however far it is shifted.
    /// </summary>
    [Theory]
    [InlineData("(1n << 999999n) >> 999999n", "1")]
    [InlineData("(-3n << 999998n) >> 999998n", "-3")]
    [InlineData("((2n ** 64n) << 999935n) >> 999935n", "18446744073709551616")]
    [InlineData("0n << 2147483647n", "0")]
    [InlineData("0n << (2n ** 64n)", "0")]
    [InlineData("let x = 0n; x <<= 2147483647n; x", "0")]
    public void BigIntLeftShiftsUpToTheMaximumBigIntSize(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The constructor name of whatever <paramref name="source"/> throws, or <c>did not throw</c>. Written in
    /// script so that a CLR exception leaking out of the engine fails the test instead of being caught.
    /// </summary>
    private static string ThrownErrorName(string source) => new Engine()
        .Evaluate($"(function () {{ try {{ {source}; return 'did not throw'; }} catch (e) {{ return e.constructor.name; }} }})()")
        .AsString();

    [Theory]
    [InlineData("BigInt.asIntN")]
    [InlineData("BigInt.asUintN")]
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
