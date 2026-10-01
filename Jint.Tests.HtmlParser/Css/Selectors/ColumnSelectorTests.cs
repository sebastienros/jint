#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class ColumnSelectorTests
{
    private static CompiledSelector Parse(string source) => SelectorCompiler.Compile(source, null, default);

    private static Element Add(Document document, Node parent, string name, string? id = null)
    {
        var element = document.CreateElement(name);
        if (id is not null) element.SetAttribute("id", id);
        parent.AppendChild(element);
        return element;
    }

    [Test]
    public void ColumnRelationUsesExplicitColumnsAndReturnsDomOrder()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var group = Add(document, table, "colgroup");
        var first = Add(document, group, "col");
        first.SetAttribute("span", "2");
        var selected = Add(document, group, "col", "selected");
        var row1 = Add(document, table, "tr");
        Add(document, row1, "td", "a");
        Add(document, row1, "td", "b");
        var c = Add(document, row1, "td", "c");
        var row2 = Add(document, table, "tr");
        var d = Add(document, row2, "td", "d");
        d.SetAttribute("colspan", "2");
        var e = Add(document, row2, "td", "e");
        var row3 = Add(document, table, "tr");
        Add(document, row3, "td", "f");
        var g = Add(document, row3, "td", "g");
        g.SetAttribute("colspan", "2");

        var selector = Parse("col#selected || td");
        SelectorMatcher.QuerySelectorAll(selector, table).Should().Equal(c, e, g);
        SelectorMatcher.QuerySelector(selector, table).Should().BeSameAs(c);
        SelectorMatcher.Matches(Parse("td || td"), c).Should().BeFalse();
        SelectorMatcher.Matches(Parse("td || col"), selected).Should().BeFalse();
        SelectorMatcher.Matches(Parse("colgroup || td"), c).Should().BeFalse();
        SelectorMatcher.Matches(Parse("col || col"), selected).Should().BeFalse();
        SelectorMatcher.TryMatch(Parse("td, col#selected || td"), c, out var specificity)
            .Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 0, 2));
    }

    [Test]
    public void ColumnPredecessorsBacktrackAndOrdinaryChainsRemainAvailable()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var group = Add(document, table, "colgroup");
        var cold = Add(document, group, "col");
        var hot = Add(document, group, "col");
        hot.SetAttribute("class", "hot");
        var row = Add(document, table, "tr");
        var cell = Add(document, row, "td");
        cell.SetAttribute("colspan", "2");
        var child = Add(document, cell, "em");

        SelectorMatcher.Matches(Parse("col.hot || td"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("col.hot || td > em"), child).Should().BeTrue();
        SelectorMatcher.Matches(Parse("colgroup > col.hot || td > em"), child).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:is(col.hot || td)"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:not(col.cold || td)"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-child(1 of col.hot || td)"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("col.cold || td"), cell).Should().BeFalse();
        cold.SetAttribute("class", "cold");
        SelectorMatcher.Matches(Parse("col.cold || td"), cell).Should().BeTrue();
        SelectorMatcher.QuerySelectorAll(Parse("col || td, td"), table).Should().Equal(cell);
    }

    [Test]
    public void NthColumnsUsePlacedIntervalsFinalWidthAndExactArithmetic()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var group = Add(document, table, "colgroup");
        var declaration = Add(document, group, "col");
        declaration.SetAttribute("span", "5");
        var row1 = Add(document, table, "tr");
        var a = Add(document, row1, "td");
        a.SetAttribute("rowspan", "2");
        var b = Add(document, row1, "td");
        var row2 = Add(document, table, "tr");
        var c = Add(document, row2, "td");
        var wide = Add(document, row2, "td");
        wide.SetAttribute("colspan", "2");

        SelectorMatcher.Matches(Parse("td:nth-col(1)"), a).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-col(1)"), c).Should().BeFalse();
        SelectorMatcher.Matches(Parse("td:nth-col(2)"), c).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-last-col(5)"), a).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-last-col(1)"), a).Should().BeFalse();
        SelectorMatcher.Matches(Parse("td:nth-col(odd):nth-col(even)"), wide).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-last-col(-2n+3)"), wide).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-col(0n+3)"), wide).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-col(0n+5)"), wide).Should().BeFalse();
        SelectorMatcher.Matches(Parse("td:nth-col(18446744073709551616n+2)"), c).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-col(-18446744073709551616n+2)"), c).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-col(2n-100)"), c).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-col(-2n+102)"), c).Should().BeTrue();
        SelectorMatcher.Matches(Parse("col:nth-col(1)"), declaration).Should().BeFalse();
        SelectorMatcher.Matches(Parse("td:nth-col(1)"), b).Should().BeFalse();
    }

    [Test]
    public void HasDiscoversLeadingAndInternalColumnEdges()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var group = Add(document, table, "colgroup");
        var cold = Add(document, group, "col");
        var hot = Add(document, group, "col");
        hot.SetAttribute("class", "hot");
        var row = Add(document, table, "tr");
        var cell = Add(document, row, "td");
        cell.SetAttribute("class", "x");
        cell.SetAttribute("colspan", "2");
        Add(document, cell, "em");

        SelectorMatcher.Matches(Parse("col:has(|| td.x)"), hot).Should().BeTrue();
        SelectorMatcher.Matches(Parse("col:has(|| td.x)"), cold).Should().BeTrue();
        SelectorMatcher.Matches(Parse("table:has(> colgroup > col.hot || td > em)"), table)
            .Should().BeTrue();
        SelectorMatcher.Matches(Parse("table:has(> colgroup > col.cold || td > em)"), table)
            .Should().BeFalse();
        SelectorMatcher.Matches(Parse("table:has(> colgroup > col.hot || td > strong)"), table)
            .Should().BeFalse();
        SelectorMatcher.QuerySelectorAll(Parse("col:has(|| td.x)"), table).Should().Equal(cold, hot);
    }

    [Test]
    public void QueriesMayFindColumnsOutsideRootAndProgramsSeeMutations()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var group = Add(document, table, "colgroup");
        var cold = Add(document, group, "col");
        var hot = Add(document, group, "col");
        hot.SetAttribute("class", "hot");
        var body = Add(document, table, "tbody");
        var row = Add(document, body, "tr");
        var cell = Add(document, row, "td");
        cell.SetAttribute("colspan", "2");
        var program = Parse("col.hot || td");

        var before = SelectorMatcher.QuerySelectorAll(program, body);
        before.Should().Equal(cell);
        SelectorMatcher.Closest(program, cell).Should().BeSameAs(cell);
        cell.SetAttribute("colspan", "1");
        SelectorMatcher.QuerySelectorAll(program, body).Should().BeEmpty();
        before.Should().Equal(cell);
        cold.SetAttribute("span", "2");
        cell.SetAttribute("colspan", "3");
        SelectorMatcher.QuerySelectorAll(program, body).Should().Equal(cell);
        group.RemoveChild(hot);
        SelectorMatcher.QuerySelectorAll(program, body).Should().BeEmpty();
        group.AppendChild(hot);
        SelectorMatcher.QuerySelectorAll(program, body).Should().Equal(cell);
    }

    [Test]
    public void OnlyProcessedColElementsSupplyTheLeftSide()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var empty = Add(document, table, "colgroup");
        empty.SetAttribute("span", "3");
        var group = Add(document, table, "colgroup");
        var hot = Add(document, group, "col");
        hot.SetAttribute("class", "hot");
        hot.SetAttribute("span", "2");
        var direct = Add(document, table, "col");
        direct.SetAttribute("class", "direct");
        var row = Add(document, table, "tr");
        var cell = Add(document, row, "td");
        cell.SetAttribute("colspan", "4");
        var late = Add(document, table, "colgroup");
        var lateColumn = Add(document, late, "col");
        lateColumn.SetAttribute("class", "late");

        SelectorMatcher.Matches(Parse("col.hot || td"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("col.direct || td"), cell).Should().BeFalse();
        SelectorMatcher.Matches(Parse("col.late || td"), cell).Should().BeFalse();
        SelectorMatcher.Matches(Parse("colgroup || td"), cell).Should().BeFalse();
        SelectorMatcher.Matches(Parse("colgroup > col.hot || td"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-col(4)"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("td:nth-col(5)"), cell).Should().BeFalse();
    }

    [Test]
    public void HtmlNamespaceRolesWorkInXmlAndNestedTablesStaySeparate()
    {
        var document = Document.CreateXml();
        var outer = document.CreateElementNS(Namespaces.Html, "table");
        document.AppendChild(outer);
        var group = document.CreateElementNS(Namespaces.Html, "colgroup");
        var column = document.CreateElementNS(Namespaces.Html, "col");
        var row = document.CreateElementNS(Namespaces.Html, "tr");
        var cell = document.CreateElementNS(Namespaces.Html, "td");
        outer.AppendChild(group);
        group.AppendChild(column);
        outer.AppendChild(row);
        row.AppendChild(cell);
        var nested = document.CreateElementNS(Namespaces.Html, "table");
        var nestedRow = document.CreateElementNS(Namespaces.Html, "tr");
        var nestedCell = document.CreateElementNS(Namespaces.Html, "td");
        cell.AppendChild(nested);
        nested.AppendChild(nestedRow);
        nestedRow.AppendChild(nestedCell);
        var foreign = document.CreateElementNS("urn:foreign", "td");
        row.AppendChild(foreign);
        var uppercase = document.CreateElementNS(Namespaces.Html, "TD");
        row.AppendChild(uppercase);

        SelectorMatcher.Matches(Parse("*|*:nth-col(1)"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("*|* || *|*"), cell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("*|* || *|*"), nestedCell).Should().BeFalse();
        SelectorMatcher.Matches(Parse("*|*:nth-col(1)"), nestedCell).Should().BeTrue();
        SelectorMatcher.Matches(Parse("*|*:nth-col(1)"), foreign).Should().BeFalse();
        SelectorMatcher.Matches(Parse("*|*:nth-col(1)"), uppercase).Should().BeFalse();
    }

    [Test]
    public void AttributeValueAndAdoptionRebuildTheRelevantTable()
    {
        var firstDocument = Document.CreateHtml();
        var first = Add(firstDocument, firstDocument, "table");
        var firstGroup = Add(firstDocument, first, "colgroup");
        var selected = Add(firstDocument, firstGroup, "col");
        selected.SetAttribute("class", "selected");
        var firstRow = Add(firstDocument, first, "tr");
        var cell = Add(firstDocument, firstRow, "td");
        var program = Parse("col.selected || td");
        SelectorMatcher.QuerySelectorAll(program, first).Should().Equal(cell);

        selected.GetAttributeNode("span").Should().BeNull();
        selected.SetAttribute("span", "2");
        selected.GetAttributeNode("span")!.Value = "0";
        SelectorMatcher.QuerySelectorAll(program, first).Should().Equal(cell);
        firstRow.RemoveChild(cell);
        SelectorMatcher.QuerySelectorAll(program, first).Should().BeEmpty();

        var secondDocument = Document.CreateHtml();
        var second = Add(secondDocument, secondDocument, "table");
        var secondGroup = Add(secondDocument, second, "colgroup");
        var secondColumn = Add(secondDocument, secondGroup, "col");
        secondColumn.SetAttribute("class", "selected");
        secondColumn.SetAttribute("span", "2");
        var secondRow = Add(secondDocument, second, "tr");
        secondDocument.AdoptNode(cell);
        secondRow.AppendChild(cell);
        SelectorMatcher.QuerySelectorAll(program, second).Should().Equal(cell);
        SelectorMatcher.QuerySelectorAll(program, first).Should().BeEmpty();
        var clone = (Element) cell.CloneNode();
        secondRow.AppendChild(clone);
        SelectorMatcher.QuerySelectorAll(program, second).Should().Equal(cell, clone);
    }

    [Test]
    public void AttributeValueAndColumnReorderChangeAReusedProgramsAnswer()
    {
        var document = Document.CreateHtml();
        var table = Add(document, document, "table");
        var group = Add(document, table, "colgroup");
        var spacer = Add(document, group, "col");
        spacer.SetAttribute("span", "1");
        var selected = Add(document, group, "col");
        selected.SetAttribute("class", "selected");
        var row = Add(document, table, "tr");
        var firstCell = Add(document, row, "td");
        var target = Add(document, row, "td");
        var program = Parse("col.selected || td");

        var original = SelectorMatcher.QuerySelectorAll(program, table);
        original.Should().Equal(target);
        spacer.GetAttributeNode("span")!.Value = "2";
        SelectorMatcher.QuerySelectorAll(program, table).Should().BeEmpty();
        spacer.GetAttributeNode("span")!.Value = "1";
        SelectorMatcher.QuerySelectorAll(program, table).Should().Equal(target);
        group.InsertBefore(selected, spacer);
        SelectorMatcher.QuerySelectorAll(program, table).Should().Equal(firstCell);
        group.AppendChild(selected);
        SelectorMatcher.QuerySelectorAll(program, table).Should().Equal(target);
        original.Should().Equal(target);
    }
}
