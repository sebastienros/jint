using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Serialization;

public sealed class MarkupSerializerTests
{
    [Test]
    public void PublicMethodsSelectFormatExplicitlyAndPreserveIdentities()
    {
        var document = MarkupParser.ParseXml("<r xmlns='urn:r'><x>&amp;</x></r>");
        var root = document.DocumentElement!;
        var child = root.FirstChild!;
        using var mutations = document.ObserveMutations(root, new MutationObserverOptions { ChildList = true, Subtree = true, Attributes = true });
        MarkupSerializer.ToHtml(root).Should().Be("<r xmlns=\"urn:r\"><x>&amp;</x></r>");
        MarkupSerializer.ToHtmlChildren(root).Should().Be("<x>&amp;</x>");
        MarkupSerializer.ToXml(root).Should().Be("<r xmlns=\"urn:r\"><x>&amp;</x></r>");
        MarkupSerializer.ToXmlChildren(root, true).Should().Be("<x xmlns=\"urn:r\">&amp;</x>");
        root.FirstChild.Should().BeSameAs(child);
        mutations.TakeRecords().Should().BeEmpty();
        var html = Document.CreateHtml().CreateElement("br");
        MarkupSerializer.ToHtml(html).Should().Be("<br>");
        MarkupSerializer.ToXml(html, true).Should().Be("<br xmlns=\"http://www.w3.org/1999/xhtml\" />");
        MarkupSerializer.ToXml(document.CreateAttribute("a"), true).Should().BeEmpty();
    }

    [Test]
    public void EveryPublicRouteEnforcesExactQuotaCancellationAndArguments()
    {
        var document = MarkupParser.ParseHtml("<p>&amp;</p>");
        var root = document.DocumentElement!;
        var attribute = document.CreateAttribute("a");
        var routes = new Func<SerializationLimits?, CancellationToken, string>[]
        {
            (limits, token) => MarkupSerializer.ToHtml(root, limits: limits, cancellationToken: token),
            (limits, token) => MarkupSerializer.ToHtmlChildren(root, limits: limits, cancellationToken: token),
            (limits, token) => MarkupSerializer.ToXml(root, limits: limits, cancellationToken: token),
            (limits, token) => MarkupSerializer.ToXmlChildren(root, limits: limits, cancellationToken: token)
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        foreach (var route in routes)
        {
            var expected = route(null, default);
            route(new() { MaxOutputCharacters = expected.Length }, default).Should().Be(expected);
            var failure = Assert.Throws<SerializationLimitException>(() => route(new() { MaxOutputCharacters = expected.Length - 1 }, default))!;
            failure.Limit.Should().Be(expected.Length - 1);
            failure.Observed.Should().Be(expected.Length);
            Assert.Throws<OperationCanceledException>(() => route(null, cancellation.Token));
        }
        Assert.Throws<OperationCanceledException>(() => MarkupSerializer.ToXml(attribute, cancellationToken: cancellation.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SerializationLimits { MaxOutputCharacters = -1 });
        Assert.Throws<ArgumentNullException>(() => MarkupSerializer.ToHtml(null!));
        Assert.Throws<ArgumentNullException>(() => MarkupSerializer.ToHtmlChildren(null!));
        Assert.Throws<ArgumentNullException>(() => MarkupSerializer.ToXml((Node) null!));
        Assert.Throws<ArgumentNullException>(() => MarkupSerializer.ToXml((Attr) null!));
        Assert.Throws<ArgumentNullException>(() => MarkupSerializer.ToXmlChildren(null!));
        Assert.Throws<ArgumentException>(() => MarkupSerializer.ToHtmlChildren(document.CreateTextNode("x")));
        Assert.Throws<ArgumentException>(() => MarkupSerializer.ToXmlChildren(document.CreateTextNode("x")));
        Assert.Throws<DomException>(() => MarkupSerializer.ToXml(Document.CreateXml(), true))!.Name.Should().Be("InvalidStateError");
    }

    [Test]
    public void PublicShadowAcquisitionSupportsSelectionAndNativeOwnership()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = host.AttachShadow(new(ShadowRootMode.Closed, DelegatesFocus: true, Serializable: true,
            SlotAssignment: SlotAssignmentMode.Manual, Clonable: true));
        root.Host.Should().BeSameAs(host);
        root.ParentNode.Should().BeNull();
        root.OwnerDocument.Should().BeSameAs(document);
        root.Mode.Should().Be(ShadowRootMode.Closed);
        root.DelegatesFocus.Should().BeTrue();
        root.Serializable.Should().BeTrue();
        root.SlotAssignment.Should().Be(SlotAssignmentMode.Manual);
        root.Clonable.Should().BeTrue();
        host.OpenShadowRoot.Should().BeNull();
        root.AppendChild(document.CreateTextNode("shadow"));
        host.AppendChild(document.CreateTextNode("light"));
        var roots = new List<ShadowRoot> { root, root };
        var options = new HtmlSerializationOptions(shadowRoots: roots);
        roots.Clear();
        options.ShadowRoots.Should().Equal(root);
        const string selected = "<template shadowrootmode=\"closed\" shadowrootdelegatesfocus=\"\"" +
            " shadowrootserializable=\"\" shadowrootslotassignment=\"manual\" shadowrootclonable=\"\">shadow</template>";
        MarkupSerializer.ToHtml(host).Should().Be("<div>light</div>");
        MarkupSerializer.ToHtml(host, options).Should().Be("<div>" + selected + "light</div>");
        MarkupSerializer.ToHtmlChildren(host, new(serializableShadowRoots: true)).Should().Be(selected + "light");
        MarkupSerializer.ToHtml(root).Should().Be("shadow");
        MarkupSerializer.ToXml(root).Should().Be("shadow");
        MarkupSerializer.ToXml(host).Should().Be("<div xmlns=\"http://www.w3.org/1999/xhtml\">light</div>");
        var clone = (Element) host.CloneNode(true);
        MarkupSerializer.ToHtml(clone, new(serializableShadowRoots: true)).Should().Be("<div>" + selected + "light</div>");
        Assert.Throws<DomException>(() => host.AttachShadow(new(ShadowRootMode.Closed)));
        Assert.Throws<DomException>(() => root.CloneNode(true));
        Assert.Throws<DomException>(() => document.AdoptNode(root));
        Assert.Throws<DomException>(() => document.ImportNode(root, true));
        var shadowChild = root.FirstChild;
        host.AppendChild(root);
        root.ChildCount.Should().Be(0);
        root.Host.Should().BeSameAs(host);
        host.LastChild.Should().BeSameAs(shadowChild);
    }

    [Test]
    public void ShadowFactoryValidatesBeforeAttachmentAndHonorsCancellation()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("span");
        Assert.Throws<ArgumentOutOfRangeException>(() => host.AttachShadow(new((ShadowRootMode) 99)));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.AttachShadow(new(ShadowRootMode.Open, SlotAssignment: (SlotAssignmentMode) 99)));
        Assert.Throws<DomException>(() => document.CreateElement("br").AttachShadow(new(ShadowRootMode.Open)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => host.AttachShadow(new(ShadowRootMode.Open), cancellation.Token));
        host.OpenShadowRoot.Should().BeNull();
        var root = host.AttachShadow(new(ShadowRootMode.Open));
        host.OpenShadowRoot.Should().BeSameAs(root);
    }
}
