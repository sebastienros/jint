#nullable enable
using System.Xml;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

// Differential checks cover only behavior shared with System.Xml. XML fifth-edition
// names, no-fetch omissions, and native node kinds have separate specification tests.
public class XmlReaderIntersectionTests
{
    [TestCase("<r a='x\r\ny&#10;z'>first\r\nsecond</r>", "a")]
    [TestCase("<!DOCTYPE r [<!ENTITY word 'one&amp;two'>]><r a='&word;'>&word;</r>", "a")]
    [TestCase("<!DOCTYPE r [<!ATTLIST r a NMTOKENS '  one   two  '>]><r>text</r>", "a")]
    [TestCase("<p:r xmlns:p='urn:p' xmlns='urn:default' p:a='v'>text</p:r>", "p:a")]
    public void AgreesWithXmlReaderOnSupportedRootValues(string source, string attributeName)
    {
        var native = MarkupParser.ParseXml(source).DocumentElement!;
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        using var reader = XmlReader.Create(new StringReader(source), settings);
        reader.MoveToContent();

        native.LocalName.Should().Be(reader.LocalName);
        native.NamespaceUri.Should().Be(reader.NamespaceURI.Length == 0 ? null : reader.NamespaceURI);
        native.GetAttribute(attributeName).Should().Be(reader.GetAttribute(attributeName));
        ((Text) native.FirstChild!).Data.Should().Be(reader.ReadElementContentAsString());
    }
}
