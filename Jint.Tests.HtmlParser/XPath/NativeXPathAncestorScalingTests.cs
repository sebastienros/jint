#nullable enable
using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser.Tests.XPath;

[TestFixture]
public sealed class NativeXPathAncestorScalingTests
{
    [TestCase("//*[ancestor::r]", true)]
    [TestCase("//*[ancestor::missing]", false)]
    [TestCase("//*[ancestor::*]", true)]
    [TestCase("//*[ancestor-or-self::r]", true)]
    public void BareAncestorPredicatesHaveLinearWork(string source, bool matches)
    {
        var expression = NativeXPath.Compile(source);
        int Measure(int depth)
        {
            var document = DeepTree(depth);
            var work = 0;
            var nodes = NativeXPath.Select(document, expression, (_, count) => work = count, default);
            nodes.Count.Should().Be(matches ? depth + (source.Contains("or-self", StringComparison.Ordinal) ? 1 : 0) : 0);
            if (nodes.Count != 0)
            {
                nodes[0].Should().BeSameAs(source.Contains("or-self", StringComparison.Ordinal)
                    ? document.DocumentElement : document.DocumentElement!.FirstChild);
                nodes[^1].Should().BeSameAs(Deepest(document));
            }
            return work;
        }
        var small = Measure(512);
        var medium = Measure(1024);
        var large = Measure(2048);
        small.Should().BeGreaterThan(0);
        medium.Should().BeLessThan(small * 5 / 2);
        large.Should().BeLessThan(medium * 5 / 2);
        large.Should().BeLessThan(2048 * 40);
    }

    [Test]
    public void PredicateResultsMatchBclIncludingReversePositionsAndNamespaces()
    {
        const string xml = "<r xmlns:p='urn:p'><p:x a='one'><x a='two'><p:x a='three'/></x></p:x><x a='four'/></r>";
        var native = MarkupParser.ParseXml(xml);
        var reference = new XmlDocument { XmlResolver = null };
        reference.LoadXml(xml);
        var manager = new XmlNamespaceManager(new NameTable());
        manager.AddNamespace("p", "urn:p");
        foreach (var source in new[]
        {
            "//*[ancestor::r]", "//*[ ancestor :: p:x ]", "//*[ancestor::p:*]",
            "//*[ancestor-or-self::p:x]", "//*[ancestor::node()]", "//@*[ancestor::p:x]",
            "//*[ancestor::r][1]", "//*[ancestor::r][last()]", "//*[ancestor::p:x]/ancestor::*[1]",
            "//*[ancestor::*[1][self::r]]", "//*[count(ancestor::*)=2]", "(//*[ancestor::r])[2]",
            "//*[ancestor::p:x] | //*[ancestor-or-self::r]", "//*[ancestor::missing]",
            "//*[ancestor::r]/namespace::*[ancestor::r]", "string('text [ancestor::r] jintAncestor')"
        })
        {
            var expected = reference.CreateNavigator()!.Evaluate(source, manager);
            var actual = NativeXPath.Evaluate(native, source, manager);
            if (expected is XPathNodeIterator iterator)
            {
                var values = new List<(XPathNodeType, string, string, string)>();
                while (iterator.MoveNext()) values.Add((iterator.Current!.NodeType, iterator.Current.LocalName, iterator.Current.NamespaceURI, iterator.Current.Value));
                var observed = actual.Nodes.Select(node =>
                {
                    var cursor = node switch
                    {
                        Node n => NativeXPath.CreateNavigator(n, default),
                        Attr a => NativeXPath.CreateNavigator(a, default),
                        XPathNamespaceBinding binding => NativeXPath.CreateNavigator(binding, null, default),
                        _ => throw new InvalidOperationException()
                    };
                    return (cursor.NodeType, cursor.LocalName, cursor.NamespaceURI, cursor.Value);
                });
                observed.Should().Equal(values, source);
            }
            else actual.StringValue.Should().Be((string) expected, source);
        }
    }

    [Test]
    public void CachedAncestorAnswersDoNotOutliveAnEvaluation()
    {
        var document = MarkupParser.ParseXml("<r><x><x/></x></r>");
        var expression = NativeXPath.Compile("//*[ancestor::r]");
        NativeXPath.Select(document, expression).Should().HaveCount(2);
        var subtree = document.DocumentElement!.FirstChild!;
        document.DocumentElement.RemoveChild(subtree);
        NativeXPath.Select(subtree, expression).Should().BeEmpty();
        var other = MarkupParser.ParseXml("<r><x/></r>");
        NativeXPath.Select(other, expression).Should().HaveCount(1);
        other.DocumentElement!.AppendChild(subtree);
        NativeXPath.Select(other, expression).Should().HaveCount(3);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AncestorCacheConstructionCannotPublishAfterMutationOrCancellation(bool cancel)
    {
        var document = DeepTree(2048);
        var expression = NativeXPath.Compile("//*[ancestor::missing]");
        using var cancellation = new CancellationTokenSource();
        var reached = false;
        IReadOnlyList<object>? published = null;
        void Checkpoint(XPathWorkStage stage, int work)
        {
            if (stage != XPathWorkStage.AncestorScan || reached) return;
            reached = true;
            if (cancel) cancellation.Cancel();
            else document.DocumentElement!.SetAttribute("changed", "yes");
        }
        if (cancel) Assert.Throws<OperationCanceledException>(() => published = NativeXPath.Select(document, expression, Checkpoint, cancellation.Token));
        else Assert.Throws<InvalidOperationException>(() => published = NativeXPath.Select(document, expression, Checkpoint, default));
        reached.Should().BeTrue();
        published.Should().BeNull();
        NativeXPath.Select(document, expression).Should().BeEmpty();
    }

    [TestCase("//*[ancestor::missing]")]
    [TestCase("child::*[ancestor::missing]")]
    public void HostCheckpointFailuresKeepTheirOriginalIdentity(string source)
    {
        var document = DeepTree(2048);
        var context = source.StartsWith("child", StringComparison.Ordinal) ? Deepest(document).ParentNode! : document;
        var failure = new NotSupportedException("host budget exhausted");
        var expression = NativeXPath.Compile(source);
        var error = Assert.Throws<NotSupportedException>(() => NativeXPath.Select(context, expression,
            (stage, work) => { if (stage == XPathWorkStage.AncestorScan && (!source.StartsWith("child", StringComparison.Ordinal) || work > 2048)) throw failure; }, default));
        error.Should().BeSameAs(failure);
    }

    private static Document DeepTree(int depth)
        => MarkupParser.ParseXml("<r>" + string.Concat(Enumerable.Repeat("<x>", depth)) + string.Concat(Enumerable.Repeat("</x>", depth)) + "</r>");

    private static Node Deepest(Document document)
    {
        Node node = document.DocumentElement!;
        while (node.FirstChild is { } child) node = child;
        return node;
    }
}
