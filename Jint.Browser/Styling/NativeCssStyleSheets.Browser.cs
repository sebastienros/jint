using Jint.Browser.Dom;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

internal static partial class NativeCssStyleSheets
{
    internal static void Install(DomRealm realm, Element owner, string text, string sourceUrl)
    {
        var document = owner.OwnerDocument ?? throw new ArgumentException("A stylesheet owner needs a document.", nameof(owner));
        var work = new CssValueWork(realm.CancellationToken, realm.Engine.Constraints.Check);
        var baseUrl = DomDocumentState.BaseUri(document, realm.Engine.Constraints.Check, realm.CancellationToken);
        Install(document, owner, text, sourceUrl, baseUrl, work);
    }
}
