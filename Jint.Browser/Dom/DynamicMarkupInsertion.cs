using System.Runtime.CompilerServices;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Construction;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Jint.Browser.Dom;

/// <summary>
/// HTML's <a href="https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html">dynamic markup
/// insertion</a> — the
/// <a href="https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-open-steps">open
/// steps</a>, the
/// <a href="https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps">write
/// steps</a> and the
/// <a href="https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-close">close
/// steps</a> — for an HTML document <em>no</em> parser is reading and <em>no</em> browsing context is
/// showing: <c>DOMParser</c>'s, <c>DOMImplementation.createHTMLDocument</c>'s, and any other document the
/// page is not displaying.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the steps are here rather than AngleSharp's.</b> <c>Document.Open</c> cannot serve any of them,
/// and the three reasons are measured rather than assumed — they are the register's
/// <c>document.write</c> row. It reads <c>_context?.Parent!.Active</c>, so every context
/// <c>BrowsingContext.New</c> built raises <see cref="NullReferenceException"/> out of a member a script
/// called; past that it blocks on <c>PromptToUnloadAsync().Result</c> and <c>Unload(recycle: true).Wait()</c>
/// on whatever thread the script ran on; and even where it completes it empties the document, puts the ready
/// state back to <c>loading</c> and creates <b>no parser</b>, so the re-entrant <c>Write</c> inserts into a
/// text source nothing will ever read and the markup is silently lost. An upstream fix to the first alone
/// would turn a loud refusal into a quiet one.
/// </para>
/// <para>
/// <b>The document object survives.</b> Everything here mutates the target in place, because the reference
/// <c>DOMParser</c> handed the script, the wrapper <see cref="DomRealm"/> keeps for it and every node
/// reference the script already holds all name that one object. Building a replacement document and
/// swapping it in would be a different document wearing the same name.
/// </para>
/// <para>
/// <b>The insertion point is modelled as the written source, not as a parser.</b> HTML inserts what is
/// written into the parser's input stream at the insertion point, which is what makes
/// <c>write('&lt;p&gt;a'); write('b&lt;/p&gt;')</c> one paragraph rather than two documents. There is no
/// resumable tokenizer to hand a suffix to — AngleSharp's parse is a method, not a coroutine — so a
/// <b>write session</b> holds everything written since the last open, and each write reparses that whole
/// source and replaces the document's children with the result. The final tree is the one a browser's
/// parser arrives at; what differs is that a node an <em>earlier</em> write in the same session produced is
/// replaced rather than kept, which the divergence register states.
/// </para>
/// <para>
/// <b>Nothing written can run.</b> The reparse takes <see cref="Views.ViewInstaller.ParserConfiguration"/>
/// and a browsing context of its own with no scripting service and <c>IsScripting</c> false, exactly as
/// <see cref="Views.JsDomParser"/> does, and the document the nodes are adopted into has no scripting
/// service either. A <c>&lt;script&gt;</c> written into one is an element with text and nothing more.
/// </para>
/// <para>
/// <b>And nothing written is a custom element.</b>
/// <a href="https://html.spec.whatwg.org/multipage/custom-elements.html#look-up-a-custom-element-definition">
/// Look up a custom element definition</a> step 1 is "if document's browsing context is null, then return
/// null", so a document with none has no definitions to match and no upgrade to run — which is why no
/// <c>CustomElementRegistry.SubtreeCreated</c> walk follows the insertion, where
/// <c>innerHTML</c> and <c>insertAdjacentHTML</c> both owe one.
/// </para>
/// </remarks>
internal static class DynamicMarkupInsertion
{
    /// <summary>
    /// The write session of each document that has one, keyed on the document so that it dies with it. It is
    /// the standard's "script-created parser" and its input stream: a document with no entry here has an
    /// <em>undefined</em> insertion point, which is what makes the next write imply an open.
    /// </summary>
    private static readonly ConditionalWeakTable<IDocument, WriteSession> s_sessions = new();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-open-steps
    /// </summary>
    /// <remarks>
    /// Every step this leaves out is one guarded on a browsing context the target does not have: the
    /// same-origin check (step 3), the active-parser and unload counters (steps 4 to 6), the navigation
    /// abort and the unload itself (steps 7 to 11), and the session-history and URL work (step 13). What is
    /// left is step 12's "replace all with null within document" and step 14's fresh parser — and the ready
    /// state step 14 sets, which <c>Document.ReadyState</c>'s <c>protected</c> setter puts out of reach for
    /// the same reason the register's <c>document.readyState</c> row already gives.
    /// </remarks>
    internal static void Open(IHtmlDocument document) => StartSession(document);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps
    /// </summary>
    /// <remarks>
    /// Step 1's XML refusal and step 2's counter are the caller's; what is here is step 4 — "if the
    /// insertion point is undefined … run the document open steps" — followed by step 5's insertion into
    /// the input stream and step 6's "have the HTML parser process input".
    /// </remarks>
    internal static void Write(DomRealm realm, IHtmlDocument document, string text)
    {
        if (!s_sessions.TryGetValue(document, out var session))
        {
            session = StartSession(document);
        }

        session.Source.Append(text);
        Reparse(realm, document, session.Source);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-close
    /// </summary>
    /// <remarks>
    /// Step 3 is "if there is no script-created parser associated with the document, then return", so a
    /// close with no session open is the standard's own no-op. Closing the session is step 4's end of the
    /// parse: the next write has an undefined insertion point again and therefore opens the document afresh.
    /// What it does not do is fire <c>DOMContentLoaded</c> and <c>load</c>, because the readiness those
    /// events announce cannot be moved — see <see cref="Open"/>.
    /// </remarks>
    internal static void Close(IDocument document) => s_sessions.Remove(document);

    private static WriteSession StartSession(IHtmlDocument document)
    {
        // "Replace all with null within document": the whole tree goes, the doctype with it, and whatever
        // the script still holds is left off the document but still owned by it — which is what a browser's
        // document.open() leaves behind too, since only the document's own children are removed.
        ReplaceAllWithNull(document);

        var session = new WriteSession();
        s_sessions.Remove(document);
        s_sessions.Add(document, session);
        return session;
    }

    /// <summary>
    /// Parses everything written in this session and makes it the document's content, in place.
    /// </summary>
    private static void Reparse(DomRealm realm, IHtmlDocument document, StringBuilder source)
    {
        var parsed = new HtmlParser(new HtmlParserOptions { IsScripting = false }, NewContext())
            .ParseDocument(source.ToString());

        ReplaceAllWithNull(document);

        // The doctype decides compatMode, and the parse is what read it. Document.QuirksMode is internal, so
        // the construction interface the public generic parse API is built on is how it is carried across.
        if (document is IConstructableDocument target && parsed is IConstructableDocument built)
        {
            target.QuirksMode = built.QuirksMode;
        }

        foreach (var child in parsed.ChildNodes.ToArray())
        {
            parsed.RemoveChild(child);

            // AppendChild adopts, so the node's owner becomes the target before anything reads it; the
            // creation realm is recorded afterwards, exactly as an insertAdjacentHTML insertion is.
            document.AppendChild(child);
            realm.RecordSubtree(child);
        }
    }

    private static void ReplaceAllWithNull(IDocument document)
    {
        while (document.LastChild is { } child)
        {
            document.RemoveChild(child);
        }
    }

    /// <summary>
    /// A browsing context with the CSS services and nothing else — no requester, so nothing written reaches
    /// the network, and no scripting service, so nothing written runs.
    /// </summary>
    private static IBrowsingContext NewContext() => BrowsingContext.New(Views.ViewInstaller.ParserConfiguration);

    /// <summary>Everything written into one document since the open that started it.</summary>
    private sealed class WriteSession
    {
        internal StringBuilder Source { get; } = new();
    }
}
