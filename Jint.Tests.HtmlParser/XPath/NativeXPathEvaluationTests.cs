#nullable enable
using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser.Tests.XPath;

[TestFixture]
public sealed class NativeXPathEvaluationTests
{
    [Test]
    public void DetachedFollowingGuardPreservesNestedAxesAndLiteralResults()
    {
        var attribute = Document.CreateXml().CreateAttribute("key");
        attribute.Value = "value";
        foreach (var source in new[]
        {
            "following::node()", "following::*", "following \t::\n node()",
            "following::node() | self::node()[following::node()]",
            "self::node()[count(following::node()) = 0]/following::node()",
            "parent::node()", "child::node()", "descendant::node()",
            "ancestor::node()", "preceding::node()", "following-sibling::node()",
            "preceding-sibling::node()", "attribute::node()", "namespace::node()",
            "id('missing')"
        })
        {
            NativeXPath.Select(attribute, source, null, default).Should().BeEmpty(source);
        }

        NativeXPath.Evaluate(attribute, "count(following::node()) + 1", null, default)
            .NumberValue.Should().Be(1d);
        NativeXPath.Evaluate(attribute, "count(self::node()[count(following::node()) = 0])", null, default)
            .NumberValue.Should().Be(1d);
        NativeXPath.Evaluate(attribute, "string(self::node()[position() = last()])", null, default)
            .StringValue.Should().Be("value");
        NativeXPath.Select(attribute, "self::node() | following::node()", null, default)
            .Should().ContainSingle().Which.Should().BeSameAs(attribute);
        NativeXPath.Evaluate(attribute, "contains('following::node()', 'following::')", null, default)
            .BooleanValue.Should().BeTrue();
        NativeXPath.Select(attribute, "following-sibling::node()", null, default).Should().BeEmpty();
        NativeXPath.Evaluate(attribute, "string(.)", null, default).StringValue.Should().Be("value");
    }

    [Test]
    public void GuardPreservesOrdinaryAttachedAndNamespaceFollowingPositions()
    {
        var document = MarkupParser.ParseXml("<r xmlns:p='urn:p'><a key='v'/><b/><b/></r>");
        var a = (Element)document.DocumentElement!.FirstChild!;
        var b = (Element)a.NextSibling!;
        var lastB = (Element)b.NextSibling!;
        NativeXPath.Select(a, "following::b", null, default).Should().Equal(b, lastB);
        NativeXPath.Select(a.GetAttributeNode("key")!, "following::b", null, default)
            .Should().Equal(b, lastB);
        NativeXPath.Select(document, "//a/following::b", null, default)
            .Should().Equal(b, lastB);
        NativeXPath.Select(a, "following::b[position() = last()]", null, default)
            .Should().ContainSingle().Which.Should().BeSameAs(lastB);
        NativeXPath.Evaluate(a, "count(following::b[position() = last()])", null, default)
            .NumberValue.Should().Be(1d);

        var namespaceCursor = (NativeXPathNavigator)NativeXPath.CreateNavigator(a, default);
        namespaceCursor.MoveToFirstNamespace(XPathNamespaceScope.All).Should().BeTrue();
        var prepared = NativeXPath.Compile("following::b", null, default);
        var iterator = (XPathNodeIterator)namespaceCursor.EvaluatePrepared(prepared);
        iterator.MoveNext().Should().BeTrue();
        iterator.Current!.UnderlyingObject.Should().BeSameAs(b);
        iterator.MoveNext().Should().BeTrue();
        iterator.Current!.UnderlyingObject.Should().BeSameAs(lastB);
        iterator.MoveNext().Should().BeFalse();

        var detached = document.CreateElement("detached");
        NativeXPath.Select(detached, "following::node()", null, default).Should().BeEmpty();
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(document.CreateElement("only"));
        NativeXPath.Select(fragment, "following::node()", null, default).Should().BeEmpty();
    }

