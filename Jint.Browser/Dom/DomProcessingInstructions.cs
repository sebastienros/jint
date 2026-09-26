namespace Jint.Browser.Dom;

/// <summary>https://dom.spec.whatwg.org/#concept-pi-initialize.</summary>
internal static class DomProcessingInstructions
{
    // The native factory validates XML scalar values, including supplementary Names.
    // https://dom.spec.whatwg.org/#dom-document-createprocessinginstruction
    internal static Jint.HtmlParser.ProcessingInstruction Create(Jint.HtmlParser.Document document, string target, string data)
        => document.CreateProcessingInstruction(target, data);

}
