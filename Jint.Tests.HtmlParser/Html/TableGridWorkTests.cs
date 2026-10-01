#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

[TestFixture]
public sealed class TableGridWorkTests
{
    private static Element Add(Node parent, Document document, string name)
    {
        var element = document.CreateElement(name);
        parent.AppendChild(element);
        return element;
    }

    [Test]
    public void WorkDependsOnNodesRatherThanRowspanTimesColspanArea()
    {
        static int Checkpoints(string rowspan, string colspan)
        {
            var document = Document.CreateHtml();
            var table = Add(document, document, "table");
            var cell = Add(Add(table, document, "tr"), document, "td");
            cell.SetAttribute("rowspan", rowspan);
            cell.SetAttribute("colspan", colspan);
            var checks = 0;
            HtmlTableGrid.Build(table, () => checks++, default).ColumnCount.Should().Be(long.Parse(colspan));
            return checks;
        }

        var small = Checkpoints("1", "1");
        var large = Checkpoints("65534", "1000");
        large.Should().BeLessThan(small * 2 + 50);
    }

    [Test]
    public void FlatRowsHaveSubquadraticCheckpointGrowth()
    {
        static int Checkpoints(int rows)
        {
            var document = Document.CreateHtml();
            var table = Add(document, document, "table");
            for (var i = 0; i < rows; i++)
                Add(Add(table, document, "tr"), document, "td");
            var checks = 0;
            HtmlTableGrid.Build(table, () => checks++, default).ColumnCount.Should().Be(1);
            return checks;
        }

        var first = Checkpoints(256);
        var second = Checkpoints(512);
        first.Should().BeGreaterThan(0);
        second.Should().BeLessThan(first * 3);
    }

    [Test]
    public void ColumnQueriesDoNotScanEveryCellForEveryColumn()
    {
        static int QueryChecks(int width)
        {
            var document = Document.CreateHtml();
            var table = Add(document, document, "table");
            var row = Add(table, document, "tr");
            for (var i = 0; i < width; i++) Add(row, document, "td");
            var grid = HtmlTableGrid.Build(table, default);
            var checks = 0;
            for (var i = 0; i < width; i++)
                grid.CellsOverlapping(i, i + 1, () => checks++, default).Should().ContainSingle();
            return checks;
        }

        var first = QueryChecks(256);
        var second = QueryChecks(512);
        first.Should().BeGreaterThan(0);
        // A per-column all-cells scan would grow fourfold on doubling.
        second.Should().BeLessThan(first * 3 + 1);
    }

    [Test]
    public void SparseForwardQueryGrowsWithIndexDepth()
    {
        static int QueryChecks(int width)
        {
            var document = Document.CreateHtml();
            var table = Add(document, document, "table");
            var row = Add(table, document, "tr");
            Element? last = null;
            for (var i = 0; i < width; i++) last = Add(row, document, "td");
            var grid = HtmlTableGrid.Build(table, default);
            // Build the lazy index outside the measured lookup.
            grid.CellsOverlapping(0, 1, default).Should().ContainSingle();
            var checks = 0;
            grid.CellsOverlapping(width - 1, width, () => checks++, default)
                .Should().ContainSingle().Which.Should().BeSameAs(last);
            return checks;
        }

        var first = QueryChecks(256);
        var second = QueryChecks(512);
        first.Should().BeGreaterThan(0);
        second.Should().BeLessThan(first + 8);
    }

    [Test]
    public void ForwardIndexBuildIsLazyAndCancellationLeavesNoPartialIndex()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var row = Add(table, document, "tr");
        for (var i = 0; i < 512; i++) Add(row, document, "td");
        var grid = HtmlTableGrid.Build(table, default);
        using var cancelled = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => grid.CellsOverlapping(0, 1,
            () => cancelled.Cancel(), cancelled.Token).ToArray());

        var firstChecks = 0;
        grid.CellsOverlapping(0, 1, () => firstChecks++, default).Should().ContainSingle();
        var secondChecks = 0;
        grid.CellsOverlapping(0, 1, () => secondChecks++, default).Should().ContainSingle();
        firstChecks.Should().BeGreaterThan(secondChecks);
    }

    [Test]
    public void CancellationIsObservedDuringInvalidAttributeDigitsAndSkippedNodes()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var col = Add(Add(table, document, "colgroup"), document, "col");
        col.SetAttribute("span", "-" + new string('9', 100_000));
        using var digits = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTableGrid.Build(table,
            () => digits.Cancel(), digits.Token));

        col.SetAttribute("span", "1");
        for (var i = 0; i < 2000; i++) table.AppendChild(document.CreateComment("skip"));
        using var skipped = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTableGrid.Build(table,
            () => skipped.Cancel(), skipped.Token));
    }

    [Test]
    public void CancellationIsObservedDuringOccupancyAndResultEnumeration()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var first = Add(table, document, "tr");
        for (var i = 0; i < 1000; i++)
        {
            var cell = Add(first, document, "td");
            cell.SetAttribute("rowspan", "2");
        }

        var second = Add(table, document, "tr");
        for (var i = 0; i < 1000; i++) Add(second, document, "td");
        using var building = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTableGrid.Build(table,
            () => building.Cancel(), building.Token));

        var grid = HtmlTableGrid.Build(table, default);
        using var enumerating = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => grid.CellsOverlapping(0, grid.ColumnCount,
            () => enumerating.Cancel(), enumerating.Token).ToArray());
    }

    [Test]
    public void PreCancelledEmptyBuildAndRangeQueriesThrow()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => HtmlTableGrid.Build(table, cancelled.Token));
        var grid = HtmlTableGrid.Build(table, default);
        Assert.Throws<OperationCanceledException>(() => grid.ColumnsOverlapping(0, 0, cancelled.Token).ToArray());
        Assert.Throws<OperationCanceledException>(() => grid.CellsOverlapping(0, 0, cancelled.Token).ToArray());
    }
}
