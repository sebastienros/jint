using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

// CSS Values 4 §§ 5–7 and CSS Conditional 5 § 2.2, Editor's Draft 2026-09-23.
[TestFixture]
public sealed class NumericAtomTests
{
    private static readonly (string Names, CssUnitCategory Category)[] UnitGroups =
    [
        ("px cm mm q in pt pc em rem ex rex cap rcap ch rch ic ric lh rlh vw vh vi vb vmin vmax svw svh svi svb svmin svmax lvw lvh lvi lvb lvmin lvmax dvw dvh dvi dvb dvmin dvmax cqw cqh cqi cqb cqmin cqmax", CssUnitCategory.Length),
        ("deg grad rad turn", CssUnitCategory.Angle),
        ("s ms", CssUnitCategory.Time),
        ("hz khz", CssUnitCategory.Frequency),
        ("dpi dpcm dppx x", CssUnitCategory.Resolution),
        ("fr", CssUnitCategory.Flex)
    ];

    [Test]
    public void EveryFiniteUnitHasOneIdentityAndCorrectCategory()
    {
        var seen = new HashSet<CssUnit>();
        foreach (var (names, category) in UnitGroups)
        {
            foreach (var name in names.Split(' '))
            {
                var atom = Parse("1" + name);
                atom.IsMatch.Should().BeTrue(name);
                atom.Value.Kind.Should().Be(CssNumericKind.Dimension);
                atom.Value.Unit.Category().Should().Be(category, name);
                seen.Add(atom.Value.Unit).Should().BeTrue(name);
                Parse("1" + name.ToUpperInvariant()).Value.Unit.Should().Be(atom.Value.Unit);
            }
        }
        seen.Count.Should().Be(62);
        Enum.GetValues<CssUnit>().Length.Should().Be(63);
        CssUnit.None.Category().Should().Be(CssUnitCategory.None);
    }

    [TestCase("1furlongs")]
    [TestCase("1cqq")]
    [TestCase("1vfoo")]
    [TestCase("1calc")]
    public void UnknownUnitIsNoMatch(string source)
    {
        var atom = Parse(source);
        atom.IsMatch.Should().BeFalse();
        atom.Span.Start.Should().Be(0);
        Assert.Throws<InvalidOperationException>(() => _ = atom.Value);
    }

    [TestCase("1", (int) CssNumericKind.Number, true)]
    [TestCase("1.0", (int) CssNumericKind.Number, false)]
    [TestCase("1e0", (int) CssNumericKind.Number, false)]
    [TestCase("1%", (int) CssNumericKind.Percentage, true)]
    [TestCase("1.0%", (int) CssNumericKind.Percentage, false)]
    [TestCase("1px", (int) CssNumericKind.Dimension, true)]
    [TestCase("1.0px", (int) CssNumericKind.Dimension, false)]
    [TestCase("0", (int) CssNumericKind.Number, true)]
    public void TokenKindAndIntegerFlagRemainLexical(string source, int expectedKind, bool integer)
    {
        var atom = Parse(source).Value;
        var kind = (CssNumericKind) expectedKind;
        atom.Kind.Should().Be(kind);
        atom.IsIntegerToken.Should().Be(integer);
        atom.Number.Spelling.Should().Be(kind == CssNumericKind.Percentage ? source[..^1] :
            kind == CssNumericKind.Dimension ? source[..^2] : source);
        if (kind != CssNumericKind.Dimension) atom.Unit.Should().Be(CssUnit.None);
    }

    [TestCase("calc(1)")]
    [TestCase("1 2")]
    [TestCase("[1]")]
    [TestCase("1px 2px")]
    public void NumericAtomDoesNotClaimExpressionsOrLists(string source) => Parse(source).IsMatch.Should().BeFalse();

    [Test]
    public void FirstInvalidNumericComponentWinsOverTrailingNumber()
    {
        var wrongKind = Parse("foo 1");
        var unknownUnit = Parse("1furlong 2px");
        var trailing = Parse("1px 2px");
        wrongKind.IsMatch.Should().BeFalse();
        wrongKind.Span.Start.Should().Be(0);
        unknownUnit.IsMatch.Should().BeFalse();
        unknownUnit.Span.Start.Should().Be(0);
        trailing.IsMatch.Should().BeFalse();
        trailing.Span.Start.Should().Be(4);
    }

    private static CssPrimitiveResult<CssNumericAtom> Parse(string source) =>
        CssPrimitiveParser.ParseNumericAtom(MarkupParser.ParseCssComponentValues(source), new CssValueWork(default));
}
