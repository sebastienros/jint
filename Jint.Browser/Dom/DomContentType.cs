namespace Jint.Browser.Dom;

/// <summary>Document content types and HTML's XML MIME type predicate.</summary>
internal static class DomContentType
{
    /// <summary>DOM §4.5: the content type of the document its own constructor makes.</summary>
    internal const string Xml = "application/xml";

    /// <summary>DOM §4.5.1: the content type <c>createDocument</c> gives the XHTML namespace.</summary>
    internal const string Xhtml = "application/xhtml+xml";

    /// <summary>DOM §4.5.1: the content type <c>createDocument</c> gives the SVG namespace.</summary>
    internal const string Svg = "image/svg+xml";

    /// <summary>The fourth type <c>DOMParser</c>'s <c>SupportedType</c> enumeration names for an XML parse.</summary>
    internal const string TextXml = "text/xml";

    /// <summary>The type HTML's <i>read HTML</i> produces, and the one a page parses unless told otherwise.</summary>
    internal const string Html = "text/html";

    /// <summary>
    /// Whether <paramref name="essence"/> is an
    /// <a href="https://mimesniff.spec.whatwg.org/#xml-mime-type">XML MIME type</a>, which is what
    /// <a href="https://html.spec.whatwg.org/multipage/document-lifecycle.html#read-xml">HTML's read XML</a>
    /// selects the XML parser on.
    /// </summary>
    /// <remarks>
    /// <b>This is deliberately not <c>DOMParser</c>'s list.</b> <c>SupportedType</c> is a closed WebIDL
    /// enumeration of five values and a sixth is a <c>TypeError</c>, so <see cref="Views.JsDomParser"/> names
    /// its five rather than asking this. What a page <i>navigates</i> to, and what a frame is served, is the
    /// open predicate below instead — so <c>application/rss+xml</c> is an XML document there and is still not
    /// an argument <c>parseFromString</c> would have accepted. The two share these constants and not the
    /// decision, because they are not the same decision.
    /// </remarks>
    internal static bool IsXml(string? essence)
        => essence is not null
            && (essence.Equals(TextXml, StringComparison.OrdinalIgnoreCase)
                || essence.Equals(Xml, StringComparison.OrdinalIgnoreCase)
                || essence.EndsWith("+xml", StringComparison.OrdinalIgnoreCase));

}
