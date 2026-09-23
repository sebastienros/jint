#nullable enable
using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser.Tests.XPath;

[TestFixture]
public sealed class NativeXPathIdentifierTests
{
    [Test]
    public void ParsedIdsUseExactTypedValuesAndBclTokenOrdering()
    {
        const string xml = "<!DOCTYPE r [<!ATTLIST x key ID #IMPLIED id CDATA #IMPLIED ref IDREF #IMPLIED><!ATTLIST y key ID #IMPLIED>]><r><x key='b' id='a' ref='c'/><y key='a'/><x key='b'/></r>";
        var document = MarkupParser.ParseXml(xml);
        var root = document.DocumentElement!;
        var first = (Element)root.FirstChild!;
        var second = (Element)first.NextSibling!;
        var nav = NativeXPath.CreateNavigator(document, default);
        nav.MoveToId("b").Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(first);
        nav.MoveToId("a").Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(second);
        nav.MoveToId("c").Should().BeFalse();
        nav.UnderlyingObject.Should().BeSameAs(second);
        nav.MoveToId("a b").Should().BeFalse();
        nav.MoveToId("").Should().BeFalse();
        Assert.Throws<ArgumentNullException>(() => nav.MoveToId(null!));

        var results = NativeXPath.CreateNavigator(document, default).Select("id('b a b')");
        results.MoveNext().Should().BeTrue();
        results.Current!.UnderlyingObject.Should().BeSameAs(first);
        results.MoveNext().Should().BeTrue();
        results.Current!.UnderlyingObject.Should().BeSameAs(second);
        results.MoveNext().Should().BeFalse();
        var fromNodes = NativeXPath.CreateNavigator(document, default).Select("id(/r/x/@key)");
        fromNodes.MoveNext().Should().BeTrue();
        fromNodes.Current!.UnderlyingObject.Should().BeSameAs(first);
        fromNodes.MoveNext().Should().BeFalse();

        var attribute = NativeXPath.CreateNavigator(first.GetAttributeNode("id")!, default);
        attribute.MoveToId("missing").Should().BeFalse();
        attribute.UnderlyingObject.Should().BeSameAs(first.GetAttributeNode("id"));
        var namespaceCursor = NativeXPath.CreateNavigator(root, default);
        namespaceCursor.MoveToFirstNamespace(XPathNamespaceScope.All).Should().BeTrue();
        var binding = namespaceCursor.UnderlyingObject;
        namespaceCursor.MoveToId("missing").Should().BeFalse();
        namespaceCursor.UnderlyingObject.Should().BeSameAs(binding);
    }

