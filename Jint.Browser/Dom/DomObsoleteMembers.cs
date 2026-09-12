using System.Runtime.CompilerServices;
using AngleSharp.Dom;
using Jint.Browser.Dom.Collections;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// The members <a href="https://html.spec.whatwg.org/multipage/obsolete.html">HTML §16</a> keeps for
/// compatibility and defines to answer nothing.
/// </summary>
/// <remarks>
/// A removed feature and a feature that must still answer are different facts, and <c>html/dom/historical.html</c>
/// is the file that tells them apart. <c>HTMLAppletElement</c> is gone outright, so nothing declares it;
/// <c>document.applets</c> is <b>not</b> gone — HTML §16.3 still puts it on <c>Document</c> and requires it to
/// answer an <c>HTMLCollection</c> "whose filter matches nothing", which is a member the binding has to have
/// rather than one it has to lose.
/// </remarks>
internal static class DomObsoleteMembers
{
    /// <summary>
    /// One empty collection per document, because the IDL attribute is <c>[SameObject]</c>: two reads of
    /// <c>document.applets</c> must be the same object, and the wrapper cache keys on the AngleSharp object,
    /// so a fresh collection per read would be a fresh wrapper per read.
    /// </summary>
    private static readonly ConditionalWeakTable<IDocument, DomLiveHtmlCollection> _applets = new();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/obsolete.html#dom-document-applets — an <c>HTMLCollection</c>
    /// rooted at the document whose filter matches nothing, which is to say permanently empty.
    /// </summary>
    /// <remarks>
    /// It is a <see cref="DomLiveHtmlCollection"/> over an empty sequence rather than a snapshot type, so the
    /// ordinary <c>HTMLCollection</c> wrapper — its indexed access, its named access, its <c>length</c> — is
    /// the one every other collection uses, and <c>document.applets</c> is a real <c>HTMLCollection</c>
    /// instead of an object that merely looks like one.
    /// </remarks>
    internal static JsValue Applets(DomRealm realm, IDocument document)
        => realm.WrapCollection<IElement>(
            _applets.GetValue(document, static _ => new DomLiveHtmlCollection(static () => [])));
}
