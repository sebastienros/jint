using Jint.HtmlParser;
using Jint.Browser.Accessibility;
using Jint.Browser.Styling;
using Jint.Browser.Dom;
using Jint.Native;

namespace Jint.Tests.Browser;

/// <summary>Native binding fixture: a minimally parsed document and an engine with Web APIs.</summary>
/// <remarks>Completed ordinary-tree styles are registered; CSS parsing and enhanced HTML state remain lazy.</remarks>
internal sealed class DomTestFixture : IDisposable
{
    private DomTestFixture(Document document, Engine engine)
    {
        Document = document;
        Engine = engine;
    }

    internal Document Document { get; }

    internal Engine Engine { get; }

    /// <summary>Parses <paramref name="html"/> and installs it as <c>document</c> on a fresh engine.</summary>
    internal static DomTestFixture Create(string html)
    {
        var engine = new Engine(options => options.UseWebApis());
        var document = ContentDom.Parse(html, checkpoint: engine.Constraints.Check);
        DomBindings.Install(engine);
        NativeCssStyleSheets.Associate(DomRealm.Of(engine), document);
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

    /// <summary>Runs <paramref name="source"/> for its effect on the document.</summary>
    internal void Execute(string source) => Engine.Execute(source);

    public void Dispose() => Engine.Dispose();
}
