#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>StringToBigInt</c> reads a <c>StringIntegerLiteral</c>, and a leading sign is part of that grammar
/// on the decimal spelling and on no other.
/// </summary>
/// <remarks>
/// <para>
/// <see href="https://tc39.es/ecma262/#sec-stringtobigint">StringToBigInt</see> parses its argument as
/// <c>StringIntegerLiteral</c>, whose <c>StrIntegerLiteral</c> is either a <c>SignedInteger</c> —
/// <c>DecimalDigits</c>, <c>+ DecimalDigits</c> or <c>- DecimalDigits</c> — or a
/// <c>NonDecimalIntegerLiteral</c>, which takes no sign at all. Jint accepted only the minus, so
/// <c>BigInt('+12')</c> was a <c>SyntaxError</c> where every other engine answers <c>12n</c>
/// (<see href="https://github.com/sebastienros/jint/issues/3540">#3540</see>).
/// </para>
/// <para>
/// The two halves are tested together on purpose: the sign has to start being accepted on the decimal
/// spelling and has to keep being rejected on the hexadecimal, octal and binary ones, and a fix that
/// only did the first half would look right from the issue alone.
/// </para>
/// </remarks>
public class StringToBigIntTests
{
    /// <summary>
    /// <c>SignedInteger ::: + DecimalDigits</c>, which is the production the issue is about.
    /// </summary>
    [TestCase("BigInt('+12')", "12")]
    [TestCase("BigInt('-12')", "-12")]
    [TestCase("BigInt('12')", "12")]
    [TestCase("BigInt('+0')", "0")]
    [TestCase("BigInt('-0')", "0")]
    [TestCase("BigInt('+007')", "7")]
    [TestCase("BigInt('+9007199254740993')", "9007199254740993")]
    public void ASignedDecimalStringIsTheBigIntItNames(string expression, string expected)
    {
        new Engine().Evaluate(expression).ToString().Should().Be(expected);
    }

    /// <summary>
    /// The value is exact past what a Number could hold, because nothing on this path is a double.
    /// </summary>
    [Test]
    public void ASignedDecimalStringWiderThanANumberIsExact()
    {
        var digits = new string('9', 40);

        new Engine().Evaluate($"BigInt('+{digits}') === BigInt('{digits}')").AsBoolean().Should().BeTrue();
        new Engine().Evaluate($"BigInt('+{digits}').toString()").ToString().Should().Be(digits);
        new Engine().Evaluate($"BigInt('-{digits}').toString()").ToString().Should().Be("-" + digits);
    }

    /// <summary>
    /// <c>StringIntegerLiteral</c> is <c>StrWhiteSpace</c>-padded on both sides, and the sign sits inside
    /// the padding rather than outside it.
    /// </summary>
    [TestCase("' +12 '")]
    [TestCase("String.fromCharCode(0x09) + '+12' + String.fromCharCode(0x0A)")]
    [TestCase("String.fromCharCode(0xFEFF) + '+12'")]
    [TestCase("String.fromCharCode(0x00A0) + '+12' + String.fromCharCode(0x2028)")]
    public void WhiteSpaceMayPadASignedDecimalString(string argument)
    {
        new Engine().Evaluate($"BigInt({argument}) === 12n").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>NonDecimalIntegerLiteral</c> has no sign in its grammar, so a signed one is not a literal.
    /// </summary>
    [TestCase("'+0x10'")]
    [TestCase("'-0x10'")]
    [TestCase("'+0X10'")]
    [TestCase("'+0b11'")]
    [TestCase("'-0b11'")]
    [TestCase("'+0o17'")]
    [TestCase("'-0o17'")]
    public void ASignedNonDecimalStringIsNotALiteral(string argument)
    {
        ShouldBeASyntaxError(argument);
    }

    /// <summary>
    /// A sign that stands alone, doubles, or is separated from its digits is not a
    /// <c>SignedInteger</c> either — nor is a decimal spelling carrying a fraction or an exponent, which
    /// <c>StrIntegerLiteral</c> does not have.
    /// </summary>
    [TestCase("'+'")]
    [TestCase("'-'")]
    [TestCase("'++12'")]
    [TestCase("'+-12'")]
    [TestCase("'--12'")]
    [TestCase("'+ 12'")]
    [TestCase("'12+'")]
    [TestCase("'12-'")]
    [TestCase("'+12.5'")]
    [TestCase("'+12.'")]
    [TestCase("'+1e3'")]
    [TestCase("'+Infinity'")]
    [TestCase("'+ '")]
    public void ASignWithoutDecimalDigitsIsASyntaxError(string argument)
    {
        ShouldBeASyntaxError(argument);
    }

    /// <summary>
    /// Loose equality against a string runs the same conversion, in both operand orders
    /// (<see href="https://tc39.es/ecma262/#sec-islooselyequal">IsLooselyEqual</see> steps 6 and 7).
    /// </summary>
    [TestCase("12n == '+12'", true)]
    [TestCase("'+12' == 12n", true)]
    [TestCase("-12n == '-12'", true)]
    [TestCase("12n == '+13'", false)]
    [TestCase("12n == '+0x0c'", false)]
    [TestCase("0n == '+0'", true)]
    public void LooseEqualityReadsTheSignToo(string expression, bool expected)
    {
        new Engine().Evaluate(expression).AsBoolean().Should().Be(expected);
    }

    /// <summary>
    /// So does the relational comparison, which answers <c>false</c> for a string that is not a literal
    /// and has to stop doing that for one that is.
    /// </summary>
    [TestCase("12n < '+13'", true)]
    [TestCase("12n < '+11'", false)]
    [TestCase("'+11' < 12n", true)]
    [TestCase("'+13' < 12n", false)]
    [TestCase("12n < '+0x1f'", false)]
    public void RelationalComparisonReadsTheSignToo(string expression, bool expected)
    {
        new Engine().Evaluate(expression).AsBoolean().Should().Be(expected);
    }

    /// <summary>
    /// The sibling lane already read the sign, and the two now agree wherever both accept the text.
    /// </summary>
    [TestCase("+12")]
    [TestCase("-12")]
    [TestCase("+0")]
    [TestCase("+123456789")]
    public void TheNumberLaneAndTheBigIntLaneReadTheSameSign(string text)
    {
        new Engine().Evaluate($"BigInt('{text}') === BigInt(Number('{text}'))").AsBoolean().Should().BeTrue();
    }

    private static void ShouldBeASyntaxError(string argument)
    {
        var engine = new Engine();

        var exception = Invoking(() => engine.Evaluate($"BigInt({argument})"))
            .Should().ThrowExactly<JavaScriptException>().Which;

        exception.Error.InstanceofOperator(engine.Intrinsics.SyntaxError).Should().BeTrue(
            $"BigInt({argument}) is not a StringIntegerLiteral");
    }
}
