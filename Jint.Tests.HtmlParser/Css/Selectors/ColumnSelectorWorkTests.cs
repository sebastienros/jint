#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class ColumnSelectorWorkTests
{
    private static CompiledSelector Parse(string source) => SelectorCompiler.Compile(source, null, default);

    private static (Document Document, Element Table, Element Group) NewTable()
    {
        var document = Document.CreateHtml();
        var table = document.CreateElement("table");
        var group = document.CreateElement("colgroup");
        document.AppendChild(table);
        table.AppendChild(group);
        return (document, table, group);
    }

    [Test]
    public void OneQuerySharesTheTableModelAcrossCandidates()
    {
        static int Checkpoints(int count)
        {
            var (document, table, group) = NewTable();
            group.AppendChild(document.CreateElement("col"));
            for (var index = 0; index < count; index++)
            {
                var row = document.CreateElement("tr");
                row.AppendChild(document.CreateElement("td"));
                table.AppendChild(row);
            }
            var checkpoints = 0;
            SelectorMatcher.QuerySelectorAll(Parse("col || td"), table,
                () => checkpoints++, default).Should().HaveCount(count);
            return checkpoints;
        }

        var small = Checkpoints(128);
        var large = Checkpoints(256);
        small.Should().BeGreaterThan(0);
        large.Should().BeLessThan(small * 3);
    }

    [Test]
    public void RelativeColumnDiscoveryUsesTheForwardIntervalIndex()
    {
        static int Checkpoints(int count)
        {
            var (document, table, group) = NewTable();
            var row = document.CreateElement("tr");
            table.AppendChild(row);
            for (var index = 0; index < count; index++)
            {
                group.AppendChild(document.CreateElement("col"));
                row.AppendChild(document.CreateElement("td"));
            }
            var checkpoints = 0;
            SelectorMatcher.QuerySelectorAll(Parse("col:has(|| td.missing)"), table,
                () => checkpoints++, default).Should().BeEmpty();
            return checkpoints;
        }

        var small = Checkpoints(128);
        var large = Checkpoints(256);
        small.Should().BeGreaterThan(0);
        large.Should().BeLessThan(small * 3);
    }

    [Test]
    public void HugeSpansDoNotExpandSlotAreaOrImpliedRows()
    {
        static int Checkpoints(string colspan, string rowspan)
        {
            var (document, table, _) = NewTable();
            var row = document.CreateElement("tr");
            var cell = document.CreateElement("td");
            table.AppendChild(row);
            row.AppendChild(cell);
            cell.SetAttribute("colspan", colspan);
            cell.SetAttribute("rowspan", rowspan);
            var checkpoints = 0;
            SelectorMatcher.Matches(Parse("td:nth-col(1)"), cell, null,
                () => checkpoints++, default).Should().BeTrue();
            return checkpoints;
        }

        var small = Checkpoints("2", "2");
        var large = Checkpoints("1000", "65534");
        large.Should().BeLessThan(small * 2 + 100);
    }

    [Test]
    public void ModelBuildAndRelativeSearchObserveCancellation()
    {
        var (document, table, group) = NewTable();
        var column = document.CreateElement("col");
        group.AppendChild(column);
        column.SetAttribute("span", new string('9', 2048));
        var row = document.CreateElement("tr");
        var cell = document.CreateElement("td");
        table.AppendChild(row);
        row.AppendChild(cell);
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse("td:nth-col(1)"), cell, null, () =>
            {
                if (++checkpoints == 128) source.Cancel();
            }, source.Token));
        checkpoints.Should().BeGreaterThanOrEqualTo(128);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.QuerySelectorAll(Parse("col || td"), document, cancelled.Token));
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse("col:has(|| td.missing)"), column, null, cancelled.Token));
    }

    [Test]
    public void UnsuccessfulColumnRelativeDiscoveryChecksCancellationAfterIndexBuild()
    {
        var (document, table, group) = NewTable();
        var row = document.CreateElement("tr");
        table.AppendChild(row);
        for (var index = 0; index < 1024; index++)
        {
            group.AppendChild(document.CreateElement("col"));
            row.AppendChild(document.CreateElement("td"));
        }

        var preparationSteps = 0;
        var grid = HtmlTableGrid.Build(table, () => preparationSteps++, default);
        foreach (var _ in grid.CellsOverlapping(0, 1, () => preparationSteps++, default)) { }
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse("table:has(> colgroup > col || td.missing)"), table,
                null, () =>
                {
                    if (++checkpoints == preparationSteps + 1000) source.Cancel();
                }, source.Token));
        checkpoints.Should().BeGreaterThan(preparationSteps);
    }
}
