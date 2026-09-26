#nullable enable

using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeMapImagesTests
{
    [Test]
    public void ImagesViewIsSameObjectLiveImgOnlyAndFirstMapWinsEachName()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var first = document.CreateElement("map");
        first.SetAttributeNS(null, "name", "x");
        var second = document.CreateElement("map");
        second.SetAttributeNS(null, "id", "x");
        second.SetAttributeNS(null, "name", "y");
        root.AppendChild(first); root.AppendChild(second);
        var x = document.CreateElement("img"); x.SetAttributeNS(null, "usemap", "junk#x"); root.AppendChild(x);
        var y = document.CreateElement("img"); y.SetAttributeNS(null, "usemap", "#y"); root.AppendChild(y);
        var obj = document.CreateElement("object"); obj.SetAttributeNS(null, "usemap", "#y"); root.AppendChild(obj);
        var view = BrowserMapImages.Of(second);
        view.Should().BeSameAs(BrowserMapImages.Of(second));
        realm.WrapCollection(view).Should().BeSameAs(realm.WrapCollection(view));
        view.GetLength(realm).Should().Be(1);
        view.GetItem(realm, 0).Should().BeSameAs(y);
        root.RemoveChild(first);
        view.GetLength(realm).Should().Be(2);
        view.GetItem(realm, 0).Should().BeSameAs(x);
        view.GetItem(realm, 2).Should().BeNull();
        x.SetAttributeNS(null, "usemap", "#other");
        view.GetLength(realm).Should().Be(1);
        var otherDocument = Document.CreateHtml();
        var otherRoot = otherDocument.CreateElement("html"); otherDocument.AppendChild(otherRoot);
        otherRoot.AppendChild(otherDocument.AdoptNode(second));
        view.GetLength(realm).Should().Be(0);
        otherRoot.AppendChild(otherDocument.AdoptNode(y));
        view.GetLength(realm).Should().Be(1);
    }

    [TestCase("  junk#x", "x")]
    [TestCase("# x", " x")]
    [TestCase("#x ", "x ")]
    [TestCase("#x#y", "x#y")]
    [TestCase("#", null)]
    [TestCase("x", null)]
    public void HashNameUsesLiteralSuffixAfterFirstHash(string raw, string? expected)
    {
        var work = new DomReadWork(null, default);
        BrowserMapImages.HashName(raw, work).Should().Be(expected);
        work.Check();
    }

    [Test]
    public void HashNameChargesLongSuffixBeforeCopying()
    {
        var charged = 0;
        var work = new DomReadWork(units => { charged += units; if (charged >= 256) throw new OperationCanceledException(); }, default);
        Caught.Exception(() => BrowserMapImages.HashName("#" + new string('x', 8192), work)).Should().BeOfType<OperationCanceledException>();
        charged.Should().Be(256);
    }

    [Test]
    public void ImagesDoNotCrossShadowTreeBoundary()
    {
        using var engine = new Engine(options => options.UseWebApis());
        var realm = DomRealm.Of(engine);
        var document = Document.CreateHtml();
        var host = document.CreateElement("div"); document.AppendChild(host);
        var shadow = ShadowTree.Attach(host, new(ShadowRootMode.Open), default);
        var map = document.CreateElement("map"); map.SetAttributeNS(null, "name", "x"); shadow.AppendChild(map);
        var image = document.CreateElement("img"); image.SetAttributeNS(null, "usemap", "#x"); host.AppendChild(image);
        var view = BrowserMapImages.Of(map);
        view.GetLength(realm).Should().Be(0);
        shadow.AppendChild(image);
        view.GetLength(realm).Should().Be(1);
    }
}
