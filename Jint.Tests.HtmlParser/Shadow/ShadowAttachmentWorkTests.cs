using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Shadow;

public sealed class ShadowAttachmentWorkTests
{
    [Test]
    public void CancellationDuringHostNameValidationDoesNotAttachRoot()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("x-" + new string('a', 4096));
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => ShadowTree.Attach(host,
            new(ShadowRootMode.Open), default,
            units => { if (units == 256) cancellation.Cancel(); }, cancellation.Token));
        host.AttachedShadowRoot.Should().BeNull();
    }

    [Test]
    public void CancellationBetweenDeclarativeRemovalsPreservesCoherentTreeAndRetry()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new(ShadowRootMode.Open), default);
        ShadowTree.SetDeclarativeTemplateContent(document.CreateElement("template"), root, false);
        var first = document.CreateTextNode("first");
        var second = document.CreateTextNode("second");
        var third = document.CreateTextNode("third");
        root.AppendChild(first);
        root.AppendChild(second);
        root.AppendChild(third);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => ShadowTree.Attach(host,
            new(ShadowRootMode.Open), default,
            _ => { if (root.ChildCount == 2) cancellation.Cancel(); }, cancellation.Token));
        root.Declarative.Should().BeTrue();
        first.ParentNode.Should().BeNull();
        root.FirstChild.Should().BeSameAs(second);
        second.PreviousSibling.Should().BeNull();
        second.NextSibling.Should().BeSameAs(third);
        ShadowTree.Attach(host, new(ShadowRootMode.Open), default).Should().BeSameAs(root);
        root.ChildCount.Should().Be(0);
        root.Declarative.Should().BeFalse();
    }

    [Test]
    public void CancellationAfterAttachmentKeepsPublishedRoot()
    {
        var host = Document.CreateHtml().CreateElement("div");
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => ShadowTree.Attach(host,
            new(ShadowRootMode.Open), default,
            _ => { if (host.AttachedShadowRoot is not null) cancellation.Cancel(); }, cancellation.Token));
        host.AttachedShadowRoot.Should().NotBeNull();
        Assert.That(Assert.Throws<DomException>(() => ShadowTree.Attach(host,
            new(ShadowRootMode.Open), default))!.Name, Is.EqualTo("NotSupportedError"));
    }
}
