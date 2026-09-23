#nullable enable
using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser.Tests.XPath;

[TestFixture]
public sealed class NativeXPathTests
{
    [Test]
    public void BclEvaluationPreservesNodeAttributeAndNamespaceIdentities()
    {
        var document = MarkupParser.ParseXml("<r xmlns='urn:default' xmlns:p='urn:p' p:a='v'><p:x>one<![CDATA[two]]></p:x><p:x>three</p:x></r>");
        var root = document.DocumentElement!;
        var first = (Element) root.FirstChild!;
        var attr = root.GetAttributeNodeNS("urn:p", "a")!;
        var navigator = NativeXPath.CreateNavigator(document, default);
        var manager = new XmlNamespaceManager(navigator.NameTable);
        manager.AddNamespace("p", "urn:p");
        var expression = navigator.Compile("/d:r/p:x | /d:r/@p:a | /d:r/namespace::*");
        manager.AddNamespace("d", "urn:default");
        expression.SetContext(manager);
        var iterator = navigator.Select(expression);
        var results = new List<object>();
        while (iterator.MoveNext()) results.Add(iterator.Current!.UnderlyingObject!);

        results.Should().Contain(item => ReferenceEquals(item, first));
        results.Should().Contain(item => ReferenceEquals(item, attr));
        results.OfType<XPathNamespaceBinding>().Select(binding => (binding.Prefix, binding.NamespaceUri))
            .Should().Contain(("p", "urn:p"));
        var text = NativeXPath.CreateNavigator((Text) first.FirstChild!, default);
        text.Value.Should().Be("onetwo");
        text.MoveToNext().Should().BeFalse();
        text.MoveToParent().Should().BeTrue();
        text.UnderlyingObject.Should().BeSameAs(first);
        var cdata = NativeXPath.CreateNavigator((CDataSection) first.LastChild!, default);
        cdata.UnderlyingObject.Should().BeSameAs(first.FirstChild);
        cdata.Value.Should().Be("onetwo");
    }

    [Test]
    public void BclScalarResultsMatchResolverDisabledXmlDocument()
    {
        const string xml = "<r xmlns:p='urn:p' p:a='A'><p:x>one<![CDATA[two]]></p:x><p:x>three</p:x><!--ignored--></r>";
        var native = NativeXPath.CreateNavigator(MarkupParser.ParseXml(xml), default);
        var reference = new XmlDocument { XmlResolver = null };
        reference.LoadXml(xml);
        var bcl = reference.CreateNavigator()!;
        var nativeNs = new XmlNamespaceManager(native.NameTable);
        var bclNs = new XmlNamespaceManager(bcl.NameTable);
        nativeNs.AddNamespace("p", "urn:p");
        bclNs.AddNamespace("p", "urn:p");
        foreach (var expression in new[] { "count(/r/p:x)", "string(/r/p:x[1])", "string(/r/@p:a)", "boolean(/r/p:x[2])", "sum(/r/p:x)", "string(/r)" })
        {
            var actual = native.Evaluate(expression, nativeNs);
            var expected = bcl.Evaluate(expression, bclNs);
            actual.Should().Be(expected, expression);
        }
    }

    [Test]
    public void CursorOrderCloneAndCrossSessionMoveFollowLogicalTree()
    {
        var document = MarkupParser.ParseXml("<r xmlns:p='urn:p' b='2' a='1'><x/>hello<![CDATA[ world]]><y/></r>");
        var root = document.DocumentElement!;
        var element = NativeXPath.CreateNavigator(root, default);
        var ns = element.Clone();
        ns.MoveToFirstNamespace(XPathNamespaceScope.ExcludeXml).Should().BeTrue();
        var attr = element.Clone();
        attr.MoveToFirstAttribute().Should().BeTrue();
        var child = element.Clone();
        child.MoveToFirstChild().Should().BeTrue();
        element.ComparePosition(ns).Should().Be(XmlNodeOrder.Before);
        ns.ComparePosition(attr).Should().Be(XmlNodeOrder.Before);
        attr.ComparePosition(child).Should().Be(XmlNodeOrder.Before);
        attr.UnderlyingObject.Should().BeSameAs(root.GetAttributeNode("b"));
        attr.MoveToNextAttribute().Should().BeTrue();
        attr.UnderlyingObject.Should().BeSameAs(root.GetAttributeNode("a"));
        var copy = NativeXPath.CreateNavigator(root, default);
        copy.MoveTo(ns).Should().BeTrue();
        copy.IsSamePosition(ns).Should().BeTrue();
        copy.UnderlyingObject.Should().NotBeSameAs(ns.UnderlyingObject);
        var sameSession = ns.Clone();
        sameSession.UnderlyingObject.Should().BeSameAs(ns.UnderlyingObject);
        child.MoveToNext().Should().BeTrue();
        child.Value.Should().Be("hello world");
        child.MoveToNext().Should().BeTrue();
        child.UnderlyingObject.Should().BeSameAs(root.LastChild);
    }

