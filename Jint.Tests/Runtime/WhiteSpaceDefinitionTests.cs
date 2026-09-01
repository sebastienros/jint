#nullable enable
using System.Globalization;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the one definition of JavaScript white space against every lane that reads it. The set is built
/// here from the specification's own productions and never from the engine, so a lane that drifts is
/// caught rather than described.
/// </summary>
public class WhiteSpaceDefinitionTests
{
    // Spelled as code points so that no source file has to carry one.
    private const char Tab = (char) 0x0009;
    private const char LineFeed = (char) 0x000A;
    private const char VerticalTab = (char) 0x000B;
    private const char FormFeed = (char) 0x000C;
    private const char CarriageReturn = (char) 0x000D;
    private const char LineSeparator = (char) 0x2028;
    private const char ParagraphSeparator = (char) 0x2029;
    private const char Zwnbsp = (char) 0xFEFF;

    /// <summary>
    /// The union of <c>WhiteSpace</c> (https://tc39.es/ecma262/#sec-white-space) and
    /// <c>LineTerminator</c> (https://tc39.es/ecma262/#sec-line-terminators), which is what
    /// <c>TrimString</c> removes and what <c>StrWhiteSpace</c> admits.
    /// </summary>
    /// <remarks>
    /// The <c>Space_Separator</c> half is read from the running framework's Unicode table rather than
    /// enumerated, so this is an independent statement of the set and not a copy of the engine's list. SP
    /// and NBSP are in that category and need no entry of their own.
    /// </remarks>
    private static bool IsSpecWhiteSpace(char c)
    {
        // WhiteSpace, minus its Space_Separator members: TAB, VT, FF and ZWNBSP.
        if (c == Tab || c == VerticalTab || c == FormFeed || c == Zwnbsp)
        {
            return true;
        }

        // LineTerminator: LF, CR, LS and PS.
        if (c == LineFeed || c == CarriageReturn || c == LineSeparator || c == ParagraphSeparator)
        {
            return true;
        }

        return CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;
    }

    /// <summary>
    /// The premise of the whole file, restated per target framework: <c>char.IsWhiteSpace</c> answers a
    /// Unicode question rather than an ECMAScript one, and the two disagree in both directions.
    /// </summary>
    [Test]
    public void TheFrameworkDisagreesWithTheSpecOnExactlyTwoCharacters()
    {
        var frameworkAddsIt = new List<string>();
        var frameworkDropsIt = new List<string>();

        for (var i = 0; i <= 0xFFFF; i++)
        {
            var c = (char) i;
            var framework = char.IsWhiteSpace(c);
            if (framework == IsSpecWhiteSpace(c))
            {
                continue;
            }

            (framework ? frameworkAddsIt : frameworkDropsIt).Add($"U+{i:X4}");
        }

        frameworkAddsIt.Should().Equal(new[] { "U+0085" }, "NEL is category Cc and is in neither production");
        frameworkDropsIt.Should().Equal(new[] { "U+FEFF" }, "ZWNBSP left the framework's set but is still WhiteSpace");
    }

    /// <summary>
    /// The set is the same twenty-five characters on every target framework, which is what lets one
    /// definition answer alike wherever an embedder loads the engine.
    /// </summary>
    [Test]
    public void TheSpecWhiteSpaceSetIsTheSameOnEveryTargetFramework()
    {
        var members = new List<string>();
        for (var i = 0; i <= 0xFFFF; i++)
        {
            if (IsSpecWhiteSpace((char) i))
            {
                members.Add($"U+{i:X4}");
            }
        }

        members.Should().Equal(new[]
        {
            "U+0009", "U+000A", "U+000B", "U+000C", "U+000D", "U+0020", "U+00A0", "U+1680",
            "U+2000", "U+2001", "U+2002", "U+2003", "U+2004", "U+2005", "U+2006", "U+2007",
            "U+2008", "U+2009", "U+200A", "U+2028", "U+2029", "U+202F", "U+205F", "U+3000",
            "U+FEFF",
        });
    }

