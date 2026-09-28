using Jint.HtmlParser;
using Jint.HtmlParser.Css;

namespace Jint.Tests.HtmlParser;

public sealed class LayeredParsingTests
{
    [Test]
    public void HtmlBuildsStructureWithoutInterpretingEmbeddedLanguagesOrInputValues()
    {
        const string css = "@media future(foo) { @scope (???) { x { unknown:invalid() } } }";
        const string script = "this is not valid JavaScript";
        var document = MarkupParser.ParseHtml(
            "<style id=s>" + css + "</style><script id=j>" + script + "</script>" +
            "<input id=i type=date value=not-a-date style='color:invalid()' onclick='not JavaScript' data-selector='???'>");
        var head = document.DocumentElement!.FirstChild!;
        var style = (Element) head.FirstChild!;
        var javascript = (Element) style.NextSibling!;
        var input = (Element) document.DocumentElement.LastChild!.FirstChild!;
        ((Text) style.FirstChild!).Data.Should().Be(css);
        ((Text) javascript.FirstChild!).Data.Should().Be(script);
        input.ExistingInputValueState.Should().BeNull();
        input.GetAttribute("value").Should().Be("not-a-date");
        input.GetAttribute("style").Should().Be("color:invalid()");
        input.GetAttribute("onclick").Should().Be("not JavaScript");
        input.GetAttribute("data-selector").Should().Be("???");
        input.GetHtmlState()!.InputValue!.GetValue(default).Should().BeEmpty();
        input.GetAttribute("value").Should().Be("not-a-date");
    }

    [Test]
    public void XmlAndSvgPreserveEmbeddedTextAndDelayProcessingInstructionAttributes()
    {
        const string source = "<?xml-stylesheet href='style.css' media='future(foo)'?>" +
            "<svg xmlns='http://www.w3.org/2000/svg'><style>@media ??? { p { color:invalid() } }</style>" +
            "<script>not JavaScript</script><path d='not path data' style='fill:invalid()'/></svg>";
        foreach (var document in new[] { MarkupParser.ParseXml(source), MarkupParser.ParseSvg(source) })
        {
            var instruction = (ProcessingInstruction) document.FirstChild!;
            instruction.HasAttributeState.Should().BeFalse();
            var svg = document.DocumentElement!;
            ((Text) svg.FirstChild!.FirstChild!).Data.Should().Be("@media ??? { p { color:invalid() } }");
            ((Text) svg.FirstChild.NextSibling!.FirstChild!).Data.Should().Be("not JavaScript");
            ((Element) svg.LastChild!).GetAttribute("d").Should().Be("not path data");
            instruction.GetAttribute("href").Should().Be("style.css");
            instruction.HasAttributeState.Should().BeTrue();
        }
    }

    [Test]
    public void EveryCssSyntaxEntryRetainsUninterpretedNestedValuesAfterTheParsingLifetimeEnds()
    {
        using var cancellation = new CancellationTokenSource();
        var sheet = MarkupParser.ParseCss("@media future(foo([bar])) { p { color:unknown(1px) } }",
            cancellationToken: cancellation.Token);
        var rule = MarkupParser.ParseCssRule("@media future(foo([bar])) {}", cancellationToken: cancellation.Token);
        var declaration = MarkupParser.ParseCssDeclaration("color:future(foo([bar]))",
            cancellationToken: cancellation.Token);
        var component = MarkupParser.ParseCssComponentValue("future(foo([bar]))", cancellationToken: cancellation.Token);
        var values = MarkupParser.ParseCssComponentValues("future(foo([bar]))", cancellationToken: cancellation.Token);
        cancellation.Cancel();
        foreach (var function in new[]
        {
            sheet.Rules[0].Prelude.Single(value => value.Kind == CssComponentKind.Function),
            rule.Prelude.Single(value => value.Kind == CssComponentKind.Function),
            declaration.Value[0], component, values[0]
        })
        {
            function.FunctionName.Should().Be("future");
            function.Values[0].FunctionName.Should().Be("foo");
            function.Values[0].Values[0].Values[0].Token.Text.Should().Be("bar");
        }
    }
}
