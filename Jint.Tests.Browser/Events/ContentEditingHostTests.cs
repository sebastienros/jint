using Jint.Browser.Events;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Events;

public sealed class ContentEditingHostTests
{
    [TestCase(null, "parent")]
    [TestCase("", "child")]
    [TestCase("TrUe", "child")]
    [TestCase("PLAINTEXT-ONLY", "child")]
    [TestCase("false", null)]
    [TestCase(" true ", "parent")]
    [TestCase("\tfalse\n", "parent")]
    [TestCase("\u00a0true\u00a0", "parent")]
    [TestCase("invalid", "parent")]
    public void HostLookupUsesExactEnumeratedKeywordsAndInheritsInvalidValues(string? value, string? expected)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        parent.SetAttribute("id", "parent");
        parent.SetAttribute("contenteditable", "true");
        var child = document.CreateElement("span");
        child.SetAttribute("id", "child");
        if (value is not null) child.SetAttribute("contenteditable", value);
        parent.AppendChild(child);

        ContentEditing.HostOf(child)?.GetAttribute("id").Should().Be(expected);
        ContentEditing.HostOf(child, new global::Jint.Browser.Dom.DomReadWork(null, default))?.GetAttribute("id")
            .Should().Be(expected);
    }

    [TestCase("input")]
    [TestCase("textarea")]
    [TestCase("select")]
    [TestCase("button")]
    public void EditorRoutingKeepsFormControlsOutOfAnAncestorEditingHost(string name)
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        host.SetAttribute("contenteditable", "true");
        var control = document.CreateElement(name);
        control.SetAttribute("contenteditable", "true");
        host.AppendChild(control);

        ContentEditing.HostOf(control).Should().BeNull();
    }

    [Test]
    public void DesignModeRoutesInheritedEditingToTheActualDocumentElement()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        ContentEditing.HostOf(child).Should().BeNull();
        global::Jint.Browser.Dom.DomDocumentState.Of(document).DesignModeEnabled = true;
        ContentEditing.HostOf(child).Should().BeSameAs(root);
        root.SetAttribute("contenteditable", "false");
        ContentEditing.HostOf(root).Should().BeSameAs(root);
        child.SetAttribute("contenteditable", "false");
        ContentEditing.HostOf(child).Should().BeNull();
        global::Jint.Browser.Dom.DomDocumentState.Of(document).DesignModeEnabled = false;
        child.RemoveAttribute("contenteditable");
        ContentEditing.HostOf(child).Should().BeNull();
    }

    [TestCase(Namespaces.Svg, "svg", true)]
    [TestCase(Namespaces.MathMl, "math", true)]
    [TestCase(Namespaces.Svg, "g", false)]
    [TestCase("urn:other", "svg", false)]
    [TestCase("urn:other", "math", false)]
    public void OnlyExactEligibleForeignRootsCanReachAnAncestorHtmlEditingHost(string ns, string name, bool eligible)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        parent.SetAttribute("contenteditable", "true");
        var candidate = document.CreateElementNS(ns, name);
        candidate.SetAttribute("contenteditable", "true");
        parent.AppendChild(candidate);
        if (eligible) ContentEditing.HostOf(candidate).Should().BeSameAs(parent);
        else ContentEditing.HostOf(candidate).Should().BeNull();
    }

    [TestCase(Namespaces.Svg, "svg")]
    [TestCase(Namespaces.MathMl, "math")]
    public void AStandaloneForeignRootCannotBecomeAnEditingHostFromItsOwnAttribute(string ns, string name)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElementNS(ns, name);
        root.SetAttribute("contenteditable", "true");
        document.AppendChild(root);
        ContentEditing.HostOf(root).Should().BeNull();
        var realm = global::Jint.Browser.Dom.DomRealm.Of(new Engine());
        FocusController.IsFocusable(realm, root).Should().BeFalse();
    }

    [Test]
    public void AnIneligibleForeignAncestorBlocksEditorRoutingToAnHtmlHost()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        host.SetAttribute("contenteditable", "true");
        var barrier = document.CreateElementNS(Namespaces.Svg, "g");
        var child = document.CreateElement("span");
        host.AppendChild(barrier);
        barrier.AppendChild(child);
        ContentEditing.HostOf(child).Should().BeNull();
    }

    [Test]
    public void DesignModeHostQualificationUsesTheRootNamespaceRatherThanDocumentKind()
    {
        var xml = Document.CreateXml();
        var html = xml.CreateElementNS(Namespaces.Html, "html");
        xml.AppendChild(html);
        global::Jint.Browser.Dom.DomDocumentState.Of(xml).DesignModeEnabled = true;
        ContentEditing.HostOf(html).Should().BeSameAs(html);

        var htmlDocument = Document.CreateHtml();
        var svg = htmlDocument.CreateElementNS(Namespaces.Svg, "svg");
        htmlDocument.AppendChild(svg);
        global::Jint.Browser.Dom.DomDocumentState.Of(htmlDocument).DesignModeEnabled = true;
        ContentEditing.HostOf(svg).Should().BeNull();
    }
}
