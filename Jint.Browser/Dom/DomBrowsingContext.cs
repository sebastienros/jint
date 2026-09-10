using AngleSharp;
using AngleSharp.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/document-sequences.html#doc-bc">A document's browsing
/// context</a>: the browsing context whose active document it is, and <see langword="null"/> when nothing is
/// showing it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every document this package can build has an AngleSharp browsing context, and only one of them has
/// HTML's.</b> <c>DOMParser</c>, <c>new Document()</c>, <c>DOMImplementation.createDocument</c> and
/// <c>createHTMLDocument</c> each parse into a context of their own — <see cref="DomContentType"/> says why
/// they have to — so <c>document.Context is not null</c> answers <see langword="true"/> for all four. What
/// separates them from the page's document is the context's own <see cref="IBrowsingContext.Active"/>: it is
/// the document being displayed, and nothing else.
/// </para>
/// <para>
/// <b>It is here rather than on <see cref="DomHostHooks"/> because two unrelated algorithms name it.</b>
/// HTML's <c>document.location</c> answers <see langword="null"/> for a document that is not fully active,
/// and HTML §4.13.4's look-up-a-custom-element-definition returns null at step 1 for a document whose
/// browsing context is null — so the same sentence gates a member of <c>Document</c> and every creation path
/// that consults the registry. Two spellings of it is how one of them would come to answer about a document
/// nobody can see.
/// </para>
/// <para>
/// <b>It asks the document rather than the page</b>, which <c>PageRuntime.FindBrowsingContext</c> asks for
/// the page's own browsing-context tree. The two agree on all four secondary spellings above — each gets a
/// context that is not the page's — and this one needs no engine, so a binding installed without a page
/// runtime still tells a displayed document from a manufactured one.
/// </para>
/// </remarks>
internal static class DomBrowsingContext
{
    /// <summary>
    /// The browsing context <paramref name="document"/> is the active document of, or
    /// <see langword="null"/> when it is the active document of none.
    /// </summary>
    internal static IBrowsingContext? Of(IDocument? document)
        => document?.Context is { } context && ReferenceEquals(context.Active, document) ? context : null;
}
