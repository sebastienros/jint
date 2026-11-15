using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

/// <summary>
/// <c>element.children[i]</c> continues from the last child read while the tree is unchanged, so an indexed
/// loop over a list is linear in the list rather than quadratic (#4619).
/// </summary>
/// <remarks>
/// The work is what the read charges to its checkpoint, which is the same unit every native read is bounded
/// in — not a wall-clock figure. Text and comment nodes sit between the elements, because skipping them is
/// what the collection does that <c>childNodes</c> does not.
/// </remarks>
public sealed class ChildElementCursorTests
{
    [TestCase("forward")]
    [TestCase("reverse")]
    [TestCase("stride")]
    public void AnIndexedLoopChargesLinearSiblingWork(string mode)
    {
        var small = Walk(512, mode);
        var large = Walk(1024, mode);

        large.Should().BeLessThanOrEqualTo(3 * small + 8, "doubling the list may at most double the work");
        large.Should().BeLessThanOrEqualTo(6 * 1024, "each element is a couple of sibling steps away from the last one read");
    }

    [Test]
    public void ReadsFollowEveryMutationRatherThanTheCursor()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("ul");
        var items = new List<Element>();
        for (var i = 0; i < 8; i++)
        {
            root.AppendChild(document.CreateTextNode(" "));
            var item = document.CreateElement("li");
            items.Add(item);
            root.AppendChild(item);
        }

        var children = DomChildHtmlCollection.Of(root);
        Read(children, 5).Should().BeSameAs(items[5]);

        // Removing the element the cursor stands on, and one before it.
        root.RemoveChild(items[5]);
        Read(children, 5).Should().BeSameAs(items[6]);
        root.RemoveChild(items[1]);
        Read(children, 4).Should().BeSameAs(items[6]);

        // Inserting ahead of it.
        var inserted = document.CreateElement("li");
        root.InsertBefore(inserted, root.FirstChild);
        Read(children, 0).Should().BeSameAs(inserted);
        Read(children, 5).Should().BeSameAs(items[6]);

        // Backwards from the cursor, and past the end.
        Read(children, 1).Should().BeSameAs(items[0]);
        Read(children, 6).Should().BeSameAs(items[7]);
        Read(children, 7).Should().BeNull();
    }

    [Test]
    public void ACollectionReadOnceDoesNotKeepARemovedChildAlive()
    {
        var (children, weak) = ReadAndRemove();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        weak.TryGetTarget(out _).Should().BeFalse("the cursor holds the element it stands on weakly");
        GC.KeepAlive(children);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (DomChildHtmlCollection Children, WeakReference<Element> Removed) ReadAndRemove()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("ul");
        for (var i = 0; i < 4; i++) root.AppendChild(document.CreateElement("li"));

        var children = DomChildHtmlCollection.Of(root);
        var removed = Read(children, 2)!;
        root.RemoveChild(removed);
        return (children, new WeakReference<Element>(removed));
    }

    private static Element? Read(DomChildHtmlCollection children, uint index) => children.ReadItem(index, null, default);

    private static long Walk(int count, string mode)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("ul");
        var expected = new List<Element>();
        for (var i = 0; i < count; i++)
        {
            root.AppendChild(document.CreateTextNode("\n"));
            var item = document.CreateElement("li");
            expected.Add(item);
            root.AppendChild(item);
        }

        var children = DomChildHtmlCollection.Of(root);
        long charged = 0;
        for (var i = 0; i < count; i++)
        {
            var index = mode switch
            {
                "reverse" => count - i - 1,
                // Every other element, then back to fill in the gaps: two passes, each one step at a time.
                "stride" => i < count / 2 ? i * 2 : (i - (count / 2)) * 2 + 1,
                _ => i,
            };

            children.ReadItem((uint) index, units => charged += units, default).Should().BeSameAs(expected[index]);
        }

        return charged;
    }
}
