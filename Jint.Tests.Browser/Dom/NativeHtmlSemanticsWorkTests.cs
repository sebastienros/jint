#nullable enable

using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeHtmlSemanticsWorkTests
{
    [Test]
    public void AttributeScansAndFinalChecksRemainInterruptible()
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var element = Document.CreateHtml().CreateElement("div");
        for (var i = 0; i < 1024; i++) element.SetAttributeNS(null, "data-" + i, "x");
        element.SetAttributeNS(null, "spellcheck", "true");
        probe.Remaining = 2;
        Caught.Exception(() => BrowserHtmlSemantics.GetSpellcheck(realm, element)).Should().BeOfType<OperationCanceledException>();
        probe.Remaining = 0;
        BrowserHtmlSemantics.GetSpellcheck(realm, element).Should().BeTrue();
        var small = element.OwnerDocument!.CreateElement("div");
        probe.Remaining = 2;
        Caught.Exception(() => BrowserHtmlSemantics.GetTranslate(realm, small)).Should().BeOfType<OperationCanceledException>();
    }

    [Test]
    public void MapReadChecksNonElementLinksAndFinalIndexedHit()
    {
        var probe = new ReadProbe();
        using var engine = new Engine(options => options.AddConstraint(probe));
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        var map = document.CreateElement("map"); map.SetAttributeNS(null, "name", "x"); root.AppendChild(map);
        for (var i = 0; i < 2048; i++) root.AppendChild(document.CreateComment(""));
        var image = document.CreateElement("img"); image.SetAttributeNS(null, "usemap", "#x"); root.AppendChild(image);
        var view = BrowserMapImages.Of(map);
        probe.Remaining = 3;
        Caught.Exception(() => view.GetLength(realm)).Should().BeOfType<OperationCanceledException>();
        probe.Remaining = 0;
        view.GetLength(realm).Should().Be(1);
        var detached = document.CreateElement("div");
        detached.AppendChild(map); detached.AppendChild(image);
        probe.Remaining = 2;
        Caught.Exception(() => view.GetItem(realm, 0)).Should().BeOfType<OperationCanceledException>();
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
