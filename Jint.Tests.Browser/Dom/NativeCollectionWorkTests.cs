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
