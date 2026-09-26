#nullable enable

namespace Jint.Tests.Browser.Dom;

public sealed class TableMemberTests
{
    [Test]
    public void RowsAreLiveAndOrderHeadBeforeBodyBeforeFoot()
    {
        using var dom = DomTestFixture.Create("<table id=t><tfoot><tr id=f></tr></tfoot><tbody><tr id=b></tr></tbody><thead><tr id=h></tr></thead></table>");
        dom.Execute("var t=document.getElementById('t'), rows=t.rows;");
        dom.Text("Array.from(rows, r=>r.id).join(',')").Should().Be("h,b,f");
        dom.Bool("rows===t.rows").Should().BeTrue();
        dom.Execute("var r=t.insertRow(1); r.id='inserted';");
        dom.Text("Array.from(rows, r=>r.id).join(',')").Should().Be("h,inserted,b,f");
        dom.Number("r.rowIndex").Should().Be(1);
        dom.Number("r.sectionRowIndex").Should().Be(0);
        dom.Execute("t.deleteRow(-1);");
        dom.Number("rows.length").Should().Be(3);
    }

    [Test]
    public void TableCreationAndSectionReplacementFollowNativeLinks()
    {
        using var dom = DomTestFixture.Create("<table id=t><colgroup></colgroup><tbody id=b></tbody><!--tail--></table>");
        dom.Execute("var t=document.getElementById('t'), head=t.createTHead(), caption=t.createCaption(), body=t.createTBody();");
        dom.Bool("t.firstChild===caption && head.nextSibling.id==='b' && body.previousSibling.id==='b'").Should().BeTrue();
        dom.Bool("t.createCaption()===caption && t.createTHead()===head").Should().BeTrue();
        dom.Number("t.tBodies.length").Should().Be(2);
        dom.Execute("var foot=t.createTFoot();");
        dom.Bool("t.lastChild===foot").Should().BeTrue();
        dom.Text("(()=>{try{t.tHead=foot}catch(e){return e.name}})()").Should().Be("HierarchyRequestError");
        dom.Bool("t.tHead===head").Should().BeTrue();
        dom.Execute("t.caption=null;");
        dom.Bool("t.caption===null && caption.parentNode===null").Should().BeTrue();
    }

    [Test]
    public void EmptyTableAndDetachedSectionUseSpecifiedInsertionAndDeletionRules()
    {
        using var dom = DomTestFixture.Create("<table id=t></table>");
        dom.Execute("var t=document.getElementById('t'), r=t.insertRow(), cells=r.cells; var c=r.insertCell();");
        dom.Bool("r.parentNode===t.tBodies[0] && cells===r.cells && cells[0]===c").Should().BeTrue();
        dom.Number("c.cellIndex").Should().Be(0);
        dom.Execute("r.deleteCell(-1); r.deleteCell(-1); t.deleteRow(-1); t.deleteRow(-1);");
        dom.Number("cells.length").Should().Be(0);
        dom.Text("(()=>{try{r.deleteCell(0)}catch(e){return e.name}})()").Should().Be("IndexSizeError");
        dom.Text("(()=>{try{t.insertRow(1)}catch(e){return e.name}})()").Should().Be("IndexSizeError");
        dom.Execute("var s=document.createElement('tbody'), a=s.insertRow();");
        dom.Number("a.rowIndex").Should().Be(-1);
        dom.Number("a.sectionRowIndex").Should().Be(0);
    }
}
