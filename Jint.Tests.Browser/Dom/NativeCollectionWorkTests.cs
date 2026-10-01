using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeCollectionWorkTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ElementCollectionReadsCheckNonElementChildLinks(bool descendants)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < 2048; i++) root.AppendChild(document.CreateComment(""));
        root.AppendChild(document.CreateElement("span"));
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        DomHtmlCollection<Element> collection = descendants
            ? new DomLiveHtmlCollection(root, new EveryElement()) : DomChildHtmlCollection.Of(root);
        probe.Remaining = 3;
        Assert.Throws<OperationCanceledException>(() => collection.GetLength(realm));
        probe.Remaining = 0;
        collection.GetLength(realm).Should().Be(1);
    }

    [Test]
    public void NativeChildNodeCursorChecksLongWalkAndStillUsesTheActualMutationStamp()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < 2048; i++) root.AppendChild(document.CreateComment(""));
        var list = DomChildNodeList.Of(root);
        var checks = 0;
        Assert.Throws<OperationCanceledException>(() => list.ReadItem(2047,
            _ => { if (++checks == 3) throw new OperationCanceledException(); }, default));
        var last = list.ReadItem(2047, null, default);
        last.Should().BeSameAs(root.LastChild);
        root.InsertBefore(document.CreateComment("new"), root.FirstChild);
        list.ReadItem(2047, null, default).Should().BeSameAs(root.LastChild!.PreviousSibling);
    }

    [Test]
    public void NativeChildNodeCursorInvalidatesWhenAdoptedIntoAnotherDocumentWithTheSameStamp()
    {
        var source = Document.CreateHtml();
        var root = source.CreateElement("div");
        var first = source.CreateComment("first");
        var second = source.CreateComment("second");
        root.AppendChild(first);
        root.AppendChild(second);
        for (var i = 0; i < 32; i++) source.CreateElement("div").SetAttribute("data-padding", "");
        var list = DomChildNodeList.Of(root);
        list.ReadItem(1, null, default).Should().BeSameAs(second);
        var stamp = source.MutationStamp;
        var destination = Document.CreateHtml();
        destination.AdoptNode(root);
        root.AppendChild(first);
        while (destination.MutationStamp < stamp)
            destination.CreateElement("div").SetAttribute("data-padding", "");
        destination.MutationStamp.Should().Be(stamp);
        list.ReadItem(1, null, default).Should().BeSameAs(first);
    }

    [Test]
    public void NamedAttributeReadsUseTheCurrentRealmBudget()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < 2048; i++) root.SetAttribute("data-" + i, "");
        root.SetAttribute("id", "last");
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var map = DomNamedNodeMap.Of(root);
        probe.Remaining = 3;
        Assert.Throws<OperationCanceledException>(() => map.SupportedNames(realm));
        probe.Remaining = 3;
        Assert.Throws<OperationCanceledException>(() => map.GetNamedItem(realm, "ID"));
        probe.Remaining = 0;
        map.GetNamedItem(realm, "ID").Should().BeSameAs(root.GetAttributeNode("id"));
        map.HasSupportedName(realm, "ID").Should().BeFalse();
        map.HasSupportedName(realm, "id").Should().BeTrue();
        map.SupportedNames(realm).Should().HaveCount(2049);
    }

    private sealed class EveryElement : DomElementFilter
    {
        internal override bool Matches(Element element) => true;
    }

    private sealed class ReadProbe : Constraint
    {
        internal int Remaining;
        public override void Check()
        {
            if (Remaining > 0 && --Remaining == 0) throw new OperationCanceledException();
        }
        public override void Reset() { }
    }
}
