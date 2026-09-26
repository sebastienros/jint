using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class DescendantTextWorkTests
{
    [Test]
    public void NativeTextReadKeepsRawTextAndDoesNotCrossTemplateContents()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        root.AppendChild(document.CreateTextNode("a\uD800"));
        root.AppendChild(document.CreateComment("ignored"));
        var child = document.CreateElement("span");
        child.AppendChild(document.CreateTextNode("b"));
        root.AppendChild(child);
        var template = document.CreateElement("template");
        template.TemplateContent!.AppendChild(document.CreateTextNode("ignored template"));
        root.AppendChild(template);
        DomDescendantText.Read(root, null, default).Should().Be("a\uD800b");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeTextReadChecksWideLinksAndLongCharacterData(bool longData)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        if (longData) root.AppendChild(document.CreateTextNode(new string('x', 8192)));
        else for (var i = 0; i < 8192; i++) root.AppendChild(document.CreateComment(""));
        var checks = 0;
        Assert.Throws<OperationCanceledException>(() => DomDescendantText.Read(root,
            _ => { if (++checks == 3) throw new OperationCanceledException(); }, default));
        checks.Should().Be(3);
    }
}
