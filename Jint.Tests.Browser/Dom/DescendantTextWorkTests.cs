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
    [Test]
    public void ParserBackedTextReadChargesCharactersBeforeMaterializingData()
    {
        using var fixture = DomTestFixture.Create("<div>" + new string('x', 8192) + "</div>");
        var text = fixture.Document.DocumentElement!.LastChild!.FirstChild!.FirstChild!;
        var cachedData = typeof(Text).GetField("_cachedParsedData",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        cachedData.GetValue(text).Should().BeNull();
        var charged = 0;
        Assert.Throws<OperationCanceledException>(() => DomDescendantText.Read(text.ParentNode!, units =>
        {
            charged += units;
            if (charged >= 512) throw new OperationCanceledException();
        }, default));
        charged.Should().Be(512);
        cachedData.GetValue(text).Should().BeNull();
        DomDescendantText.Read(text.ParentNode!, null, default).Should().Be(new string('x', 8192));
        cachedData.GetValue(text).Should().BeNull();
    }

    [Test]
    public void ChildTextDoesNotIncludeNestedElements()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("script");
        root.AppendChild(document.CreateTextNode("a"));
        var nested = document.CreateElement("span");
        nested.AppendChild(document.CreateTextNode("b"));
        root.AppendChild(nested);
        root.AppendChild(document.CreateTextNode("c"));
        DomDescendantText.ReadChildren(root, null, default).Should().Be("ac");
        DomDescendantText.Read(root, null, default).Should().Be("abc");
    }

}
