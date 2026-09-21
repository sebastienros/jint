namespace Jint.Tests.Browser.Dom;

public class ProcessingInstructionRangeTests
{
    [TestCase("cloneContents", "whole", "data|false")]
    [TestCase("extractContents", "whole", "data|true")]
    [TestCase("cloneContents", "same", "at|false")]
    [TestCase("extractContents", "same", "at|false")]
    [TestCase("cloneContents", "start", "ata|false")]
    [TestCase("extractContents", "start", "ata|false")]
    [TestCase("cloneContents", "end", "dat|false")]
    [TestCase("extractContents", "end", "dat|false")]
    public void RangePreservesPiDataAndDistinguishesCopiesFromMoves(string operation, string mode, string expected)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const p = document.createProcessingInstruction('t', 'data');
              const div = document.createElement('div'); div.append(p);
              const range = document.createRange();
              if ('{{mode}}' === 'whole') range.selectNode(p);
              else if ('{{mode}}' === 'same') { range.setStart(p, 1); range.setEnd(p, 3); }
              else if ('{{mode}}' === 'start') { range.setStart(p, 1); range.setEnd(div, 1); }
              else { range.setStart(div, 0); range.setEnd(p, 3); }
              const copy = range.{{operation}}().firstChild;
              return copy.data + '|' + (copy === p);
            })()
            """).ToString().Should().Be(expected);
    }

    [TestCase("cloneContents")]
    [TestCase("extractContents")]
    public void MixedPiBoundariesKeepNativeSubstringsAndContainedData(string operation)
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate($$"""
            (() => {
              const div = document.createElement('div');
              const first = document.createProcessingInstruction('first', 'abcd');
              const middle = document.createProcessingInstruction('middle', 'data');
              const last = document.createProcessingInstruction('last', 'wxyz');
              const inner = document.createElement('span'); inner.append(middle);
              div.append(first, inner, last);
              const range = document.createRange(); range.setStart(first, 2); range.setEnd(last, 2);
              const copy = range.{{operation}}();
              return [copy.firstChild.data, copy.children[0].firstChild.data, copy.lastChild.data].join('|');
            })()
            """).ToString().Should().Be("cd|data|wx");
    }

    [Test]
    public void FullyContainedTemplateCopyPreservesPiContent()
    {
        using var fixture = DomTestFixture.Create("");
        fixture.Evaluate("""
            (() => {
              const div = document.createElement('div'); const template = document.createElement('template');
              template.content.append(document.createProcessingInstruction('t', 'data')); div.append(template);
              const range = document.createRange(); range.selectNode(template);
              return range.cloneContents().firstChild.content.firstChild.data;
            })()
            """).ToString().Should().Be("data");
    }
}
