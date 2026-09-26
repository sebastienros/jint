#nullable enable

using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public sealed class AttributeStructureIdentityTests
{
    [Test]
    public void StructureProofIsLazyAndValueChangesKeepItsIdentity()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.SetAttributeNS(null, "class", "first");
        element.ExistingAttributeStructureIdentity.Should().BeNull();
        var identity = element.GetAttributeStructureIdentity();
        element.GetAttributeStructureIdentity().Should().BeSameAs(identity);
        element.GetAttributeNodeNS(null, "class")!.Value = "second";
        element.ExistingAttributeStructureIdentity.Should().BeSameAs(identity);
        element.SetAttributeNS(null, "class", "third");
        element.ExistingAttributeStructureIdentity.Should().BeSameAs(identity);
        document.CreateElement("span").AppendChild(element);
        element.ExistingAttributeStructureIdentity.Should().BeSameAs(identity);
        Document.CreateHtml().AdoptNode(element);
        element.ExistingAttributeStructureIdentity.Should().BeSameAs(identity);
        ((Element) element.CloneNode()).ExistingAttributeStructureIdentity.Should().BeNull();
    }

    [Test]
    public void AppendRemoveAndReplacementInvalidateWithoutAllocatingSuccessorToken()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        var initial = element.GetAttributeStructureIdentity();
        element.SetAttributeNS(null, "class", "value");
        element.ExistingAttributeStructureIdentity.Should().BeNull();
        var appended = element.GetAttributeStructureIdentity();
        appended.Should().NotBeSameAs(initial);
        var old = element.GetAttributeNodeNS(null, "class")!;
        var replacement = document.CreateAttribute("class"); replacement.Value = "replacement";
        element.SetAttributeNode(replacement).Should().BeSameAs(old);
        old.OwnerElement.Should().BeNull();
        element.ExistingAttributeStructureIdentity.Should().BeNull();
        var replaced = element.GetAttributeStructureIdentity();
        replaced.Should().NotBeSameAs(appended);
        element.SetAttributeNode(replacement);
        element.ExistingAttributeStructureIdentity.Should().BeSameAs(replaced);
        element.RemoveAttributeNode(replacement);
        element.ExistingAttributeStructureIdentity.Should().BeNull();
    }

    [Test]
    public void ParsedInitializationMergeAndCopyInvalidateOnlyStructuralChanges()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.GetAttributeStructureIdentity();
        element.InitializeParsedAttributes([new ParserAttribute(null, "class", null, "one")], default);
        element.ExistingAttributeStructureIdentity.Should().BeNull();
        var initialized = element.GetAttributeStructureIdentity();
        element.AddMissingParsedAttributes([new ParserAttribute(null, "class", null, "ignored")], default);
        element.ExistingAttributeStructureIdentity.Should().BeSameAs(initialized);
        element.AddMissingParsedAttributes([new ParserAttribute(null, "id", null, "new")], default);
        element.ExistingAttributeStructureIdentity.Should().BeNull();
        var copy = document.CreateElement("div");
        copy.GetAttributeStructureIdentity();
        copy.CopyAttributesFrom(element, document);
        copy.ExistingAttributeStructureIdentity.Should().BeNull();
    }
}
