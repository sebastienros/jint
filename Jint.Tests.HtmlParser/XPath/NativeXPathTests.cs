#nullable enable
using System.Xml;
using System.Xml.XPath;
using System.Reflection;

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
        manager.AddNamespace("d", "urn:default");
        var expression = NativeXPath.Compile("/d:r/p:x | /d:r/@p:a | /d:r/namespace::*", manager, default);
        var results = NativeXPath.Select(document, expression, default);

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
            var result = NativeXPath.Evaluate(MarkupParser.ParseXml(xml), expression, nativeNs, default);
            object actual = result.ResultType switch
            {
                XPathResultType.Number => result.NumberValue,
                XPathResultType.String => result.StringValue,
                XPathResultType.Boolean => result.BooleanValue,
                _ => throw new InvalidOperationException("Expected a scalar XPath result.")
            };
            var expected = bcl.Evaluate(expression, bclNs);
            actual.Should().Be(expected, expression);
        }
    }

    [Test]
    public void BclNamespaceResolverFallbacksMatchWithoutExposingReservedAxisPositions()
    {
        foreach (var xml in new[] { "<r/>", "<r xmlns='urn:d' xmlns:p='urn:p'/>", "<r xmlns=''/>" })
        {
            var native = NativeXPath.CreateNavigator(MarkupParser.ParseXml(xml).DocumentElement!, default);
            var reference = new XmlDocument { XmlResolver = null };
            reference.LoadXml(xml);
            var bcl = reference.DocumentElement!.CreateNavigator()!;
            foreach (var prefix in new[] { "", "xml", "xmlns", "p", "missing" })
            {
                native.LookupNamespace(prefix).Should().Be(bcl.LookupNamespace(prefix), xml + " prefix " + prefix);
                if (native.LookupNamespace(prefix) is { } uri)
                {
                    ReferenceEquals(uri, native.NameTable.Get(uri)).Should().BeTrue();
                }
            }

            foreach (var uri in new[] { "", Namespaces.Xml, Namespaces.Xmlns, "urn:d", "urn:p", "urn:missing" })
            {
                native.LookupPrefix(uri).Should().Be(bcl.LookupPrefix(uri), xml + " URI " + uri);
            }

            var axis = native.Clone();
            if (axis.MoveToFirstNamespace(XPathNamespaceScope.All))
            {
                do
                {
                    axis.LocalName.Should().NotBe("xmlns");
                    axis.Value.Should().NotBeEmpty();
                } while (axis.MoveToNextNamespace(XPathNamespaceScope.All));
            }
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
        nav.LookupNamespace("missing").Should().BeNull();
        nav.LookupNamespace("xmlns").Should().Be(Namespaces.Xmlns);
        nav.LookupPrefix(Namespaces.Xmlns).Should().Be("xmlns");
        nav.LookupPrefix("").Should().BeNull();
        ReferenceEquals(nav.LookupNamespace("p"), nav.NameTable.Get("urn:r")).Should().BeTrue();
        ReferenceEquals(nav.LookupNamespace("xml"), nav.NameTable.Get(Namespaces.Xml)).Should().BeTrue();
        ReferenceEquals(nav.LookupNamespace("xmlns"), nav.NameTable.Get(Namespaces.Xmlns)).Should().BeTrue();
        ReferenceEquals(nav.LookupPrefix(Namespaces.Xmlns), nav.NameTable.Get("xmlns")).Should().BeTrue();
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
        emptyNav.LookupNamespace("").Should().BeEmpty();
        emptyNav.LookupNamespace("missing").Should().BeNull();
        ReferenceEquals(emptyNav.LookupNamespace(""), emptyNav.NameTable.Get("")).Should().BeTrue();
        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(empty.GetAttributeNodeNS(Namespaces.Xmlns, "xmlns")!, default));

        var ordinary = document.CreateAttribute("xmlns");
        ordinary.Value = "plain";
        root.SetAttributeNode(ordinary);
        NativeXPath.CreateNavigator(ordinary, default).Value.Should().Be("plain");
    }

    [Test]
    public void DetachedBoundariesAndIdMissAreExplicit()
    {
        var document = Document.CreateXml();
        var detached = document.CreateElement("outer");
        var inner = document.CreateElement("inner");
        detached.AppendChild(inner);
        var nav = NativeXPath.CreateNavigator(inner, default);
        nav.MoveToRoot();
        nav.UnderlyingObject.Should().BeSameAs(detached);
        nav.NodeType.Should().Be(XPathNodeType.Element);
        NativeXPath.Select(detached, "/", null, default).Should().ContainSingle().Which.Should().BeSameAs(detached);
        nav.MoveToId("x").Should().BeFalse();
        NativeXPath.CreateNavigator(document.CreateAttribute("a"), default).NodeType.Should().Be(XPathNodeType.Attribute);
        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(document.CreateTextNode(""), default));
        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(document.CreateDocumentType("r"), default));
        var fragment = document.CreateDocumentFragment();
        NativeXPath.CreateNavigator(fragment, default).NodeType.Should().Be(XPathNodeType.Root);
        var fragmentNav = NativeXPath.CreateNavigator(fragment, default);
        fragmentNav.LookupNamespace("").Should().BeEmpty();
        fragmentNav.LookupNamespace("missing").Should().BeNull();
        fragmentNav.LookupNamespace("xmlns").Should().Be(Namespaces.Xmlns);
        fragmentNav.LookupPrefix("").Should().BeEmpty();
        fragmentNav.LookupPrefix(Namespaces.Xmlns).Should().Be("xmlns");
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
        var traversal = NativeXPath.CreateNavigator(root, (stage, _) =>
        {
            if (stage != XPathWorkStage.DescendantScan) return;
            checkpoints++;
            cancellation.Cancel();
        }, cancellation.Token);
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
        var nav = NativeXPath.CreateNavigator(deepest, (_, _) => checkpoints++, default);
        nav.MoveToFirstNamespace(XPathNamespaceScope.All).Should().BeTrue();
        nav.Value.Should().Be(Namespaces.Xml);
        checkpoints.Should().BeLessThan(30);
        var before = checkpoints;
        for (var i = 0; i < 100; i++) nav.LookupNamespace("xml").Should().Be(Namespaces.Xml);
        // One atomization charge per answer is expected; an ancestor rescan is not.
        checkpoints.Should().BeLessThan(before + 30);

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
        var nav = NativeXPath.CreateNavigator(element, (_, _) => checkpoints++, default);
        nav.MoveToFirstAttribute().Should().BeTrue();
        var scanned = checkpoints;
        for (var i = 1; i < 2048; i++) nav.MoveToNextAttribute().Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(element.GetAttributeNode("a2047"));
        checkpoints.Should().Be(scanned);
        checkpoints.Should().BeLessThan(100);

        using var cancellation = new CancellationTokenSource();
        var canceledAtScan = false;
        var canceled = NativeXPath.CreateNavigator(element, (stage, _) =>
        {
            if (stage != XPathWorkStage.AttributeScan) return;
            canceledAtScan = true;
            cancellation.Cancel();
        }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => canceled.MoveToFirstAttribute());
        canceledAtScan.Should().BeTrue();
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
        var first = NativeXPath.CreateNavigator(root, (stage, _) =>
        {
            if (changed || stage != XPathWorkStage.OrderIndex) return;
            changed = true;
            root.SetAttribute("changed", "yes");
        }, default);
        var final = NativeXPath.CreateNavigator(last!, default);
        Assert.Throws<InvalidOperationException>(() => first.ComparePosition(final));
        changed.Should().BeTrue();
        Assert.Throws<InvalidOperationException>(() => first.MoveToFirstChild());
    }

    [Test]
    public void PreviousTraversalAndInterleavedEmptyTextKeepOneRunIdentity()
    {
        var document = Document.CreateXml();
        var fragment = document.CreateDocumentFragment();
        var before = document.CreateElement("before");
        var first = document.CreateTextNode("A");
        var empty = document.CreateTextNode("");
        var last = document.CreateCDataSection(" B");
        var after = document.CreateElement("after");
        fragment.AppendChild(before);
        fragment.AppendChild(first);
        fragment.AppendChild(empty);
        fragment.AppendChild(last);
        fragment.AppendChild(after);

        var nav = NativeXPath.CreateNavigator(last, default);
        nav.UnderlyingObject.Should().BeSameAs(first);
        nav.Value.Should().Be("A B");
        Assert.Throws<ArgumentException>(() => NativeXPath.CreateNavigator(empty, default));
        nav.MoveToNext().Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(after);
        nav.MoveToPrevious().Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(first);
        nav.Value.Should().Be("A B");
        nav.MoveToPrevious().Should().BeTrue();
        nav.UnderlyingObject.Should().BeSameAs(before);
        nav.MoveToPrevious().Should().BeFalse();
        nav.UnderlyingObject.Should().BeSameAs(before);
    }

    [Test]
    public void AdoptionAndSaturatedStampInvalidateReadSessions()
    {
        var firstDocument = Document.CreateXml();
        var secondDocument = Document.CreateXml();
        var detached = firstDocument.CreateElement("detached");
        var nav = NativeXPath.CreateNavigator(detached, default);
        secondDocument.AdoptNode(detached);
        Assert.Throws<InvalidOperationException>(() => _ = nav.Value);
        NativeXPath.CreateNavigator(detached, default).UnderlyingObject.Should().BeSameAs(detached);

        var stamp = typeof(Document).GetField("_mutationStamp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        stamp.SetValue(secondDocument, ulong.MaxValue);
        Assert.Throws<InvalidOperationException>(() => NativeXPath.CreateNavigator(detached, default));
    }

    [Test]
    public void NameAndNamespaceScansCancelAtTheirOwnCheckpoints()
    {
        var document = Document.CreateXml();
        var named = document.CreateElement(new string('n', 1024));
        using var nameCancellation = new CancellationTokenSource();
        var nameStageReached = false;
        var nameNavigator = NativeXPath.CreateNavigator(named, (stage, _) =>
        {
            if (stage != XPathWorkStage.NameAtomization) return;
            nameStageReached = true;
            nameCancellation.Cancel();
        }, nameCancellation.Token);
        Assert.Throws<OperationCanceledException>(() => _ = nameNavigator.Name);
        nameStageReached.Should().BeTrue();

        var root = document.CreateElement("r");
        document.AppendChild(root);
        for (var i = 0; i < 500; i++) root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p" + i, "urn:" + i);
        using var namespaceCancellation = new CancellationTokenSource();
        var namespaceStageReached = false;
        var namespaceNavigator = NativeXPath.CreateNavigator(root, (stage, _) =>
        {
            if (stage != XPathWorkStage.NamespaceScan) return;
            namespaceStageReached = true;
            namespaceCancellation.Cancel();
        }, namespaceCancellation.Token);
        Assert.Throws<OperationCanceledException>(() => namespaceNavigator.MoveToFirstNamespace(XPathNamespaceScope.All));
        namespaceStageReached.Should().BeTrue();
    }
}
