using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Io;
using Jint.Browser.Dom;

namespace Jint.Browser.Runtime.Parsing;

/// <summary>
/// The <c>IDocumentFactory</c> a page parses through: AngleSharp's own table, with
/// <a href="https://html.spec.whatwg.org/multipage/document-lifecycle.html#read-xml">HTML's read XML</a>
/// reaching every <a href="https://mimesniff.spec.whatwg.org/#xml-mime-type">XML MIME type</a> rather than
/// the three <c>WithXml()</c> happens to name.
/// </summary>
/// <remarks>
/// <para>
/// <b>The unreachable half of the problem was the document factory; the parser choice was never it.</b>
/// <c>AngleSharp.Xml</c>'s <c>WithXml()</c> reaches into the configuration's <c>DefaultDocumentFactory</c>
/// and re-registers <c>text/xml</c>, <c>application/xml</c> and <c>image/svg+xml</c> on its own creators —
/// and leaves <c>application/xhtml+xml</c> on the <b>HTML</b> creator the base class seeded, which is where
/// <c>&lt;html xmlns="…"&gt;…&lt;/html&gt;</c> served as XHTML came back wrapped in a second HTML skeleton.
/// Every other <c>+xml</c> type has no entry at all and falls through to <c>CreateDefaultAsync</c>, which is
/// HTML as well. Standing a <i>second</i> <c>IDocumentFactory</c> beside the first breaks AngleSharp's own
/// resolution, and dropping the first discards what <c>WithXml()</c> contributed — but <b>subclassing</b> it
/// does neither: <c>WithXml()</c> finds this instance by <c>OfType&lt;DefaultDocumentFactory&gt;()</c> and
/// registers its creators on it exactly as it would on AngleSharp's own.
/// </para>
/// <para>
/// <b>Which is why the XML creator is taken back out of the table rather than written here.</b>
/// <c>AngleSharp.Xml</c>'s <c>LoadXmlAsync</c> is private and the <c>XmlDocument</c> it builds has an
/// internal constructor, so nothing outside that assembly can make one bound to a browsing context.
/// <see cref="ReadXmlWithTheXmlParser"/> unregisters <c>text/xml</c> purely for the delegate
/// <c>Unregister</c> hands back, puts it straight back, and keeps it for the types AngleSharp does not route
/// there. Nothing of the table is copied, so a version of AngleSharp that maps one of these differently
/// moves this with it.
/// </para>
/// <para>
/// <b><c>image/svg+xml</c> is left exactly where <c>WithXml()</c> put it</b>, on the creator that builds an
/// <c>SvgDocument</c> rather than a plain XML one, because that is the document DOM §4.5.1 asks for and
/// AngleSharp already makes it. Only a type AngleSharp has <i>no</i> answer for reaches the override below.
/// </para>
/// </remarks>
internal sealed class PageDocumentFactory : DefaultDocumentFactory
{
    private Creator? _xml;

    /// <summary>
    /// Points every XML MIME type AngleSharp does not route itself at the XML parser. Called once, after
    /// <c>WithXml()</c> has registered its creators on this instance.
    /// </summary>
    internal void ReadXmlWithTheXmlParser()
    {
        // The only way to hold AngleSharp.Xml's creator: take it out of the table WithXml() put it in, and
        // put it straight back. A configuration built without WithXml() has none, and then nothing here
        // changes anything at all.
        _xml = Unregister(MimeTypeNames.Xml);

        if (_xml is null)
        {
            return;
        }

        Register(MimeTypeNames.Xml, _xml);

        // The one XML MIME type AngleSharp maps, and it maps it to the HTML creator. Removing the entry is
        // what lets it fall through to CreateDefaultAsync, where the predicate below claims it.
        Unregister(MimeTypeNames.ApplicationXHtml);
    }

    /// <inheritdoc />
    /// <remarks>
    /// This is reached only for a content type no registered creator matched, which is what makes the test
    /// below a widening of AngleSharp's table and never an override of it.
    /// </remarks>
    protected override Task<IDocument> CreateDefaultAsync(
        IBrowsingContext context,
        CreateDocumentOptions options,
        CancellationToken cancellationToken)
        => _xml is { } xml && DomContentType.IsXml(options.ContentType?.Content)
            ? xml(context, options, cancellationToken)
            : base.CreateDefaultAsync(context, options, cancellationToken);
}
