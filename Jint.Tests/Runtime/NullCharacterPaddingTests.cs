#nullable enable
using Jint.Native;

namespace Jint.Tests.Runtime;

/// <summary>
/// U+0000 pads nothing. It is category <c>Cc</c> and in neither <c>WhiteSpace</c> nor <c>LineTerminator</c>,
/// so it may not sit beside a <c>StringNumericLiteral</c> nor inside an array index — but the framework's
/// integer parsers end a number at one and accept whatever follows, the way a C string terminates.
/// </summary>
/// <remarks>
/// The tolerance is the parser's and not a <see cref="System.Globalization.NumberStyles"/> flag's, so it
/// survives every style combination and reaches every lane that hands raw script text to
/// <c>long.TryParse</c>, <c>int.TryParse</c> or <c>uint.TryParse</c> (sebastienros/jint#3541).
/// </remarks>
public class NullCharacterPaddingTests
{
    private static JsValue Evaluate(string script) => new Engine().Evaluate(script);

    private const string Nul = "var nul = String.fromCharCode(0);\n";

    /// <summary>
    /// The whole-number fast path is the one that read a trailing NUL, in every spelling that reaches it:
    /// a sign, an exponent and an empty fraction all land on <c>long.TryParse</c> too.
    /// </summary>
    [TestCase("'12' + nul")]
    [TestCase("'12' + nul + nul")]
    [TestCase("'1' + nul")]
    [TestCase("'0' + nul")]
    [TestCase("'-0' + nul")]
    [TestCase("'-12' + nul")]
    [TestCase("'+12' + nul")]
    [TestCase("'1e3' + nul")]
    [TestCase("'12.' + nul")]
    [TestCase("'12.0' + nul")]
    [TestCase("' 12' + nul")]
    [TestCase("'12' + nul + ' '")]
    public void ANumberStringPaddedWithATrailingNulIsNotANumber(string expression)
    {
        double.IsNaN(Evaluate(Nul + "Number(" + expression + ")").AsNumber()).Should().BeTrue();
    }

    /// <summary>
    /// The lanes that were already right, so that the fix is a lane joining them rather than a new rule.
    /// </summary>
    [TestCase("'1.5' + nul")]
    [TestCase("nul + '12'")]
    [TestCase("nul")]
    [TestCase("'12' + nul + '34'")]
    [TestCase("'0x10' + nul")]
    [TestCase("'Infinity' + nul")]
    public void TheLanesThatAlreadyRejectedANulGoOnRejectingIt(string expression)
    {
        double.IsNaN(Evaluate(Nul + "Number(" + expression + ")").AsNumber()).Should().BeTrue();
    }

    /// <summary>
    /// Every implicit <c>ToNumber</c> reads the same string, so the coercions that go through it answer
    /// <c>NaN</c> too rather than only the explicit call.
    /// </summary>
    [Test]
    public void EveryImplicitToNumberReadsTheSameString()
    {
        Evaluate(Nul + "isNaN(+('12' + nul))").AsBoolean().Should().BeTrue();
        Evaluate(Nul + "('12' + nul) == 12").AsBoolean().Should().BeFalse();
        double.IsNaN(Evaluate(Nul + "('12' + nul) * 1").AsNumber()).Should().BeTrue();
        double.IsNaN(Evaluate(Nul + "Math.abs('12' + nul)").AsNumber()).Should().BeTrue();

        // ToIntegerOrInfinity turns the NaN into 0, so the character read is the first one and not the second.
        Evaluate(Nul + "'abc'.charAt('1' + nul)").AsString().Should().Be("a");
    }

    /// <summary>
    /// What the fast path may still read, unchanged. <c>"12."</c> is a <c>StrUnsignedDecimalLiteral</c> with
    /// an empty fraction and no longer takes the fast path, so its value is stated here rather than assumed.
    /// </summary>
    [Test]
    public void TheNumberStringsThatWereAlreadyRightKeepTheirValues()
    {
        Evaluate("Number('12')").AsNumber().Should().Be(12);
        Evaluate("Number('12.')").AsNumber().Should().Be(12);
        Evaluate("Number('12.0')").AsNumber().Should().Be(12);
        Evaluate("Number('-12')").AsNumber().Should().Be(-12);
        Evaluate("Number('+12')").AsNumber().Should().Be(12);
        Evaluate("Number('1e3')").AsNumber().Should().Be(1000);
        Evaluate("Number('1.5')").AsNumber().Should().Be(1.5);
        Evaluate("Number('  ')").AsNumber().Should().Be(0);
        Evaluate("Number('')").AsNumber().Should().Be(0);

        // -0 keeps its sign, which is the one thing the fast path is there to get right by hand.
        Evaluate("1 / Number('-0')").AsNumber().Should().Be(double.NegativeInfinity);
        Evaluate("1 / Number('-0.0')").AsNumber().Should().Be(double.NegativeInfinity);
        Evaluate("1 / Number('0')").AsNumber().Should().Be(double.PositiveInfinity);
    }

