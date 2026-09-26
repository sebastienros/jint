#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class ElementIsValueTests
{
    [Test]
    public void ValidatedAndParsedFactoriesKeepExactCreationValueWithoutAnAttribute()
    {
        var html = Document.CreateHtml();
        var xml = Document.CreateXml();
        var xhtml = Document.CreateXml("application/xhtml+xml");

        foreach (var isValue in new string?[] { null, "", "MiXeD", "not a custom name" })
        {
            var elements = new[]
            {
                html.CreateElement("DiV", isValue),
                xml.CreateElement("Item", isValue),
                xhtml.CreateElement("Item", isValue),
                html.CreateElementNS(Namespaces.Svg, "s:rect", isValue),
                xml.CreateElementNS(null, "Item", isValue),
                xml.CreateParsedElement(Namespaces.Html, "MiXeD", null, isValue)
            };
            foreach (var element in elements)
            {
                element.IsValue.Should().Be(isValue);
                element.AttributeCount.Should().Be(0);
                element.GetAttribute("is").Should().BeNull();
            }

            elements[0].LocalName.Should().Be("div");
            elements[0].NamespaceUri.Should().Be(Namespaces.Html);
            elements[1].LocalName.Should().Be("Item");
            elements[1].NamespaceUri.Should().BeNull();
            elements[2].NamespaceUri.Should().Be(Namespaces.Html);
            elements[3].NamespaceUri.Should().Be(Namespaces.Svg);
            elements[4].NamespaceUri.Should().BeNull();
            elements[5].LocalName.Should().Be("MiXeD");
        }

        html.CreateElement("DiV").IsValue.Should().BeNull();
        html.CreateElementNS(Namespaces.Html, "div").IsValue.Should().BeNull();
        Assert.Throws<DomException>(() => html.CreateElement("bad name", "valid-name"));
        Assert.Throws<DomException>(() => xml.CreateElementNS(null, "p:root", "valid-name"));
    }

    [Test]
    public void AttributeEditsDoNotChangeCreationValueOrMutationRecords()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root", "creation");
        var ordinary = document.CreateElement("ordinary");
        ordinary.SetAttribute("is", "later");
        ordinary.IsValue.Should().BeNull();

        using var subscription = document.ObserveMutations(element,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true });
        element.SetAttribute("is", "first");
        element.SetAttribute("is", "second");
        var replacement = document.CreateAttribute("is");
        replacement.Value = "replacement";
        var previous = element.SetAttributeNode(replacement)!;
        element.RemoveAttribute("is");
        element.SetAttributeNode(previous);

        element.IsValue.Should().Be("creation");
        element.GetAttribute("is").Should().Be("second");
        subscription.TakeRecords().Select(record => (record.Kind, record.AttributeName, record.OldValue))
            .Should().Equal(
                (MutationRecordKind.Attributes, "is", null),
                (MutationRecordKind.Attributes, "is", "first"),
                (MutationRecordKind.Attributes, "is", "second"),
                (MutationRecordKind.Attributes, "is", "replacement"),
                (MutationRecordKind.Attributes, "is", null));
    }

    [Test]
    public void CloneAndImportKeepCreationValueIndependentlyOfCopiedAttributes()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateHtml();
        var root = source.CreateElementNS(Namespaces.Html, "root", "creation");
        root.SetAttribute("is", "attribute");
        var child = source.CreateElementNS(Namespaces.Svg, "child", "child creation");
        child.SetAttribute("is", "transient");
        child.RemoveAttribute("is");
        root.AppendChild(child);

        foreach (var copy in new[]
                 {
                     (Element)root.CloneNode(),
                     (Element)root.CloneNode(deep: true),
                     (Element)destination.ImportNode(root),
                     (Element)destination.ImportNode(root, deep: true)
                 })
        {
            copy.IsValue.Should().Be("creation");
            copy.GetAttribute("is").Should().Be("attribute");
            copy.GetAttributeNode("is").Should().NotBeSameAs(root.GetAttributeNode("is"));
            if (copy.ChildCount != 0)
            {
                var copiedChild = (Element)copy.FirstChild!;
                copiedChild.IsValue.Should().Be("child creation");
                copiedChild.GetAttribute("is").Should().BeNull();
                copiedChild.Should().NotBeSameAs(child);
            }
        }

        root.RemoveAttribute("is");
        root.IsValue.Should().Be("creation");
        ((Element)root.CloneNode()).IsValue.Should().Be("creation");
        ((Element)destination.ImportNode(root)).GetAttribute("is").Should().BeNull();
    }

    [Test]
    public void HostedTemplateAndClonableShadowDescendantsRetainValueThroughCopyAndAdoption()
    {
        var source = Document.CreateHtml();
        var destination = Document.CreateXml("application/xhtml+xml");
        var host = source.CreateElement("x-host", "host creation");
        var template = source.CreateElement("template", "template creation");
        host.AppendChild(template);
        var inertOwner = template.TemplateContent!.OwnerDocument!;
        var hosted = inertOwner.CreateParsedElement(Namespaces.Html, "span", null, "hosted creation");
        template.TemplateContent.AppendChild(hosted);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open, Clonable: true), default);
        var shadowChild = source.CreateElement("b", "shadow creation");
        shadow.AppendChild(shadowChild);

        var clone = (Element)host.CloneNode(deep: true);
        clone.IsValue.Should().Be("host creation");
        var clonedTemplate = (Element)clone.FirstChild!;
        clonedTemplate.IsValue.Should().Be("template creation");
        ((Element)clonedTemplate.TemplateContent!.FirstChild!).IsValue.Should().Be("hosted creation");
        ((Element)clone.AttachedShadowRoot!.FirstChild!).IsValue.Should().Be("shadow creation");

        var imported = (Element)destination.ImportNode(host, deep: true);
        imported.IsValue.Should().Be("host creation");
        var importedTemplate = (Element)imported.FirstChild!;
        importedTemplate.IsValue.Should().Be("template creation");
        ((Element)importedTemplate.TemplateContent!.FirstChild!).IsValue.Should().Be("hosted creation");
        ((Element)imported.AttachedShadowRoot!.FirstChild!).IsValue.Should().Be("shadow creation");

        destination.AdoptNode(host).Should().BeSameAs(host);
        host.IsValue.Should().Be("host creation");
        template.IsValue.Should().Be("template creation");
        hosted.IsValue.Should().Be("hosted creation");
        shadowChild.IsValue.Should().Be("shadow creation");
        hosted.OwnerDocument.Should().BeSameAs(template.TemplateContent.OwnerDocument);
        shadowChild.OwnerDocument.Should().BeSameAs(destination);
    }

    [Test]
    public void XmlDocumentAndFragmentCaptureOnlyResolvedUnprefixedLowercaseIs()
    {
        const string source = "<r xmlns='urn:default' xmlns:p='urn:other' is='a&amp;B'>" +
                              "<explicit is=''/><prefixed p:is='wrong'/><capital IS='wrong'/>" +
                              "<foreign xmlns='' is='MiXeD'/></r>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        var root = document.DocumentElement!;
        root.NamespaceUri.Should().Be("urn:default");
        root.IsValue.Should().Be("a&B");
        root.GetAttribute("is").Should().Be("a&B");
        var children = root.ChildNodes.Cast<Element>().ToArray();
        children.Select(child => child.IsValue).Should().Equal("", null, null, "MiXeD");
        children[1].GetAttributeNS("urn:other", "is").Should().Be("wrong");
        children[2].GetAttribute("IS").Should().Be("wrong");
        children[3].NamespaceUri.Should().BeNull();

        var fragment = XmlTreeParser.ParseFragment("<x is='A&#38;B'/><y p:is='no'/> ", root,
            ParseLimits.Unbounded, default);
        ((Element)fragment.FirstChild!).IsValue.Should().Be("A&B");
        ((Element)fragment.FirstChild!.NextSibling!).IsValue.Should().BeNull();
    }

    [Test]
    public void XmlDtdDefaultsAndTemplateContentKeepProcessedValueAndMetadata()
    {
        const string source = "<!DOCTYPE html [<!ENTITY value 'a&amp;B'>" +
                              "<!ATTLIST html is CDATA '&value;'>" +
                              "<!ATTLIST span is ID '  default   value  '>]>" +
                              "<html xmlns='http://www.w3.org/1999/xhtml'><template><span/></template></html>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        var root = document.DocumentElement!;
        root.IsValue.Should().Be("a&B");
        root.GetAttribute("is").Should().Be("a&B");
        var template = (Element)root.FirstChild!;
        template.IsValue.Should().BeNull();
        var hosted = (Element)template.TemplateContent!.FirstChild!;
        hosted.IsValue.Should().Be("default value");
        hosted.GetAttributeNode("is")!.IsDtdId.Should().BeTrue();
        hosted.OwnerDocument.Should().BeSameAs(template.TemplateContent.OwnerDocument);

        const string unread = "<!DOCTYPE r [<!ENTITY % ext SYSTEM 'missing.dtd'>" +
                              "<!ATTLIST r before CDATA 'kept'>%ext;" +
                              "<!ATTLIST r is CDATA 'unread'>]><r/>";
        var omitted = XmlTreeParser.ParseDocument(unread, ParseLimits.Unbounded, default);
        omitted.DocumentElement!.IsValue.Should().BeNull();
        omitted.DocumentElement.GetAttribute("is").Should().BeNull();
        omitted.DocumentElement.GetAttribute("before").Should().Be("kept");
        omitted.SkippedXmlEntities.Should().ContainSingle();
        omitted.SkippedXmlEntities[0].Offset.Should().Be(unread.IndexOf("%ext;", StringComparison.Ordinal));
    }
}
