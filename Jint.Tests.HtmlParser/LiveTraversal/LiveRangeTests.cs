#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class LiveRangeTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void FragmentInsertionAndRemovalRepairBothEndpoints(bool observe)
    {
        var doc = Document.CreateHtml();
        var parent = doc.CreateElement("div");
        doc.AppendChild(parent);
        var a = doc.CreateTextNode("abc");
        var b = doc.CreateTextNode("def");
        parent.AppendChild(a); parent.AppendChild(b);
        using var observer = observe ? doc.ObserveMutations(parent, new MutationObserverOptions { ChildList = true, Subtree = true }) : null;
        var range = new DomRange(doc);
        range.SetStart(new(parent), 1); range.SetEnd(new(parent), 2);
        var fragment = doc.CreateDocumentFragment();
        fragment.AppendChild(doc.CreateComment("x")); fragment.AppendChild(doc.CreateComment("y"));
        parent.InsertBefore(fragment, b);
        range.Start.Offset.Should().Be(1); range.End.Offset.Should().Be(4);
        range.SetStart(new(a), 2);
        parent.RemoveChild(a);
        range.Start.Should().Be(new BoundaryPoint(new(parent), 0));
        range.End.Offset.Should().Be(3);
        parent.ReplaceChildren();
        range.Collapsed.Should().BeTrue();
    }

    [Test]
    public void EqualDataWritesReplaceAndParserAppendKeepsExactEnd()
    {
        var doc = Document.CreateHtml(); var text = doc.CreateTextNode("abc");
        var range = new DomRange(doc); range.SetStart(new(text), 1); range.SetEnd(new(text), 3);
        text.AppendParsedData("def".AsSpan(), default);
        range.End.Offset.Should().Be(3);
        text.Data = "abcdef";
        range.Start.Offset.Should().Be(0); range.End.Offset.Should().Be(0);
    }

    [Test]
    public void AdoptionRehomesDetachedRootAndCloneIsIndependentlyLive()
    {
        var doc = Document.CreateXml(); var other = Document.CreateXml();
        var text = doc.CreateTextNode("abc");
        var range = new DomRange(doc); range.SelectNodeContents(new(text));
        var clone = range.CloneRange(); clone.Collapse(true);
        other.AdoptNode(text); text.Data = "xy";
        range.End.Offset.Should().Be(0); clone.End.Offset.Should().Be(0);
        range.SetEnd(new(text), 2); range.GetText(default).Should().Be("xy");
        clone.Collapsed.Should().BeTrue();
    }

    [Test]
    public void SettersCollapseAcrossRootsAndRejectUnsignedOverflowBeforeMutation()
    {
        var doc = Document.CreateHtml(); var a = doc.CreateTextNode("abc"); var b = doc.CreateTextNode("xyz");
        var range = new DomRange(doc); range.SelectNodeContents(new(a));
        Assert.Throws<DomException>(() => range.SetEnd(new(a), uint.MaxValue))!.Name.Should().Be("IndexSizeError");
        range.End.Offset.Should().Be(3);
        range.SetEnd(new(b), 2); range.Start.Should().Be(range.End);
        var attr = doc.CreateAttribute("x"); range.SetStart(new(attr), 0); range.Collapsed.Should().BeTrue();
    }
}
