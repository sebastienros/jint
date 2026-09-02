using AngleSharp;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Native;

namespace Jint.Tests.Browser;

/// <summary>
/// What every binding test needs: a document parsed by AngleSharp, an engine with the web APIs on, and the
/// DOM interface objects installed on it.
/// </summary>
/// <remarks>
/// There is no browser runtime yet — no <c>Window</c>, no navigation, no parser driver — so the document is
/// parsed directly with AngleSharp's own parser and handed to the engine as a global. That is exactly the
/// composition the runtime will make later; what it proves now is that the binding layer stands on its own,
/// which is also what makes it something AngleSharp.Js could adopt without adopting anything else here.
/// </remarks>
internal sealed class DomTestFixture : IDisposable
{
    private DomTestFixture(IDocument document, Engine engine)
    {
        Document = document;
        Engine = engine;
    }

    internal IDocument Document { get; }

    internal Engine Engine { get; }

    /// <summary>Parses <paramref name="html"/> and installs it as <c>document</c> on a fresh engine.</summary>
    internal static DomTestFixture Create(string html)
    {
        // WithCss() is what makes `element.style` answer anything at all: AngleSharp.Css registers the
        // declaration factory the inline-style extension reads through, and without it GetStyle() answers
        // null. WithDefaultLoader is deliberately absent, so nothing here can reach the network.
        var context = BrowsingContext.New(Configuration.Default.WithCss());
        var document = context.OpenAsync(response => response.Content(html)).GetAwaiter().GetResult();

        var engine = new Engine(options => options.UseWebApis());
        DomBindings.Install(engine);
        engine.SetValue("document", DomBindings.Wrap(engine, document));

        return new DomTestFixture(document, engine);
    }

    /// <summary>Evaluates <paramref name="source"/> against the fixture's engine.</summary>
    internal JsValue Evaluate(string source) => Engine.Evaluate(source);

    /// <summary>
    /// Evaluates <paramref name="source"/> once and reads the result as a string, with <c>null</c> answering
    /// <see langword="null"/> so that a <c>DOMString?</c> member can be asserted directly.
    /// </summary>
    internal string? Text(string source)
    {
        var value = Evaluate(source);
        return value.IsNull() ? null : value.AsString();
    }

    /// <summary>Evaluates <paramref name="source"/> and reads the result as a boolean.</summary>
    internal bool Bool(string source) => Evaluate(source).AsBoolean();

    /// <summary>Evaluates <paramref name="source"/> and reads the result as a number.</summary>
    internal double Number(string source) => Evaluate(source).AsNumber();

    public void Dispose() => Document.Dispose();
}
