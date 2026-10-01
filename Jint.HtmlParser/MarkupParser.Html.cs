using Jint.HtmlParser.Html;

namespace Jint.HtmlParser;

public static partial class MarkupParser
{
    /// <summary>Parses HTML into a native document, recovering from malformed markup without executing scripts.</summary>
    public static Document ParseHtml(string source, HtmlParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new HtmlParseOptions();
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document, options,
            scriptingMode: options.ScriptingEnabled ? HtmlParserScriptingMode.Inert : HtmlParserScriptingMode.Disabled);
        session.AppendInput(source, isFinal: true);
        CompleteHtml(session, cancellationToken);
        return document;
    }

    /// <summary>Parses detached HTML children in the context and owner document of an existing element.</summary>
    public static DocumentFragment ParseHtmlFragment(string source, Element context, HtmlParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var session = HtmlParserSession.CreateFragment(context, options);
        session.AppendInput(source, isFinal: true);
        CompleteHtml(session, cancellationToken);
        return session.Fragment!;
    }

    private static void CompleteHtml(HtmlParserSession session, CancellationToken cancellationToken)
    {
        HtmlParseStep step;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            step = session.Drive(4096, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        } while (step.Kind == HtmlParseStepKind.Yielded);
        if (step.Kind != HtmlParseStepKind.Complete)
            throw new InvalidOperationException($"HTML parsing did not complete: {step.Kind} ({step.MissingFeature}).");
    }
}
