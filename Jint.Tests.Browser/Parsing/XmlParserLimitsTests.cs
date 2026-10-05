#nullable enable
using System.Text.Json;
using Jint.Browser;
using Jint.Browser.Runtime;
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

public class XmlParserLimitsTests
{
    private static string HostileXml()
    {
        var source = "<!DOCTYPE r [<!ENTITY e0 'lol'>";
        for (var i = 1; i <= 7; i++)
            source += "<!ENTITY e" + i + " '" + string.Concat(Enumerable.Repeat("&e" + (i - 1) + ";", 10)) + "'>";
        return source + "]><r>&e7;</r>";
    }

    [TestCase(0L, 10_000_000L)]
    [TestCase(long.MaxValue, 10_000_000L)]
    [TestCase(1L, 1L)]
    [TestCase(4_000_000L, 2_000_000L)]
    [TestCase(40_000_000L, 10_000_000L)]
    public void XmlLimitsRespectFiniteMemoryBudgets(long memory, long expansion)
    {
        var limits = BrowserXmlParsing.Options(new BrowserOptions { MemoryLimit = memory }).Limits;
        limits.MaxEntityExpansionCharacters.Should().Be(expansion);
        var input = memory == 0 || memory == long.MaxValue ? 0 : Math.Max(1, memory / 2);
        limits.MaxInputCharacters.Should().Be(input);
        limits.MaxTokenCharacters.Should().Be((int) Math.Min(int.MaxValue, input));
    }

    [TestCase(0L, 10_000_000L)]
    [TestCase(4_000_000L, 2_000_000L)]
    public async Task DomParserRejectsSmallEntityBombAsAResourceFailure(long memory, long expansion)
    {
        var options = new BrowserOptions { MemoryLimit = memory };
        // Isolate the parser's budget-derived ceiling: allocation accounting may otherwise fail first.
        options.ConfigureEngine(engineOptions => engineOptions.LimitMemory(0));
        await using var browser = new global::Jint.Browser.Browser(options);
        var page = await browser.NewPageAsync();
        var source = HostileXml();
        source.Length.Should().BeLessThan(700);
        var error = await Caught.ExceptionAsync(() => page.EvaluateAsync(
            "new DOMParser().parseFromString(" + JsonSerializer.Serialize(source) + ", 'text/xml')"));
        var limit = error.Should().BeOfType<ParseLimitException>().Subject;
        limit.Kind.Should().Be(ParseLimitKind.EntityExpansionCharacters);
        limit.Limit.Should().Be(expansion);
    }

    [Test]
    public async Task XmlNavigationRejectsSmallEntityBombWithoutScript()
    {
        await using var loopback = await LoopbackPage.CreateAsync(server => server
            .Map("/", _ => LoopbackResponse.Bytes(HostileXml(), "text/xml")),
            configureBrowser: options => options.MemoryLimit = 4_000_000);
        var error = await Caught.ExceptionAsync(() => loopback.Page.NavigateAsync(loopback.Url("/")));
        var limit = error.Should().BeOfType<ParseLimitException>().Subject;
        limit.Kind.Should().Be(ParseLimitKind.EntityExpansionCharacters);
        limit.Limit.Should().Be(2_000_000);
    }

    [Test]
    public async Task XmlFragmentInputUsesThePagesMemoryCeiling()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions { MemoryLimit = 4_000_000 });
        var page = await browser.NewPageAsync();
        var markup = new string('x', 2_000_001);
        var error = await Caught.ExceptionAsync(() => page.RunOnLoopAsync(engine =>
        {
            var context = MarkupParser.ParseXml("<r/>").DocumentElement!;
            return DomFragmentParser.Parse(PageRuntime.Find(engine)!.Dom, markup, context, context).ChildCount;
        }));
        var limit = error.Should().BeOfType<ParseLimitException>().Subject;
        limit.Kind.Should().Be(ParseLimitKind.InputCharacters);
        limit.Limit.Should().Be(2_000_000);
    }
}
