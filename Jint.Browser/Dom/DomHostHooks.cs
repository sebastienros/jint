using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>
/// The seam for the DOM members whose behaviour depends on whether a page runtime is behind the binding:
/// the ones that parse markup into the tree and so have to reach the script scheduler, the three whose
/// answer is an object the host made rather than one AngleSharp did, and the handful whose value the host
/// simply <em>has</em> and AngleSharp does not.
/// </summary>
/// <remarks>
/// <para>
/// The generated members call these instead of AngleSharp directly. There is one instance and no subclass —
/// <see cref="DomRealm.Hooks"/> is a seam nothing currently replaces — so a member whose answer differs with
/// a page asks for one here, through <c>PageRuntime.Find</c>, and falls through to AngleSharp when there is
/// none. That is what a binding-only engine gets, and it is the behaviour these members have always had.
/// </para>
/// <para>
/// Two of them the parser driver settled rather than replaced. A <c>&lt;script&gt;</c> inserted through
/// <c>innerHTML</c> needs nothing here: AngleSharp's fragment parser marks it "already started", so adopting
/// it into the tree never runs it, which is HTML's own rule. And <c>document.write</c> <i>during</i> a parse
/// is AngleSharp's own call and is correct — its writable text source inserts at the parser's index while the
/// baton has the parser parked. Only the after-the-parse half needed a decision, and it is below.
/// </para>
/// </remarks>
internal class DomHostHooks
{
    /// <summary>The behaviour a binding with no runtime behind it has: AngleSharp, called directly.</summary>
    internal static readonly DomHostHooks Default = new();

