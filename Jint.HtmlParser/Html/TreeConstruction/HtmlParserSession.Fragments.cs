using System;
using System.Threading;

namespace Jint.HtmlParser.Html;

internal sealed partial class HtmlParserSession
{
    internal DocumentFragment? Fragment { get; private init; }

    // HTML Standard §13.4 (2026-09-25). The real target supplies ownership;
    // Browser chooses its context/target for template contents and shadow roots.
    internal static HtmlParserSession CreateFragment(Element context, HtmlParseOptions? options = null,
        Node? target = null, HtmlParserScriptingMode? scriptingMode = null, bool allowDeclarativeShadowRoots = false,
        IHtmlShadowHostContextProvider? shadowHostContextProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        target ??= context;
        if (target is not (Element or DocumentFragment))
            throw new ArgumentException("A fragment target must be an element or document fragment.", nameof(target));
        options ??= new HtmlParseOptions();
        var mode = scriptingMode ?? (options.ScriptingEnabled ? HtmlParserScriptingMode.Inert : HtmlParserScriptingMode.Disabled);
        if (mode is not (HtmlParserScriptingMode.Disabled or HtmlParserScriptingMode.Inert or HtmlParserScriptingMode.Fragment))
            throw new ArgumentOutOfRangeException(nameof(scriptingMode));
        var document = Document.CreateHtml();
        document.SetParserMode(context.OwnerDocument!.Mode);
        var session = new HtmlParserSession(document, options,
            context: new HtmlDocumentContext(AllowDeclarativeShadowRoots: allowDeclarativeShadowRoots,
                ShadowHostContextProvider: shadowHostContextProvider), scriptingMode: mode)
        {
            Fragment = target.OwnerDocument!.CreateDocumentFragment()
        };
        session._builder.InitializeFragment(context, session.Fragment);
        return session;
    }

    internal static DocumentFragment ParseFragment(string source, Element context, HtmlParseOptions? options = null,
        Node? target = null, HtmlParserScriptingMode? scriptingMode = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var session = CreateFragment(context, options, target, scriptingMode);
        session.AppendInput(source, isFinal: true);
        HtmlParseStep step;
        do { step = session.Drive(4096, cancellationToken); } while (step.Kind == HtmlParseStepKind.Yielded);
        if (step.Kind != HtmlParseStepKind.Complete)
            throw new InvalidOperationException($"Fragment parsing did not complete: {step.Kind} ({step.MissingFeature}).");
        return session.Fragment!;
    }
}
