#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // HTML Standard §13.2.6.4.6–7, .18–19 and .21, updated 2026-09-25.
    [TestCase("<frameset><frame src=a></frameset>",
        "<html><head></head><frameset><frame></frame></frameset></html>")]
    [TestCase("<frameset><frameset><frame/></frameset><frame></frameset>",
        "<html><head></head><frameset><frameset><frame></frame></frameset><frame></frame></frameset></html>")]
    [TestCase("<input type=hidden> \t<!--gone--><frameset><frame></frameset>",
        "<html><head></head><frameset><frame></frame></frameset></html>")]
    [TestCase("<input type=hidden><frameset><frame></frameset>",
        "<html><head></head><frameset><frame></frame></frameset></html>")]
    [TestCase("<body>x<frameset><p>y", "<html><head></head><body>x<p>y</p></body></html>")]
    [TestCase("<body><input type=text><frameset><p>y", "<html><head></head><body><input></input><p>y</p></body></html>")]
    [TestCase("<frameset><frame><p>discard</frameset><frame><p>discard", 
        "<html><head></head><frameset><frame></frame></frameset></html>")]
    [TestCase("<frameset><frame></frameset><noframes><script>x</script></noframes>",
        "<html><head></head><frameset><frame></frame></frameset><noframes><script>x</script></noframes></html>")]
    [TestCase("<frameset><frame></frameset> \n<!--tail--><?Tail yes?></html>\t<!--doc--><?Doc yes?>",
        "<html><head></head><frameset><frame></frame></frameset> \n<!--tail--><?Tail yes?>\t</html><!--doc--><?Doc yes?>")]
    public void FramesetModesProduceSpecifiedTrees(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
        }
    }

    [Test]
    public void BodyReplacementRemovesTheActualBodyAndKeepsHeadIdentity()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<head><title>old</title></head><input type=hidden>");
        HtmlParseStep step;
        do step = session.Drive(1, CancellationToken.None);
        while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.NeedInput);
        var html = document.DocumentElement!;
        var head = html.FirstChild!;
        var body = html.LastChild!;
        body.Should().BeOfType<Element>().Which.LocalName.Should().Be("body");
        session.AppendInput("<frameset><frame src=about:blank/></frameset>", isFinal: true);
        do step = session.Drive(1, CancellationToken.None);
        while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.Complete);
        html.FirstChild.Should().BeSameAs(head);
        body.ParentNode.Should().BeNull();
        html.LastChild.Should().BeOfType<Element>().Which.LocalName.Should().Be("frameset");
        ((Element) html.LastChild!.FirstChild!).GetAttribute("src").Should().Be("about:blank");
    }

    [Test]
    public void FramesetTailsPreserveSplitAndQuotaResults()
    {
        const string source = "<frameset><frame/></frameset> \n<?Tail x?></html>\t<!--doc--><noframes><b>raw</b></noframes>";
        const string expected = "<html><head></head><frameset><frame></frame></frameset> \n<?Tail x?>\t<noframes><b>raw</b></noframes></html><!--doc-->";
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            HtmlParseStep step;
            do step = session.Drive(1, CancellationToken.None);
            while (step.Kind == HtmlParseStepKind.Yielded);
            step.Kind.Should().Be(HtmlParseStepKind.NeedInput);
            session.AppendInput(source[split..], isFinal: true);
            do step = session.Drive(1, CancellationToken.None);
            while (step.Kind == HtmlParseStepKind.Yielded);
            step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(document).Should().Be(expected);
        }
    }

    [Test]
    public void ReplacementScanChargesOnceAcrossSmallQuotas()
    {
        static long Work(int count, int quota)
        {
            var source = string.Concat(Enumerable.Repeat("<input type=hidden>", count)) + "<frameset></frameset>";
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be("<html><head></head><frameset></frameset></html>");
            return parsed.Session.WorkCount;
        }

        var shortWork = Work(64, 1);
        var longWork = Work(128, 1);
        Work(128, 100_000).Should().Be(longWork);
        longWork.Should().BeGreaterThan(shortWork);
        longWork.Should().BeLessThan(shortWork * 3);
    }
}
