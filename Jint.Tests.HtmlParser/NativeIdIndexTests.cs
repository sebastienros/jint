#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser;

public sealed class NativeIdIndexTests
{
    private static Element? Find(Node root, string id, Action? checkpoint = null)
    {
        var work = new SelectorMatchWork(root, default, checkpoint);
        return NativeIdIndex.Find(root, id, ref work);
    }

    private static Element Add(Node parent, string id)
    {
        var node = (parent as Document ?? parent.OwnerDocument!).CreateElement("div");
        node.SetAttribute("id", id);
        parent.AppendChild(node);
        return node;
    }

    [Test]
    public void DuplicateOrderAttributesRemovalAndAdoptionInvalidate()
    {
        var document = Document.CreateHtml();
        var root = Add(document, "root");
        var first = Add(root, "x");
        var second = Add(root, "x");
        Find(document, "x").Should().BeSameAs(first);
        root.InsertBefore(second, first);
        Find(document, "x").Should().BeSameAs(second);
        second.GetAttributeNode("id")!.Value = "other";
        Find(document, "x").Should().BeSameAs(first);
        var attr = document.CreateAttribute("id");
        attr.Value = "x";
        second.SetAttributeNode(attr);
        Find(document, "x").Should().BeSameAs(second);
        second.RemoveAttribute("id");
        Find(document, "x").Should().BeSameAs(first);
        first.SetAttributeNS("urn:test", "t:id", "namespaced");
        Find(document, "namespaced").Should().BeNull();
        var other = Document.CreateHtml();
        var destination = Add(other, "destination");
        destination.AppendChild(first);
        Find(document, "x").Should().BeNull();
        Find(other, "x").Should().BeSameAs(first);
        Find(root, "root").Should().BeNull();
        Find(document, "").Should().BeNull();
    }

    [Test]
    public void ShadowFragmentAndDisconnectedScopesStaySeparate()
    {
        var document = Document.CreateHtml();
        var host = Add(document, "host");
        var light = Add(host, "x");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var dark = Add(shadow, "x");
        var fragment = document.CreateDocumentFragment();
        var detached = Add(fragment, "x");
        Find(document, "x").Should().BeSameAs(light);
        Find(shadow, "x").Should().BeSameAs(dark);
        Find(fragment, "x").Should().BeSameAs(detached);
        host.RemoveChild(light);
        Find(document, "x").Should().BeNull();
        Find(fragment, "x").Should().BeSameAs(detached);
        host.AppendChild(fragment);
        Find(fragment, "x").Should().BeNull();
        Find(document, "x").Should().BeSameAs(detached);
        document.RemoveChild(host);
        Find(document, "x").Should().BeNull();
        Find(host, "x").Should().BeSameAs(detached);
        var other = Document.CreateHtml();
        other.AdoptNode(host);
        Find(host, "x").Should().BeSameAs(detached);
        Find(shadow, "x").Should().BeSameAs(dark);
    }

    [TestCase(128)]
    [TestCase(512)]
    [TestCase(2048)]
    public void ManyDifferentLookupsChargeLinearWork(int size)
    {
        var document = Document.CreateHtml();
        var root = Add(document, "root");
        var nodes = Enumerable.Range(0, size).Select(i => Add(root, "item" + i)).ToArray();
        var polls = 0;
        for (var i = 0; i < size; i++)
        {
            Find(document, "item" + i, () => polls++).Should().BeSameAs(nodes[i]);
            var selector = SelectorCompiler.Compile("#item" + i, null, default);
            var work = new SelectorMatchWork(document, default, () => polls++);
            SelectorMatcher.QuerySelector(selector, document, default, ref work).Should().BeSameAs(nodes[i]);
        }
        polls.Should().BeLessThan(size * 8); // Full repeated scans exceed this envelope.
        var warmPolls = 0;
        root.SetAttribute("class", "unrelated");
        root.AppendChild(document.CreateTextNode("text"));
        Find(document, "item0"); // Warm after structural edit.
        root.FirstChild!.NextSibling!.AppendChild(document.CreateComment("comment"));
        Find(document, "item0");
        root.SetAttribute("class", "again");
        Find(document, "item0", () => warmPolls++).Should().BeSameAs(nodes[0]);
        warmPolls.Should().Be(2);
    }

    [Test]
    public void DeepCommentOnlyFinalAscentRemainsCancellable()
    {
        var document = Document.CreateHtml();
        var root = Add(document, "root");
        var parent = root;
        for (var i = 0; i < 1024; i++) parent = Add(parent, "");
        parent.AppendChild(document.CreateComment("last"));
        var calls = 0;
        using var cancelled = new CancellationTokenSource();
        Action lookup = () =>
        {
            var work = new SelectorMatchWork(root, cancelled.Token, () =>
            {
                // The first 3,072 units cover the descent. Cancel in the final ascent.
                if (++calls == 15) cancelled.Cancel();
            });
            NativeIdIndex.Find(root, "absent", ref work);
        };
        lookup.Should().Throw<OperationCanceledException>();
        calls.Should().Be(15);
        Find(root, "absent").Should().BeNull();
    }

    [Test]
    public void MutationDuringBuildCannotPublishAndWarmLookupStillChecks()
    {
        var document = Document.CreateHtml();
        var root = Add(document, "root");
        for (var i = 0; i < 1024; i++) Add(root, "item" + i);
        var calls = 0;
        Action lookup = () => Find(document, "item1000", () =>
        {
            if (++calls == 2) root.SetAttribute("id", "changed");
        });
        lookup.Should().Throw<InvalidOperationException>();
        Find(document, "changed").Should().BeSameAs(root);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Action cancel = () =>
        {
            var work = new SelectorMatchWork(document, cancelled.Token);
            NativeIdIndex.Find(document, "changed", ref work);
        };
        cancel.Should().Throw<OperationCanceledException>();
        lookup = () => Find(document, "changed", () => root.SetAttribute("class", "mutate"));
        lookup.Should().Throw<InvalidOperationException>();
    }
}
