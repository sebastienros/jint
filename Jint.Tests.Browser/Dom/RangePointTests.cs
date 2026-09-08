namespace Jint.Tests.Browser.Dom;

/// <summary>DOM §5.5 compares inclusive boundary points, including the sole point of an Attr root.</summary>
public sealed class RangePointTests
{
    [TestCase("range.comparePoint(attr, 0)", "WrongDocumentError")]
    [TestCase("range.comparePoint(attr, 1)", "WrongDocumentError")]
    [TestCase("range.isPointInRange(attr, 1)", "false")]
    [TestCase("range.selectNodeContents(attr); return range.comparePoint(attr, 0)", "0")]
    [TestCase("range.selectNodeContents(attr); return range.isPointInRange(attr, 0)", "true")]
    [TestCase("range.selectNodeContents(attr); return range.comparePoint(attr, 1)", "IndexSizeError")]
    [TestCase("range.selectNodeContents(attr); return range.isPointInRange(attr, 1)", "IndexSizeError")]
    [TestCase("range.selectNodeContents(attr); return range.isPointInRange(attr, -1)", "IndexSizeError")]
    [TestCase("range.selectNodeContents(attr); return range.isPointInRange(attr, 4294967296)", "true")]
    [TestCase("range.comparePoint(document.doctype, 99)", "InvalidNodeTypeError")]
    [TestCase("range.isPointInRange(document.doctype, 99)", "InvalidNodeTypeError")]
    [TestCase("range.selectNodeContents(attr); return range.comparePoint(document.doctype, 99)", "WrongDocumentError")]
    [TestCase("range.selectNodeContents(attr); return range.isPointInRange(document.doctype, 99)", "false")]
    [TestCase("range.comparePoint(attr, { valueOf() { throw new Error('conversion'); } })", "Error")]
    [TestCase("range.isPointInRange(attr, { valueOf() { throw new Error('conversion'); } })", "Error")]
    public void RootAndOffsetChecksFollowTheDomOrder(string source, string expected)
    {
        using var fixture = DomTestFixture.Create("<!doctype html><div x='abc'>hello</div>");
        fixture.Text($$"""
            String((() => {
              const attr = document.querySelector('div').getAttributeNode('x');
              const range = document.createRange();
              try { {{(source.Contains("return", StringComparison.Ordinal) ? source : "return " + source)}}; }
              catch (error) { return error.name; }
            })())
            """).Should().Be(expected);
    }

    [TestCase(0, -1, false)]
    [TestCase(1, 0, true)]
    [TestCase(2, 0, true)]
    [TestCase(4, 0, true)]
    [TestCase(5, 1, false)]
    public void TextPointsIncludeBothEndpoints(int offset, int comparison, bool contained)
    {
        using var fixture = DomTestFixture.Create("<!doctype html><div>hello</div>");
        fixture.Text($$"""
            (() => {
              const text = document.querySelector('div').firstChild;
              const range = document.createRange();
              range.setStart(text, 1); range.setEnd(text, 4);
              return JSON.stringify([range.comparePoint(text, {{offset}}), range.isPointInRange(text, {{offset}})]);
            })()
            """).Should().Be($"[{comparison},{contained.ToString().ToLowerInvariant()}]");
    }

    [TestCase("before", -1, false)]
    [TestCase("start", 0, true)]
    [TestCase("middle", 0, true)]
    [TestCase("end", 0, true)]
    [TestCase("after", 1, false)]
    public void DistinctContainersUseTreeOrderWithoutChangingTheRange(string id, int comparison, bool contained)
    {
        using var fixture = DomTestFixture.Create("<!doctype html><div id='before'></div><div id='start'></div><div id='middle'></div><div id='end'></div><div id='after'></div>");
        fixture.Text($$"""
            (() => {
              const range = document.createRange();
              const start = document.getElementById('start'), end = document.getElementById('end');
              range.setStart(start, 0); range.setEnd(end, 0);
              const point = document.getElementById('{{id}}');
              return JSON.stringify([range.comparePoint(point, 0), range.isPointInRange(point, 0),
                range.startContainer === start && range.endContainer === end && range.startOffset === 0 && range.endOffset === 0]);
            })()
            """).Should().Be($"[{comparison},{contained.ToString().ToLowerInvariant()},true]");
    }
}