    // U+0085 NEXT LINE is category Cc. The parser has always known that; these are the lanes that asked
    // char.IsWhiteSpace instead and so took it for a separator.
    [TestCase("(nel + 'abc').trim() === nel + 'abc'")]
    [TestCase("(nel + 'abc').trimStart() === nel + 'abc'")]
    [TestCase("('abc' + nel).trimEnd() === 'abc' + nel")]
    [TestCase("(nel + 'abc' + nel).trim() === nel + 'abc' + nel")]
    [TestCase("isNaN(parseInt(nel + '12'))")]
    [TestCase("isNaN(parseFloat(nel + '1.5'))")]
    [TestCase("isNaN(Number(nel + '12'))")]
    [TestCase("isNaN(Number('12' + nel))")]
    [TestCase("isNaN(Number(nel))")]
    [TestCase("isNaN(+(nel + '12'))")]
    [TestCase("!/\\s/.test(nel)")]
    public void NextLineIsNotWhiteSpace(string expression)
    {
        Evaluate($"var nel = String.fromCharCode(0x85); {expression}")
            .Should().BeTrue(expression);
    }

    [Test]
    public void NextLineDoesNotSeparateATokenEither()
    {
        Evaluate("""
            var nel = String.fromCharCode(0x85);
            (function () { try { eval('var' + nel + 'x = 1;'); return false; } catch (e) { return e instanceof SyntaxError; } })()
            """).Should().BeTrue("this half of the engine was always right");
    }

    [Test]
    public void NextLineCannotPadABigInt()
    {
        Evaluate("""
            var nel = String.fromCharCode(0x85);
            (function (s) { try { BigInt(s); return false; } catch (e) { return e instanceof SyntaxError; } })(nel + '12')
            """).Should().BeTrue();

        Evaluate("""
            var nel = String.fromCharCode(0x85);
            (function (s) { try { BigInt(s); return false; } catch (e) { return e instanceof SyntaxError; } })(nel)
            """).Should().BeTrue();
    }

    // U+FEFF ZWNBSP is the other direction: it is WhiteSpace to the specification, and has not been white
    // space to the framework since .NET Framework 4.0 on any target framework this suite runs on.
    [TestCase("(bom + 'abc').trim() === 'abc'")]
    [TestCase("('abc' + bom).trimEnd() === 'abc'")]
    [TestCase("parseInt(bom + '12') === 12")]
    [TestCase("parseFloat(bom + '1.5') === 1.5")]
    [TestCase("Number(bom + '12') === 12")]
    [TestCase("Number('12' + bom) === 12")]
    [TestCase("Number(bom) === 0")]
    [TestCase("/\\s/.test(bom)")]
    public void ZeroWidthNoBreakSpaceIsWhiteSpace(string expression)
    {
        Evaluate($"var bom = String.fromCharCode(0xFEFF); {expression}")
            .Should().BeTrue(expression);
    }

    [Test]
    public void ZeroWidthNoBreakSpacePadsABigInt()
    {
        Evaluate("""
            var bom = String.fromCharCode(0xFEFF);
            BigInt(bom + '12') === BigInt(12) && BigInt(bom) === BigInt(0)
            """).Should().BeTrue();
    }