    /// <summary>
    /// <c>parseInt</c> and <c>parseFloat</c> read the longest prefix and discard the rest, so a trailing NUL
    /// was never observable on those lanes and stays unobservable.
    /// </summary>
    [Test]
    public void ThePrefixReadingLanesAreUnaffected()
    {
        Evaluate(Nul + "parseInt('12' + nul)").AsNumber().Should().Be(12);
        Evaluate(Nul + "parseInt('12' + nul + '34')").AsNumber().Should().Be(12);
        Evaluate(Nul + "parseFloat('12' + nul)").AsNumber().Should().Be(12);
        Evaluate(Nul + "parseFloat('1.5' + nul)").AsNumber().Should().Be(1.5);
        Evaluate(Nul + "1 / parseInt('-0' + nul)").AsNumber().Should().Be(double.NegativeInfinity);
        Evaluate(Nul + "isNaN(parseInt(nul + '12'))").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// <c>BigInt</c> scans the characters itself before it parses, so it rejected the NUL already.
    /// </summary>
    [Test]
    public void BigIntGoesOnRefusingANul()
    {
        Evaluate(Nul + "(function () { try { BigInt('12' + nul); return 'no throw'; } catch (e) { return e.name; } })()")
            .AsString().Should().Be("SyntaxError");
    }

    /// <summary>
    /// An array index is the canonical decimal spelling of its value, so nothing may pad it — and the same
    /// framework tolerance made <c>uint.TryParse</c> read one that carried a trailing NUL or trailing white
    /// space as the index it merely resembled.
    /// </summary>
    [TestCase("'1' + nul")]
    [TestCase("'1' + nul + nul")]
    [TestCase("'1 '")]
    [TestCase("'1\\t'")]
    [TestCase("'1 ' + nul")]
    [TestCase("' 1'")]
    [TestCase("'+1'")]
    [TestCase("'01'")]
    public void AKeyThatMerelyResemblesAnArrayIndexIsNotOne(string expression)
    {
        Evaluate(Nul + "[1, 2, 3][" + expression + "] === undefined").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// And writing under such a key adds an ordinary property rather than an element, so neither
    /// <c>length</c> nor the element itself moves.
    /// </summary>
    [Test]
    public void WritingUnderSuchAKeyDoesNotReachTheElements()
    {
        Evaluate(Nul + "(function () { var a = []; a['3' + nul] = 'z'; return a.length; })()")
            .AsNumber().Should().Be(0);
        Evaluate(Nul + "(function () { var a = []; a['3' + nul] = 'z'; return a[3] === undefined; })()")
            .AsBoolean().Should().BeTrue();
        Evaluate(Nul + "(function () { var a = []; a['3' + nul] = 'z'; return Object.keys(a).length === 1 && Object.keys(a)[0].length === 2; })()")
            .AsBoolean().Should().BeTrue();
        Evaluate("(function () { var a = []; a['3 '] = 'z'; return a.length; })()")
            .AsNumber().Should().Be(0);
    }

    /// <summary>
    /// The indices that are canonical go on being indices, which is the half of the guard that has to keep
    /// working.
    /// </summary>
    [Test]
    public void TheCanonicalIndicesAreStillIndices()
    {
        Evaluate("[1, 2, 3]['0'] === 1 && [1, 2, 3]['2'] === 3").AsBoolean().Should().BeTrue();
        Evaluate("(function () { var a = []; a['0'] = 'x'; a['10'] = 'y'; return a.length; })()")
            .AsNumber().Should().Be(11);
        Evaluate("(function () { var a = [1, 2, 3]; return a[1]; })()").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// A wrapped host list reads its positions through the same parse, so a padded key addressed an element
    /// there too.
    /// </summary>
    [Test]
    public void AWrappedHostListReadsTheSameIndices()
    {
        var engine = new Engine();
        engine.SetValue("list", new List<string> { "a", "b", "c" });

        engine.Evaluate("list[1]").AsString().Should().Be("b");
        engine.Evaluate("list['1']").AsString().Should().Be("b");
        engine.Evaluate("list[String.fromCharCode(49, 0)] === undefined").AsBoolean().Should().BeTrue();
        engine.Evaluate("list['1 '] === undefined").AsBoolean().Should().BeTrue();
    }
}
