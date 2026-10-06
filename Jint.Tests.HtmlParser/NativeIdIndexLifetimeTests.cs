#nullable enable
using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser;

[NonParallelizable]
public sealed class NativeIdIndexLifetimeTests
{
    [Test]
    public void WarmDocumentDoesNotRetainRemovedIndexedDescendants()
    {
        var document = Document.CreateHtml();
        var weak = WarmAndRemove(document);
        for (var i = 0; i < 5 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        weak.IsAlive.Should().BeFalse();
        GC.KeepAlive(document);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference WarmAndRemove(Document document)
    {
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var child = document.CreateElement("p");
        child.SetAttribute("id", "removed");
        root.AppendChild(child);
        var work = new SelectorMatchWork(document, default);
        NativeIdIndex.Find(document, "removed", ref work).Should().BeSameAs(child);
        root.RemoveChild(child);
        return new WeakReference(child);
    }
}
