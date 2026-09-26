#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // HTML Standard §13.2.6.1 and §13.2.6.4.9–10, revision 2026-09-22.
    // Comparison corpus pin: html5lib/html5lib-tests 9329e64694e7835d0dcff9811e22856ef6ad16f9.
    [TestCase("<table> \n<tr><td>x</table>",
        "<html><head></head><body><table> \n<tbody><tr><td>x</td></tr></tbody></table></body></html>")]
    [TestCase("<table> \nA<tr><td>x</table>",
        "<html><head></head><body> \nA<table><tbody><tr><td>x</td></tr></tbody></table></body></html>")]
    [TestCase("<table>A&amp;B</table>",
        "<html><head></head><body>A&B<table></table></body></html>")]
    [TestCase("<table>A\r\nB</table>",
        "<html><head></head><body>A\nB<table></table></body></html>")]
    [TestCase("<table>A\0B</table>",
        "<html><head></head><body>AB<table></table></body></html>")]
    [TestCase("<table>A<tr>B</tr>C</table>",
        "<html><head></head><body>ABC<table><tbody><tr></tr></tbody></table></body></html>")]
    [TestCase("<table> A<!--x--> B</table>",
        "<html><head></head><body> A B<table><!--x--></table></body></html>")]
    [TestCase("<table><script>x</script>y</table>",
        "<html><head></head><body>y<table><script>x</script></table></body></html>")]
    [TestCase("<table><tr><td><table>A</table>B</table>",
        "<html><head></head><body><table><tbody><tr><td>A<table></table>B</td></tr></tbody></table></body></html>")]
    public void PendingTableCharactersUseWholeRunClassification(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
        }
    }

    [Test]
    public void PendingRunIsInvisibleUntilTriggerAndThenCoalescesBeforeTable()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<table> \n");
        DrainToNeedInput(session);
        var body = (Element) document.DocumentElement!.LastChild!;
        var table = (Element) body.FirstChild!;
        table.FirstChild.Should().BeNull();
        body.FirstChild.Should().BeSameAs(table);

        session.AppendInput("A&amp;B");
        DrainToNeedInput(session);
        table.FirstChild.Should().BeNull();
        body.FirstChild.Should().BeSameAs(table);

        session.AppendInput("</table>", isFinal: true);
        DrainToCompletion(session);
        Serialize(document).Should().Be("<html><head></head><body> \nA&B<table></table></body></html>");
        body.FirstChild.Should().BeOfType<Text>();
        var text = (Text) body.FirstChild!;
        text.NextSibling.Should().BeSameAs(table);
        table.PreviousSibling.Should().BeSameAs(text);
    }

    [Test]
    public void QuotaOneFlushPublishesOnlyCommittedPrefix()
    {
        const string pending = " abcdef";
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<table>" + pending);
        DrainToNeedInput(session);
        var body = (Element) document.DocumentElement!.LastChild!;
        var table = (Element) body.FirstChild!;
        session.AppendInput("</table>", isFinal: true);

        var sawPartial = false;
        for (var turn = 0; turn < 100_000; turn++)
        {
            var step = session.Drive(1, CancellationToken.None);
            if (body.FirstChild is Text text)
            {
                pending.StartsWith(text.Data, StringComparison.Ordinal).Should().BeTrue();
                if (text.Data.Length < pending.Length) sawPartial = true;
                text.NextSibling.Should().BeSameAs(table);
            }
            if (step.Kind == HtmlParseStepKind.Complete) break;
            if (step.Kind != HtmlParseStepKind.Yielded) throw new InvalidOperationException("Unexpected table flush result.");
            if (turn == 99_999) throw new InvalidOperationException("Table flush stalled.");
        }

        sawPartial.Should().BeTrue();
        body.ChildCount.Should().Be(2);
        ((Text) body.FirstChild!).Data.Should().Be(pending);
    }

    [Test]
    public void TableTextAndTriggerSurviveEveryShortSplit()
    {
        const string source = "<table> \r\nA&amp;B<tr>C</tr>D</table>";
        const string expected = "<html><head></head><body> \nA&BCD<table><tbody><tr></tr></tbody></table></body></html>";
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
    public void RepeatedFosterRunsReuseTheSameTextNode()
    {
        var parsed = Parse("<table>A<tr>B</tr>C</table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = (Element) parsed.Document.DocumentElement!.LastChild!;
        body.ChildCount.Should().Be(2);
        var text = (Text) body.FirstChild!;
        text.Data.Should().Be("ABC");
        text.NextSibling.Should().BeOfType<Element>();
        text.NextSibling!.PreviousSibling.Should().BeSameAs(text);
    }

    [Test]
    public void NullAndNonwhiteDiagnosticsKeepOriginalTextOffsets()
    {
        var parsed = Parse("<table>A\0B</table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        parsed.Diagnostics.Items.Should().Contain(item =>
            item.Code == "html/tree-unexpected-null-character" && item.Offset == 8);
        parsed.Diagnostics.Items.Should().Contain(item =>
            item.Code == "html/tree-nonwhite-table-text" && item.Offset == 7);
        parsed.Diagnostics.Items.Count(item => item.Code == "html/tree-nonwhite-table-text").Should().Be(1);
    }

    [Test]
    public void AppropriatePlaceUsesParserStackAndActualTableParent()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<table>");
        DrainToNeedInput(session);
        var body = (Element) document.DocumentElement!.LastChild!;
        var table = (Element) body.FirstChild!;
        var builder = BuilderOf(session);
        FosterOn(builder);

        var attached = LocationOf(builder);
        attached.Parent.Should().BeSameAs(body);
        attached.Before.Should().BeSameAs(table);

        var movedParent = document.CreateElement("div");
        body.AppendChild(movedParent);
        movedParent.AppendChild(table);
        var moved = LocationOf(builder);
        moved.Parent.Should().BeSameAs(movedParent);
        moved.Before.Should().BeSameAs(table);

        movedParent.RemoveChild(table);
        var detached = LocationOf(builder);
        detached.Parent.Should().BeSameAs(body);
        detached.Before.Should().BeNull();

        var template = document.CreateElement("template");
        var adjusted = LocationOf(builder, template);
        adjusted.Parent.Should().BeSameAs(template.TemplateContent);
        adjusted.Before.Should().BeNull();
        adjusted.Parent.OwnerDocument.Should().NotBeSameAs(document);
    }

    [Test]
    public void AppropriatePlaceWithoutTableUsesStackRoot()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<div>");
        DrainToNeedInput(session);
        var builder = BuilderOf(session);
        FosterOn(builder);
        var detachedTable = document.CreateElement("table");
        var location = LocationOf(builder, detachedTable);
        location.Parent.Should().BeSameAs(document.DocumentElement);
        location.Before.Should().BeNull();
    }

    [Test]
    public void LaterTemplateOnStackWinsOverTableForFosterLocation()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<table>");
        DrainToNeedInput(session);
        var table = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var builder = BuilderOf(session);
        var template = document.CreateElement("template");
        typeof(HtmlTreeBuilder).GetMethod("Push",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(builder, [template]);
        FosterOn(builder);
        var location = LocationOf(builder, table);
        location.Parent.Should().BeSameAs(template.TemplateContent);
        location.Before.Should().BeNull();
    }

    [Test]
    public void BufferedTextFlushesBeforeTemplatePatchFallback()
    {
        var parsed = Parse("<table>A<template for=target>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(parsed.Document).Should().Be("<html><head></head><body>A<table><template></template></table></body></html>");
    }

    [Test]
    public void CancellationBetweenTableClassificationSlicesLeavesPendingTextUnpublished()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<table>abcdef");
        var builder = BuilderOf(session);
        var modeField = typeof(HtmlTreeBuilder).GetField("_mode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var indexField = typeof(HtmlTreeBuilder).GetField("_textIndex",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        for (var turn = 0; turn < 100_000; turn++)
        {
            session.Drive(1, CancellationToken.None);
            if (modeField.GetValue(builder)!.ToString() == "InTableText" &&
                (int) indexField.GetValue(builder)! is > 0 and < 6)
                break;
            if (turn == 99_999) throw new InvalidOperationException("Table classification stage was not reached.");
        }

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, canceled.Token));
        Serialize(document).Should().Be("<html><head></head><body><table></table></body></html>");
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, CancellationToken.None));
    }

    [Test]
    public void CancellationBetweenFlushSlicesKeepsCommittedTextAndLinksCoherent()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput("<table>abcdef");
        DrainToNeedInput(session);
        session.AppendInput("</table>", isFinal: true);
        var body = (Element) document.DocumentElement!.LastChild!;
        var table = (Element) body.FirstChild!;
        for (var turn = 0; turn < 100_000; turn++)
        {
            session.Drive(1, CancellationToken.None);
            if (body.FirstChild is Text { Data.Length: > 0 and < 6 }) break;
            if (turn == 99_999) throw new InvalidOperationException("Partial table flush was not reached.");
        }

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(1, canceled.Token));
        var committed = (Text) body.FirstChild!;
        "abcdef".StartsWith(committed.Data, StringComparison.Ordinal).Should().BeTrue();
        committed.NextSibling.Should().BeSameAs(table);
        table.PreviousSibling.Should().BeSameAs(committed);
        body.ChildCount.Should().Be(2);
        Assert.Throws<InvalidOperationException>(() => session.Drive(1, CancellationToken.None));
    }

    [Test]
    public void LongPendingRunHasDeterministicLinearCountedWork()
    {
        static string Source(int length) => "<table>" + new string('x', length) + "</table>";
        var smaller = Parse(Source(128), 1);
        var larger = Parse(Source(256), 1);
        var repeated = Parse(Source(256), 1);
        smaller.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        larger.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        larger.Session.WorkCount.Should().Be(repeated.Session.WorkCount);
        larger.Session.WorkCount.Should().BeLessThan(smaller.Session.WorkCount * 3);
    }

    [TestCase("x")]
    [TestCase(" ")]
    [TestCase("\0")]
    public void FosterCharacterReportsTableErrorOnceAcrossQuotasAndSplits(string character)
    {
        var source = "<!doctype html><table><div>" + character + "</div></table>";
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            TableErrorOffsets(parsed.Diagnostics).Should().Equal(22L, 27L, 28L);

            for (var split = 0; split <= source.Length; split++)
            {
                var diagnostics = new ParseDiagnosticCollector();
                var document = Document.CreateHtml();
                var session = new HtmlParserSession(document, new HtmlParseOptions { Diagnostics = diagnostics });
                session.AppendInput(source[..split]);
                DrainToNeedInput(session, quota);
                session.AppendInput(source[split..], isFinal: true);
                DrainToCompletion(session, quota);
                TableErrorOffsets(diagnostics).Should().Equal(new[] { 22L, 27L, 28L },
                    $"character {(int) character[0]:X4}, split {split}, quota {quota}");
            }
        }
    }

    private static long[] TableErrorOffsets(ParseDiagnosticCollector diagnostics)
        => diagnostics.Items.Where(item => item.Code == "html/tree-unexpected-token-in-table")
            .Select(item => item.Offset).ToArray();

    private static object BuilderOf(HtmlParserSession session)
        => typeof(HtmlParserSession).GetField("_builder",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;

    private static void FosterOn(object builder)
        => typeof(HtmlTreeBuilder).GetField("_fosterParenting",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(builder, true);

    private static (Node Parent, Node? Before) LocationOf(object builder, Node? target = null)
    {
        var method = typeof(HtmlTreeBuilder).GetMethod("FindAdjustedInsertionLocation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var location = method.Invoke(builder, [target])!;
        var type = location.GetType();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        return ((Node) type.GetProperty("Parent", flags)!.GetValue(location)!,
            (Node?) type.GetProperty("Before", flags)!.GetValue(location));
    }

    private static void DrainToNeedInput(HtmlParserSession session, int quota = 1)
    {
        for (var turn = 0; turn < 100_000; turn++)
        {
            var step = session.Drive(quota, CancellationToken.None);
            if (step.Kind == HtmlParseStepKind.NeedInput) return;
            if (step.Kind != HtmlParseStepKind.Yielded) throw new InvalidOperationException("Unexpected table parse result.");
        }
        throw new InvalidOperationException("Table parser stalled before more input.");
    }

    private static void DrainToCompletion(HtmlParserSession session, int quota = 1)
    {
        for (var turn = 0; turn < 100_000; turn++)
        {
            var step = session.Drive(quota, CancellationToken.None);
            if (step.Kind == HtmlParseStepKind.Complete) return;
            if (step.Kind != HtmlParseStepKind.Yielded) throw new InvalidOperationException("Unexpected table parse result.");
        }
        throw new InvalidOperationException("Table parser stalled after final input.");
    }
}
