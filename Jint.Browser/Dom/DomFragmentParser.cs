using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Browser.Dom;

/// <summary>HTML §13.4 and XML fragment parsing on the actual native context and target.</summary>
internal static class DomFragmentParser
{
    internal static DocumentFragment Parse(DomRealm realm, string markup, Element context, Node target,
        bool contextual = false)
    {
        realm.Engine.Constraints.Check();
        if (context.OwnerDocument!.Kind == DocumentKind.Xml)
        {
            var fragment = MarkupParser.ParseXmlFragment(markup, context, cancellationToken: realm.CancellationToken);
            realm.Engine.Constraints.Check();
            return fragment;
        }
        var mode = !realm.ScriptingEnabled ? HtmlParserScriptingMode.Disabled
            : contextual ? HtmlParserScriptingMode.Fragment : HtmlParserScriptingMode.Inert;
        var session = HtmlParserSession.CreateFragment(context,
            new HtmlParseOptions { ScriptingEnabled = realm.ScriptingEnabled }, target, mode);
        session.AppendInput(markup, isFinal: true);
        while (true)
        {
            realm.Engine.Constraints.Check();
            var step = session.Drive(4096, realm.CancellationToken);
            if (step.Kind == HtmlParseStepKind.Yielded) continue;
            if (step.Kind != HtmlParseStepKind.Complete)
                throw new InvalidOperationException("The native fragment parser returned " + step.Kind + ".");
            realm.Engine.Constraints.Check();
            return session.Fragment!;
        }
    }

    // outerHTML/insertAdjacentHTML use a body context for a fragment parent or an HTML root.
    internal static Element ContextFor(Node parent)
        => parent is Element element && !(element.NamespaceUri == Namespaces.Html && element.LocalName == "html")
            ? element : parent.OwnerDocument!.CreateElementNS(Namespaces.Html, "body");
}