    /// <summary>
    /// Every lane, over the whole Basic Multilingual Plane. <paramref name="grammarExtras"/> holds the
    /// characters an operation legitimately accepts in that position for a reason that is not white space,
    /// and it is deliberately tiny: anything else in it would be a lane disagreeing with the set.
    /// </summary>
    [TestCase("(c + 'x' + c).trim() === 'x'", "", TestName = "String.prototype.trim")]
    [TestCase("(c + 'x').trimStart() === 'x'", "", TestName = "String.prototype.trimStart")]
    [TestCase("('x' + c).trimEnd() === 'x'", "", TestName = "String.prototype.trimEnd")]
    // "+12" and "012" carry the StrDecimalLiteral's own leading sign and leading zero.
    [TestCase("parseInt(c + '12') === 12", "002B,0030", TestName = "parseInt")]
    [TestCase("parseFloat(c + '1.5') === 1.5", "002B,0030", TestName = "parseFloat")]
    [TestCase("Number(c + '12') === 12", "002B,0030", TestName = "Number with a leading pad")]
    // "12." is a StrUnsignedDecimalLiteral with an empty fraction, and it is the only extra left: U+0000 was
    // here too until sebastienros/jint#3541 took the trailing NUL that long.TryParse read as a C string
    // terminator off this lane.
    [TestCase("Number('12' + c) === 12", "002E", TestName = "Number with a trailing pad")]
    // "+12" and "012" carry the StringIntegerLiteral's own leading sign and leading zero, which is the
    // same pair the decimal lanes above admit: StrIntegerLiteral's SignedInteger takes either sign
    // (sebastienros/jint#3540).
    [TestCase("(function () { try { return BigInt(c + '12') === BigInt(12); } catch (e) { return false; } })()", "002B,0030", TestName = "BigInt with a leading pad")]
    [TestCase("(function () { try { return BigInt('12' + c) === BigInt(12); } catch (e) { return false; } })()", "", TestName = "BigInt with a trailing pad")]
    [TestCase("/\\s/.test(c)", "", TestName = "the backslash-s character class")]
    public void EveryLaneAcceptsExactlyTheSpecWhiteSpaceSet(string predicate, string grammarExtras)
    {
        var accepted = AcceptedOverTheWholePlane(predicate);

        var extras = CodePoints(grammarExtras);

        var disagreements = new List<string>();
        for (var i = 0; i <= 0xFFFF; i++)
        {
            var expected = IsSpecWhiteSpace((char) i) || extras.Contains(i);
            var actual = accepted.Contains(i);
            if (actual != expected)
            {
                disagreements.Add($"U+{i:X4} was {(actual ? "accepted" : "rejected")}");
            }
        }

        disagreements.Should().BeEmpty("the lane must accept exactly the specification's white space");
    }

    /// <summary>
    /// The parser's set, which was already right, read through the same sweep, so that the engine holding
    /// one definition is measured rather than asserted. Only white space separates <c>var</c> from the
    /// name it declares.
    /// </summary>
    [Test]
    public void TheParserSeparatesTokensOnTheSameSet()
    {
        var accepted = AcceptedOverTheWholePlane(
            "(function () { try { return eval('var' + c + 'zzz = 1; typeof zzz') === 'number'; } catch (e) { return false; } })()");

        var disagreements = new List<string>();
        for (var i = 0; i <= 0xFFFF; i++)
        {
            if (accepted.Contains(i) != IsSpecWhiteSpace((char) i))
            {
                disagreements.Add($"U+{i:X4}");
            }
        }

        disagreements.Should().BeEmpty();
    }

    private static bool Evaluate(string script) => new Engine().Evaluate(script).AsBoolean();

    private static HashSet<int> CodePoints(string commaSeparatedHex)
    {
        var result = new HashSet<int>();
        if (commaSeparatedHex.Length == 0)
        {
            return result;
        }

        foreach (var part in commaSeparatedHex.Split(','))
        {
            result.Add(int.Parse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        return result;
    }

    private static HashSet<int> AcceptedOverTheWholePlane(string predicate)
    {
        var script = """
            (function () {
                var accepted = [];
                for (var i = 0; i <= 0xFFFF; i++) {
                    var c = String.fromCharCode(i);
                    var ok;
                    try { ok = (PREDICATE); } catch (e) { ok = false; }
                    if (ok === true) { accepted.push(i); }
                }
                return accepted.join(',');
            })()
            """.Replace("PREDICATE", predicate);

        var text = new Engine().Evaluate(script).AsString();

        var accepted = new HashSet<int>();
        if (text.Length > 0)
        {
            foreach (var part in text.Split(','))
            {
                accepted.Add(int.Parse(part, CultureInfo.InvariantCulture));
            }
        }

        return accepted;
    }
}
