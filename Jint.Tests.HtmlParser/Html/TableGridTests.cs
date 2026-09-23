#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

[TestFixture]
public sealed class TableGridTests
{
    private static Element Child(Node parent, Document document, string name, string? attribute = null,
        string? value = null)
    {
        var element = document.CreateElement(name);
        if (attribute is not null) element.SetAttribute(attribute, value!);
        parent.AppendChild(element);
        return element;
    }

    private static (long Start, long End) Cell(HtmlTableGrid grid, Element element)
    {
        grid.TryGetCellColumns(element, out var start, out var end).Should().BeTrue();
        return (start, end);
    }

    private static (long Start, long End) Column(HtmlTableGrid grid, Element element)
    {
        grid.TryGetColumnColumns(element, out var start, out var end).Should().BeTrue();
        return (start, end);
    }

    [Test]
    public void ExplicitColumnsAndCellsUseTheirNativeIdentitiesAndIntervals()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var group = Child(table, document, "colgroup", "span", "8");
        var first = Child(group, document, "col", "span", "2");
        var selected = Child(group, document, "col");
        var row1 = Child(table, document, "tr");
        var a = Child(row1, document, "td");
        var b = Child(row1, document, "td");
        var c = Child(row1, document, "td");
        var row2 = Child(table, document, "tr");
        var d = Child(row2, document, "td", "colspan", "2");
        var e = Child(row2, document, "td");
        var row3 = Child(table, document, "tr");
        var f = Child(row3, document, "td");
        var g = Child(row3, document, "td", "colspan", "2");

