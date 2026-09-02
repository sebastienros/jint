using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser;

/// <summary>
/// One browsing context of a page: its URL, its name, and the frames nested inside it.
/// </summary>
/// <remarks>
/// <para>
/// A page has exactly one scripted frame, its main frame. Child frames are real — the parse found them, they
/// are in the frame tree, and their attributes are readable — but nothing loads or scripts them: an
/// <c>&lt;iframe&gt;</c>'s <c>contentWindow</c> is absent and its <c>src</c> is not fetched. One realm per
/// frame on the same engine is what makes <c>parent.document</c> answerable, and that is a later change.
/// </para>
/// <para>
/// A frame is a snapshot taken when the document finished loading, not a live view: a frame added by script
/// afterwards is not in this tree.
/// </para>
/// </remarks>
public sealed class Frame
{
    private Frame(Page page, Frame? parent, string url, string name, IReadOnlyList<Frame> frames)
    {
        Page = page;
        Parent = parent;
        Url = url;
        Name = name;
        Frames = frames;
    }

    /// <summary>The page this frame belongs to.</summary>
    public Page Page { get; }

    /// <summary>The frame this one is nested in, or <c>null</c> for a page's main frame.</summary>
    public Frame? Parent { get; }

    /// <summary>The frame's URL: the document's for a main frame, the element's <c>src</c> for a child.</summary>
    public string Url { get; }

    /// <summary>The frame's <c>name</c> attribute, or the empty string.</summary>
    public string Name { get; }

    /// <summary>The frames nested directly inside this one; empty for a child frame, which is not parsed.</summary>
    /// <remarks>
    /// Every frame element in a document is a direct child browsing context of it, however deeply the element
    /// is nested in the markup — a frame's own frames are in its own document, which is not loaded here — so
    /// this really is one level of the tree.
    /// </remarks>
    public IReadOnlyList<Frame> Frames { get; }

    /// <summary>Whether this frame runs script. Only a page's main frame does.</summary>
    public bool IsScripted => Parent is null;

    internal static Frame Detached(Page page) => new(page, parent: null, "about:blank", "", []);

    internal static Frame Build(Page page, IDocument document, string url)
    {
        // The list is handed to the main frame before it is filled, because a child needs the parent it is
        // being added to. Nothing outside this method sees either until both are complete.
        var children = new List<Frame>();
        var main = new Frame(page, parent: null, url, "", children);

        foreach (var element in document.QuerySelectorAll("iframe, frame"))
        {
            var source = element.GetAttribute("src");
            var name = element is IHtmlInlineFrameElement inline ? inline.Name : element.GetAttribute("name");

            children.Add(new Frame(page, main, source ?? "about:blank", name ?? "", []));
        }

        return main;
    }
}
