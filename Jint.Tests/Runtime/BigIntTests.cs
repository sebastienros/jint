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

    /// <summary>
    /// A value that already fits in the requested width is its own result, however large the bit count, so none
    /// of these may build 2^bits (issue #4432).
    /// </summary>
    [TestCase("BigInt.asIntN(2147483648, 1n)", "1")]
    [TestCase("BigInt.asUintN(2147483648, 1n)", "1")]
    [TestCase("BigInt.asIntN(2 ** 31 - 1, 5n)", "5")]
    [TestCase("BigInt.asIntN(2 ** 31 - 1, -5n)", "-5")]
    [TestCase("BigInt.asUintN(2 ** 31 - 1, 5n)", "5")]
    [TestCase("BigInt.asUintN(2 ** 31 - 2, 5n)", "5")]
    [TestCase("BigInt.asIntN(2 ** 53 - 1, -5n)", "-5")]
    [TestCase("BigInt.asUintN(2 ** 53 - 1, 5n)", "5")]
    [TestCase("BigInt.asUintN(2 ** 30, 2n ** 64n)", "18446744073709551616")]
    public void FixedWidthBigIntOperationsReturnAValueThatFitsUnchanged(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The ordinary widths, against the values the spec's modulo arithmetic gives, around each sign boundary.
    /// </summary>
    [TestCase("BigInt.asIntN(8, 127n)", "127")]
    [TestCase("BigInt.asIntN(8, 128n)", "-128")]
    [TestCase("BigInt.asIntN(8, 255n)", "-1")]
    [TestCase("BigInt.asIntN(8, 256n)", "0")]
    [TestCase("BigInt.asIntN(8, -128n)", "-128")]
    [TestCase("BigInt.asIntN(8, -129n)", "127")]
    [TestCase("BigInt.asIntN(1, 1n)", "-1")]
    [TestCase("BigInt.asIntN(1, -1n)", "-1")]
    [TestCase("BigInt.asIntN(0, 5n)", "0")]
    [TestCase("BigInt.asIntN(0, -5n)", "0")]
    [TestCase("BigInt.asIntN(64, 2n ** 63n)", "-9223372036854775808")]
    [TestCase("BigInt.asIntN(64, -(2n ** 63n) - 1n)", "9223372036854775807")]
    [TestCase("BigInt.asUintN(8, 255n)", "255")]
    [TestCase("BigInt.asUintN(8, 256n)", "0")]
    [TestCase("BigInt.asUintN(8, 257n)", "1")]
    [TestCase("BigInt.asUintN(8, -1n)", "255")]
    [TestCase("BigInt.asUintN(8, -256n)", "0")]
    [TestCase("BigInt.asUintN(8, -257n)", "255")]
    [TestCase("BigInt.asUintN(0, 5n)", "0")]
    [TestCase("BigInt.asUintN(0, -5n)", "0")]
    [TestCase("BigInt.asUintN(64, -1n)", "18446744073709551615")]
    [TestCase("BigInt.asUintN(1000000, -1n).toString(16).length", "250000")]
    public void FixedWidthBigIntOperationsWrapAroundTheWidth(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// A negative value is the one case whose result is as wide as the bit count, so a count past the BigInt size
    /// <c>**</c> enforces is a RangeError the script can catch, never a CLR exception.
    /// </summary>
    [TestCase("BigInt.asUintN(1000001, -1n)")]
    [TestCase("BigInt.asUintN(2 ** 31 - 1, -1n)")]
    [TestCase("BigInt.asUintN(2147483648, -1n)")]
    [TestCase("BigInt.asUintN(2 ** 53 - 1, -1n)")]
    public void FixedWidthBigIntOperationsRejectAResultTooWideToBuild(string expression)
    {
        ThrownErrorName(expression).Should().Be("RangeError");
    }

    /// <summary>
    /// BigInt::leftShift and BigInt::signedRightShift are defined for any count. A count outside Int32 that moves
    /// right leaves only the sign; the CLR cast used to turn every one of them into an OverflowException.
    /// </summary>
    [TestCase("1n >> (2n ** 32n)", "0")]
    [TestCase("-1n >> (2n ** 32n)", "-1")]
    [TestCase("-5n >> (2n ** 64n)", "-1")]
    [TestCase("1n << -(2n ** 32n)", "0")]
    [TestCase("-1n << -(2n ** 32n)", "-1")]
    [TestCase("0n << (2n ** 32n)", "0")]
    [TestCase("0n >> -(2n ** 32n)", "0")]
    [TestCase("1n >> 2147483647n", "0")]
    [TestCase("-1n >> 2147483647n", "-1")]
    [TestCase("5n << -2147483648n", "0")]
    [TestCase("-5n << -2147483648n", "-1")]
    [TestCase("let x = 1n; x >>= 2n ** 32n; x", "0")]
    [TestCase("let x = -1n; x <<= -(2n ** 32n); x", "-1")]
    public void BigIntShiftsByACountOutsideInt32(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The same counts moving a non-zero value left cannot be represented.
    /// </summary>
    [TestCase("1n << (2n ** 32n)")]
    [TestCase("-1n << (2n ** 32n)")]
    [TestCase("1n >> -(2n ** 32n)")]
    [TestCase("1n >> -2147483648n")]
    [TestCase("let x = 1n; x <<= 2n ** 32n")]
    [TestCase("let x = 1n; x >>= -(2n ** 32n)")]
    public void BigIntLeftShiftsByACountOutsideInt32AreARangeError(string expression)
    {
        ThrownErrorName(expression).Should().Be("RangeError");
    }

    /// <summary>
    /// A count inside Int32 that would build a result wider than <see cref="JsBigInt.MaxBitLength"/> bits is the
    /// same RangeError <c>**</c> gives, raised before anything is allocated: <c>1n &lt;&lt; 2147483600n</c> was a CLR
    /// OverflowException on .NET 10 and a 256 MB allocation on the older runtimes.
    /// </summary>
    [TestCase("1n << 2147483600n")]
    [TestCase("1n << 2147483647n")]
    [TestCase("-1n << 2147483647n")]
    [TestCase("1n << 1000000n")]
    [TestCase("(2n ** 64n) << 999937n")]
    [TestCase("1n >> -2147483647n")]
    [TestCase("let x = 1n; x <<= 2147483600n")]
    public void BigIntLeftShiftsPastTheMaximumBigIntSizeAreARangeError(string expression)
    {
        ThrownErrorName(expression).Should().Be("RangeError");
    }

    /// <summary>
    /// Up to that size a left shift is exact, and a zero is a zero however far it is shifted.
    /// </summary>
    [TestCase("(1n << 999999n) >> 999999n", "1")]
    [TestCase("(-3n << 999998n) >> 999998n", "-3")]
    [TestCase("((2n ** 64n) << 999935n) >> 999935n", "18446744073709551616")]
    [TestCase("0n << 2147483647n", "0")]
    [TestCase("0n << (2n ** 64n)", "0")]
    [TestCase("let x = 0n; x <<= 2147483647n; x", "0")]
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
