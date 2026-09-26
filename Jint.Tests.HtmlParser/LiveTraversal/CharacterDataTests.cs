#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class CharacterDataTests
{
    [TestCase(0u)] [TestCase(2u)] [TestCase(4u)]
    public void SplitTransfersOnlyStrictlyLaterEndpointsAndParentGap(uint offset)
    {
        var doc = Document.CreateXml(); var parent = doc.CreateElement("p"); var text = doc.CreateTextNode("abcd"); parent.AppendChild(text);
        var range = new DomRange(doc); range.SetStart(new(text), offset); range.SetEnd(new(text), 4);
        var gap = new DomRange(doc); gap.SetStart(new(parent), 1); gap.Collapse(true);
        var tail = NativeCharacterData.SplitText(text, offset);
        range.Start.Should().Be(new BoundaryPoint(new(text), offset));
        range.End.Should().Be(offset < 4 ? new BoundaryPoint(new(tail), 4 - offset) : new BoundaryPoint(new(text), 4));
        gap.Start.Should().Be(new BoundaryPoint(new(parent), 2));
        (text.Data + tail.Data).Should().Be("abcd");
    }
    [Test]
    public void DetachedSplitClampsInsteadOfTransferringAndCdataCreatesText()
    {
        var doc = Document.CreateXml(); var cdata = doc.CreateCDataSection("a\ud83d\ude00b"); var range = new DomRange(doc);
        range.SetStart(new(cdata), 2); range.SetEnd(new(cdata), 4);
        var tail = NativeCharacterData.SplitText(cdata, 2);
        range.End.Should().Be(new BoundaryPoint(new(cdata), 2)); tail.Data.Should().Be("\ude00b");
        NativeCharacterData.SubstringData(cdata, 1, uint.MaxValue).Should().Be("\ud83d");
    }
    [Test]
    public void NormalizeTransfersTextAndParentEndpointsWithoutCrossingCdata()
    {
        var doc = Document.CreateXml(); var parent = doc.CreateElement("p");
        var a = doc.CreateTextNode("ab"); var empty = doc.CreateTextNode(""); var b = doc.CreateTextNode("cd");
        var cdata = doc.CreateCDataSection("x"); var c = doc.CreateTextNode("ef");
        parent.AppendChild(a); parent.AppendChild(empty); parent.AppendChild(b); parent.AppendChild(cdata); parent.AppendChild(c);
        var range = new DomRange(doc); range.SelectNodeContents(new(b));
        var gap = new DomRange(doc); gap.SetStart(new(parent), 2); gap.Collapse(true);
        NativeCharacterData.Normalize(parent);
        parent.ChildCount.Should().Be(3); a.Data.Should().Be("abcd"); c.Data.Should().Be("ef");
        range.Start.Should().Be(new BoundaryPoint(new(a), 2)); range.End.Offset.Should().Be(4);
        gap.Start.Should().Be(new BoundaryPoint(new(a), 2));
    }
    [Test]
    public void PartialReplacementRepairsUtf16AndEqualFullReplacementCollapses()
    {
        var doc = Document.CreateXml(); var pi = doc.CreateProcessingInstruction("x", "abcde"); var range = new DomRange(doc);
        range.SetStart(new(pi), 2); range.SetEnd(new(pi), 5);
        NativeCharacterData.ReplaceData(pi, 1, 2, "XYZZ");
        range.Start.Offset.Should().Be(1); range.End.Offset.Should().Be(7); pi.Data.Should().Be("aXYZZde");
        pi.Data = pi.Data; range.End.Offset.Should().Be(0);
        Assert.Throws<DomException>(() => NativeCharacterData.ReplaceData(pi, uint.MaxValue, 0, ""))!.Name.Should().Be("IndexSizeError");
        Assert.Throws<ArgumentException>(() => NativeCharacterData.GetLength(doc));
    }
    [TestCase(false)]
    [TestCase(true)]
    public void ManyShortNormalizationRunsHaveLinearLinkWork(bool withRange)
    {
        var small = NormalizeRuns(256, withRange);
        var large = NormalizeRuns(512, withRange);
        large.Should().BeLessThanOrEqualTo(small * 2 + 4);
        large.Should().BeLessThanOrEqualTo(512 * 8 + 4);
    }

    private static int NormalizeRuns(int count, bool withRange)
    {
        var doc = Document.CreateHtml();
        var parent = doc.CreateElement("div");
        for (var i = 0; i < count; i++)
        {
            parent.AppendParsedChild(doc.CreateTextNode("a"));
            parent.AppendParsedChild(doc.CreateTextNode("b"));
            parent.AppendParsedChild(doc.CreateElement("sep"));
        }
        var range = withRange ? new DomRange(doc) : null;
        range?.SetStart(new(parent), (uint) parent.ChildCount);
        range?.Collapse(true);
        var work = 0;
        NativeCharacterData.Normalize(parent, steps => work = steps);
        parent.ChildCount.Should().Be(count * 2);
        for (var child = parent.FirstChild; child is not null; child = child.NextSibling!.NextSibling)
            ((Text) child).Data.Should().Be("ab");
        if (range is not null) range.Start.Offset.Should().Be((uint) parent.ChildCount);
        return work;
    }

    [Test]
    public void NormalizeTracksIndicesAcrossNestedRunsAndEmptyTextRemovals()
    {
        var doc = Document.CreateHtml(); var parent = doc.CreateElement("div"); var branch = doc.CreateElement("section");
        parent.AppendChild(doc.CreateTextNode("")); parent.AppendChild(branch); parent.AppendChild(doc.CreateTextNode("a")); parent.AppendChild(doc.CreateTextNode("b"));
        branch.AppendChild(doc.CreateTextNode("")); branch.AppendChild(doc.CreateTextNode("x")); branch.AppendChild(doc.CreateTextNode("y"));
        var outer = new DomRange(doc); outer.SetStart(new(parent), 4); outer.Collapse(true);
        var inner = new DomRange(doc); inner.SetStart(new(branch), 3); inner.Collapse(true);
        NativeCharacterData.Normalize(parent);
        parent.ChildCount.Should().Be(2); branch.ChildCount.Should().Be(1);
        ((Text) branch.FirstChild!).Data.Should().Be("xy"); ((Text) parent.LastChild!).Data.Should().Be("ab");
        outer.Start.Should().Be(new BoundaryPoint(new(parent), 2)); inner.Start.Should().Be(new BoundaryPoint(new(branch), 1));
    }

}