    [Test]
    public void XmlnsTypedIdCountsAlthoughItsAttributePositionIsExcluded()
    {
        var document = MarkupParser.ParseXml("<!DOCTYPE r [<!ELEMENT r EMPTY><!ATTLIST r xmlns:p ID #IMPLIED>]><r xmlns:p='key'/>");
        var root = document.DocumentElement!;
        var declaration = root.GetAttributeNodeNS(Namespaces.Xmlns, "p")!;
        declaration.IsDtdId.Should().BeTrue();
        var nav = NativeXPath.CreateNavigator(document, default);
        nav.MoveToId("key").Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(root);
        var idResult = NativeXPath.CreateNavigator(document, default).Select("id('key')");
        idResult.MoveNext().Should().BeTrue();
        idResult.Current!.UnderlyingObject.Should().BeSameAs(root);
        NativeXPath.CreateNavigator(root, default).Select("attribute::*").MoveNext().Should().BeFalse();
        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(declaration, default));
    }

    [Test]
    public void DirectLookupUsesExactStoredValueWithoutIdOrXmlIdHeuristics()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("r");
        root.InitializeParsedAttributes(
        [
            new ParserAttribute(null, "key", null, "a b", isDtdId: true),
            new ParserAttribute(null, "id", null, "ordinary"),
            new ParserAttribute(Namespaces.Xml, "id", "xml", "xml-ordinary")
        ], default);
        document.AppendChild(root);
        var nav = NativeXPath.CreateNavigator(document, default);
        nav.MoveToId("a b").Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(root);
        nav.MoveToId("a").Should().BeFalse();
        nav.MoveToId("ordinary").Should().BeFalse();
        nav.MoveToId("xml-ordinary").Should().BeFalse();
        NativeXPath.CreateNavigator(document, default).Select("id('a b')").MoveNext().Should().BeFalse();
    }

    [Test]
    public void IdIndexIsSharedAndRebuiltOnlyInFreshSessions()
    {
        var document = MarkupParser.ParseXml("<!DOCTYPE r [<!ATTLIST x key ID #IMPLIED>]><r><x key='a'/><x key='a'/></r>");
        var root = document.DocumentElement!;
        var first = (Element)root.FirstChild!;
        var second = (Element)first.NextSibling!;
        for (var i = 0; i < 500; i++) root.SetAttribute("untyped" + i, "v");
        var visits = 0;
        var nav = NativeXPath.CreateNavigator(document, (stage, _) =>
        {
            if (stage == XPathWorkStage.IdIndex) visits++;
        }, default);
        nav.MoveToId("a").Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(first);
        var built = visits;
        built.Should().BeGreaterThan(0);
        var clone = nav.Clone();
        for (var i = 0; i < 20; i++)
        {
            clone.MoveToId("missing").Should().BeFalse();
            clone.MoveToId("a").Should().BeTrue();
        }
        visits.Should().BeLessThan(built + 3);
        root.RemoveChild(first);
        Assert.Throws<InvalidOperationException>(() => nav.MoveToId("a"));
        var fresh = NativeXPath.CreateNavigator(document, default);
        fresh.MoveToId("a").Should().BeTrue();
        fresh.UnderlyingObject.Should().BeSameAs(second);
        second.GetAttributeNode("key")!.Value = "new";
        fresh = NativeXPath.CreateNavigator(document, default);
        fresh.MoveToId("a").Should().BeFalse();
        fresh.MoveToId("new").Should().BeTrue();
        fresh.UnderlyingObject.Should().BeSameAs(second);
    }

    [Test]
    public void DetachedAttributeIsItsOwnRootAndFreshnessDomain()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("r");
        document.AppendChild(element);
        var attribute = document.CreateAttributeNS("urn:p", "p:key");
        attribute.Value = "value";
        var nav = NativeXPath.CreateNavigator(attribute, default);
        var fresh = NativeXPath.CreateNavigator(attribute, default);
        nav.NodeType.Should().Be(XPathNodeType.Attribute);
        nav.UnderlyingObject.Should().BeSameAs(attribute);
        nav.IsSamePosition(fresh).Should().BeTrue();
        nav.ComparePosition(fresh).Should().Be(XmlNodeOrder.Same);
        nav.MoveTo(fresh).Should().BeTrue();
        nav.MoveToParent().Should().BeFalse();
        nav.MoveToFirstChild().Should().BeFalse();
        nav.MoveToNext().Should().BeFalse();
        nav.MoveToFirstAttribute().Should().BeFalse();
        nav.MoveToFirstNamespace(XPathNamespaceScope.All).Should().BeFalse();
        nav.MoveToId("value").Should().BeFalse();
        nav.MoveToRoot();
        nav.UnderlyingObject.Should().BeSameAs(attribute);
        var absolute = nav.Select("/");
        absolute.MoveNext().Should().BeTrue();
        absolute.Current!.UnderlyingObject.Should().BeSameAs(attribute);
        absolute.MoveNext().Should().BeFalse();
        nav.Value.Should().Be("value");
        nav.XmlLang.Should().BeEmpty();
        nav.LookupNamespace("p").Should().BeNull();
        nav.NamespaceURI.Should().Be("urn:p");
        nav.LookupNamespace("xml").Should().Be(Namespaces.Xml);
        nav.GetNamespacesInScope(XmlNamespaceScope.All).Should().ContainSingle().Which.Key.Should().Be("xml");
        nav.Select("self::node()").MoveNext().Should().BeTrue();
        nav.Select("ancestor::node()").MoveNext().Should().BeFalse();
        nav.Select("preceding::node()").MoveNext().Should().BeFalse();
        nav.MoveToFollowing(XPathNodeType.All).Should().BeFalse();
        nav.Evaluate("string(.)").Should().Be("value");
        nav.Evaluate("count(../*)").Should().Be(0d);
        var other = NativeXPath.CreateNavigator(document.CreateAttributeNS("urn:p", "p:key"), default);
        nav.IsSamePosition(other).Should().BeFalse();
        nav.ComparePosition(other).Should().Be(XmlNodeOrder.Unknown);
        nav.MoveTo(other).Should().BeFalse();

        var clone = nav.Clone();
        attribute.Value = "value";
        Assert.Throws<InvalidOperationException>(() => _ = nav.Value);
        Assert.Throws<InvalidOperationException>(() => clone.MoveToRoot());
        fresh = NativeXPath.CreateNavigator(attribute, default);
        element.SetAttributeNode(attribute);
        Assert.Throws<InvalidOperationException>(() => _ = fresh.NodeType);
        fresh = NativeXPath.CreateNavigator(attribute, default);
        fresh.MoveToParent().Should().BeTrue();
        fresh.UnderlyingObject.Should().BeSameAs(element);
        element.RemoveAttributeNode(attribute);
        Assert.Throws<InvalidOperationException>(() => _ = fresh.Value);
        NativeXPath.CreateNavigator(attribute, default).MoveToParent().Should().BeFalse();
    }

    [Test]
    public void IdIndexCancellationDoesNotMoveCursorOrPublishIndex()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("r");
        document.AppendChild(root);
        for (var i = 0; i < 1000; i++) root.SetAttribute("untyped" + i, "v");
        var child = document.CreateElement("x");
        child.InitializeParsedAttributes([new ParserAttribute(null, "key", null, "target", isDtdId: true)], default);
        root.AppendChild(child);
        using var cancellation = new CancellationTokenSource();
        var reached = false;
        var nav = NativeXPath.CreateNavigator(root, (stage, _) =>
        {
            if (stage != XPathWorkStage.IdIndex || reached) return;
            reached = true;
            cancellation.Cancel();
        }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => nav.MoveToId("target"));
        reached.Should().BeTrue();
        var retry = NativeXPath.CreateNavigator(root, default);
        retry.MoveToId("target").Should().BeTrue();
        retry.UnderlyingObject.Should().BeSameAs(child);
    }

    [Test]
    public void IdIndexPollsNodeVisitsAndItsWorkGrowsLinearly()
    {
        int Measure(int count)
        {
            var document = Document.CreateXml();
            var root = document.CreateElement("r");
            document.AppendChild(root);
            for (var i = 0; i < count; i++) root.AppendChild(document.CreateElement("child"));
            var progress = 0;
            var nav = NativeXPath.CreateNavigator(root, (stage, work) =>
            {
                if (stage == XPathWorkStage.IdIndex) progress = work;
            }, default);
            nav.MoveToId("missing").Should().BeFalse();
            return progress;
        }

        var narrow = Measure(512);
        var wide = Measure(1024);
        narrow.Should().BeGreaterThan(0);
        wide.Should().BeGreaterThan(narrow);
        wide.Should().BeLessThan(narrow * 5 / 2);

        var document = Document.CreateXml();
        var root = document.CreateElement("r");
        document.AppendChild(root);
        for (var i = 0; i < 1024; i++) root.AppendChild(document.CreateElement("child"));
        using var cancellation = new CancellationTokenSource();
        var reached = false;
        var canceled = NativeXPath.CreateNavigator(root, (stage, _) =>
        {
            if (stage != XPathWorkStage.IdIndex || reached) return;
            reached = true;
            cancellation.Cancel();
        }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => canceled.MoveToId("missing"));
        reached.Should().BeTrue();
        NativeXPath.CreateNavigator(root, default).MoveToId("missing").Should().BeFalse();
    }

    [Test]
    public void TypedIdentitySurvivesCloneImportAdoptAndAttributeReplacement()
    {
        var source = MarkupParser.ParseXml("<!DOCTYPE r [<!ATTLIST r key ID #IMPLIED>]><r key='old'/>");
        var root = source.DocumentElement!;
        var typed = root.GetAttributeNode("key")!;
        var original = NativeXPath.CreateNavigator(source, default);
        original.MoveToId("old").Should().BeTrue();
        var cloned = (Element)root.CloneNode(deep: true);
        var clonedAttribute = cloned.GetAttributeNode("key")!;
        clonedAttribute.Should().NotBeSameAs(typed);
        NativeXPath.CreateNavigator(cloned, default).MoveToId("old").Should().BeTrue();

        var importedDocument = Document.CreateXml();
        var imported = (Element)importedDocument.ImportNode(root, deep: true);
        importedDocument.AppendChild(imported);
        imported.GetAttributeNode("key")!.Should().NotBeSameAs(typed);
        NativeXPath.CreateNavigator(importedDocument, default).MoveToId("old").Should().BeTrue();

        root.RemoveAttributeNode(typed);
        Assert.Throws<InvalidOperationException>(() => original.MoveToId("old"));
        NativeXPath.CreateNavigator(source, default).MoveToId("old").Should().BeFalse();
        root.SetAttribute("key", "old");
        NativeXPath.CreateNavigator(source, default).MoveToId("old").Should().BeFalse();
        root.SetAttributeNode(typed);
        NativeXPath.CreateNavigator(source, default).MoveToId("old").Should().BeTrue();

        var destination = Document.CreateXml();
        destination.AdoptNode(root);
        destination.AppendChild(root);
        root.GetAttributeNode("key").Should().BeSameAs(typed);
        typed.OwnerDocument.Should().BeSameAs(destination);
        NativeXPath.CreateNavigator(source, default).MoveToId("old").Should().BeFalse();
        NativeXPath.CreateNavigator(destination, default).MoveToId("old").Should().BeTrue();
    }

    [Test]
    public void IdLookupStaysWithinDetachedFragmentTemplateAndShadowRoots()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        document.AppendChild(host);
        var template = document.CreateElement("template");
        host.AppendChild(template);
        var templateChild = document.CreateElement("inside");
        templateChild.InitializeParsedAttributes([new ParserAttribute(null, "key", null, "template", isDtdId: true)], default);
        template.TemplateContent!.AppendChild(templateChild);
        NativeXPath.CreateNavigator(document, default).MoveToId("template").Should().BeFalse();
        var templateNav = NativeXPath.CreateNavigator(template.TemplateContent, default);
        templateNav.MoveToId("template").Should().BeTrue();
        templateNav.UnderlyingObject.Should().BeSameAs(templateChild);

        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var shadowChild = document.CreateElement("inside");
        shadowChild.InitializeParsedAttributes([new ParserAttribute(null, "key", null, "shadow", isDtdId: true)], default);
        shadow.AppendChild(shadowChild);
        NativeXPath.CreateNavigator(document, default).MoveToId("shadow").Should().BeFalse();
        var shadowNav = NativeXPath.CreateNavigator(shadow, default);
        shadowNav.MoveToId("shadow").Should().BeTrue();
        shadowNav.UnderlyingObject.Should().BeSameAs(shadowChild);

        var detached = document.CreateDocumentFragment();
        var detachedChild = document.CreateElement("inside");
        detachedChild.InitializeParsedAttributes([new ParserAttribute(null, "key", null, "fragment", isDtdId: true)], default);
        detached.AppendChild(detachedChild);
        NativeXPath.CreateNavigator(document, default).MoveToId("fragment").Should().BeFalse();
        NativeXPath.CreateNavigator(detached, default).MoveToId("fragment").Should().BeTrue();
        NativeXPath.CreateNavigator(detachedChild, default).MoveToId("fragment").Should().BeTrue();
        NativeXPath.CreateNavigator(template.TemplateContent, default).MoveToId("shadow").Should().BeFalse();
    }

    [Test]
    public void DetachedFollowingAxisCanBeCanceledWhileBclRetriesItsParent()
    {
        var attribute = Document.CreateXml().CreateAttribute("key");
        using var cancellation = new CancellationTokenSource();
        var reached = false;
        var nav = NativeXPath.CreateNavigator(attribute, (stage, _) =>
        {
            if (stage != XPathWorkStage.Other || reached) return;
            reached = true;
            cancellation.Cancel();
        }, cancellation.Token);
        var iterator = nav.Select("following::node()");
        Assert.Throws<OperationCanceledException>(() => iterator.MoveNext());
        reached.Should().BeTrue();
    }

    [Test]
    [Ignore("The BCL FollowingQuery retries a parentless Attribute forever; requires a reviewed evaluator seam.")]
    public void DetachedFollowingAxisShouldCompleteWithoutCancellation()
    {
        var attribute = Document.CreateXml().CreateAttribute("key");
        var nav = NativeXPath.CreateNavigator(attribute, default);
        nav.Select("following::node()").MoveNext().Should().BeFalse();
    }
}