    [Test]
    public void NamespacesAndDirectXmlnsContextsRespectExpandedNames()
    {
        var document = Document.CreateXml();
        var root = document.CreateElementNS("urn:r", "p:r");
        document.AppendChild(root);
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:wrong");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:q", "urn:q");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns", "urn:d");
        var child = document.CreateElementNS("urn:r", "p:c");
        root.AppendChild(child);
        child.SetAttributeNS(Namespaces.Xmlns, "xmlns:q", "");
        child.SetAttributeNS(Namespaces.Xmlns, "xmlns:xml", Namespaces.Xml);
        var nav = NativeXPath.CreateNavigator(child, default);
        nav.LookupNamespace("p").Should().Be("urn:r");
        nav.LookupNamespace("q").Should().BeNull();
        nav.LookupNamespace("").Should().Be("urn:d");
        var local = nav.GetNamespacesInScope(XmlNamespaceScope.Local);
        local.Should().ContainKey("p").WhoseValue.Should().Be("urn:r");
        local.Should().ContainKey("xml").WhoseValue.Should().Be(Namespaces.Xml);
        local.Should().NotContainKey("q");
        var all = nav.GetNamespacesInScope(XmlNamespaceScope.All);
        all.Should().ContainKey("p").WhoseValue.Should().Be("urn:r");
        all.Should().NotContainKey("q");
        var axis = nav.Clone();
        axis.MoveToFirstNamespace(XPathNamespaceScope.Local).Should().BeTrue();
        axis.LocalName.Should().Be("p");
        axis.NamespaceURI.Should().BeEmpty();
        axis.Prefix.Should().BeEmpty();
        axis.MoveToNextNamespace(XPathNamespaceScope.Local).Should().BeTrue();
        axis.LocalName.Should().Be("xml");
        axis.Value.Should().Be(Namespaces.Xml);
        axis.MoveToNextNamespace(XPathNamespaceScope.Local).Should().BeFalse();
        axis.LocalName.Should().Be("xml");
        foreach (var declaration in root.Attributes.Where(a => a.NamespaceUri == Namespaces.Xmlns))
        {
            Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(declaration, default));
        }

        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(child.GetAttributeNodeNS(Namespaces.Xmlns, "q")!, default));
        var empty = document.CreateElement("empty");
        root.AppendChild(empty);
        empty.SetAttributeNS(Namespaces.Xmlns, "xmlns", "");
        var emptyNav = NativeXPath.CreateNavigator(empty, default);
        emptyNav.LookupNamespace("").Should().BeNull();
        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(empty.GetAttributeNodeNS(Namespaces.Xmlns, "xmlns")!, default));

        var ordinary = document.CreateAttribute("xmlns");
        ordinary.Value = "plain";
        root.SetAttributeNode(ordinary);
        NativeXPath.CreateNavigator(ordinary, default).Value.Should().Be("plain");
    }

    [Test]
    public void DetachedBoundariesAndUnsupportedIdAreExplicit()
    {
        var document = Document.CreateXml();
        var detached = document.CreateElement("outer");
        var inner = document.CreateElement("inner");
        detached.AppendChild(inner);
        var nav = NativeXPath.CreateNavigator(inner, default);
        nav.MoveToRoot();
        nav.UnderlyingObject.Should().BeSameAs(detached);
        nav.NodeType.Should().Be(XPathNodeType.Element);
        var absolute = nav.Select("/");
        absolute.MoveNext().Should().BeTrue();
        absolute.Current!.UnderlyingObject.Should().BeSameAs(detached);
        absolute.MoveNext().Should().BeFalse();
        Assert.Throws<NotSupportedException>(() => nav.MoveToId("x"));
        Assert.Throws<NotSupportedException>(() => NativeXPath.CreateNavigator(document.CreateAttribute("a"), default));
        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(document.CreateTextNode(""), default));
        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(document.CreateDocumentType("r"), default));
        var fragment = document.CreateDocumentFragment();
        NativeXPath.CreateNavigator(fragment, default).NodeType.Should().Be(XPathNodeType.Root);
    }

    [Test]
    public void XmlLangUsesOnlyXmlNamespaceAndValuesSkipHostedContent()
    {
        var document = MarkupParser.ParseXml("<r lang='plain' xml:lang='fr'><c a='v'>text<!--skip--><?p skip?></c></r>");
        var root = document.DocumentElement!;
        var child = (Element) root.FirstChild!;
        var nav = NativeXPath.CreateNavigator(child.GetAttributeNode("a")!, default);
        nav.XmlLang.Should().Be("fr");
        nav.MoveToParent().Should().BeTrue();
        nav.Value.Should().Be("text");
        root.RemoveAttributeNS(Namespaces.Xml, "lang");
        NativeXPath.CreateNavigator(child, default).XmlLang.Should().BeEmpty();

        var html = Document.CreateHtml();
        var template = html.CreateElement("template");
        html.AppendChild(template);
        template.TemplateContent!.AppendChild(html.CreateTextNode("hosted"));
        NativeXPath.CreateNavigator(template, default).Value.Should().BeEmpty();
        NativeXPath.CreateNavigator(template.TemplateContent, default).Value.Should().Be("hosted");
    }

    [Test]
    public void MutationInvalidatesClonesAndCancellationOccursInsideLongTraversal()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("r");
        document.AppendChild(root);
        for (var i = 0; i < 2000; i++) root.AppendChild(document.CreateTextNode("a"));
        var nav = NativeXPath.CreateNavigator(root, default);
        var clone = nav.Clone();
        root.SetAttribute("new", "value");
        Assert.Throws<InvalidOperationException>(() => _ = nav.Value);
        Assert.Throws<InvalidOperationException>(() => clone.MoveToFirstChild());

        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        var traversal = NativeXPath.CreateNavigator(root, _ => { checkpoints++; cancellation.Cancel(); }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => _ = traversal.Value);
        checkpoints.Should().Be(1);
    }

    [Test]
    public void DeepScopeAndPreorderIndexUseLinearWorkAndShareCachedState()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("n");
        document.AppendChild(root);
        var deepest = root;
        for (var i = 0; i < 1200; i++)
        {
            var child = document.CreateElement("n");
            deepest.AppendChild(child);
            deepest = child;
        }

        var checkpoints = 0;
        var nav = NativeXPath.CreateNavigator(deepest, _ => checkpoints++, default);
        nav.MoveToFirstNamespace(XPathNamespaceScope.All).Should().BeTrue();
        nav.Value.Should().Be(Namespaces.Xml);
        checkpoints.Should().BeLessThan(30);
        var before = checkpoints;
        for (var i = 0; i < 100; i++) nav.LookupNamespace("xml").Should().Be(Namespaces.Xml);
        checkpoints.Should().Be(before);

        var first = NativeXPath.CreateNavigator(root, default);
        var last = NativeXPath.CreateNavigator(deepest, default);
        first.ComparePosition(last).Should().Be(XmlNodeOrder.Before);
        for (var i = 0; i < 100; i++) first.ComparePosition(last).Should().Be(XmlNodeOrder.Before);
    }

    [Test]
    public void WideAttributeAxisCachesItsScanAndCanCancelDuringIt()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("r");
        document.AppendChild(element);
        for (var i = 0; i < 2048; i++) element.SetAttribute("a" + i, "v");

        var checkpoints = 0;
        var nav = NativeXPath.CreateNavigator(element, _ => checkpoints++, default);
        nav.MoveToFirstAttribute().Should().BeTrue();
        var scanned = checkpoints;
        for (var i = 1; i < 2048; i++) nav.MoveToNextAttribute().Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(element.GetAttributeNode("a2047"));
        checkpoints.Should().Be(scanned);
        checkpoints.Should().BeLessThan(100);

        using var cancellation = new CancellationTokenSource();
        var canceled = NativeXPath.CreateNavigator(element, _ => cancellation.Cancel(), cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => canceled.MoveToFirstAttribute());
    }

    [Test]
    public void MutationDuringLazyOrderConstructionDoesNotPublishStaleIndex()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("r");
        document.AppendChild(root);
        Element? last = null;
        for (var i = 0; i < 800; i++)
        {
            last = document.CreateElement("c");
            root.AppendChild(last);
        }

        var changed = false;
        var first = NativeXPath.CreateNavigator(root, _ =>
        {
            if (changed) return;
            changed = true;
            root.SetAttribute("changed", "yes");
        }, default);
        var final = NativeXPath.CreateNavigator(last!, default);
        Assert.Throws<InvalidOperationException>(() => first.ComparePosition(final));
        changed.Should().BeTrue();
        Assert.Throws<InvalidOperationException>(() => first.MoveToFirstChild());
    }
}
