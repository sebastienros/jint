#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class RangeContentsTests
{
    [TestCase(false)] [TestCase(true)]
    public void CrossBranchContentsPreservePartialClonesAndWholeNodeIdentity(bool extract)
    {
        var doc = Document.CreateXml(); var root = doc.CreateElement("root"); var a = doc.CreateElement("a"); var b = doc.CreateElement("b");
        var textA = doc.CreateTextNode("abcd"); var textB = doc.CreateTextNode("efgh"); var middle = doc.CreateElementNS("urn:x", "x:middle");
        middle.SetAttributeNS("urn:a", "a:key", "v");
        a.AppendChild(textA); b.AppendChild(textB); root.AppendChild(a); root.AppendChild(middle); root.AppendChild(b);
        var range = new DomRange(doc); range.SetStart(new(textA), 2); range.SetEnd(new(textB), 2);
        range.GetText(default).Should().Be("cdef");
        var fragment = extract ? range.ExtractContents() : range.CloneContents();
        fragment.ChildCount.Should().Be(3);
        ((Text) fragment.FirstChild!.FirstChild!).Data.Should().Be("cd");
        ((Text) fragment.LastChild!.FirstChild!).Data.Should().Be("ef");
        fragment.FirstChild.Should().NotBeSameAs(a);
        var copiedMiddle = (Element) fragment.FirstChild.NextSibling!;
        copiedMiddle.NamespaceUri.Should().Be("urn:x"); copiedMiddle.GetAttributeNS("urn:a", "key").Should().Be("v");
        if (extract)
        {
            copiedMiddle.Should().BeSameAs(middle); textA.Data.Should().Be("ab"); textB.Data.Should().Be("gh");
            range.Start.Should().Be(new BoundaryPoint(new(root), 1)); range.Collapsed.Should().BeTrue();
        }
        else { copiedMiddle.Should().NotBeSameAs(middle); range.GetText(default).Should().Be("cdef"); }
    }
    [Test]
    public void SameDataPiAndCdataCopiesUseMutableDataAndDeleteUtf16()
    {
        var doc = Document.CreateXml(); var pi = doc.CreateProcessingInstruction("x", "abc"); pi.Data = "a?>bc";
        var range = new DomRange(doc); range.SetStart(new(pi), 1); range.SetEnd(new(pi), 4);
        ((ProcessingInstruction) range.CloneContents().FirstChild!).Data.Should().Be("?>b"); range.GetText(default).Should().Be("");
        range.DeleteContents(); pi.Data.Should().Be("ac"); range.Collapsed.Should().BeTrue();
        var cdata = doc.CreateCDataSection("a\ud83d\ude00b"); range.SetStart(new(cdata), 2); range.SetEnd(new(cdata), 3);
        range.GetText(default).Should().Be("\ude00"); ((CDataSection) range.ExtractContents().FirstChild!).Data.Should().Be("\ude00");
    }
    [Test]
    public void InsertPrevalidatesBeforeSplitAndCollapsedInsertionExpandsRange()
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("div"); var text = doc.CreateTextNode("abcd"); root.AppendChild(text);
        var range = new DomRange(doc); range.SetStart(new(text), 2); range.Collapse(true);
        Assert.Throws<DomException>(() => range.InsertNode(new(root)))!.Name.Should().Be("HierarchyRequestError");
        text.Data.Should().Be("abcd"); root.ChildCount.Should().Be(1);
        var inserted = doc.CreateElement("b"); range.InsertNode(new(inserted)); root.ChildCount.Should().Be(3);
        range.Start.Should().Be(new BoundaryPoint(new(text), 2)); range.End.Should().Be(new BoundaryPoint(new(root), 2));
        ((Text) root.LastChild!).Data.Should().Be("cd");
    }
    [Test]
    public void DoctypeCopyFailsBeforeMutationButDeleteRemovesIt()
    {
        var doc = Document.CreateXml(); var type = doc.CreateDocumentType("root"); var root = doc.CreateElement("root"); doc.AppendChild(type); doc.AppendChild(root);
        var range = new DomRange(doc); range.SelectNode(new(type));
        Assert.Throws<DomException>(() => range.ExtractContents())!.Name.Should().Be("HierarchyRequestError");
        Assert.Throws<DomException>(() => range.CloneContents())!.Name.Should().Be("HierarchyRequestError");
        range.Collapsed.Should().BeFalse(); type.ParentNode.Should().BeSameAs(doc);
        range.DeleteContents(); type.ParentNode.Should().BeNull(); range.Collapsed.Should().BeTrue();
    }
    [Test]
    public void SurroundRefusesPartialNonTextBeforeParentTypeAndUsesTheActualRange()
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("div"); var a = doc.CreateElement("a"); var text = doc.CreateTextNode("abcd"); a.AppendChild(text); root.AppendChild(a);
        var range = new DomRange(doc); range.SetStart(new(text), 1); range.SetEnd(new(root), 1);
        Assert.Throws<DomException>(() => range.SurroundContents(new(doc)))!.Name.Should().Be("InvalidStateError");
        range.SetEnd(new(text), 3); var wrapper = doc.CreateElement("b"); range.SurroundContents(new(wrapper));
        ((Text) wrapper.FirstChild!).Data.Should().Be("bc"); range.Start.Container.Node.Should().BeSameAs(a); range.End.Offset.Should().Be(range.Start.Offset + 1);
    }
    [Test]
    public void GetTextUsesContainerGapsAndExcludesUnselectedSiblingText()
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("div"); root.AppendChild(doc.CreateTextNode("a")); root.AppendChild(doc.CreateTextNode("b")); root.AppendChild(doc.CreateTextNode("c"));
        var range = new DomRange(doc); range.SetStart(new(root), 1); range.SetEnd(new(root), 2); range.GetText(default).Should().Be("b");
        range.SetStart(new(root.FirstChild!), 1); range.SetEnd(new(root.LastChild!), 0); range.GetText(default).Should().Be("b");
    }
    [Test]
    public void FullTemplateCloneCopiesHostedContentsButPartialOrdinaryCopyDoesNot()
    {
        var doc = Document.CreateHtml(); var parent = doc.CreateElement("div"); var template = doc.CreateElement("template");
        template.TemplateContent!.AppendChild(template.TemplateContent.OwnerDocument!.CreateTextNode("hosted"));
        var ordinary = doc.CreateTextNode("light"); template.AppendChild(ordinary); parent.AppendChild(template);
        var range = new DomRange(doc); range.SelectNode(new(template));
        var full = (Element) range.CloneContents().FirstChild!;
        ((Text) full.TemplateContent!.FirstChild!).Data.Should().Be("hosted"); full.TemplateContent.FirstChild.Should().NotBeSameAs(template.TemplateContent.FirstChild);
        range.SetStart(new(ordinary), 2); range.SetEnd(new(parent), 1);
        var partial = (Element) range.CloneContents().FirstChild!;
        partial.TemplateContent!.ChildCount.Should().Be(0); ((Text) partial.FirstChild!).Data.Should().Be("ght");
    }
    [Test]
    public void DeepPartialCloneAndExtractUseFrames()
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("div"); var branch = doc.CreateElement("b"); root.AppendChild(branch);
        var node = branch;
        for (var i = 0; i < 3000; i++) { var child = doc.CreateElement("i"); node.AppendParsedChild(child); node = child; }
        var text = doc.CreateTextNode("abcd"); node.AppendParsedChild(text);
        var range = new DomRange(doc); range.SetStart(new(text), 2); range.SetEnd(new(root), 1);
        range.CloneContents().ChildCount.Should().Be(1);
        range.ExtractContents().ChildCount.Should().Be(1); text.Data.Should().Be("ab"); range.Collapsed.Should().BeTrue();
    }

}
