using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css;

public sealed class RenderlessCssTests
{
    [TestCase(".x{visibility:hidden} #t{visibility:visible}", "", "visible")]
    [TestCase(".x{visibility:hidden} .x{visibility:collapse}", "", "collapse")]
    [TestCase("#t{visibility:hidden!important}", "visibility:visible", "hidden")]
    [TestCase("#t{visibility:hidden!important}", "visibility:visible!important", "visible")]
    [TestCase("@layer a,b; @layer a{#t{visibility:hidden}} @layer b{.x{visibility:visible}}", "", "visible")]
    [TestCase("@layer a,b; @layer a{.x{visibility:hidden!important}} @layer b{#t{visibility:visible!important}}", "", "hidden")]
    [TestCase("@media (min-width:600px){.x{visibility:hidden}} @media print{.x{visibility:collapse}}", "", "hidden")]
    [TestCase("@container (width>0px){.x{visibility:hidden}}", "", "visible")]
    public void CascadePrecedenceSurvivesTheTextBoundary(string css, string inline, string expected)
    {
        var document = MarkupParser.ParseHtml("<div id=t class=x></div>");
        var element = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var work = new CssValueWork(default);
        var query = new NativeCssQuery(document, [new(CssStyleSheet.Parse(css), NativeCssOrigin.Author)],
            [(element, CssDeclarationBlock.Parse(inline))], new CssMediaEnvironment { Width = 800 },
            new(document, null, null, null), work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(element, "visibility", ref matching).Text.Should().Be(expected);
    }

    [TestCase("@keyframes move {from {opacity:0} to {opacity:1}}")]
    [TestCase("@property --x {syntax:'<color>'; inherits:false; initial-value:red}")]
    [TestCase("@container (width > 20px) {div {display:none}}")]
    [TestCase("@page :first {margin:20px}")]
    [TestCase("@counter-style stars {symbols:'*'}")]
    [TestCase("@namespace svg 'http://www.w3.org/2000/svg';")]
    [TestCase("@unknown arbitrary {nested {opaque:calc(1 + 2)}}")]
    public void GenericRulesRoundTripAsOpaqueText(string source)
    {
        var sheet = CssStyleSheet.Parse(source);
        var rule = sheet.Rules.Single();
        rule.Should().BeOfType<CssGenericRule>();
        rule.Type.Should().Be(CssRuleType.Unknown);
        rule.CssText.Should().Be(source);
        rule.Rules.Should().BeEmpty();
        rule.ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.Serialize().Should().Be(source);
        sheet.DeleteRule(0);
        rule.ParentStyleSheet.Should().BeNull();
        sheet.InsertRule(source, 0).Should().Be(0);
        sheet.Rules[0].CssText.Should().Be(source);
    }

    [Test]
    public void FontDescriptorsArePassiveTextAndApiPriorityIsPreserved()
    {
        var block = CssDeclarationBlock.Parse("font-family:A,B;src:url(a) format(unknown);color:red;--x:1",
            CssDeclarationContext.FontFace);
        block.Count.Should().Be(2);
        block.GetPropertyValue("font-family").Should().Be("A,B");
        block.GetPropertyValue("src").Should().Be("url(a) format(unknown)");
        block.SetProperty("font-family", "MiXeD", "important");
        block.GetPropertyPriority("font-family").Should().Be("important");
        block.SetProperty("color", "blue");
        block.Count.Should().Be(2);
    }
}
