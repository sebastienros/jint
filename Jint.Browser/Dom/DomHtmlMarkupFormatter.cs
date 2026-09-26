using Jint.HtmlParser;
using Jint.HtmlParser.Serialization;

namespace Jint.Browser.Dom;

/// <summary>DOM Parsing serialization over the actual native tree, with bounded host work.</summary>
internal static class DomHtmlMarkupFormatter
{
    private static readonly HtmlSerializationOptions Scripting = new(scriptingEnabled: true);
    private static readonly HtmlSerializationOptions Inert = new(scriptingEnabled: false);

    internal static string InnerHtml(DomRealm realm, Node node)
    {
        var document = node as Document ?? node.OwnerDocument!;
        return document.Kind == DocumentKind.Html
            ? HtmlMarkupSerializer.SerializeChildren(node, Options(realm, document), checkpoint: _ => realm.Engine.Constraints.Check(), cancellationToken: realm.CancellationToken)
            : XmlMarkupSerializer.SerializeChildren(node, requireWellFormed: true, checkpoint: _ => realm.Engine.Constraints.Check(), cancellationToken: realm.CancellationToken);
    }

    internal static string OuterHtml(DomRealm realm, Node node)
    {
        var document = node as Document ?? node.OwnerDocument!;
        return document.Kind == DocumentKind.Html
            ? HtmlMarkupSerializer.Serialize(node, Options(realm, document), checkpoint: _ => realm.Engine.Constraints.Check(), cancellationToken: realm.CancellationToken)
            : XmlMarkupSerializer.Serialize(node, requireWellFormed: true, checkpoint: _ => realm.Engine.Constraints.Check(), cancellationToken: realm.CancellationToken);
    }

    private static HtmlSerializationOptions Options(DomRealm realm, Document document)
        => realm.ScriptingEnabled && DomBrowsingContext.Of(document) is not null ? Scripting : Inert;
}