    [Test]
    public void PreparedExpressionsRetainSourceAndResolverAcrossFreshTrees()
    {
        var resolver = new XmlNamespaceManager(new NameTable());
        resolver.AddNamespace("p", "urn:p");
        const string source = "//p:a/following::p:b";
        var expression = NativeXPath.Compile(source, resolver, default);
        expression.Source.Should().Be(source);
        expression.ReturnType.Should().Be(XPathResultType.NodeSet);
        var first = MarkupParser.ParseXml("<r xmlns:p='urn:p'><p:a/><p:b/></r>");
        var second = MarkupParser.ParseXml("<r xmlns:p='urn:p'><p:a/><p:b/></r>");
        var firstB = ((Element)first.DocumentElement!.FirstChild!).NextSibling!;
        var secondB = ((Element)second.DocumentElement!.FirstChild!).NextSibling!;
        NativeXPath.Select(first, expression, default).Should().ContainSingle().Which.Should().BeSameAs(firstB);
        NativeXPath.Select(second, expression, default).Should().ContainSingle().Which.Should().BeSameAs(secondB);
        NativeXPath.Select(first, expression, default).Should().ContainSingle().Which.Should().BeSameAs(firstB);

        var detached = first.CreateAttribute("key");
        Assert.Throws<XPathException>(() => NativeXPath.Select(detached, "following::p:b", null, default));
        Assert.Throws<XPathException>(() => NativeXPath.Compile("following::[", resolver, default));
        NativeXPath.Compile("contains('following::', 'following::')", null, default)
            .Source.Should().Be("contains('following::', 'following::')");

        var lookalikes = MarkupParser.ParseXml("<r xmlns:p='urn:p'><following/><p:following/></r>");
        NativeXPath.Select(lookalikes, "//following", null, default).Should().ContainSingle();
        NativeXPath.Select(lookalikes, "//p:following", resolver, default).Should().ContainSingle();
        NativeXPath.Select(lookalikes, "//following-sibling::following", null, default).Should().BeEmpty();
    }

    [Test]
    public void PublishedResultsAreTypedImmutableAndStableAfterMutation()
    {
        var document = MarkupParser.ParseXml("<r xmlns:p='urn:p' a='old'><x>one<![CDATA[two]]></x></r>");
        var root = document.DocumentElement!;
        var attr = root.GetAttributeNode("a")!;
        var text = (Text)root.FirstChild!.FirstChild!;
        var nodes = NativeXPath.Evaluate(document, "//text()", null, default);
        nodes.ResultType.Should().Be(XPathResultType.NodeSet);
        nodes.Nodes.Should().ContainSingle().Which.Should().BeSameAs(text);
        nodes.FirstNodeStringValue.Should().Be("onetwo");
        Assert.Throws<InvalidOperationException>(() => _ = nodes.StringValue);
        Assert.Throws<InvalidOperationException>(() => _ = nodes.NumberValue);
        Assert.Throws<InvalidOperationException>(() => _ = nodes.BooleanValue);
        var selected = NativeXPath.Select(root, "@a", null, default);
        selected.Should().ContainSingle().Which.Should().BeSameAs(attr);
        Assert.Throws<NotSupportedException>(() => ((IList<object>)selected)[0] = root);
        NativeXPath.Evaluate(root, "@a", null, default).FirstNodeStringValue.Should().Be("old");
        var namespaceResult = NativeXPath.Evaluate(root, "namespace::p", null, default);
        namespaceResult.FirstNodeStringValue.Should().Be("urn:p");
        namespaceResult.Nodes.Should().ContainSingle().Which.Should().BeOfType<XPathNamespaceBinding>();
        NativeXPath.Evaluate(root, "missing", null, default).FirstNodeStringValue.Should().BeEmpty();

        var number = NativeXPath.Evaluate(root, "count(./*)", null, default);
        number.ResultType.Should().Be(XPathResultType.Number);
        number.NumberValue.Should().Be(1d);
        Assert.Throws<InvalidOperationException>(() => _ = number.Nodes);
        var textResult = NativeXPath.Evaluate(root, "string(@a)", null, default);
        textResult.ResultType.Should().Be(XPathResultType.String);
        textResult.StringValue.Should().Be("old");
        var boolean = NativeXPath.Evaluate(root, "boolean(./x)", null, default);
        boolean.ResultType.Should().Be(XPathResultType.Boolean);
        boolean.BooleanValue.Should().BeTrue();
        Assert.Throws<XPathException>(() => NativeXPath.Select(root, "count(./*)", null, default));

        attr.Value = "new";
        ((Text)root.FirstChild!.FirstChild!).Data = "changed";
        nodes.FirstNodeStringValue.Should().Be("onetwo");
        nodes.Nodes[0].Should().BeSameAs(text);
        textResult.StringValue.Should().Be("old");
        selected[0].Should().BeSameAs(attr);
    }

