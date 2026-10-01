using System.Xml;
using System.Xml.XPath;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.XPath;

public sealed class NativeXPathNamespaceContextTests
{
    [Test]
    public void NamespaceContextSupportsEveryOwnedOverloadAndNativeIdentity()
    {
        var document = MarkupParser.ParseXml("<r xmlns:p='urn:p'><a/><p:b/></r>");
        var owner = (Element) document.DocumentElement!.FirstChild!;
        var binding = (XPathNamespaceBinding) NativeXPath.Select(owner, "namespace::p").Single();
        binding.OwnerElement.Should().BeSameAs(owner);
        binding.Prefix.Should().Be("p");
        binding.NamespaceUri.Should().Be("urn:p");

        var resolver = new XmlNamespaceManager(new NameTable());
        resolver.AddNamespace("p", "urn:p");
        var expression = NativeXPath.Compile("following::p:b", resolver);
        NativeXPath.Select(binding, expression).Should().Equal(owner.NextSibling!);
        NativeXPath.Select(binding, "following::p:b", resolver).Should().Equal(owner.NextSibling!);
        NativeXPath.Evaluate(binding, expression).Nodes.Should().Equal(owner.NextSibling!);
        NativeXPath.Evaluate(binding, "following::p:b", resolver).Nodes.Should().Equal(owner.NextSibling!);
        NativeXPath.Evaluate(binding, "string(.)").StringValue.Should().Be("urn:p");
        NativeXPath.Evaluate(binding, "name(.)").StringValue.Should().Be("p");
        NativeXPath.Evaluate(binding, "namespace-uri(.)").StringValue.Should().BeEmpty();
        NativeXPath.Select(binding, "..").Should().Equal(owner);
        NativeXPath.Select(binding, "/").Should().Equal(document);
        NativeXPath.Select(binding, "self::node()").Single().Should().BeOfType<XPathNamespaceBinding>()
            .Which.OwnerElement.Should().BeSameAs(owner);
        Assert.Throws<XPathException>(() => NativeXPath.Select(binding, "count(.)"));
    }

    [Test]
    public void CapturedBindingsRejectChangedOrRemovedUrisButNotUnrelatedMutations()
    {
        var document = MarkupParser.ParseXml("<r xmlns:p='urn:p'><a/></r>");
        var root = document.DocumentElement!;
        var owner = (Element) root.FirstChild!;
        var binding = (XPathNamespaceBinding) NativeXPath.Select(owner, "namespace::p").Single();
        var expression = NativeXPath.Compile("string(.)");
        owner.SetAttribute("id", "unrelated");
        NativeXPath.Evaluate(binding, expression).StringValue.Should().Be("urn:p");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:changed");
        Assert.Throws<InvalidOperationException>(() => NativeXPath.Evaluate(binding, expression));
        Assert.Throws<InvalidOperationException>(() => NativeXPath.Select(binding, "self::node()"));
        binding.NamespaceUri.Should().Be("urn:p");
        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:p", "urn:p");
        NativeXPath.Evaluate(binding, expression).StringValue.Should().Be("urn:p");
        root.RemoveAttributeNS(Namespaces.Xmlns, "p");
        Assert.Throws<InvalidOperationException>(() => NativeXPath.Evaluate(binding, expression));
    }

    [Test]
    public void NamespaceOfDetachedElementKeepsTheActualRoot()
    {
        var owner = Document.CreateXml().CreateElementNS("urn:p", "p:r");
        var binding = (XPathNamespaceBinding) NativeXPath.Select(owner, "namespace::p").Single();
        NativeXPath.Select(binding, "/").Should().Equal(owner);
        NativeXPath.Select(binding, "parent::*").Should().Equal(owner);
        NativeXPath.Select(binding, "following::node()").Should().BeEmpty();
        NativeXPath.Evaluate(binding, "count(following::node()) + 1").NumberValue.Should().Be(1);
    }

    [Test]
    public void NamespaceContextsHonorNullsCancellationAndBindingTimeMutation()
    {
        var root = MarkupParser.ParseXml("<r xmlns:p='urn:p'/>").DocumentElement!;
        var binding = (XPathNamespaceBinding) NativeXPath.Select(root, "namespace::p").Single();
        var expression = NativeXPath.Compile(".");
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Evaluate((XPathNamespaceBinding) null!, expression));
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Select((XPathNamespaceBinding) null!, "."));
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Evaluate(binding, (NativeXPathExpression) null!));
        Assert.Throws<ArgumentNullException>(() => NativeXPath.Select(binding, (string) null!));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => NativeXPath.Evaluate(binding, expression, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => NativeXPath.Select(binding, ".", cancellationToken: cancellation.Token));

        root.SetAttributeNS(Namespaces.Xmlns, "xmlns:long", "urn:" + new string('x', 1024));
        var checkpoints = 0;
        Assert.Throws<InvalidOperationException>(() => NativeXPath.CreateNavigator(binding, (_, _) =>
        {
            checkpoints++;
            root.SetAttribute("mutated", "yes");
        }, default));
        checkpoints.Should().BeGreaterThan(0);
    }
}
