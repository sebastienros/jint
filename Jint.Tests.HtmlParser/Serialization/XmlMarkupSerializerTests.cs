using Jint.HtmlParser;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Serialization;

[TestFixture]
public sealed class XmlMarkupSerializerTests
{
    [Test]
    public void WholeNodesChildrenAndAttributeDispatchHaveLiteralResults()
    {
        var document = Document.CreateXml();
        var root = document.CreateElementNS("urn:one", "root");
        root.AppendChild(document.CreateTextNode("<&>"));
        document.AppendChild(document.CreateDocumentType("root"));
        document.AppendChild(root);
        XmlMarkupSerializer.Serialize(document).Should().Be("<!DOCTYPE root><root xmlns=\"urn:one\">&lt;&amp;&gt;</root>");
        XmlMarkupSerializer.Serialize(root).Should().Be("<root xmlns=\"urn:one\">&lt;&amp;&gt;</root>");
        XmlMarkupSerializer.SerializeChildren(root).Should().Be("&lt;&amp;&gt;");
        XmlMarkupSerializer.Serialize(document.CreateAttribute("a")).Should().BeEmpty();
        XmlMarkupSerializer.Serialize(document.CreateComment("x")).Should().Be("<!--x-->");
        XmlMarkupSerializer.Serialize(document.CreateCDataSection("x")).Should().Be("<![CDATA[x]]>");
        XmlMarkupSerializer.Serialize(document.CreateProcessingInstruction("p", "v")).Should().Be("<?p v?>");
        Assert.Throws<ArgumentException>(() => XmlMarkupSerializer.SerializeChildren(document.CreateTextNode("x")));
    }