    [Test]
    public void CursorFencesEveryOpaqueBclEvaluatorEntry()
    {
        var document = MarkupParser.ParseXml("<r/>");
        var cursor = NativeXPath.CreateNavigator(document, default);
        var compiled = XPathExpression.Compile("self::node()");
        Action[] calls =
        [
            () => cursor.Compile("["),
            () => cursor.Evaluate("["),
            () => cursor.Evaluate("[", null),
            () => cursor.Evaluate(compiled),
            () => cursor.Evaluate(compiled, null),
            () => cursor.Select("["),
            () => cursor.Select("[", null),
            () => cursor.Select(compiled),
            () => cursor.SelectSingleNode("["),
            () => cursor.SelectSingleNode("[", null),
            () => cursor.SelectSingleNode(compiled),
            () => cursor.Matches("["),
            () => cursor.Matches(compiled)
        ];
        calls.Should().HaveCount(13);
        foreach (var call in calls)
        {
            Assert.Throws<NotSupportedException>(() => call());
        }

        document.DocumentElement!.SetAttribute("changed", "yes");
        Assert.Throws<InvalidOperationException>(() => cursor.Evaluate(compiled, null));
        using var canceled = new CancellationTokenSource();
        var canceledCursor = NativeXPath.CreateNavigator(document, canceled.Token);
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceledCursor.Compile("["));
    }

    [Test]
    public void OwnedEntryPointsRejectNullInputsBeforeEvaluation()
    {
        var root = MarkupParser.ParseXml("<r/>").DocumentElement!;
        var expression = NativeXPath.Compile("self::node()", null, default);
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Compile(null!, null, default));
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Evaluate((Node)null!, expression, default));
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Evaluate(root, (NativeXPathExpression)null!, default));
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Select(root, (NativeXPathExpression)null!, default));
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Select(root, (string)null!, null, default));
    }

    [Test]
    public void ScanAndMaterializationCancelWithoutPublishingPartialResults()
    {
        var source = string.Join(" | ", Enumerable.Repeat("following::node()", 400));
        using var scanCancellation = new CancellationTokenSource();
        var scanned = false;
        Assert.Throws<OperationCanceledException>(() => NativeXPath.Compile(source, null, (stage, _) =>
        {
            if (stage != XPathWorkStage.CompilationScan || scanned) return;
            scanned = true;
            scanCancellation.Cancel();
        }, scanCancellation.Token));
        scanned.Should().BeTrue();

        int ScanWork(int guards)
        {
            var text = string.Join(" | ", Enumerable.Repeat("following::node()", guards));
            var progress = 0;
            NativeXPath.Compile(text, null, (stage, work) =>
            {
                if (stage == XPathWorkStage.CompilationScan) progress = work;
            }, default);
            return progress;
        }

        var shortWork = ScanWork(128);
        var longWork = ScanWork(256);
        shortWork.Should().BeGreaterThan(0);
        longWork.Should().BeGreaterThan(shortWork);
        longWork.Should().BeLessThan(shortWork * 5 / 2);

        var document = Document.CreateXml();
        var root = document.CreateElement("r");
        document.AppendChild(root);
        for (var i = 0; i < 700; i++) root.AppendChild(document.CreateElement("x"));
        var expression = NativeXPath.Compile("/r/x", null, default);
        using var resultCancellation = new CancellationTokenSource();
        var reached = false;
        NativeXPathResult? published = null;
        Assert.Throws<OperationCanceledException>(() => published = NativeXPath.Evaluate(document, expression,
            (stage, _) =>
            {
                if (stage != XPathWorkStage.ResultMaterialization || reached) return;
                reached = true;
                resultCancellation.Cancel();
            }, resultCancellation.Token));
        reached.Should().BeTrue();
        published.Should().BeNull();
        NativeXPath.Select(document, expression, default).Should().HaveCount(700);

        var changed = false;
        Assert.Throws<InvalidOperationException>(() => published = NativeXPath.Evaluate(document, expression,
            (stage, _) =>
            {
                if (stage != XPathWorkStage.ResultMaterialization || changed) return;
                changed = true;
                root.SetAttribute("changed", "yes");
            }, default));
        changed.Should().BeTrue();
        published.Should().BeNull();
        NativeXPath.Select(document, expression, default).Should().HaveCount(700);

        var reachedPublication = false;
        Assert.Throws<InvalidOperationException>(() => published = NativeXPath.Evaluate(document, expression,
            (stage, _) =>
            {
                if (stage != XPathWorkStage.ResultPublication || reachedPublication) return;
                reachedPublication = true;
                root.SetAttribute("published", "no");
            }, default));
        reachedPublication.Should().BeTrue();
        published.Should().BeNull();
        NativeXPath.Select(document, expression, default).Should().HaveCount(700);
    }
}
