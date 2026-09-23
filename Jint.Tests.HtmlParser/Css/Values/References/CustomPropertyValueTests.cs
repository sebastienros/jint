#nullable enable
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class CustomPropertyValueTests
{
    private static CssCustomPropertyResult Parse(string name, string source) =>
        CssReferenceParser.ParseCustomProperty(name, CssReferenceInput.Parse(source, null, default),
            new CssValueWork(default));

    [Test]
    public void EmptyAndCommentOnlyValuesRetainTheirEntireSource()
    {
        Parse("--empty", "").Kind.Should().Be(CssCustomPropertyKind.Value);
        var result = Parse("--notes", " /* before */ /**/ ");
        result.Kind.Should().Be(CssCustomPropertyKind.Value);
        result.Value.Input.Source.Should().Be(" /* before */ /**/ ");
        result.Value.Program.Count.Should().Be(0);
    }

    [TestCase("initial", CssWideKeyword.Initial)]
    [TestCase(" /**/ iNiTiAl /**/ ", CssWideKeyword.Initial)]
    [TestCase("revert-layer", CssWideKeyword.RevertLayer)]
    public void WholeValueWideKeywordsAreCascadeInstructions(string source, CssWideKeyword keyword)
    {
        var result = Parse("--x", source);
        result.Kind.Should().Be(CssCustomPropertyKind.WideKeyword);
        result.WideKeyword.Should().Be(keyword);
        result.Input.Source.Should().Be(source);
    }

    [Test]
    public void SubstringsAndFallbackKeywordsRemainValues()
    {
        Parse("--x", "initial red").Kind.Should().Be(CssCustomPropertyKind.Value);
        Parse("--x", "var(--y, initial)").Kind.Should().Be(CssCustomPropertyKind.Value);
    }

    [Test]
    public void UnicodeNameIsKeptExactly()
    {
        Parse("--é", "red").Value.DecodedName.Should().Be("--é");
        Parse("--e\u0301", "red").Value.DecodedName.Should().Be("--e\u0301");
    }

    [Test]
    public void InvalidNameIsProgrammerErrorAndSyntaxFailureIsAResult()
    {
        Assert.Throws<ArgumentException>(() => Parse("--", "red"));
        Assert.Throws<ArgumentException>(() => Parse("color", "red"));
        Parse("--x", "var()").Kind.Should().Be(CssCustomPropertyKind.InvalidSyntax);
        Parse("--x", "attr(data-x)").Kind.Should().Be(CssCustomPropertyKind.PendingFeature);
    }
}
