#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class TemplateOwnershipTests
{
    [Test]
    public void OnlyExactHtmlNamespaceLowercaseTemplateHasStableHostedContent()
    {
        var html = Document.CreateHtml();
        var template = html.CreateElement("TEMPLATE");
        var content = template.TemplateContent;
        content.Should().NotBeNull();
        template.TemplateContent.Should().BeSameAs(content);
        content!.Host.Should().BeSameAs(template);
        content.ParentNode.Should().BeNull();
        template.ChildCount.Should().Be(0);
        content.OwnerDocument!.Kind.Should().Be(DocumentKind.Html);
        content.OwnerDocument.ContentType.Should().Be("application/xml");
        content.OwnerDocument.CharacterSet.Should().Be("UTF-8");
        content.OwnerDocument.DocumentElement.Should().BeNull();

        var second = html.CreateElement("template");
        second.TemplateContent!.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
        second.TemplateContent.Should().NotBeSameAs(content);

        var xhtml = Document.CreateXml("application/xhtml+xml");
        xhtml.CreateElement("template").TemplateContent.Should().NotBeNull();
        xhtml.CreateElement("Template").TemplateContent.Should().BeNull();
        xhtml.CreateElement("template").TemplateContent!.OwnerDocument!.Kind.Should().Be(DocumentKind.Xml);
        xhtml.CreateElement("template").TemplateContent!.OwnerDocument!.ContentType.Should().Be("application/xml");

        html.CreateElementNS(Namespaces.Svg, "template").TemplateContent.Should().BeNull();
        html.CreateElementNS(null, "template").TemplateContent.Should().BeNull();
        Document.CreateXml().CreateElement("template").TemplateContent.Should().BeNull();
    }

    [Test]
    public void OrdinaryChildrenAndHostedContentHaveDistinctParentsAndOwners()
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        var ordinary = document.CreateTextNode("ordinary");
        template.AppendChild(ordinary);
        var content = template.TemplateContent!;
        var inert = content.OwnerDocument!;
        var parsed = inert.CreateParsedElement(Namespaces.Html, "span", null);
        content.AppendParsedChild(parsed);

        template.ChildNodes.Should().Equal(ordinary);
        content.ChildNodes.Should().Equal(parsed);
        ordinary.OwnerDocument.Should().BeSameAs(document);
        parsed.OwnerDocument.Should().BeSameAs(inert);
        parsed.ParentNode.Should().BeSameAs(content);
        content.ParentNode.Should().BeNull();
    }

    [Test]
    public void HostIncludingCyclesAreRejectedBeforeMutation()
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        var content = template.TemplateContent!;
        Assert.That(Assert.Throws<DomException>(() => content.AppendChild(template))!.Name, Is.EqualTo("HierarchyRequestError"));
        Assert.That(Assert.Throws<DomException>(() => content.ReplaceChildren(template))!.Name, Is.EqualTo("HierarchyRequestError"));
        template.ParentNode.Should().BeNull();
        content.ChildCount.Should().Be(0);

        var nested = content.OwnerDocument!.CreateParsedElement(Namespaces.Html, "template", null);
        content.AppendParsedChild(nested);
        Assert.That(Assert.Throws<DomException>(() => nested.TemplateContent!.AppendChild(template))!.Name,
            Is.EqualTo("HierarchyRequestError"));
        nested.ParentNode.Should().BeSameAs(content);
    }

    [Test]
    public void AdoptionMovesOrdinaryChildrenAndContentIntoTheirAppropriateOwners()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateXml("application/xhtml+xml");
        var template = source.CreateElement("template");
        var ordinary = source.CreateTextNode("ordinary");
        template.AppendChild(ordinary);
        var content = template.TemplateContent!;
        var inner = content.OwnerDocument!.CreateParsedElement(Namespaces.Html, "template", null);
        var text = inner.TemplateContent!.OwnerDocument!.CreateTextNode("deep");
        inner.TemplateContent.AppendChild(text);
        content.AppendChild(inner);
        source.AppendChild(template);
        var oldInert = content.OwnerDocument;

        destination.AdoptNode(template);
        template.OwnerDocument.Should().BeSameAs(destination);
        template.ParentNode.Should().BeNull();
        ordinary.OwnerDocument.Should().BeSameAs(destination);
        template.TemplateContent.Should().BeSameAs(content);
        content.OwnerDocument.Should().NotBeSameAs(oldInert);
        content.OwnerDocument!.ContentType.Should().Be("application/xml");
        content.OwnerDocument.Kind.Should().Be(DocumentKind.Xml);
        inner.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
        inner.TemplateContent!.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
        text.OwnerDocument.Should().BeSameAs(content.OwnerDocument);
        source.DocumentElement.Should().BeNull();

        var sameContent = content.OwnerDocument;
        destination.AppendChild(template);
        destination.AdoptNode(template);
        content.OwnerDocument.Should().BeSameAs(sameContent);
        template.ParentNode.Should().BeNull();
    }

    [Test]
    public void ClonesAndImportsKeepHostedContentsSeparateAndIterative()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateXml("application/xhtml+xml");
        var template = source.CreateElement("template");
        template.AppendChild(source.CreateTextNode("ordinary"));
        var first = template;
        for (var i = 0; i < 4_000; i++)
        {
            var next = first.TemplateContent!.OwnerDocument!.CreateParsedElement(Namespaces.Html, "template", null);
            first.TemplateContent.AppendParsedChild(next);
            first = next;
        }

        var shallow = (Element)template.CloneNode();
        shallow.TemplateContent.Should().NotBeSameAs(template.TemplateContent);
        shallow.TemplateContent!.ChildCount.Should().Be(0);
        shallow.ChildCount.Should().Be(0);

        foreach (var copy in new[] { (Element)template.CloneNode(deep: true), (Element)destination.ImportNode(template, deep: true) })
        {
            copy.TemplateContent.Should().NotBeSameAs(template.TemplateContent);
            copy.ChildCount.Should().Be(1);
            copy.TemplateContent!.OwnerDocument.Should().BeSameAs(copy.OwnerDocument!.GetTemplateContentsOwnerDocument());
            var current = copy;
            for (var i = 0; i < 4_000; i++)
            {
                current = (Element)current.TemplateContent!.FirstChild!;
                current.OwnerDocument.Should().BeSameAs(copy.TemplateContent.OwnerDocument);
            }

            current.TemplateContent!.ChildCount.Should().Be(0);
        }

        var contentClone = (DocumentFragment)template.TemplateContent!.CloneNode(deep: true);
        contentClone.Host.Should().BeNull();
        contentClone.ChildCount.Should().Be(1);
        template.TemplateContent.FirstChild.Should().NotBeSameAs(contentClone.FirstChild);
    }

    [Test]
    public void DeepNestedTemplateAdoptionUsesOneIterativeTraversal()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var root = source.CreateElement("template");
        var current = root;
        for (var i = 0; i < 4_000; i++)
        {
            var next = current.TemplateContent!.OwnerDocument!.CreateParsedElement(Namespaces.Html, "template", null);
            current.TemplateContent.AppendParsedChild(next);
            current = next;
        }

        destination.AdoptNode(root);
        var destinationInert = root.TemplateContent!.OwnerDocument;
        current.OwnerDocument.Should().BeSameAs(destinationInert);
        current.TemplateContent!.OwnerDocument.Should().BeSameAs(destinationInert);
        root.OwnerDocument.Should().BeSameAs(destination);
    }
}
