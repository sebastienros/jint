#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // HTML Standard §13.2.4.3 and §13.2.6.4.7, revision 2026-09-22.
    // Comparison corpus pin: html5lib/html5lib-tests 9329e64694e7835d0dcff9811e22856ef6ad16f9.
    [TestCase("<p><b>x</p>y", "<html><head></head><body><p><b>x</b></p><b>y</b></body></html>")]
    [TestCase("<p><b>x</p> <i>y", "<html><head></head><body><p><b>x</b></p><b> <i>y</i></b></body></html>")]
    [TestCase("<p><b>x</p><img>y", "<html><head></head><body><p><b>x</b></p><b><img></img>y</b></body></html>")]
    [TestCase("<p><b>x</p><button>y", "<html><head></head><body><p><b>x</b></p><b><button>y</button></b></body></html>")]
    [TestCase("<p><b>x</p><plaintext>y", "<html><head></head><body><p><b>x</b></p><plaintext><b>y</b></plaintext></body></html>")]
    [TestCase("<p><b>x</p></body> ", "<html><head></head><body><p><b>x</b></p><b> </b></body></html>")]
    [TestCase("<p><b>x</p><table><tr><td>y</td></tr></table>z",
        "<html><head></head><body><p><b>x</b></p><table><tbody><tr><td>y</td></tr></tbody></table><b>z</b></body></html>")]
    [TestCase("<p><b>x</p><table> A<tr><td>y</td></tr></table>",
        "<html><head></head><body><p><b>x</b></p><b> A</b><table><tbody><tr><td>y</td></tr></tbody></table></body></html>")]
    [TestCase("<p><b>x</p><applet>y</applet>z",
        "<html><head></head><body><p><b>x</b></p><b><applet>y</applet>z</b></body></html>")]
    [TestCase("<p><b>x</p><marquee>y</marquee>z",
        "<html><head></head><body><p><b>x</b></p><b><marquee>y</marquee>z</b></body></html>")]
    [TestCase("<table><tr><td><b>x</td><td>y</td></tr></table>",
        "<html><head></head><body><table><tbody><tr><td><b>x</b></td><td>y</td></tr></tbody></table></body></html>")]
    [TestCase("<table><caption><b>x</caption>y</table>",
        "<html><head></head><body>y<table><caption><b>x</b></caption></table></body></html>")]
    public void ReconstructionUsesOriginalEntryAndInsertionMode(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
        }
    }

    [Test]
    public void ReconstructionKeepsCreationAttributesAndChangesElementIdentity()
    {
        var parsed = Parse("<p><b id=original data-x=one>x</p>y", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = (Element) parsed.Document.DocumentElement!.LastChild!;
        var original = (Element) body.FirstChild!.FirstChild!;
        var recreated = (Element) body.LastChild!;
        recreated.Should().NotBeSameAs(original);
        recreated.GetAttribute("id").Should().Be("original");
        recreated.GetAttribute("data-x").Should().Be("one");
        recreated.Attributes.Select(attribute => attribute.Name).Should().Equal("id", "data-x");
    }

    [Test]
    public void NoahsArkComparesUnorderedCreationAttributesWithinEachMarker()
    {
        var equivalent = Parse("<b a=1 b=2><b b=2 a=1><b a=1 b=2><b b=2 a=1>", 1);
        equivalent.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        FormattingCount(equivalent.Session).Should().Be(3);

        var different = Parse("<b a=1 b=2><b b=2 a=1><b a=1 b=3><b b=2 a=1>", 1);
        different.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        FormattingCount(different.Session).Should().Be(4);

        var marked = Parse("<b a=1><object><b a=1><b a=1><b a=1></object><b a=1>", 1);
        marked.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        FormattingCount(marked.Session).Should().Be(2);
    }

    [Test]
    public void ShortSplitsAndQuotaOneReconstructionDoNotDuplicateNodes()
    {
        const string source = "<p><b class=x>x</p>y<img>z";
        const string expected = "<html><head></head><body><p><b>x</b></p><b>y<img></img>z</b></body></html>";
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            DrainToNeedInput(session);
            session.AppendInput(source[split..], isFinal: true);
            DrainToCompletion(session);
            Serialize(document).Should().Be(expected, $"split {split}");
            var body = (Element) document.DocumentElement!.LastChild!;
            body.ChildCount.Should().Be(2);
        }
    }

    [Test]
    public void MixedPendingTableTextReconstructsBeforeTheFirstFosteredCharacterAcrossSplits()
    {
        const string source = "<p><b>x</p><table> A&amp;B<tr><td>y</td></tr></table>";
        const string expected = "<html><head></head><body><p><b>x</b></p><b> A&B</b><table><tbody><tr><td>y</td></tr></tbody></table></body></html>";
        for (var split = 0; split <= source.Length; split++)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput(source[..split]);
            DrainToNeedInput(session);
            session.AppendInput(source[split..], isFinal: true);
            DrainToCompletion(session);
            Serialize(document).Should().Be(expected, $"split {split}");
        }
    }

    [Test]
    public void DeepReconstructionResumesEveryReplacementUnderQuotaOne()
    {
        var source = "<p>" + string.Concat(Enumerable.Range(0, 24).Select(i => $"<b id={i}>")) + "x</p>y";
        var quotaOne = Parse(source, 1);
        var large = Parse(source, 100_000);
        quotaOne.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(quotaOne.Document).Should().Be(Serialize(large.Document));
        var body = (Element) quotaOne.Document.DocumentElement!.LastChild!;
        body.ChildCount.Should().Be(2);
        var node = body.LastChild!;
        for (var i = 0; i < 24; i++)
        {
            var b = (Element) node;
            b.LocalName.Should().Be("b");
            b.GetAttribute("id").Should().Be(i.ToString());
            node = b.FirstChild!;
        }
        ((Text) node).Data.Should().Be("y");
    }

    [Test]
    public void ReconstructionChecksDepthBeforeCreatingAReplacement()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, new HtmlParseOptions
        {
            Limits = new ParseLimits { MaxNestingDepth = 4 }
        });
        session.AppendInput("<p><b>x</p><div><div>y", isFinal: true);
        var failure = Assert.Throws<ParseLimitException>(() => DrainToCompletion(session));
        failure!.Kind.Should().Be(ParseLimitKind.NestingDepth);
        Serialize(document).Should().Be(
            "<html><head></head><body><p><b>x</b></p><div><div></div></div></body></html>");
    }

    [Test]
    public void CancellationDuringBackwardReconstructionLeavesTheCommittedTreeIntact()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        var prefix = "<p>" + string.Concat(Enumerable.Range(0, 24).Select(i => $"<b id={i}>")) + "x</p>";
        session.AppendInput(prefix);
        DrainToNeedInput(session);
        var before = Serialize(document);
        session.AppendInput("y", isFinal: true);
        var builder = BuilderOf(session);
        var nodeField = typeof(HtmlTreeBuilder).GetField("_reconstructionNode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        for (var turn = 0; turn < 100_000; turn++)
        {
            session.Drive(1, CancellationToken.None);
            if (nodeField.GetValue(builder) is not null) break;
            if (turn == 99_999) throw new InvalidOperationException("Reconstruction scan did not start.");
        }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, cancelled.Token));
        Serialize(document).Should().Be(before);
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, CancellationToken.None));
    }

    [Test]
    public void DistinctFormattingFamiliesHaveLinearCountedWork()
    {
        static long Work(int count)
        {
            var source = "<body>" + string.Concat(Enumerable.Range(0, count).Select(i => $"<b data-id={i}>")) + "x";
            var parsed = Parse(source, 1);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            return parsed.Session.WorkCount;
        }

        var shortWork = Work(64);
        Work(128).Should().BeLessThan(shortWork * 3);
    }

    [Test]
    public void EquivalentFamiliesInNestedMarkersHaveLinearCountedWork()
    {
        static (long Work, long LargestTurn) Count(int depth)
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput("<body>" + string.Concat(Enumerable.Repeat("<object><b>", depth)), isFinal: true);
            long largestTurn = 0;
            for (var turn = 0; turn < depth * 100 + 1_000; turn++)
            {
                var before = session.WorkCount;
                var step = session.Drive(1, CancellationToken.None);
                largestTurn = Math.Max(largestTurn, session.WorkCount - before);
                if (step.Kind == HtmlParseStepKind.Complete) return (session.WorkCount, largestTurn);
                step.Kind.Should().Be(HtmlParseStepKind.Yielded);
            }
            throw new InvalidOperationException("Nested formatting markers did not finish.");
        }

        var smaller = Count(128);
        var larger = Count(256);
        larger.Work.Should().BeLessThan(smaller.Work * 3);
        larger.LargestTurn.Should().BeLessThan(32);
    }

    [Test]
    public void WideEquivalentAttributesCompareAcrossQuotaOneDrivesWithoutReinserting()
    {
        var forward = string.Join(" ", Enumerable.Range(0, 256).Select(i => $"a{i}=v{i}"));
        var reversed = string.Join(" ", Enumerable.Range(0, 256).Reverse().Select(i => $"a{i}=v{i}"));
        var source = $"<body><b {forward}><b {reversed}><b {forward}><b {reversed}>x";
        var expected = Serialize(Parse(source, 100_000).Document);
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput(source, isFinal: true);
        var pendingField = typeof(HtmlTreeBuilder).GetField("_pendingFormattingElement",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var builder = BuilderOf(session);
        var comparisonTurns = 0;
        for (var turn = 0; turn < 100_000; turn++)
        {
            var comparing = pendingField.GetValue(builder) is not null;
            var before = session.WorkCount;
            var step = session.Drive(1, CancellationToken.None);
            if (comparing)
            {
                comparisonTurns++;
                (session.WorkCount - before).Should().BeLessThan(64);
            }
            if (step.Kind == HtmlParseStepKind.Complete) break;
            step.Kind.Should().Be(HtmlParseStepKind.Yielded);
            if (turn == 99_999) throw new InvalidOperationException("Wide formatting comparison stalled.");
        }
        comparisonTurns.Should().BeGreaterThan(256);
        Serialize(document).Should().Be(expected);
        FormattingCount(session).Should().Be(3);
    }

    [Test]
    public void WideReconstructionChargesNativeAttributeBatchAndYieldsBeforeText()
    {
        var attributes = string.Join(" ", Enumerable.Range(0, 1_000).Select(i => $"a{i}=v{i}"));
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput($"<p><b {attributes}>x</p>");
        DrainToNeedInput(session);
        session.AppendInput("y", isFinal: true);
        var body = (Element) document.DocumentElement!.LastChild!;
        long largestTurn = 0;
        var observedReplacement = false;
        for (var turn = 0; turn < 100_000; turn++)
        {
            var before = session.WorkCount;
            var step = session.Drive(1, CancellationToken.None);
            largestTurn = Math.Max(largestTurn, session.WorkCount - before);
            if (!observedReplacement && body.ChildCount == 2)
            {
                observedReplacement = true;
                var recreated = (Element) body.LastChild!;
                recreated.AttributeCount.Should().Be(1_000);
                recreated.FirstChild.Should().BeNull();
            }
            if (step.Kind == HtmlParseStepKind.Complete) break;
            step.Kind.Should().Be(HtmlParseStepKind.Yielded);
            if (turn == 99_999) throw new InvalidOperationException("Wide formatting reconstruction stalled.");
        }
        observedReplacement.Should().BeTrue();
        largestTurn.Should().BeGreaterThan(1_000);
        ((Text) body.LastChild!.FirstChild!).Data.Should().Be("y");
    }

    [Test]
    public void FormattingEntryOwnsAttributesFromAMutableTokenCollection()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<p>");
        DrainToNeedInput(session);
        var builder = (HtmlTreeBuilder) BuilderOf(session);
        static void Feed(HtmlTreeBuilder target, HtmlToken token)
        {
            target.SetToken(token);
            for (var turn = 0; target.HasToken && turn < 10_000; turn++)
                target.Process(1, CancellationToken.None);
            target.HasToken.Should().BeFalse();
        }

        var attributes = new[] { new HtmlAttribute("id", "original") };
        Feed(builder, new HtmlToken(HtmlTokenKind.StartTag, name: "b", attributes: attributes));
        attributes[0] = new HtmlAttribute("id", "changed");
        Feed(builder, new HtmlToken(HtmlTokenKind.Text, data: "x"));
        Feed(builder, new HtmlToken(HtmlTokenKind.EndTag, name: "p"));
        Feed(builder, new HtmlToken(HtmlTokenKind.Text, data: "y"));

        var body = (Element) document.DocumentElement!.LastChild!;
        ((Element) body.FirstChild!.FirstChild!).GetAttribute("id").Should().Be("original");
        ((Element) body.LastChild!).GetAttribute("id").Should().Be("original");
    }

    [Test]
    public void CancellationDuringWideCandidateComparisonKeepsOneCommittedElement()
    {
        var attributes = string.Join(" ", Enumerable.Range(0, 256).Select(i => $"a{i}=v{i}"));
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput($"<b {attributes}><b {attributes}>", isFinal: true);
        var builder = BuilderOf(session);
        var comparingField = typeof(HtmlTreeBuilder).GetField("_pendingFormattingComparing",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        for (var turn = 0; turn < 100_000; turn++)
        {
            session.Drive(1, CancellationToken.None);
            if ((bool) comparingField.GetValue(builder)!) break;
            if (turn == 99_999) throw new InvalidOperationException("Formatting comparison did not start.");
        }
        var committed = Serialize(document);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, cancelled.Token));
        Serialize(document).Should().Be(committed);
        var body = (Element) document.DocumentElement!.LastChild!;
        body.ChildCount.Should().Be(1);
        ((Element) body.FirstChild!).ChildCount.Should().Be(1);
    }

    [TestCase("<a>")]
    [TestCase("<nobr>")]
    [TestCase("<b></b>")]
    public void AdoptionBranchesRemainTerminalBeforeMutation(string source)
    {
        var parsed = Parse(source, 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.MissingFeature);
        parsed.Step.MissingFeature.Should().Be(HtmlMissingFeature.Formatting);
    }

    private static int FormattingCount(HtmlParserSession session)
    {
        var builder = BuilderOf(session);
        var formatting = typeof(HtmlTreeBuilder).GetField("_formatting",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(builder)!;
        return (int) formatting.GetType().GetProperty("Count")!.GetValue(formatting)!;
    }
}