    /// <summary>
    /// A wrapper has just been created and is the one this engine will keep for its object; the runtime may
    /// add members the generator could not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called once per object per engine, from the wrapper cache, and never again — so a member added here is
    /// an own property of a wrapper rather than of a prototype, which is what keeps every generated prototype
    /// shaped. It is the seam for a member whose <i>whole</i> body belongs to the runtime rather than one
    /// whose body is replaced: <c>form.submit()</c> is a navigation and <c>form.requestSubmit()</c> does not
    /// exist in AngleSharp at all, so neither is generated and neither can be.
    /// </para>
    /// <para>
    /// The cost is one virtual call per wrapper creation, not per member access.
    /// </para>
    /// </remarks>
    /// <param name="realm">The DOM state of the engine the wrapper belongs to.</param>
    /// <param name="target">The AngleSharp object being wrapped.</param>
    /// <param name="wrapper">The wrapper, before anything has read a property off it.</param>
    internal virtual void WrapperCreated(DomRealm realm, object target, ObjectInstance wrapper)
    {
        // The handler content attributes an element's markup declares, registered the moment its wrapper wins
        // the cache — which is before anything can dispatch through it or add a listener of its own, and is
        // what puts a markup handler ahead of a script's in the listener list. It is here rather than in
        // DomNodeObject's constructor because this hook fires exactly once for the wrapper that won, and a
        // re-entrant member that built a second one would otherwise have scanned the loser too.
        // https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-content-attributes step 3:
        // "if scripting is disabled for element's node document, return" — which is what
        // Emulation.setScriptExecutionDisabled turns off.
        if (wrapper is DomNodeObject node && realm.ScriptingEnabled)
        {
            Events.EventHandlerContentAttributes.InstallFromMarkup(node);
        }
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-innerhtml</summary>
    /// <remarks>
    /// The <c>[CEReactions]</c> half is the second line, and it is only the <i>detached</i> case: a
    /// connected element's assignment produced a mutation record, which upgraded and connected what it
    /// parsed before AngleSharp's own call returned. A detached one produces no record, and HTML
    /// upgrades there too. See <c>CustomElements/CustomElementRegistry.Tree.cs</c>.
    /// </remarks>
    internal virtual void SetInnerHtml(DomRealm realm, IElement element, string markup)
    {
        element.InnerHtml = markup;
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, element);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-outerhtml</summary>
    /// <remarks>
    /// The markup replaces the element, so what is walked afterwards is the parent it was in — the
    /// element itself is no longer in the tree the new content went into.
    /// </remarks>
    internal virtual void SetOuterHtml(DomRealm realm, IElement element, string markup)
    {
        var parent = element.Parent;
        element.OuterHtml = markup;
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, parent ?? element);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-setattribute, hooked so that a handler content attribute a
    /// script writes activates its handler <i>then</i> — which is what fixes the handler's position in the
    /// element's listener list. See <c>Events.EventHandlerContentAttributes.AttributeChanged</c>.
    /// </summary>
    internal virtual void SetAttribute(DomRealm realm, IElement element, JsValue[] arguments)
    {
        var name = DomConvert.RequiredText(arguments, 0, "Element.setAttribute");
        element.SetAttribute(name, DomConvert.RequiredText(arguments, 1, "Element.setAttribute"));
        Events.EventHandlerContentAttributes.AttributeChanged(realm, element, name);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-element-removeattribute, the other half: removing the attribute
    /// deactivates the handler, and the listener goes with it.
    /// </summary>
    internal virtual void RemoveAttribute(DomRealm realm, IElement element, JsValue[] arguments)
    {
        var name = DomConvert.RequiredText(arguments, 0, "Element.removeAttribute");
        element.RemoveAttribute(name);
        Events.EventHandlerContentAttributes.AttributeChanged(realm, element, name);
    }

    /// <summary>HTML's <c>DOMStringMap</c> view over an element's <c>data-*</c> attributes.</summary>
    internal virtual JsValue Dataset(DomRealm realm, IHtmlElement element)
        => realm.WrapStringMap(element, element.Dataset);

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-insertadjacenthtml</summary>
    /// <remarks>
    /// <para>
    /// Two of the four positions insert into the element's parent, so that is what is walked when there
    /// is one; see <see cref="SetInnerHtml"/> for why the walk is here at all.
    /// </para>
    /// <para>
    /// Step 2's refusal is made here rather than left to AngleSharp, which is the one place in the pinned
    /// assembly that raises a <c>DomException</c> carrying a sentence instead of a
    /// <c>DomError</c> — so <see cref="DomFailures.NameOf"/> could only ever guess at its name, and the
    /// standard's is <c>NoModificationAllowedError</c>. Recorded in <c>AGENTS.md</c>'s divergence table.
    /// </para>
    /// </remarks>
    internal virtual void InsertAdjacentHtml(DomRealm realm, IElement element, JsValue[] arguments)
    {
        var position = DomEnums.ToAdjacentPosition(DomConvert.At(arguments, 0), "Element.insertAdjacentHTML");

        // "If position is 'beforebegin' or 'afterend' … If context is null or a Document, throw a
        // NoModificationAllowedError DOMException." A parent that is the document is not an IElement, so the
        // one test covers both halves — and it is the very test AngleSharp's own `Parent as Element` makes.
        if (position is AdjacentPosition.BeforeBegin or AdjacentPosition.AfterEnd && element.Parent is not IElement)
        {
            DomFailures.Refuse(
                realm.Engine,
                "Element.insertAdjacentHTML",
                DomExceptionNames.NoModificationAllowed,
                "the element has no parent element to insert " + (position == AdjacentPosition.BeforeBegin ? "before" : "after") + ".");
        }

        element.Insert(position, DomConvert.RequiredText(arguments, 1, "Element.insertAdjacentHTML"));
        CustomElements.CustomElementRegistry.SubtreeCreated(realm, element.Parent ?? element);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-write</summary>
    internal virtual void Write(DomRealm realm, IDocument document, JsValue[] arguments)
    {
        if (RefusedAfterTheParse(realm, document, "write"))
        {
            return;
        }

        document.Write(Join(arguments));
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#dom-document-writeln</summary>
    internal virtual void WriteLine(DomRealm realm, IDocument document, JsValue[] arguments)
    {
        if (RefusedAfterTheParse(realm, document, "writeln"))
        {
            return;
        }

        document.WriteLine(Join(arguments));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-createelement, and its namespaced and cloning siblings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These three <em>answer</em> rather than act, which is what the override table's value-returning hook
    /// form is for. Their answer belongs to the host because with the synchronous custom elements flag set,
    /// DOM's "create an element" runs the definition's <b>constructor</b> and hands back whatever it made
    /// — an object AngleSharp never saw. Everything else about the members stays AngleSharp's call: the
    /// name validation, the lower-casing, the namespace, the clone itself.
    /// </para>
    /// <para>
    /// A document with no definition at all therefore behaves exactly as the generated member did before
    /// there was a registry, which is what keeps the binding usable on its own.
    /// </para>
    /// </remarks>
    internal virtual JsValue CreateElement(DomRealm realm, IDocument document, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CreateElement(realm, document, arguments);

    /// <inheritdoc cref="CreateElement" />
    internal virtual JsValue CreateElementNS(DomRealm realm, IDocument document, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CreateElementNS(realm, document, arguments);

    /// <inheritdoc cref="CreateElement" />
    internal virtual JsValue CloneNode(DomRealm realm, INode node, JsValue[] arguments)
        => CustomElements.CustomElementCreation.CloneNode(realm, node, arguments);

    // ------------------------------------------------------------------------------------------------
    // The members whose value the host has and AngleSharp does not. Every one of them used to be an own
    // property written onto the document wrapper, because a getter could not be hooked; they are accessors
    // on Document.prototype now, which is where a browser has them, and `Object.getOwnPropertyNames(document)`
    // is empty as a result. The defaults below are AngleSharp's own answers, so a binding used without a
    // page runtime behaves exactly as it did.
    // ------------------------------------------------------------------------------------------------

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/nav-history-apis.html#dom-document-defaultview and
    /// <c>iframe.contentWindow</c>: the <c>WindowProxy</c> a member answers with.
    /// </summary>
    /// <remarks>
    /// The global object of an engine <em>is</em> its window, so the only window this can answer is the one
    /// this engine stands for; any other browsing context is <c>null</c>, which is what a browser answers for
    /// a frame that has none yet. A binding with no page runtime has no window at all.
    /// </remarks>
    internal virtual JsValue Window(DomRealm realm, IWindow window)
    {
        if (PageRuntime.Find(realm.Engine) is not { } runtime || runtime.Document is not { } document)
        {
            return JsValue.Null;
        }

        return ReferenceEquals(window, document.DefaultView)
            ? realm.Engine._mainRealm.GlobalObject
            : JsValue.Null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#dom-document-currentscript — the script whose text is
    /// running. AngleSharp answers the head of its <em>deferred</em> script queue, so it is null for exactly
    /// the case a page uses it in; the parser driver knows which script it is running.
    /// </summary>
    internal virtual JsValue CurrentScript(DomRealm realm, IDocument document)
    {
        if (PageRuntime.Find(realm.Engine) is not { } runtime)
        {
            return realm.WrapNodeValue(document.CurrentScript);
        }

        return runtime.CurrentScript is { } script ? realm.WrapNode(script) : JsValue.Null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#current-document-readiness. AngleSharp advances its own
    /// readiness on its own schedule and <c>Document.ReadyState</c>'s setter is <c>protected</c>, so nothing
    /// outside its assembly can move it; the three transitions a page observes are the parser driver's.
    /// </summary>
    internal virtual JsValue ReadyState(DomRealm realm, IDocument document)
        => JsString.Create(PageRuntime.Find(realm.Engine) is { } runtime
            ? runtime.ReadyState
            : document.ReadyState.ToString().ToLowerInvariant());

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-document-url and its <c>documentURI</c> twin. The page's URL, not
    /// AngleSharp's document address: <c>pushState</c> and a fragment navigation move the URL without
    /// reloading, and AngleSharp's address cannot follow without raising a navigation of its own.
    /// </summary>
    internal virtual JsValue DocumentUrl(DomRealm realm, IDocument document)
        => JsString.Create(PageRuntime.Find(realm.Engine)?.DocumentUrl ?? document.Url ?? "");

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-baseuri — the node document's base URL, which
    /// <c>&lt;base href&gt;</c> moves. Recomputed by the runtime because the URL it resolves against has to be
    /// the page's.
    /// </summary>
    internal virtual JsValue BaseUri(DomRealm realm, INode node)
    {
        if (PageRuntime.Find(realm.Engine) is not { } runtime)
        {
            return JsString.Create(node.BaseUri ?? "");
        }

        return JsString.Create(runtime.BaseUri);
    }

    /// <summary>https://html.spec.whatwg.org/multipage/dom.html#dom-document-referrer</summary>
    internal virtual JsValue Referrer(DomRealm realm, IDocument document)
        => JsString.Create(PageRuntime.Find(realm.Engine)?.Referrer ?? document.Referrer ?? "");

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dom.html#dom-document-cookie, over the same jar every request of
    /// the browsing context reads and writes — which is a jar AngleSharp's own document has no idea about.
    /// </summary>
    internal virtual JsValue Cookie(DomRealm realm, IDocument document)
        => JsString.Create(PageRuntime.Find(realm.Engine) is { } runtime
            ? DocumentCookies.Read(runtime)
            : document.Cookie ?? "");

    /// <inheritdoc cref="Cookie" />
    internal virtual void SetCookie(DomRealm realm, IDocument document, string value)
    {
        if (PageRuntime.Find(realm.Engine) is { } runtime)
        {
            DocumentCookies.Write(runtime, value);
            return;
        }

        document.Cookie = value;
    }

    /// <summary>
    /// Whether a write to a document that has finished parsing is refused, and the page told why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// HTML says such a write implies <c>document.open()</c>, which replaces the document. AngleSharp's
    /// <c>Document.Open</c> implements that by unloading through its own browsing context — blocking on
    /// <c>PromptToUnloadAsync().Result</c> and <c>Unload(recycle: true).Wait()</c>, on whatever thread the
    /// script ran on — and rebuilding the document behind the page's back, leaving the page's wrapper table,
    /// its frame tree and its runtime pointing at a document that no longer exists. Until the page owns that
    /// algorithm the honest answer is a page error naming it rather than a corrupted page.
    /// </para>
    /// <para>
    /// With no page runtime there is nothing to corrupt and nothing to report to, so a binding-only engine
    /// keeps AngleSharp's behaviour — which is what it had before there was a driver at all.
    /// </para>
    /// </remarks>
    private static bool RefusedAfterTheParse(DomRealm realm, IDocument document, string member)
    {
        if (document.ReadyState == DocumentReadyState.Loading)
        {
            return false;
        }

        if (Runtime.PageRuntime.Find(realm.Engine) is not { } runtime)
        {
            return false;
        }

        runtime.Recorder.Add(
            PageErrorKind.ReportedError,
            "document." + member + "() after the document finished parsing implies document.open(), which "
            + "would replace the document; Jint.Browser does not implement it, so the call did nothing. "
            + "Build the markup with the DOM, or set the page's content again.",
            document.Url);

        return true;
    }

    /// <summary>
    /// <c>document.write</c> takes a variadic <c>DOMString...</c> and concatenates it; AngleSharp's signature
    /// takes one string, so the concatenation happens here.
    /// </summary>
    private static string Join(JsValue[] arguments)
    {
        if (arguments.Length == 0)
        {
            return "";
        }

        if (arguments.Length == 1)
        {
            return TypeConverter.ToString(arguments[0]);
        }

        var builder = new System.Text.StringBuilder();
        foreach (var argument in arguments)
        {
            builder.Append(TypeConverter.ToString(argument));
        }

        return builder.ToString();
    }
}
