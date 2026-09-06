using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser;

/// <summary>
/// One browsing context of a page: its URL, its name, and the frames nested inside it.
/// </summary>
/// <remarks>
/// <para>
/// A page has exactly one scripted frame, its main frame. A child frame has a <b>document</b> — its
/// <c>src</c> is fetched over the page's own network position and parsed, so <c>contentDocument</c> answers
/// it and <c>load</c> arrives at the element — and a <b>window</b>, on the page's own realm
/// (<c>Runtime/FrameWindows</c>), so <c>contentWindow</c>, <c>defaultView</c> and <c>frames[i]</c> answer it.
/// What it has no <b>realm</b> of its own: nothing in it runs, and every constructor it reaches is the
/// page's. One realm per frame is what is left, and the survey on
/// <a href="https://github.com/sebastienros/jint/issues/3771">#3771</a> says what it would cost.
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

    /// <summary>The frames nested directly inside this one; empty for a child frame.</summary>
    /// <remarks>
    /// Every frame element in a document is a direct child browsing context of it, however deeply the element
    /// is nested in the markup, so this really is one level of the tree. A child frame's own frames are in its
    /// own document, which <i>is</i> loaded now — this list does not descend into one, because a
    /// <see cref="Frame"/> is a snapshot of what the page's document declares rather than a walk of every
    /// document the load produced.
    /// </remarks>
    public IReadOnlyList<Frame> Frames { get; }

    /// <summary>Whether this frame runs script. Only a page's main frame does.</summary>
    /// <remarks>
    /// A child frame has a document and no realm, so it is readable and inert: its markup is parsed, its
    /// style sheets load and <c>contentDocument</c> answers it, while every <c>&lt;script&gt;</c> in it is
    /// skipped rather than run somewhere it does not belong.
    /// </remarks>
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