    [Test]
    public void PrefixDefinitionsAfterUseAreRecordedBeforeAttributeOutput()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("e");
        element.SetAttributeNS("urn:a", "p:first", "1");
        element.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:a");
        XmlMarkupSerializer.Serialize(element, true).Should().Be("<e p:first=\"1\" xmlns:p=\"urn:a\"/>");
    }

    [Test]
    public void ConflictingElementPrefixIsRepairedWithoutMovingNativeAttributes()
    {
        var document = Document.CreateXml();
        var element = document.CreateElementNS("urn:actual", "p:item");
        element.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:other");
        element.SetAttributeNS("urn:other", "p:a", "&>\t");
        XmlMarkupSerializer.Serialize(element, true).Should().Be(
            "<ns1:item xmlns:ns1=\"urn:actual\" xmlns:p=\"urn:other\" p:a=\"&amp;&gt;&#9;\"/>");
        element.Prefix.Should().Be("p");
        element.GetAttributeNodeNS("urn:other", "a")!.Prefix.Should().Be("p");
    }

    [Test]
    public void GeneratedPrefixesSkipLocalDeclarationsAndCounterSpansSiblings()
    {
        var document = Document.CreateXml();
        var fragment = document.CreateDocumentFragment();
        var first = document.CreateElement("one");
        first.SetAttributeNS(Namespaces.Xmlns, "xmlns:ns1", "urn:reserved");
        first.SetAttributeNS("urn:a", "a", "v");
        var second = document.CreateElement("two");
        second.SetAttributeNS("urn:b", "b", "v");
        fragment.AppendChild(first);
        fragment.AppendChild(second);
        XmlMarkupSerializer.Serialize(fragment, true).Should().Be(
            "<one xmlns:ns1=\"urn:reserved\" xmlns:ns2=\"urn:a\" ns2:a=\"v\"/><two xmlns:ns3=\"urn:b\" ns3:b=\"v\"/>");
    }

    [Test]
    public void DefaultNamespaceResetsAndChildrenHaveIndependentContexts()
    {
        var document = Document.CreateXml();
        var root = document.CreateElementNS("urn:r", "root");
        var plain = document.CreateElement("plain");
        plain.AppendChild(document.CreateElementNS("urn:r", "again"));
        root.AppendChild(plain);
        XmlMarkupSerializer.Serialize(root, true).Should().Be(
            "<root xmlns=\"urn:r\"><plain xmlns=\"\"><again xmlns=\"urn:r\"/></plain></root>");
        XmlMarkupSerializer.SerializeChildren(root, true).Should().Be(
            "<plain><again xmlns=\"urn:r\"/></plain>");
    }

    [Test]
    public void PrefixRebindingNeverReusesAStaleUriCandidate()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:a");
        var child = document.CreateElement("child");
        child.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:b");
        child.SetAttributeNS("urn:a", "x", "value");
        root.AppendChild(child);
        XmlMarkupSerializer.Serialize(root, true).Should().Be(
            "<root xmlns:p=\"urn:a\"><child xmlns:p=\"urn:b\" xmlns:ns1=\"urn:a\" ns1:x=\"value\"/></root>");
    }

    [Test]
    public void PrefixUndoRestoresOrderedCandidatesForFollowingSiblings()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:a");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:q", "urn:a");
        var rebinding = document.CreateElement("one");
        rebinding.SetAttributeNS(Namespaces.Xmlns, "xmlns:q", "urn:b");
        rebinding.SetAttributeNS("urn:a", "x", "1");
        var following = document.CreateElement("two");
        following.SetAttributeNS("urn:a", "y", "2");
        root.AppendChild(rebinding);
        root.AppendChild(following);
        XmlMarkupSerializer.Serialize(root, true).Should().Be(
            "<root xmlns:p=\"urn:a\" xmlns:q=\"urn:a\"><one xmlns:q=\"urn:b\" p:x=\"1\"/><two q:y=\"2\"/></root>");
    }

    [Test]
    public void InheritedPrefixCanBeLocallyReboundWithoutChangingAnEarlierName()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:old");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:q", "urn:old");
        var child = document.CreateElementNS("urn:new", "p:child");
        child.SetAttributeNS("urn:newer", "q:a", "1");
        root.AppendChild(child);
        XmlMarkupSerializer.Serialize(root, true).Should().Be(
            "<root xmlns:p=\"urn:old\" xmlns:q=\"urn:old\"><p:child xmlns:p=\"urn:new\" xmlns:q=\"urn:newer\" q:a=\"1\"/></root>");
    }

    [Test]
    public void LaterAttributeCannotRebindAnAlreadyEmittedPrefix()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:old");
        var child = document.CreateElement("child");
        child.SetAttributeNS("urn:old", "p:first", "1");
        child.SetAttributeNS("urn:new", "p:second", "2");
        root.AppendChild(child);
        XmlMarkupSerializer.Serialize(root, true).Should().Be(
            "<root xmlns:p=\"urn:old\"><child p:first=\"1\" xmlns:ns1=\"urn:new\" ns1:second=\"2\"/></root>");
    }

    [Test]
    public void RedundantAncestorPrefixDeclarationIsOmittedAndDefaultConflictIsRepaired()
    {
        var document = Document.CreateXml();
        var root = document.CreateElementNS("urn:r", "root");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:a");
        var child = document.CreateElementNS("urn:c", "child");
        child.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:a");
        child.SetAttributeNS(Namespaces.Xmlns, "xmlns", "urn:wrong");
        root.AppendChild(child);
        XmlMarkupSerializer.Serialize(root, true).Should().Be(
            "<root xmlns=\"urn:r\" xmlns:p=\"urn:a\"><child xmlns=\"urn:c\"/></root>");
    }

    [Test]
    public void SafeNamespaceAndValueFixtureRoundTripsByExpandedNames()
    {
        var document = Document.CreateXml();
        var root = document.CreateElementNS("urn:r", "root");
        root.SetAttributeNS("urn:a", "p:attr", "<>&\t\n\r");
        var child = document.CreateElementNS("urn:c", "child");
        child.AppendChild(document.CreateTextNode("text & <"));
        root.AppendChild(child);
        var output = XmlMarkupSerializer.Serialize(root, true);
        output.Should().Be("<root xmlns=\"urn:r\" xmlns:p=\"urn:a\" p:attr=\"&lt;&gt;&amp;&#9;&#xA;&#xD;\"><child xmlns=\"urn:c\">text &amp; &lt;</child></root>");
        var parsed = MarkupParser.ParseXml(output).DocumentElement!;
        parsed.NamespaceUri.Should().Be(root.NamespaceUri);
        parsed.LocalName.Should().Be(root.LocalName);
        parsed.GetAttributeNS("urn:a", "attr").Should().Be(root.GetAttributeNS("urn:a", "attr"));
        var parsedChild = (Element) parsed.FirstChild!;
        parsedChild.NamespaceUri.Should().Be(child.NamespaceUri);
        parsedChild.LocalName.Should().Be(child.LocalName);
        ((Text) parsedChild.FirstChild!).Data.Should().Be(((Text) child.FirstChild!).Data);
    }

    [Test]
    public void TrueModeRejectsDuplicateExpandedNamesAndInvalidNames()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("e");
        element.InitializeParsedAttributes(
            [new ParserAttribute("urn:a", "a", "p", "1"), new ParserAttribute("urn:a", "a", "q", "2")],
            default);
        Assert.Throws<DomException>(() => XmlMarkupSerializer.Serialize(element, true))!.Name.Should().Be("InvalidStateError");
        var bad = document.CreateParsedElement(null, "bad:name", null);
        Assert.Throws<DomException>(() => XmlMarkupSerializer.Serialize(bad, true))!.Name.Should().Be("InvalidStateError");
        XmlMarkupSerializer.Serialize(bad, false).Should().Be("<bad:name/>");
        var leadingDigit = document.CreateParsedElement(null, "1bad", null);
        Assert.Throws<DomException>(() => XmlMarkupSerializer.Serialize(leadingDigit, true))!.Name.Should().Be("InvalidStateError");
        var unnamespacedXmlns = document.CreateElement("e");
        unnamespacedXmlns.SetAttribute("xmlns", "urn:false-declaration");
        Assert.Throws<DomException>(() => XmlMarkupSerializer.Serialize(unnamespacedXmlns, true))!.Name.Should().Be("InvalidStateError");
        var undeclaredPrefix = document.CreateElement("e");
        undeclaredPrefix.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "");
        Assert.Throws<DomException>(() => XmlMarkupSerializer.Serialize(undeclaredPrefix, true))!.Name.Should().Be("InvalidStateError");
    }

    [Test]
    public void HtmlNamespaceUsesSpecifiedEmptyElementSpellingsAndTemplates()
    {
        var document = Document.CreateHtml();
        XmlMarkupSerializer.Serialize(document.CreateElement("br"), true).Should().Be(
            "<br xmlns=\"http://www.w3.org/1999/xhtml\" />");
        XmlMarkupSerializer.Serialize(document.CreateElement("div"), true).Should().Be(
            "<div xmlns=\"http://www.w3.org/1999/xhtml\"></div>");
        var template = document.CreateElement("template");
        template.TemplateContent!.AppendChild(document.CreateElement("b"));
        template.AppendChild(document.CreateTextNode("ordinary"));
        XmlMarkupSerializer.Serialize(template, true).Should().Be(
            "<template xmlns=\"http://www.w3.org/1999/xhtml\"><b></b></template>");
    }

    [Test]
    public void MutableLeafErrorsFollowRequireWellFormedPolicy()
    {
        var document = Document.CreateXml();
        var comment = document.CreateComment("good");
        comment.Data = "bad--";
        XmlMarkupSerializer.Serialize(comment).Should().Be("<!--bad---->");
        Assert.Throws<DomException>(() => XmlMarkupSerializer.Serialize(comment, true))!.Name.Should().Be("InvalidStateError");
        var pi = document.CreateProcessingInstruction("target", "good");
        pi.Data = "bad?>";
        XmlMarkupSerializer.Serialize(pi).Should().Be("<?target bad?>?>");
        Assert.Throws<DomException>(() => XmlMarkupSerializer.Serialize(pi, true))!.Name.Should().Be("InvalidStateError");
        var cdata = document.CreateCDataSection("good");
        cdata.Data = "bad]]>";
        XmlMarkupSerializer.Serialize(cdata, true).Should().Be("<![CDATA[bad]]>]]>");
        var doctype = document.CreateDocumentType("not!xml");
        XmlMarkupSerializer.Serialize(doctype, true).Should().Be("<!DOCTYPE not!xml>");
    }

    [Test]
    public void EscapeQuotaAndCancellationDoNotPublishPartialResult()
    {
        var document = Document.CreateXml();
        var node = document.CreateElement("r");
        node.AppendChild(document.CreateTextNode("&"));
        XmlMarkupSerializer.Serialize(node, limits: new SerializationLimits { MaxOutputCharacters = 12 })
            .Should().Be("<r>&amp;</r>");
        var exception = Assert.Throws<SerializationLimitException>(() =>
            XmlMarkupSerializer.Serialize(node, limits: new SerializationLimits { MaxOutputCharacters = 11 }));
        exception!.Observed.Should().Be(12);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => XmlMarkupSerializer.Serialize(node,
            checkpoint: stage => { if (stage == SerializationStage.Materialize) cancellation.Cancel(); },
            cancellationToken: cancellation.Token));
    }

    [Test]
    public void SerializationLeavesNativeIdentityStampAndMutationQueueUnchanged()
    {
        var document = Document.CreateXml();
        var element = document.CreateElementNS("urn:x", "p:e");
        document.AppendChild(element);
        var attribute = document.CreateAttributeNS("urn:a", "q:a");
        attribute.Value = "v";
        element.SetAttributeNode(attribute);
        using var observer = document.ObserveMutations(element, new MutationObserverOptions { Subtree = true, Attributes = true });
        var stamp = document.MutationStamp;
        var output = XmlMarkupSerializer.Serialize(element, true);
        output.Should().Be("<p:e xmlns:p=\"urn:x\" xmlns:q=\"urn:a\" q:a=\"v\"/>");
        element.ParentNode.Should().BeSameAs(document);
        attribute.OwnerElement.Should().BeSameAs(element);
        attribute.Prefix.Should().Be("q");
        document.MutationStamp.Should().Be(stamp);
        observer.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void MutationsAtScanCheckpointInvalidateOutput()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        root.AppendChild(document.CreateTextNode(new string('a', 2_000)));
        var changed = false;
        Assert.Throws<InvalidOperationException>(() => XmlMarkupSerializer.Serialize(root,
            checkpoint: stage =>
            {
                if (stage != SerializationStage.Append || changed) return;
                changed = true;
                root.SetAttribute("changed", "yes");
            }));
        changed.Should().BeTrue();
    }

    [Test]
    public void DeepAndWideNamespaceWorkUsesCooperativePolls()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var current = root;
        for (var i = 0; i < 1_000; i++)
        {
            var child = document.CreateElementNS("urn:d", "n");
            current.AppendChild(child);
            current = child;
        }

        for (var i = 0; i < 500; i++) current.SetAttributeNS("urn:" + i, "a" + i, "v");
        using var cancellation = new CancellationTokenSource();
        var scans = 0;
        Assert.Throws<OperationCanceledException>(() => XmlMarkupSerializer.Serialize(root, true,
            checkpoint: stage =>
            {
                if (stage == SerializationStage.Scan && ++scans == 12) cancellation.Cancel();
            }, cancellationToken: cancellation.Token));
        scans.Should().Be(12);
        XmlMarkupSerializer.Serialize(root, true).Should().Contain("xmlns:ns500=\"urn:499\"");
    }

    [Test]
    public void PrefixCollisionLoopHasItsOwnCancellationPoll()
    {
        using var cancellation = new CancellationTokenSource();
        var armed = false;
        var work = new SerializationWork(cancellation.Token, stage =>
        {
            if (armed && stage == SerializationStage.Scan) cancellation.Cancel();
        });
        var scope = new XmlNamespaceScope(work);
        var reserved = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i <= 1_000; i++) reserved.Add("ns" + i);
        armed = true;
        Assert.Throws<OperationCanceledException>(() => scope.Generate("urn:new", reserved));
    }
}
