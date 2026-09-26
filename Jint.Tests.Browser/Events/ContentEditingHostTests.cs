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
}