        var grid = HtmlTableGrid.Build(table, default);
        grid.ColumnCount.Should().Be(3);
        Column(grid, first).Should().Be((0, 2));
        Column(grid, selected).Should().Be((2, 3));
        Cell(grid, a).Should().Be((0, 1));
        Cell(grid, b).Should().Be((1, 2));
        Cell(grid, c).Should().Be((2, 3));
        Cell(grid, d).Should().Be((0, 2));
        Cell(grid, e).Should().Be((2, 3));
        Cell(grid, f).Should().Be((0, 1));
        Cell(grid, g).Should().Be((1, 3));
        grid.ColumnsOverlapping(2, 3, default).Should().Equal(selected);
        grid.CellsOverlapping(2, 3, default).Should().Equal(c, e, g);
        grid.CellsOverlapping(1, 3, default).Should().Equal(b, c, d, e, g);
    }

    [Test]
    public void RowspansAndOverlapsKeepAnchoredIntervals()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var first = Child(table, document, "tr");
        var a = Child(first, document, "td", "rowspan", "2");
        var b = Child(first, document, "td");
        var second = Child(table, document, "tr");
        var c = Child(second, document, "td");
        var grid = HtmlTableGrid.Build(table, default);
        Cell(grid, a).Should().Be((0, 1));
        Cell(grid, b).Should().Be((1, 2));
        Cell(grid, c).Should().Be((1, 2));

        table.RemoveChild(first);
        table.RemoveChild(second);
        first = Child(table, document, "tr");
        var left = Child(first, document, "td");
        var spanning = Child(first, document, "td", "rowspan", "2");
        second = Child(table, document, "tr");
        var overlapping = Child(second, document, "td", "colspan", "2");
        grid = HtmlTableGrid.Build(table, default);
        Cell(grid, left).Should().Be((0, 1));
        Cell(grid, spanning).Should().Be((1, 2));
        Cell(grid, overlapping).Should().Be((0, 2));
        grid.CellsOverlapping(1, 2, default).Should().Equal(spanning, overlapping);
    }

    [Test]
    public void FinalWidthIncludesHolesAndTrailingDeclarations()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var declared = Child(table, document, "colgroup", "span", "5");
        var row = Child(table, document, "tr");
        var only = Child(row, document, "td");
        var grid = HtmlTableGrid.Build(table, default);
        grid.ColumnCount.Should().Be(5);
        Cell(grid, only).Should().Be((0, 1));
        grid.ColumnsOverlapping(0, 5, default).Should().BeEmpty();
        grid.CellsOverlapping(4, 5, default).Should().BeEmpty();
        declared.SetAttribute("span", "0");
        HtmlTableGrid.Build(table, default).ColumnCount.Should().Be(1);

        var empty = document.CreateElement("table");
        HtmlTableGrid.Build(empty, default).ColumnCount.Should().Be(0);
    }

    [Test]
    public void ZeroRowspanEndsAtExplicitGroupBoundaryEvenInQuirksMode()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var body1 = Child(table, document, "tbody");
        var row1 = Child(body1, document, "tr");
        var zero = Child(row1, document, "td", "rowspan", "-000tail");
        Child(body1, document, "tr");
        var row3 = Child(body1, document, "tr");
        var later = Child(row3, document, "td");
        var body2 = Child(table, document, "tbody");
        var next = Child(Child(body2, document, "tr"), document, "td");
        var grid = HtmlTableGrid.Build(table, default);
        Cell(grid, zero).Should().Be((0, 1));
        Cell(grid, later).Should().Be((1, 2));
        Cell(grid, next).Should().Be((0, 1));
        grid.ColumnCount.Should().Be(2);
    }

    [Test]
    public void PositiveRowspanExtendsGroupHeightBeforeNextGroup()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var body = Child(table, document, "tbody");
        var first = Child(Child(body, document, "tr"), document, "td", "rowspan", "65534");
        var next = Child(Child(Child(table, document, "tbody"), document, "tr"), document, "td");
        var grid = HtmlTableGrid.Build(table, default);
        Cell(grid, first).Should().Be((0, 1));
        Cell(grid, next).Should().Be((0, 1));
        grid.ColumnCount.Should().Be(1);
    }

    [Test]
    public void MultipleDownwardGrowingCellsAndFiniteSpansUseFirstFreeAnchors()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var body = Child(table, document, "tbody");
        var first = Child(body, document, "tr");
        var zero1 = Child(first, document, "td", "rowspan", "0");
        var finite = Child(first, document, "td", "rowspan", "2");
        var zero2 = Child(first, document, "td", "rowspan", "-0");
        var middle = Child(Child(body, document, "tr"), document, "td");
        var last = Child(Child(body, document, "tr"), document, "td");
        var grid = HtmlTableGrid.Build(table, default);
        Cell(grid, zero1).Should().Be((0, 1));
        Cell(grid, finite).Should().Be((1, 2));
        Cell(grid, zero2).Should().Be((2, 3));
        Cell(grid, middle).Should().Be((3, 4));
        Cell(grid, last).Should().Be((1, 2));
        grid.ColumnCount.Should().Be(4);
    }

    [Test]
    public void PendingFooterUsesLiteralEndTransitionAfterDirectRows()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var footer = Child(table, document, "tfoot");
        var f = Child(Child(footer, document, "tr"), document, "td");
        var direct = Child(table, document, "tr");
        var a = Child(direct, document, "td", "rowspan", "2");
        var grid = HtmlTableGrid.Build(table, default);
        Cell(grid, a).Should().Be((0, 1));
        Cell(grid, f).Should().Be((1, 2));
        grid.ColumnCount.Should().Be(2);
    }

    [Test]
    public void RepeatedGroupsAndFooterDeferralFollowProcessingOrder()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var foot1 = Child(table, document, "tfoot");
        var f1 = Child(Child(foot1, document, "tr"), document, "td");
        var head = Child(table, document, "thead");
        var h = Child(Child(head, document, "tr"), document, "td", "rowspan", "2");
        var body = Child(table, document, "tbody");
        var b = Child(Child(body, document, "tr"), document, "td");
        var foot2 = Child(table, document, "tfoot");
        var f2 = Child(Child(foot2, document, "tr"), document, "td");
        var grid = HtmlTableGrid.Build(table, default);
        Cell(grid, h).Should().Be((0, 1));
        Cell(grid, b).Should().Be((0, 1));
        Cell(grid, f1).Should().Be((0, 1));
        Cell(grid, f2).Should().Be((0, 1));
        grid.CellsOverlapping(0, 1, default).Should().Equal(h, b, f1, f2);
    }

    [Test]
    public void InitialGroupsSkipIrrelevantChildrenAndLateGroupsAreIgnored()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var implied = Child(table, document, "colgroup", "span", "3");
        table.AppendChild(document.CreateComment("between"));
        var group = Child(table, document, "colgroup", "span", "9");
        Child(group, document, "div").AppendChild(document.CreateElement("col"));
        group.AppendChild(document.CreateComment("between"));
        var col = Child(group, document, "col", "span", "2");
        var direct = Child(table, document, "col");
        var row = Child(table, document, "tr");
        Child(row, document, "td");
        var late = Child(table, document, "colgroup");
        var lateCol = Child(late, document, "col");
        var grid = HtmlTableGrid.Build(table, default);
        grid.ColumnCount.Should().Be(5);
        Column(grid, col).Should().Be((3, 5));
        grid.TryGetColumnColumns(direct, out _, out _).Should().BeFalse();
        grid.TryGetColumnColumns(lateCol, out _, out _).Should().BeFalse();
        grid.TryGetColumnColumns((Element) implied, out _, out _).Should().BeFalse();
    }

    [Test]
    public void AttributePrefixParsingAndClampsAreSaturating()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var group = Child(table, document, "colgroup");
        var col = Child(group, document, "col", "span", "  +2tail");
        var row = Child(table, document, "tr");
        var cell = Child(row, document, "td", "colspan", "2.5");
        var grid = HtmlTableGrid.Build(table, default);
        Column(grid, col).Should().Be((0, 2));
        Cell(grid, cell).Should().Be((0, 2));

        col.SetAttribute("span", new string('9', 10000));
        cell.SetAttribute("colspan", "-0");
        grid = HtmlTableGrid.Build(table, default);
        Column(grid, col).Should().Be((0, 1000));
        Cell(grid, cell).Should().Be((0, 1));
        cell.SetAttribute("colspan", new string('9', 10000));
        col.SetAttribute("span", "-1");
        grid = HtmlTableGrid.Build(table, default);
        Column(grid, col).Should().Be((0, 1));
        Cell(grid, cell).Should().Be((0, 1000));
        cell.SetAttribute("colspan", "+tail");
        Cell(HtmlTableGrid.Build(table, default), cell).Should().Be((0, 1));
        cell.SetAttribute("colspan", "٢");
        Cell(HtmlTableGrid.Build(table, default), cell).Should().Be((0, 1));
    }

    [Test]
    public void NamespaceCaseAndFixedParentShapeControlParticipation()
    {
        var document = Document.CreateXml();
        var table = document.CreateElementNS(Namespaces.Html, "table");
        document.AppendChild(table);
        var group = document.CreateElementNS(Namespaces.Html, "colgroup");
        table.AppendChild(group);
        var column = document.CreateElementNS(Namespaces.Html, "col");
        group.AppendChild(column);
        var row = document.CreateElementNS(Namespaces.Html, "tr");
        table.AppendChild(row);
        var cell = document.CreateElementNS(Namespaces.Html, "td");
        row.AppendChild(cell);
        var wrongNamespace = document.CreateElementNS(Namespaces.Svg, "td");
        row.AppendChild(wrongNamespace);
        var uppercase = document.CreateElementNS(Namespaces.Html, "TD");
        row.AppendChild(uppercase);
        var wrapper = document.CreateElementNS(Namespaces.Html, "div");
        row.AppendChild(wrapper);
        var wrapped = document.CreateElementNS(Namespaces.Html, "td");
        wrapper.AppendChild(wrapped);
        var namespacedSpan = document.CreateAttributeNS("urn:test", "x:colspan");
        namespacedSpan.Value = "9";
        cell.SetAttributeNode(namespacedSpan);
        var grid = HtmlTableGrid.Build(table, default);
        Cell(grid, cell).Should().Be((0, 1));
        Column(grid, column).Should().Be((0, 1));
        grid.TryGetCellColumns(wrongNamespace, out _, out _).Should().BeFalse();
        grid.TryGetCellColumns(uppercase, out _, out _).Should().BeFalse();
        grid.TryGetCellColumns(wrapped, out _, out _).Should().BeFalse();
        Assert.Throws<ArgumentException>(() => HtmlTableGrid.Build(document.CreateElement("table"), default));
        Assert.Throws<ArgumentException>(() => HtmlTableGrid.Build(document.CreateElementNS(Namespaces.Html, "TABLE"), default));
    }

    [Test]
    public void NestedTablesAndTemplateContentDoNotExtendOuterGrid()
    {
        var document = Document.CreateHtml();
        var outer = Child(document, document, "table");
        var cell = Child(Child(outer, document, "tr"), document, "td");
        var nested = Child(cell, document, "table");
        var nestedCell = Child(Child(nested, document, "tr"), document, "td", "colspan", "4");
        var template = Child(outer, document, "template");
        var content = template.TemplateContent!;
        var contentTable = Child(content, content.OwnerDocument!, "table");
        var contentCell = Child(Child(contentTable, content.OwnerDocument!, "tr"), content.OwnerDocument!, "td");
        var outerGrid = HtmlTableGrid.Build(outer, default);
        outerGrid.ColumnCount.Should().Be(1);
        outerGrid.TryGetCellColumns(nestedCell, out _, out _).Should().BeFalse();
        outerGrid.TryGetCellColumns(contentCell, out _, out _).Should().BeFalse();
        HtmlTableGrid.Build(nested, default).ColumnCount.Should().Be(4);
        Cell(HtmlTableGrid.Build(contentTable, default), contentCell).Should().Be((0, 1));

        var host = Child(cell, document, "div");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var shadowTable = Child(shadow, document, "table");
        var shadowCell = Child(Child(shadowTable, document, "tr"), document, "td", "colspan", "3");
        HtmlTableGrid.Build(outer, default).ColumnCount.Should().Be(1);
        Cell(HtmlTableGrid.Build(shadowTable, default), shadowCell).Should().Be((0, 3));
    }

    [Test]
    public void ACompletedGridKeepsItsIntervalsAfterAttributeAndTreeChanges()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var group = Child(table, document, "colgroup");
        var col = Child(group, document, "col");
        var row = Child(table, document, "tr");
        var cell = Child(row, document, "td");
        var before = HtmlTableGrid.Build(table, default);
        col.SetAttribute("span", "4");
        cell.SetAttribute("colspan", "3");
        var after = HtmlTableGrid.Build(table, default);
        before.ColumnCount.Should().Be(1);
        Column(before, col).Should().Be((0, 1));
        Cell(before, cell).Should().Be((0, 1));
        after.ColumnCount.Should().Be(4);
        Column(after, col).Should().Be((0, 4));
        Cell(after, cell).Should().Be((0, 3));

        row.RemoveChild(cell);
        var removed = HtmlTableGrid.Build(table, default);
        removed.TryGetCellColumns(cell, out var start, out var end).Should().BeFalse();
        (start, end).Should().Be((0, 0));
        Cell(after, cell).Should().Be((0, 3));
    }

    [Test]
    public void PresentationAttributesDoNotAffectPlacement()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var row = Child(table, document, "tr");
        var first = Child(row, document, "td");
        var second = Child(row, document, "td");
        first.SetAttribute("hidden", "");
        second.SetAttribute("style", "display:none");
        table.SetAttribute("dir", "rtl");
        var grid = HtmlTableGrid.Build(table, default);
        Cell(grid, first).Should().Be((0, 1));
        Cell(grid, second).Should().Be((1, 2));
        grid.ColumnCount.Should().Be(2);
    }

    [Test]
    public void InvalidArgumentsAndEmptyRangesAreDefined()
    {
        var document = Document.CreateHtml();
        var table = Child(document, document, "table");
        var grid = HtmlTableGrid.Build(table, default);
        Assert.Throws<ArgumentNullException>(() => HtmlTableGrid.Build(null!, default));
        Assert.Throws<ArgumentNullException>(() => grid.TryGetCellColumns(null!, out _, out _));
        Assert.Throws<ArgumentNullException>(() => grid.TryGetColumnColumns(null!, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.ColumnsOverlapping(-1, 0, default).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.CellsOverlapping(0, 1, default).ToArray());
        grid.ColumnsOverlapping(0, 0, default).Should().BeEmpty();
        grid.CellsOverlapping(0, 0, default).Should().BeEmpty();
    }
}
