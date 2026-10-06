using Jint.HtmlParser.Css.Model;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssInvalidDeclarationTests
{
    [TestCase("display", "none", "bogus")]
    [TestCase("display", "none", "blocK")]
    [TestCase("display", "none", "block block")]
    [TestCase("display", "none", "flex list-item")]
    [TestCase("display", "none", "'none'")]
    [TestCase("visibility", "hidden", "bogus")]
    [TestCase("position", "absolute", "bogus")]
    [TestCase("box-sizing", "border-box", "bogus")]
    [TestCase("width", "24px", "bogus")]
    [TestCase("width", "24px", "12")]
    [TestCase("width", "24px", "-12px")]
    [TestCase("height", "24px", "12seconds")]
    [TestCase("padding-left", "3px", "auto")]
    [TestCase("padding", "3px", "1px -2px")]
    [TestCase("margin", "3px", "1px 2px 3px 4px 5px")]
    [TestCase("font-size", "18px", "auto")]
    [TestCase("line-height", "2", "-1")]
    [TestCase("flex", "2 1 20px", "1 2 3 4")]
    [TestCase("flex-flow", "column wrap", "row column")]
    [TestCase("flex-grow", "2", "-1")]
    [TestCase("gap", "4px", "auto")]
    [TestCase("overflow", "hidden", "bogus")]
    [TestCase("border-width", "2px", "2%")]
    [TestCase("border-style", "solid", "bogus")]
    [TestCase("align-items", "center", "bogus")]
    [TestCase("justify-content", "space-between", "bogus")]
    [TestCase("white-space", "pre", "bogus")]
    [TestCase("white-space-collapse", "preserve", "bogus")]
    [TestCase("z-index", "2", "1.5")]
    public void InvalidDeclarationsAndCssomWritesPreserveEarlierValidValues(string name, string valid, string invalid)
    {
        var block = CssDeclarationBlock.Parse($"{name}:{valid};{name}:{invalid}!important");
        block.GetPropertyValue(name).Should().Be(valid);
        block.GetPropertyPriority(name).Should().BeEmpty();
        block.SetProperty(name, invalid, "important");
        block.GetPropertyValue(name).Should().Be(valid);
        block.GetPropertyPriority(name).Should().BeEmpty();
        var sheet = CssStyleSheet.Parse($"div{{{name}:{valid};{name}:{invalid}}}");
        ((CssStyleRule) sheet.Rules[0]).Style.GetPropertyValue(name).Should().Be(valid);
    }

    [TestCase("display", "inline flex")]
    [TestCase("display", "block flow-root list-item")]
    [TestCase("display", "ruby-base-container")]
    [TestCase("width", "20ch")]
    [TestCase("width", "10cqw")]
    [TestCase("width", "fit-content(50%)")]
    [TestCase("width", "calc(100% - 10px)")]
    [TestCase("width", "round(up, 50%, 1px)")]
    [TestCase("width", "env(safe-area-inset-left)")]
    [TestCase("flex-grow", "calc(1 + 2)")]
    [TestCase("overflow", "overlay")]
    [TestCase("display", "-webkit-box")]
    [TestCase("width", "clamp(10px, 50%, 100px)")]
    [TestCase("margin", "-1px auto")]
    [TestCase("font-size", "medium")]
    [TestCase("font-size", "smaller")]
    [TestCase("line-height", "1.2")]
    [TestCase("white-space", "preserve nowrap")]
    [TestCase("justify-items", "legacy center")]
    [TestCase("align-items", "safe center")]
    [TestCase("flex", "1 0 fit-content(50%)")]
    [TestCase("flex", "1 0 calc(50% - 1px)")]
    [TestCase("align-items", "anchor-center")]
    public void SupportedKeywordLengthAndFunctionTextSurvives(string name, string text)
    {
        var block = CssDeclarationBlock.Parse($"{name}:{text}");
        block.GetPropertyValue(name).Should().Be(text);
    }

    [TestCase("initial")]
    [TestCase("inherit")]
    [TestCase("unset")]
    [TestCase("revert")]
    [TestCase("revert-layer")]
    [TestCase("revert-rule")]
    public void WideKeywordsRemainAccepted(string keyword)
    {
        var block = CssDeclarationBlock.Parse($"display:none;display:{keyword};padding:2px;padding:{keyword}");
        block.GetPropertyValue("display").Should().Be(keyword);
        block.GetPropertyValue("padding").Should().Be(keyword);
    }

    [Test]
    public void KeywordEscapesCaseCommentsAndCustomTextKeepTheirDistinctSemantics()
    {
        var block = CssDeclarationBlock.Parse(@"display:block;display:N\4f NE/**/;visibility:HIDDEN;--display:bogus;--case:INITIAL;--empty:;padding:INITIAL");
        block.GetPropertyValue("display").Should().Be("none");
        block.GetPropertyValue("visibility").Should().Be("hidden");
        block.GetPropertyValue("padding").Should().Be("initial");
        block.GetPropertyValue("--display").Should().Be("bogus");
        block.GetPropertyValue("--case").Should().Be("INITIAL");
        block.GetPropertyValue("--empty").Should().Be(" ");
        block.SetProperty("display", @"N\4f NE");
        block.GetPropertyValue("display").Should().Be("none");
    }
}
