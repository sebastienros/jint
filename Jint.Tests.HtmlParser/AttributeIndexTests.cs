#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class AttributeIndexTests
{
    [Test]
    public void OwnedIndexPreservesIdentityAndTracksReplacementAndRemoval()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.GetAttributeAt(0).Should().BeNull();
        element.SetAttribute("a", "one");
        element.SetAttribute("b", "two");
        var retained = element.GetAttributeNode("b")!;
        element.GetAttributeAt(1).Should().BeSameAs(retained);
        var replacement = document.CreateAttribute("a");
        element.SetAttributeNode(replacement);
        element.GetAttributeAt(0).Should().BeSameAs(replacement);
        element.RemoveAttribute("a");
        element.GetAttributeAt(0).Should().BeSameAs(retained);
        element.GetAttributeAt(1).Should().BeNull();
        element.GetAttributeAt(uint.MaxValue).Should().BeNull();
        element.RemoveAttribute("b");
        element.GetAttributeAt(0).Should().BeNull();
        retained.Value.Should().Be("two");
    }
}
