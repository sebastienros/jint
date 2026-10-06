#nullable enable
using System.Text.Json;
using System.Globalization;
using Jint.Browser;
using Jint.Browser.Runtime;
using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.Browser.Dom.Views;
using Jint.Constraints;
using Jint.Runtime;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Parsing;

public class XmlParserLimitsTests
{
    // The malformed tail proves that the budget interrupts native parsing: a check
    // only after parsing cannot win over the XML syntax failure.
    private static string UnfinishedXml(int children)
        => "<r>" + string.Concat(Enumerable.Repeat("<x/>", children)) + "<";

    [TestCase("domparser", false)]
    [TestCase("fragment", false)]
    [TestCase("navigation", false)]
    [TestCase("domparser", true)]
    [TestCase("fragment", true)]
    [TestCase("navigation", true)]
    public async Task XmlParsingPollsThePagesBudgetBeforeSyntaxFailure(string path, bool memory)
    {
        var clock = new ParsingClock();
        var source = UnfinishedXml(memory ? 100_000 : 1_250_000);
        void Configure(BrowserOptions options)
        {
            options.MemoryLimit = memory ? 4_000_000 : 0;
            options.MaxTaskDuration = memory ? TestBudgets.WedgeCeiling : TimeSpan.FromSeconds(1);
            if (!memory)
                options.ConfigureEngine(engine => engine
                    .RemoveConstraints(static constraint => constraint is OperationDeadlineConstraint)
                    .AddConstraint(() => new OperationDeadlineConstraint(clock)));
        }

        Exception? error;
        if (path == "navigation")
        {
            await using var loopback = await LoopbackPage.CreateAsync(server => server
                .Map("/", _ => LoopbackResponse.Bytes(source, "text/xml")), configureBrowser: Configure);
            clock.Advancing = true;
            error = await Caught.ExceptionAsync(() => loopback.Page.NavigateAsync(loopback.Url("/")));
        }
        else
        {
            var options = new BrowserOptions();
            Configure(options);
            await using var browser = new global::Jint.Browser.Browser(options);
            var page = await browser.NewPageAsync();
            error = await Caught.ExceptionAsync(() => page.RunOnLoopAsync(engine =>
            {
                var runtime = PageRuntime.Find(engine)!;
                var context = MarkupParser.ParseXml("<r/>").DocumentElement!;
                var parser = new JsDomParser(runtime, engine.Realm.Intrinsics.Object.PrototypeObject);
                clock.Advancing = true;
                // Call the same Browser binding path directly, so no later interpreter
                // check can disguise a parser that failed to poll.
                if (path == "domparser") parser.ParseFromString([source, "text/xml"]);
                else DomFragmentParser.Parse(runtime.Dom, source, context, context);
                return true;
            }));
        }
        if (memory)
        {
            var failure = error.Should().BeOfType<MemoryLimitExceededException>().Subject;
            // A post-parse check would charge all 100,000 nodes (over 12 MB). Polls
            // must stop within a bounded slice of work after the 4 MB budget is spent.
            long.Parse(failure.Message.Split(' ')[3], CultureInfo.InvariantCulture).Should().BeLessThan(8_000_000);
        }
        else error.Should().BeOfType<TimeoutException>();
    }

    [Test]
    public async Task XmlInnerHtmlStopsAtTheMemoryBudgetBeforeReplacingChildren()
    {
        var options = new BrowserOptions { MemoryLimit = 4_000_000, MaxTaskDuration = TestBudgets.WedgeCeiling };
        await using var browser = new global::Jint.Browser.Browser(options);
        var page = await browser.NewPageAsync();
        await page.EvaluateAsync("globalThis.xml = new DOMParser().parseFromString('<r><original/></r>', 'text/xml')");
        var source = UnfinishedXml(100_000);
        await page.RunOnLoopAsync(engine => { engine.SetValue("markup", source); return true; });
        var error = await Caught.ExceptionAsync(() => page.EvaluateAsync("xml.documentElement.innerHTML = markup"));
        error.Should().BeOfType<MemoryLimitExceededException>();
        (await page.EvaluateAsync<string>("xml.documentElement.firstChild.localName")).Should().Be("original");
    }

    private sealed class ParsingClock : TimeProvider
    {
        private long _timestamp;
        internal bool Advancing;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp()
            => Advancing ? Interlocked.Add(ref _timestamp, TimeSpan.TicksPerMillisecond) : Interlocked.Read(ref _timestamp);
    }

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
            configureBrowser: options =>
            {
                options.MemoryLimit = 4_000_000;
                // Isolate the lexical entity ceiling from the now-active parse allocation budget.
                options.ConfigureEngine(engine => engine.LimitMemory(0));
            });
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
