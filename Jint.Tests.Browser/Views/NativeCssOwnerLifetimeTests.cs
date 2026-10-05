#nullable enable
using System.Runtime.CompilerServices;
using Jint.Browser.Styling;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Views;

[NonParallelizable]
public sealed class NativeCssOwnerLifetimeTests
{
    [Test]
    public void RetainedDocumentAndWarmOrderCachesDoNotRetainRemovedShadowTrees()
    {
        var (document, host, root, owner) = CreateAndRemoveShadowSheet();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        host.TryGetTarget(out _).Should().BeFalse();
        root.TryGetTarget(out _).Should().BeFalse();
        owner.TryGetTarget(out _).Should().BeFalse();
        NativeCssStyleSheets.Get(document, new(default), includeShadow: true).Should().BeEmpty();
        GC.KeepAlive(document);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Document, WeakReference<Element>, WeakReference<ShadowRoot>, WeakReference<Element>) CreateAndRemoveShadowSheet()
    {
        var document = Document.CreateHtml();
        var html = document.CreateElement("html");
        document.AppendChild(html);
        var host = document.CreateElement("div");
        html.AppendChild(host);
        var root = host.AttachShadow(new(ShadowRootMode.Open));
        var owner = document.CreateElement("style");
        root.AppendChild(owner);
        NativeCssStyleSheets.Install(document, owner, "p{color:red}", "https://example.test/", "https://example.test/", new(default));
        NativeCssStyleSheets.Get(document, new(default), includeShadow: true).Count.Should().Be(1);
        NativeCssStyleSheets.Get(root, new(default)).Count.Should().Be(1);
        html.RemoveChild(host);
        return (document, new(host), new(root), new(owner));
    }
}
