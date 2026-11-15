using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

/// <summary>
/// CSS Masking 1 §7.9: mask expands into its eight longhands and serializes back in shortest form (#4616).
/// </summary>
/// <remarks>https://drafts.csswg.org/css-masking-1/#the-mask</remarks>
[TestFixture]
public sealed class CssMaskShorthandTests
{
    private static readonly string[] Longhands =
        ["mask-image", "mask-position", "mask-size", "mask-repeat", "mask-origin", "mask-clip", "mask-composite", "mask-mode"];

    private static CssDeclarationBlock Syntax(string text) => CssDeclarationBlock.Parse(text,
        CssDeclarationContext.Style, null, new CssValueWork(default), default);

    private static string Longhand(CssDeclarationBlock block) =>
        string.Join(" | ", Longhands.Select(name => block.GetPropertyValue(name)));

    [TestCase("mask: url(a.svg) no-repeat", "url(a.svg) | 0% 0% | auto | no-repeat | border-box | border-box | add | match-source", "url(a.svg) no-repeat")]
    [TestCase("-webkit-mask: url(a.svg) center / contain", "url(a.svg) | center center | contain | repeat | border-box | border-box | add | match-source", "url(a.svg) center center / contain")]
    [TestCase("mask: luminance exclude no-clip content-box url(a.svg)", "url(a.svg) | 0% 0% | auto | repeat | content-box | no-clip | exclude | luminance", "url(a.svg) content-box no-clip exclude luminance")]
    [TestCase("mask: url(a.svg) no-clip", "url(a.svg) | 0% 0% | auto | repeat | border-box | no-clip | add | match-source", "url(a.svg) no-clip")]
    [TestCase("mask: url(a.svg) border-box padding-box", "url(a.svg) | 0% 0% | auto | repeat | border-box | padding-box | add | match-source", "url(a.svg) border-box padding-box")]
    [TestCase("mask: url(a.svg) fill-box, none repeat no-repeat", "url(a.svg), none | 0% 0%, 0% 0% | auto, auto | repeat, repeat no-repeat | fill-box, border-box | fill-box, border-box | add, add | match-source, match-source", "url(a.svg) fill-box, repeat no-repeat")]
    [TestCase("mask: none", "none | 0% 0% | auto | repeat | border-box | border-box | add | match-source", "none")]
    [TestCase("mask: inherit", "inherit | inherit | inherit | inherit | inherit | inherit | inherit | inherit", "inherit")]
    public void ExpandsIntoEveryLonghandAndSerializesBack(string css, string longhands, string shorthand)
    {
        var block = Syntax(css);
        Longhand(block).Should().Be(longhands);
        block.GetPropertyValue("mask").Should().Be(shorthand);
        block.GetPropertyValue("-webkit-mask").Should().Be(shorthand);
    }

    [TestCase("mask: url(a.svg) url(b.svg)")]
    [TestCase("mask: red")]
    [TestCase("mask: url(a.svg) / cover")]
    [TestCase("mask: center /")]
    [TestCase("mask: alpha luminance")]
    [TestCase("mask: add subtract")]
    [TestCase("mask: border-box padding-box content-box")]
    [TestCase("mask: no-clip no-clip")]
    [TestCase("mask: url(a.svg),")]
    [TestCase("mask: url(a.svg) top 10px")]
    [TestCase("mask: url(a.svg) 10px left")]
    public void InvalidValuesAreDropped(string css)
    {
        var block = Syntax("mask: url(z.svg); " + css);
        block.GetPropertyValue("mask-image").Should().Be("url(z.svg)");
    }

    [Test]
    public void VarDefersExpansionAndKeepsTheShorthandText()
    {
        var block = Syntax("-webkit-mask: var(--icon) no-repeat");
        block.GetPropertyValue("mask").Should().Be("var(--icon) no-repeat");
        block.GetPropertyValue("mask-image").Should().BeEmpty();
        var work = new CssValueWork(default);
        CssDeclarationBlock.PendingLonghandValue("mask", "mask-repeat", "url(a.svg) no-repeat", work).Should().Be("no-repeat");
        CssDeclarationBlock.PendingLonghandValue("mask", "mask-position", "url(a.svg) no-repeat", work).Should().Be("0% 0%");
        CssDeclarationBlock.PendingLonghandValue("mask", "mask-image", "red", work).Should().BeNull();
    }

    [Test]
    public void LegacyLonghandAliasesNameTheUnprefixedProperties()
    {
        var block = Syntax("-webkit-mask-image: url(a.svg); -webkit-mask-size: cover; -webkit-mask-composite: source-over");
        block.GetPropertyValue("mask-image").Should().Be("url(a.svg)");
        block.GetPropertyValue("mask-size").Should().Be("cover");
        block.GetPropertyValue("-webkit-mask-size").Should().Be("cover");
        block.GetPropertyValue("mask-composite").Should().BeEmpty();
        CssPropertyRegistry.NormalizeName("-WebKit-Mask-Clip").Should().Be("mask-clip");
        // At-rule and media names are lowered, never resolved as property aliases.
        CssPropertyRegistry.LowerAscii("-WebKit-Mask").Should().Be("-webkit-mask");
    }

    [Test]
    public void TheShorthandIsEmptyWhenALonghandHoldsWhatItCannotExpress()
    {
        var block = Syntax("mask: url(a.svg); mask-position: 10px");
        block.GetPropertyValue("mask").Should().Be("url(a.svg) 10px");
        block.SetProperty("mask-clip", "text");
        block.GetPropertyValue("mask").Should().BeEmpty();
        block.SetProperty("mask-clip", "content-box");
        block.GetPropertyValue("mask").Should().Be("url(a.svg) 10px border-box content-box");
    }

    [TestCase("0% 0%", "0% 0%")]
    [TestCase("center", "50% 50%")]
    [TestCase("top", "50% 0%")]
    [TestCase("right bottom", "100% 100%")]
    [TestCase("10px 20%", "10px 20%")]
    [TestCase("left 5px top 7px", "5px 7px")]
    [TestCase("right 10px bottom 25%", "calc(100% - 10px) 75%")]
    [TestCase("center, right top", "50% 50%, 100% 0%")]
    [TestCase("right -10px bottom 0px", "calc(100% + 10px) 100%")]
    [TestCase("LEFT Top", "0% 0%")]
    [TestCase("right calc(10px + 1%) top", null)]
    [TestCase("top 10px", null)]
    [TestCase("inherit", null)]
    [TestCase("left left", null)]
    public void ComputedPositionTurnsKeywordsIntoPercentages(string specified, string? computed) =>
        CssBoxShorthands.ComputedPosition(specified, new CssValueWork(default)).Should().Be(computed);
}
