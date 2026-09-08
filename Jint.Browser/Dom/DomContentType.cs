using AngleSharp;
using AngleSharp.Dom;

namespace Jint.Browser.Dom;

/// <summary>
/// The <a href="https://dom.spec.whatwg.org/#concept-document-content-type">content type</a> of a document
/// this package created, carried on the browsing context that document was parsed into.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every document DOM creates is given a content type by the algorithm that creates it</b> — DOM §4.5.1's
/// <c>createDocument</c> derives one from the namespace, DOM §4.5's <c>Document()</c> constructor states
/// <c>application/xml</c>, and HTML's <c>DOMParser.parseFromString</c> uses the type it was handed. AngleSharp
/// answers <c>text/xml</c> for all of them, because its <c>Document.ContentType</c> setter is
/// <see langword="protected"/> and <c>AngleSharp.Xml</c>'s document types have internal constructors: there is
/// no seam to set it through and no type to subclass.
/// </para>
/// <para>
/// So the value rides on the <see cref="IBrowsingContext"/>, which this package makes one of per document it
/// creates, as a service the context hands back. That is not a side table keyed on a document: a clone keeps
/// its source's context, which is exactly what makes <c>doc.cloneNode()</c> answer the same content type its
/// source does, and a document made through <c>createHTMLDocument</c> gets a context of AngleSharp's own with
/// no service on it, so it keeps AngleSharp's correct <c>text/html</c>.
/// </para>
/// </remarks>
internal sealed class DomContentType
{
    /// <summary>DOM §4.5: the content type of the document its own constructor makes.</summary>
    internal const string Xml = "application/xml";

    /// <summary>DOM §4.5.1: the content type <c>createDocument</c> gives the XHTML namespace.</summary>
    internal const string Xhtml = "application/xhtml+xml";

    /// <summary>DOM §4.5.1: the content type <c>createDocument</c> gives the SVG namespace.</summary>
    internal const string Svg = "image/svg+xml";

    private DomContentType(string value) => Value = value;

    /// <summary>The content type itself.</summary>
    internal string Value { get; }

    /// <summary>The configuration <paramref name="configuration"/> is, plus the declaration of one document's content type.</summary>
    internal static IConfiguration Declaring(IConfiguration configuration, string contentType)
        => configuration.With(new DomContentType(contentType));

    /// <summary>
    /// The content type DOM gave <paramref name="document"/>, or <see langword="null"/> when nothing here
    /// created it and AngleSharp's own answer is the right one.
    /// </summary>
    internal static string? Of(IDocument document) => document.Context?.GetService<DomContentType>()?.Value;
}
