#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public partial class HtmlTreeConstructionTests
{
    // HTML Standard §13.2.6.4.9–15, revision 2026-09-22.
    [TestCase("<table><tr><td>x<td>y</table>",
        "<html><head></head><body><table><tbody><tr><td>x</td><td>y</td></tr></tbody></table></body></html>")]
    [TestCase("<table><col><col><tr><th>x</th></tr></table>",
        "<html><head></head><body><table><colgroup><col></col><col></col></colgroup><tbody><tr><th>x</th></tr></tbody></table></body></html>")]
    [TestCase("<table><tbody><td>x",
        "<html><head></head><body><table><tbody><tr><td>x</td></tr></tbody></table></body></html>")]
    [TestCase("<table><caption><p>x</caption><tbody><tr><td>y</table>",
        "<html><head></head><body><table><caption><p>x</p></caption><tbody><tr><td>y</td></tr></tbody></table></body></html>")]
    [TestCase("<table><tr><td><div>x</table>",
        "<html><head></head><body><table><tbody><tr><td><div>x</div></td></tr></tbody></table></body></html>")]
    [TestCase("<table><tr><td><table><tr><td>x</table>y</table>",
        "<html><head></head><body><table><tbody><tr><td><table><tbody><tr><td>x</td></tr></tbody></table>y</td></tr></tbody></table></body></html>")]
    [TestCase("<table><table><tr><td>x</table>",
        "<html><head></head><body><table></table><table><tbody><tr><td>x</td></tr></tbody></table></body></html>")]
    [TestCase("<table><colgroup> \n<col></colgroup><tbody><tr><td>x</td></tr></tbody></table>",
        "<html><head></head><body><table><colgroup> \n<col></col></colgroup><tbody><tr><td>x</td></tr></tbody></table></body></html>")]
    public void TableStructure(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
        }
    }

    [Test]
    public void TableCommentAndProcessingInstructionHaveTheirOwnNodes()
    {
        var parsed = Parse("<table><!--a--><?Table yes?><colgroup><?Col c?></colgroup><tbody><?Body b?><tr><?Row r?><td>A<?Cell ok?><!--b-->B</table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var table = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        table.FirstChild.Should().BeOfType<Comment>();
        table.FirstChild!.NextSibling.Should().BeOfType<ProcessingInstruction>();
        var colgroup = (Element) table.FirstChild.NextSibling!.NextSibling!;
        colgroup.FirstChild.Should().BeOfType<ProcessingInstruction>();
        var tbody = (Element) table.LastChild!;
        tbody.FirstChild.Should().BeOfType<ProcessingInstruction>();
        var row = (Element) tbody.LastChild!;
        row.FirstChild.Should().BeOfType<ProcessingInstruction>();
        var cell = (Element) row.LastChild!;
        cell.FirstChild.Should().BeOfType<Text>();
        cell.FirstChild!.NextSibling.Should().BeOfType<ProcessingInstruction>();
        cell.FirstChild.NextSibling!.NextSibling.Should().BeOfType<Comment>();
    }

    [Test]
    public void TableHiddenInputAndFormStayInsideTable()
    {
        var parsed = Parse("<table><input type=HIDDEN id=i><form id=f><tr><td>x</table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var table = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        ((Element) table.FirstChild!).LocalName.Should().Be("input");
        ((Element) table.FirstChild!.NextSibling!).LocalName.Should().Be("form");
        ((Element) table.LastChild!).LocalName.Should().Be("tbody");
    }

    [Test]
    public void CaptionAndCellMarkersBalanceAcrossImplicitClosures()
    {
        var parsed = Parse("<table><caption>a<caption>b<tr><td>x<td>y</table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var builderField = typeof(HtmlParserSession).GetField("_builder",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var formattingField = typeof(HtmlTreeBuilder).GetField("_formatting",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var formatting = (System.Collections.ICollection) formattingField.GetValue(builderField.GetValue(parsed.Session))!;
        formatting.Count.Should().Be(0);
    }

    [Test]
    public void IgnoredTableFamilyEndTagsDoNotBecomeMissingFeatures()
    {
        var parsed = Parse("<table></td><tr></th><td>x</table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        Serialize(parsed.Document).Should().Be(
            "<html><head></head><body><table><tbody><tr><td>x</td></tr></tbody></table></body></html>");
    }

    [Test]
    public void HeadRulesAndDependentStopsRemainReachableThroughTables()
    {
        var parsed = Parse("<table><style>x</style><script>y</script><tr><td>z</table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var table = (Element) parsed.Document.DocumentElement!.LastChild!.FirstChild!;
        ((Element) table.FirstChild!).LocalName.Should().Be("style");
        ((Element) table.FirstChild!.NextSibling!).LocalName.Should().Be("script");
        foreach (var (source, family) in new[]
        {
            ("<table><template for=target>", HtmlMissingFeature.Templates)
        })
        {
            Parse(source, 1).Step.MissingFeature.Should().Be(family);
        }
    }

    [Test]
    public void TableDiagnosticsKeepStartTagOffsets()
    {
        var parsed = Parse("<table></td><tbody><td>x</table>", 1);
        parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        parsed.Diagnostics.Items.Should().Contain(item => item.Code == "html/tree-unexpected-end-tag" && item.Offset == 7);
        parsed.Diagnostics.Items.Should().Contain(item => item.Code == "html/tree-cell-without-row" && item.Offset == 19);
    }

    [Test]
    public void ScopeIndexesAgreeWithLiteralStackRules()
    {
        var scopes = new[]
        {
            (Method: "InScope", Extra: Array.Empty<string>()),
            (Method: "InButtonScope", Extra: new[] { "button" }),
            (Method: "InListItemScope", Extra: new[] { "ol", "ul" }),
            (Method: "InTableScope", Extra: new[] { "table", "template" })
        };
        foreach (var source in new[]
        {
            "<p><button>", "<table><caption><p>", "<table><tbody><tr><td><ul><li>",
            "<table><tr><td><table><tr><th>", "<table><colgroup>"
        })
        {
            var session = new HtmlParserSession(Document.CreateHtml());
            session.AppendInput(source);
            for (var turn = 0; turn < 10_000; turn++)
            {
                var step = session.Drive(1, CancellationToken.None);
                if (step.Kind == HtmlParseStepKind.NeedInput) break;
                if (step.Kind != HtmlParseStepKind.Yielded) throw new InvalidOperationException("Unexpected scope probe result.");
            }
            var builderField = typeof(HtmlParserSession).GetField("_builder",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var builder = builderField.GetValue(session)!;
            var openField = typeof(HtmlTreeBuilder).GetField("_open",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var open = (System.Collections.Generic.List<Element>) openField.GetValue(builder)!;
            foreach (var (methodName, extra) in scopes)
            {
                var method = typeof(HtmlTreeBuilder).GetMethod(methodName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                foreach (var target in new[] { "html", "body", "p", "button", "table", "caption", "tbody", "tr", "td", "th", "li" })
                {
                    var literal = false;
                    for (var i = open.Count - 1; i >= 0; i--)
                    {
                        var name = open[i].LocalName;
                        if (name == target) { literal = true; break; }
                        var boundary = methodName == "InTableScope"
                            ? name is "html" or "table" or "template"
                            : name is "html" or "table" or "caption" or "td" or "th" || Array.IndexOf(extra, name) >= 0;
                        if (boundary) break;
                    }
                    ((bool) method.Invoke(builder, new object[] { target })!).Should().Be(literal,
                        $"{methodName}({target}) on {source}");
                }
            }
        }
    }

    [Test]
    public void DeepTableRecoveryAndAbsentNamesHaveLinearCountedWork()
    {
        static string Source(int count) => "<table><tr><td>" +
            string.Concat(Enumerable.Repeat("<div></absent>", count)) + "</table>";

        var smaller = Parse(Source(128), 1);
        var larger = Parse(Source(256), 1);
        var repeated = Parse(Source(256), 1);
        smaller.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        larger.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
        larger.Session.WorkCount.Should().Be(repeated.Session.WorkCount);
        larger.Session.WorkCount.Should().BeLessThan(smaller.Session.WorkCount * 3);
    }

    [TestCase("<h1><table><tr><td></h1>x",
        "<html><head></head><body><h1><table><tbody><tr><td>x</td></tr></tbody></table></h1></body></html>")]
    [TestCase("<h1><table><tr><td></h1><td>x",
        "<html><head></head><body><h1><table><tbody><tr><td></td><td>x</td></tr></tbody></table></h1></body></html>")]
    [TestCase("<h1><table><caption></h1>x",
        "<html><head></head><body><h1><table><caption>x</caption></table></h1></body></html>")]
    public void HeadingEndCannotCrossTableScope(string source, string expected)
    {
        foreach (var quota in new[] { 1, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
            parsed.Diagnostics.Items.Should().Contain(item => item.Code == "html/tree-unexpected-heading-end-tag");
        }
    }

    [Test]
    public void RepeatedTableResetDoesNotRescanUnchangedAncestors()
    {
        static string Source(int count) => string.Concat(Enumerable.Repeat("<x>", count)) +
            string.Concat(Enumerable.Repeat("<table></table>", count));

        foreach (var quota in new[] { 1, 100_000 })
        {
            var smaller = Parse(Source(100), quota);
            var larger = Parse(Source(200), quota);
            var repeated = Parse(Source(200), quota);
            smaller.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            larger.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            larger.Session.WorkCount.Should().Be(repeated.Session.WorkCount);
            larger.Session.WorkCount.Should().BeLessThan(smaller.Session.WorkCount * 3);
        }
    }

    [TestCase("<table>x", "<html><head></head><body>x<table></table></body></html>")]
    [TestCase("<table><tbody><tr>z", "<html><head></head><body>z<table><tbody><tr></tr></tbody></table></body></html>")]
    [TestCase("<table><div>x", "<html><head></head><body><div>x</div><table></table></body></html>")]
    [TestCase("<table><input type=text>", "<html><head></head><body><input></input><table></table></body></html>")]
    public void FosterBranchesComplete(string source, string expected)
    {
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            var parsed = Parse(source, quota);
            parsed.Step.Kind.Should().Be(HtmlParseStepKind.Complete);
            Serialize(parsed.Document).Should().Be(expected);
        }
    }

    [Test]
    public void TableStructureIsStableAcrossEveryShortSplit()
    {
        const string source = "<table><tr><td>x<td>y</table>";
        var expected = Serialize(Parse(source).Document);
        foreach (var quota in new[] { 1, 3, 100_000 })
        {
            for (var split = 0; split <= source.Length; split++)
            {
                var document = Document.CreateHtml();
                var session = new HtmlParserSession(document);
                session.AppendInput(source[..split]);
                for (var turn = 0; turn < 100_000; turn++)
                {
                    var step = session.Drive(quota, CancellationToken.None);
                    if (step.Kind == HtmlParseStepKind.NeedInput) break;
                    if (step.Kind != HtmlParseStepKind.Yielded) throw new InvalidOperationException("Unexpected pre-final table result.");
                }
                session.AppendInput(source[split..], isFinal: true);
                HtmlParseStep final;
                var turns = 0;
                do
                {
                    final = session.Drive(quota, CancellationToken.None);
                    if (++turns > 100_000) throw new InvalidOperationException("Split table parse stalled.");
                } while (final.Kind == HtmlParseStepKind.Yielded);
                final.Kind.Should().Be(HtmlParseStepKind.Complete);
                Serialize(document).Should().Be(expected);
            }
        }
    }
}
