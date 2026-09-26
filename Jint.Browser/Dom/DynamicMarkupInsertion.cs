using System.Runtime.CompilerServices;
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Browser.Dom;

/// <summary>HTML dynamic markup insertion for a document with no browsing context.</summary>
/// <remarks>
/// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-open-steps
/// The document and nodes parsed by earlier writes keep their actual native identities.
/// The session retains tokenizer/tree-builder state, never a concatenated source or copied tree.
/// </remarks>
internal static class DynamicMarkupInsertion
{
    private static readonly ConditionalWeakTable<Document, HtmlParserSession> Sessions = new();

    internal static void Open(DomRealm realm, Document document)
    {
        if (Sessions.TryGetValue(document, out var previous))
        {
            while (previous.Abort().Kind == HtmlParseStepKind.Yielded) realm.Engine.Constraints.Check();
            Sessions.Remove(document);
        }
        while (document.LastChild is { } child)
        {
            realm.Engine.Constraints.Check();
            document.RemoveChild(child);
        }
        DomDocumentState.Of(document).ReadyState = "loading";
        Sessions.Add(document, new HtmlParserSession(document, new HtmlParseOptions { ScriptingEnabled = false }));
        realm.AssociateDocument(document);
    }

    // https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps
    internal static void Write(DomRealm realm, Document document, string text)
    {
        if (!Sessions.TryGetValue(document, out var session))
        {
            Open(realm, document);
            session = Sessions.GetValue(document, static _ => throw new InvalidOperationException("The write session was not created."));
        }
        session.AppendInput(text);
        Drive(realm, session, final: false);
    }

    // https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-close
    internal static void Close(DomRealm realm, Document document)
    {
        if (!Sessions.TryGetValue(document, out var session)) return;
        try
        {
            session.AppendInput("", isFinal: true);
            Drive(realm, session, final: true);
            DomDocumentState.Of(document).ReadyState = "complete";
        }
        finally
        {
            Sessions.Remove(document);
        }
    }

    private static void Drive(DomRealm realm, HtmlParserSession session, bool final)
    {
        while (true)
        {
            realm.Engine.Constraints.Check();
            var step = session.Drive(4096, realm.CancellationToken);
            if (step.Kind == HtmlParseStepKind.Yielded) continue;
            if (!final && step.Kind == HtmlParseStepKind.NeedInput || final && step.Kind == HtmlParseStepKind.Complete) return;
            throw new InvalidOperationException("The native secondary-document parser returned " + step.Kind + ".");
        }
    }
}
