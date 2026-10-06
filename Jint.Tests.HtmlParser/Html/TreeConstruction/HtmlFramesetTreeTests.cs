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
    [TestCase("<p></p><frameset><frame></frameset>",
        "<html><head></head><frameset><frame></frame></frameset></html>")]
    [TestCase("<body>x<frameset><p>y", "<html><head></head><body>x<p>y</p></body></html>")]
    [TestCase("<body><input type=text><frameset><p>y", "<html><head></head><body><input></input><p>y</p></body></html>")]
    [TestCase("<frameset><frame><p>discard</frameset><frame><p>discard",
        "<html><head></head><frameset><frame></frame></frameset></html>")]
    [TestCase("<frameset><frame></frameset><noframes><script>x</script></noframes>",
        "<html><head></head><frameset><frame></frame></frameset><noframes><script>x</script></noframes></html>")]
    [TestCase("<frameset><noframes><frame>literal</noframes><frame/></frameset>",
        "<html><head></head><frameset><noframes><frame>literal</noframes><frame></frame></frameset></html>")]
    [TestCase("<frameset><frame></frameset> \n<!--tail--><?Tail yes?></html>\t<!--doc--><?Doc yes?>",
        "<html><head></head><frameset><frame></frame></frameset> \n<!--tail--><?Tail yes?>\t</html><!--doc--><?Doc yes?>")]
    [TestCase("<frameset> \t<!--inner--><?Inner yes?><frame></frameset><!--tail--><?Tail yes?></html><!--doc--><?Doc yes?>",
        "<html><head></head><frameset> \t<!--inner--><?Inner yes?><frame></frame></frameset><!--tail--><?Tail yes?></html><!--doc--><?Doc yes?>")]
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
        session.AppendInput("<frameset><frame src='about:blank'/></frameset>", isFinal: true);
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
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(string.Concat(Enumerable.Repeat("<input type=hidden>", count)));
            DrainToNeedInput(session, 100_000);
            var before = session.WorkCount;
            session.AppendInput("<frameset></frameset>", isFinal: true);
            DrainToCompletion(session, quota);
            Serialize(document).Should().Be("<html><head></head><frameset></frameset></html>");
            return session.WorkCount - before;
        }

        var shortWork = Work(64, 1);
        var longWork = Work(128, 1);
        Work(128, 100_000).Should().Be(longWork);
        longWork.Should().BeGreaterThan(shortWork);
        longWork.Should().BeLessThan(shortWork * 3);
    }

    [Test]
    public void BodyReplacementResumesAcrossEveryInputSplit()
    {
        const string source = "<input type=hidden><p></p><!--gone--><frameset><frame/></frameset>";
        const string expected = "<html><head></head><frameset><frame></frame></frameset></html>";
        for (var split = 0; split <= source.Length; split++)
        {
            foreach (var quota in new[] { 1, 3, 100_000 })
            {
                var document = Document.CreateHtml();
                var session = new HtmlParserSession(document);
                session.AppendInput(source[..split]);
                HtmlParseStep step;
                do step = session.Drive(quota, CancellationToken.None);
                while (step.Kind == HtmlParseStepKind.Yielded);
                step.Kind.Should().Be(HtmlParseStepKind.NeedInput);
                session.AppendInput(source[split..], isFinal: true);
                do step = session.Drive(quota, CancellationToken.None);
                while (step.Kind == HtmlParseStepKind.Yielded);
                step.Kind.Should().Be(HtmlParseStepKind.Complete);
                Serialize(document).Should().Be(expected, $"split {split}, quota {quota}");
            }
        }
    }

    [Test]
    public void FramesetEofAndSelfClosingDiagnosticsFollowTheirOwnBranches()
    {
        var open = Parse("<frameset><frame/>", 1);
        open.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        open.Diagnostics.Items.Should().Contain(item => item.Code == "html/tree-eof-in-frameset");
        open.Diagnostics.Items.Should().NotContain(item => item.Code == "html/tree-unacknowledged-self-closing-flag");

        var closed = Parse("<frameset/><frame/></frameset>", 1);
        closed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        closed.Diagnostics.Items.Count(item => item.Code == "html/tree-unacknowledged-self-closing-flag")
            .Should().Be(1);
        closed.Diagnostics.Items.Should().NotContain(item => item.Code == "html/tree-eof-in-frameset");

        var ignored = Parse("<body>x<frameset><frame/>", 1);
        ignored.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(ignored.Document).Should().Be("<html><head></head><body>x</body></html>");
        ignored.Document.DocumentElement!.LastChild!.ChildCount.Should().Be(1);
    }

    [Test]
    public void CancellationDuringReplacementScanPreservesConnectedBodyAndEndsSession()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput(string.Concat(Enumerable.Repeat("<input type=hidden>", 64)));
        HtmlParseStep step;
        do step = session.Drive(1, CancellationToken.None);
        while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.NeedInput);
        var body = document.DocumentElement!.LastChild!;
        session.AppendInput("<frameset>", isFinal: true);
        var builder = typeof(HtmlParserSession).GetField("_builder",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
        var stageField = typeof(HtmlTreeBuilder).GetField("_framesetReplacementStage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var scanField = typeof(HtmlTreeBuilder).GetField("_framesetScanNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        for (var turn = 0; turn < 100_000; turn++)
        {
            step = session.Drive(1, CancellationToken.None);
            if ((int) stageField.GetValue(builder)! == 1 &&
                !ReferenceEquals(scanField.GetValue(builder), body)) break;
            if (turn == 99_999) throw new InvalidOperationException("Replacement scan did not start.");
        }
        body.ParentNode.Should().BeSameAs(document.DocumentElement);
        var before = Serialize(document);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, canceled.Token));
        Serialize(document).Should().Be(before);
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, CancellationToken.None));
    }

    [Test]
    public void BodyRemovalAndStackCleanupRemainCoherentAcrossYields()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<p><input type=hidden>");
        DrainToNeedInput(session, 1);
        var html = document.DocumentElement!;
        var body = html.LastChild!;
        session.AppendInput("<frameset><frame/></frameset>", isFinal: true);
        var builder = BuilderOf(session);
        var stageField = typeof(HtmlTreeBuilder).GetField("_framesetReplacementStage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var openField = typeof(HtmlTreeBuilder).GetField("_open",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var sawCleanup = false;
        for (var turn = 0; turn < 100_000; turn++)
        {
            var step = session.Drive(1, CancellationToken.None);
            if ((int) stageField.GetValue(builder)! == 3)
            {
                sawCleanup = true;
                body.ParentNode.Should().BeNull();
                html.LastChild!.Should().BeSameAs(html.FirstChild);
                var open = (System.Collections.IList) openField.GetValue(builder)!;
                open.Count.Should().BeGreaterThan(0);
                open[0].Should().BeSameAs(html);
            }
            if (step.Kind == HtmlParseStepKind.Complete) break;
            step.Kind.Should().Be(HtmlParseStepKind.Yielded);
            if (turn == 99_999) throw new InvalidOperationException("Replacement cleanup did not finish.");
        }
        sawCleanup.Should().BeTrue();
        Serialize(document).Should().Be("<html><head></head><frameset><frame></frame></frameset></html>");
    }
}
