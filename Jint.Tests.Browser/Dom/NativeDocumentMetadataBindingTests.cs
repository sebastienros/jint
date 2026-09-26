using System.Globalization;
using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeDocumentMetadataBindingTests
{
    [Test]
    public void MetadataReadsTheActualDocumentSnapshotAndDomainRefusalKeepsIt()
    {
        using var dom = DomTestFixture.Create("");
        var modified = new DateTimeOffset(2015, 10, 21, 7, 28, 0, TimeSpan.Zero);
        DomDocumentMetadata.Initialize(dom.Document, DomDocumentOrigin.FromUrl("https://example.test:8443/source"), modified);
        dom.Text("document.origin").Should().Be("https://example.test:8443");
        dom.Text("document.domain").Should().Be("example.test");
        dom.Text("document.lastModified").Should().Be(modified.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture));
        dom.Text("(()=>{try{document.domain='other.test'}catch(e){return e.name}})()").Should().Be("NotSupportedError");
        dom.Text("document.domain").Should().Be("example.test");
        dom.Bool("(()=>{let converted=false;try{Object.getOwnPropertyDescriptor(Document.prototype,'domain').set.call({}, {toString(){converted=true;return 'test'}})}catch(e){return e instanceof TypeError && !converted}})()").Should().BeTrue();
    }

    [Test]
    public void CreateDocumentUsesTheReceiversAssociatedDocumentOrigin()
    {
        using var dom = DomTestFixture.Create("");
        DomDocumentMetadata.Initialize(dom.Document, DomDocumentOrigin.FromUrl("https://principal.test/"));
        var other = Document.CreateHtml();
        DomDocumentMetadata.Initialize(other, DomDocumentOrigin.FromUrl("https://associated.test/"));
        dom.Engine.SetValue("otherImplementation", DomRealm.Of(dom.Engine).Wrap(DomImplementation.Of(other)));
        dom.Execute("var made=otherImplementation.createDocument('urn:xml','root');");
        dom.Bool("made.origin==='https://associated.test' && made.domain==='associated.test' && made.documentElement.localName==='root' && made.defaultView===null").Should().BeTrue();
    }
}
