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
        return Drive(realm, session, markup);
    }

    /// <summary>
    /// The HTML fragment parsing algorithm as <c>setHTML</c> and <c>setHTMLUnsafe</c> invoke it: always the
    /// HTML parser, whatever the context's document is, with declarative shadow roots allowed.
    /// </summary>
    /// <remarks>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#set-and-filter-html step 5.
    /// <paramref name="runScripts"/> is <c>SetHTMLUnsafeOptions.runScripts</c>: the <i>Fragment</i> scripting
    /// mode, whose scripts run once inserted, instead of <i>Inert</i>. Every host a declarative shadow root
    /// was attached to is reported to <paramref name="shadowHost"/>, which is how the caller finds the roots
    /// whose resources the page has to watch.
    /// </remarks>
    internal static DocumentFragment ParseHtmlWithShadowRoots(DomRealm realm, string markup, Element context, Node target,
        bool runScripts, Action<Element> shadowHost)
    {
        realm.Engine.Constraints.Check();
        var mode = !realm.ScriptingEnabled ? HtmlParserScriptingMode.Disabled
            : runScripts ? HtmlParserScriptingMode.Fragment : HtmlParserScriptingMode.Inert;
        IHtmlShadowHostContextProvider provider = Runtime.PageRuntime.Find(realm.Engine) is { } runtime
            ? new BrowserShadowHostContextProvider(runtime, shadowHost)
            : new CandidateOnlyShadowHostContextProvider(shadowHost);
        var session = HtmlParserSession.CreateFragment(context,
            new HtmlParseOptions { ScriptingEnabled = realm.ScriptingEnabled }, target, mode,
            allowDeclarativeShadowRoots: true, shadowHostContextProvider: provider);
        return Drive(realm, session, markup);
    }

    private static DocumentFragment Drive(DomRealm realm, HtmlParserSession session, string markup)
    {
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

    /// <summary>A realm with no page: the standalone attachment context, still reporting each host.</summary>
    private sealed class CandidateOnlyShadowHostContextProvider(Action<Element> shadowHost) : IHtmlShadowHostContextProvider
    {
        public ShadowAttachmentContext GetShadowAttachmentContext(Element host)
        {
            shadowHost(host);
            return new ShadowAttachmentContext(host.OwnerDocument!.CustomElementRegistry, false, false);
        }
    }

    // outerHTML/insertAdjacentHTML use a body context for a fragment parent or an HTML root.
    internal static Element ContextFor(Node parent)
        => parent is Element element && !(element.NamespaceUri == Namespaces.Html && element.LocalName == "html")
            ? element : parent.OwnerDocument!.CreateElementNS(Namespaces.Html, "body");
}
