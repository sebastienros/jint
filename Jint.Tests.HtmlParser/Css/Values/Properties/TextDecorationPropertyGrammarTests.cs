#nullable enable

using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class TextDecorationPropertyGrammarTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    [TestCase(11)]
    [TestCase(12)]
    [TestCase(13)]
    [TestCase(14)]
    [TestCase(15)]
    public void AllNonemptyLineSubsetsCanonicalizeInGrammarOrder(int flags)
    {
        var keywords = new[] { "underline", "overline", "line-through", "blink" };
        var lines = keywords.Where((_, index) => (flags & (1 << index)) != 0).ToArray();
        var expected = string.Join(" ", lines);
        var source = string.Join(" ", Enumerable.Reverse(lines));
        foreach (var context in new[] { CssDeclarationContext.Style, CssDeclarationContext.Keyframe })
        {
            var parsed = CssPropertyParser.Parse("text-decoration-line", source, context);
            parsed.Status.Should().Be(CssPropertyStatus.Valid);
            parsed.Value.Serialize().Should().Be(expected);
        }
    }

    [TestCase("none")]
    [TestCase("spelling-error")]
    [TestCase("grammar-error")]
    public void ExclusiveLineValuesAreRetained(string source) =>
        CssPropertyParser.Parse("text-decoration-line", source).Value.Serialize().Should().Be(source);

    [TestCase("underline underline")]
    [TestCase("none underline")]
    [TestCase("spelling-error overline")]
    [TestCase("grammar-error spelling-error")]
    [TestCase("blink none")]
    public void DuplicateAndExclusiveLineCombinationsAreInvalid(string source) =>
        CssPropertyParser.Parse("text-decoration-line", source).Status.Should().Be(CssPropertyStatus.Invalid);

    [TestCase("solid")]
    [TestCase("double")]
    [TestCase("dotted")]
    [TestCase("dashed")]
    [TestCase("wavy")]
    public void StylesHaveCanonicalKeywordValues(string source) =>
        CssPropertyParser.Parse("text-decoration-style", source.ToUpperInvariant()).Value.Serialize().Should().Be(source);

    [TestCase("-2px", "-2px")]
    [TestCase("-25%", "-25%")]
    [TestCase("0", "0px")]
    [TestCase("calc(-2px)", "calc(-2px)")]
    [TestCase("2em", "2em")]
    [TestCase("3rem", "3rem")]
    [TestCase("auto", "auto")]
    [TestCase("from-font", "from-font")]
    [TestCase("hairline", "hairline")]
    [TestCase("thin", "thin")]
    [TestCase("medium", "medium")]
    [TestCase("thick", "thick")]
    public void ThicknessUsesTheSignedLengthPercentageAndLineWidthProductions(string source, string expected) =>
        CssPropertyParser.Parse("text-decoration-thickness", source).Value.Serialize().Should().Be(expected);

    [TestCase("wavy red 2px underline overline", "underline overline 2px wavy red")]
    [TestCase("overline underline red wavy 2px", "underline overline 2px wavy red")]
    [TestCase("red underline overline 2px wavy", "underline overline 2px wavy red")]
    [TestCase("underline", "underline auto solid currentcolor")]
    [TestCase("red", "none auto solid red")]
    [TestCase("rgb(1, 2, 3) underline", "underline auto solid rgb(1, 2, 3)")]
    public void AnyOrderShorthandGroupsHaveOneCanonicalOrder(string source, string expected)
    {
        var value = CssPropertyParser.Parse("text-decoration", source).Value;
        value.Serialize().Should().Be(expected);
        value.Components.Should().HaveCount(4);
    }

    [Test]
    public void EveryGroupPermutationKeepsTheLineGroupContiguous()
    {
        var groups = new[] { "overline underline", "-2px", "wavy", "red" };
        for (var a = 0; a < 4; a++)
        for (var b = 0; b < 4; b++)
        for (var c = 0; c < 4; c++)
        for (var d = 0; d < 4; d++)
        {
            if (a == b || a == c || a == d || b == c || b == d || c == d) continue;
            var source = string.Join(" ", groups[a], groups[b], groups[c], groups[d]);
            CssPropertyParser.Parse("text-decoration", source).Value.Serialize()
                .Should().Be("underline overline -2px wavy red");
        }
    }

    [TestCase("underline red overline")]
    [TestCase("none red underline")]
    [TestCase("underline solid overline")]
    [TestCase("solid dashed")]
    [TestCase("1px 2px")]
    [TestCase("red blue")]
    [TestCase("underline bogus")]
    [TestCase("underline / 2px")]
    public void SplitOrRepeatedGroupsAreInvalid(string source) =>
        CssPropertyParser.Parse("text-decoration", source).Status.Should().Be(CssPropertyStatus.Invalid);

    [Test]
    public void UncompletedColorGrammarKeepsItsNamedBoundary()
    {
        var parsed = CssPropertyParser.Parse("text-decoration", "underline lab(50% 0 0)");
        parsed.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        parsed.Blocker.Should().Be("color:lab");
    }
}
