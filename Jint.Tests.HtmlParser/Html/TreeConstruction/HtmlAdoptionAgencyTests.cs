#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // HTML Standard §13.2.6.4.7 and §13.2.10.1–2, revision 2026-09-22.
    [TestCase("<p><b><i>x</b>y</i>z", "<html><head></head><body><p><b><i>x</i></b><i>y</i>z</p></body></html>")]
    [TestCase("<b><p>x</b>y</p>", "<html><head></head><body><b></b><p><b>x</b>y</p></body></html>")]
    [TestCase("<b>x</b>y", "<html><head></head><body><b>x</b>y</body></html>")]
    [TestCase("<a>x<a>y</a>z", "<html><head></head><body><a>x</a><a>y</a>z</body></html>")]
    [TestCase("<nobr>x<nobr>y</nobr>z", "<html><head></head><body><nobr>x</nobr><nobr>y</nobr>z</body></html>")]
    [TestCase("<b><div><i>x</b>y", "<html><head></head><body><b></b><div><b><i>x</i></b><i>y</i></div></body></html>")]
    [TestCase("<b><i><p>x</b>y", "<html><head></head><body><b><i></i></b><i><p><b>x</b>y</p></i></body></html>")]
    [TestCase("<p><b>x</p></b>y", "<html><head></head><body><p><b>x</b></p>y</body></html>")]
    [TestCase("<b>x</b></b>y", "<html><head></head><body><b>x</b>y</body></html>")]
    [TestCase("<table><b><div>x</b></table>", "<html><head></head><body><b></b><div><b>x</b></div><table></table></body></html>")]
    [TestCase("<a><p>x<a>y</a>", "<html><head></head><body><a></a><p><a>x</a><a>y</a></p></body></html>")]
    [TestCase("<nobr><p>x<nobr>y</nobr>", "<html><head></head><body><nobr></nobr><p><nobr>x</nobr><nobr>y</nobr></p></body></html>")]
    [TestCase("<b><b><b><b>x</b></b></b></b>y", "<html><head></head><body><b><b><b><b>x</b></b></b></b>y</body></html>")]
    [TestCase("<b><b><b><b>x</b></b></b><span>y</b>z", "<html><head></head><body><b><b><b><b>x</b></b></b><span>y</span></b>z</body></html>")]
    public void AdoptionProducesSpecifiedTreesAtEveryQuota(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
        }
    }

    [TestCase("a")]
    [TestCase("b")]
    [TestCase("big")]
    [TestCase("code")]
    [TestCase("em")]
    [TestCase("font")]
    [TestCase("i")]
    [TestCase("nobr")]
    [TestCase("s")]
    [TestCase("small")]
    [TestCase("strike")]
    [TestCase("strong")]
    [TestCase("tt")]
    [TestCase("u")]
    public void EveryFormattingEndTagClosesItsOwnEntry(string name)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse($"<{name}>x</{name}>y", quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(
                $"<html><head></head><body><{name}>x</{name}>y</body></html>");
        }
    }

    [Test]
    public void AdoptionKeepsMovedNodeIdentityAndEmitsEachNativeMoveOnce()
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var document = Document.CreateHtml();
            var session = new HtmlParserSession(document);
            session.AppendInput("<b id=original data-x=one><p><span>x");
            DrainToNeedInput(session, quota);
            var body = (Element) document.DocumentElement!.LastChild!;
            var originalB = (Element) body.FirstChild!;
            var paragraph = (Element) originalB.FirstChild!;
            var span = (Element) paragraph.FirstChild!;
            using var observation = document.ObserveMutations(body, new MutationObserverOptions
            {
                ChildList = true, Subtree = true
            });

            session.AppendInput("</b>", isFinal: true);
            DrainToCompletion(session, quota);
            body.FirstChild.Should().BeSameAs(originalB);
            body.LastChild.Should().BeSameAs(paragraph);
            paragraph.FirstChild.Should().BeOfType<Element>();
            var replacementB = (Element) paragraph.FirstChild!;
            replacementB.Should().NotBeSameAs(originalB);
            replacementB.GetAttribute("id").Should().Be("original");
            replacementB.GetAttribute("data-x").Should().Be("one");
            replacementB.FirstChild.Should().BeSameAs(span);
            var records = observation.TakeRecords();
            records.Should().HaveCount(4);
            records[0].RemovedNodes.Should().ContainSingle().Which.Should().BeSameAs(paragraph);
            records[1].AddedNodes.Should().ContainSingle().Which.Should().BeSameAs(paragraph);
            records[2].RemovedNodes.Should().ContainSingle().Which.Should().BeSameAs(span);
            records[3].AddedNodes.Should().ContainSingle().Which.Should().BeSameAs(replacementB);
        }
    }

    [Test]
    public void MoreThanThreeInnerNodesStillReachTheFormattingElement()
    {
        const string source = "<b><i><em><u><strong><p>x</b>y";
        const string expected = "<html><head></head><body><b><i><em><u><strong></strong></u></em></i></b>" +
            "<em><u><strong><p><b>x</b>y</p></strong></u></em></body></html>";
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
            var body = (Element) parsed.Document.DocumentElement!.LastChild!;
            body.ChildCount.Should().Be(2);
            var moved = (Element) body.LastChild!;
            moved.LocalName.Should().Be("em");
            var u = (Element) moved.FirstChild!;
            u.LocalName.Should().Be("u");
            var strong = (Element) u.FirstChild!;
            strong.LocalName.Should().Be("strong");
            var p = (Element) strong.FirstChild!;
            p.LocalName.Should().Be("p");
            ((Element) p.FirstChild!).LocalName.Should().Be("b");
            ((Text) p.LastChild!).Data.Should().Be("y");
        }
    }

    [Test]
    public void ChildTransferCursorMovesEachExistingChildOnceAcrossYields()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<b><p><span>x</span><em>y</em><u>z</u>");
        DrainToNeedInput(session);
        var body = (Element) document.DocumentElement!.LastChild!;
        var paragraph = (Element) body.FirstChild!.FirstChild!;
        var children = paragraph.ChildNodes.ToArray();
        using var observation = document.ObserveMutations(paragraph, new MutationObserverOptions { ChildList = true });
        session.AppendInput("</b>", isFinal: true);
        DrainToCompletion(session);
        var replacement = (Element) paragraph.FirstChild!;
        replacement.LocalName.Should().Be("b");
        replacement.ChildNodes.Should().Equal(children);
        var records = observation.TakeRecords();
        records.Should().HaveCount(4);
        records.Take(3).Select(record => record.RemovedNodes.Single()).Should().Equal(children);
        records[3].AddedNodes.Should().ContainSingle().Which.Should().BeSameAs(replacement);
    }

    [Test]
    public void ShortSplitsResumeAdoptionWithoutRepeatingNodeMoves()
    {
        const string source = "<b><i><p>x</b>y";
        const string expected = "<html><head></head><body><b><i></i></b><i><p><b>x</b>y</p></i></body></html>";
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

    [TestCase("MoveLast", false)]
    [TestCase("CreateReplacement", true)]
    public void CancellationOnEitherSideOfMoveKeepsCommittedIdentity(string stage, bool moved)
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<b><p>x");
        DrainToNeedInput(session);
        var body = (Element) document.DocumentElement!.LastChild!;
        var b = (Element) body.FirstChild!;
        var paragraph = (Element) b.FirstChild!;
        session.AppendInput("</b>", isFinal: true);
        var builder = BuilderOf(session);
        var stageField = typeof(HtmlTreeBuilder).GetField("_adoptionStage",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        for (var turn = 0; turn < 100_000; turn++)
        {
            session.Drive(1, CancellationToken.None);
            if (stageField.GetValue(builder)!.ToString() == stage) break;
            if (turn == 99_999) throw new InvalidOperationException("Adoption did not reach the requested move boundary.");
        }
        paragraph.ParentNode.Should().BeSameAs(moved ? body : b);
        var before = Serialize(document);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, cancellation.Token));
        Serialize(document).Should().Be(before);
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, CancellationToken.None));
    }

    [Test]
    public void DeepFurthestBlockSearchHasLinearCountedWork()
    {
        static long Work(int depth)
        {
            var source = "<b>" + string.Concat(Enumerable.Repeat("<span>", depth)) + "<p>x</b>";
            var parsed = Parse(source, 1);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            return parsed.Session.WorkCount;
        }
        var smaller = Work(48);
        Work(96).Should().BeLessThan(smaller * 3);
    }

    [Test]
    public void CellMarkersKeepFormattingEndTagsWithinTheCell()
    {
        const string source = "<table><tr><td><b>x</b></td><td></b>y</td></tr></table>";
        var parsed = Parse(source, 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var table = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        var row = (Element) table.FirstChild!.FirstChild!;
        var first = (Element) row.FirstChild!;
        var second = (Element) first.NextSibling!;
        ((Element) first.FirstChild!).LocalName.Should().Be("b");
        second.FirstChild.Should().BeOfType<Text>();
        ((Text) second.FirstChild!).Data.Should().Be("y");
    }

    [Test]
    public void FormattingElementBeyondTableScopeIsRetained()
    {
        var parsed = Parse("<b><table></b></table>x", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(parsed.Document).Should().Be(
            "<html><head></head><body><b><table></table>x</b></body></html>");
        parsed.Diagnostics.Items.Should().Contain(item => item.Code == "html/tree-adoption-formatting-out-of-scope");
    }

    [Test]
    public void AdoptionReplacementUsesTheExistingDepthSlot()
    {
        var options = new HtmlParseOptions { Limits = new ParseLimits { MaxNestingDepth = 4 } };
        var parsed = Parse("<b><p>x</b>", 1, options);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(parsed.Document).Should().Be(
            "<html><head></head><body><b></b><p><b>x</b></p></body></html>");
    }

    [Test]
    public void WideCreationAttributesAreReusedAndChargedDuringAdoption()
    {
        var attributes = string.Join(" ", Enumerable.Range(0, 256).Select(index => $"a{index}=v{index}"));
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput($"<b {attributes}><p>x");
        DrainToNeedInput(session);
        var before = session.WorkCount;
        session.AppendInput("</b>", isFinal: true);
        DrainToCompletion(session);
        var body = (Element) document.DocumentElement!.LastChild!;
        var replacement = (Element) body.LastChild!.FirstChild!;
        replacement.AttributeCount.Should().Be(256);
        replacement.GetAttribute("a0").Should().Be("v0");
        replacement.GetAttribute("a255").Should().Be("v255");
        (session.WorkCount - before).Should().BeGreaterThan(256);
    }
}
