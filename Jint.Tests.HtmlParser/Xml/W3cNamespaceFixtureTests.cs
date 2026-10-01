#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

// Exact fixtures from the W3C XML Conformance Test Suite 20031210,
// xmlconf/eduni/namespaces/1.0/{007,009,012,021}.xml.
// Provenance and redistribution terms: W3C-TEST-LICENSE.md.
public class W3cNamespaceFixtureTests
{
    private const string DifferentCapitalization = """
        <?xml version="1.0"?>
        <!-- Namespace inequality test: different capitalization -->
        <!DOCTYPE foo [
        <!ELEMENT foo ANY>
        <!ATTLIST foo xmlns:a CDATA #IMPLIED
                      xmlns:b CDATA #IMPLIED
                      xmlns:c CDATA #IMPLIED>
        <!ELEMENT bar ANY>
        <!ATTLIST bar a:attr CDATA #IMPLIED
                      b:attr CDATA #IMPLIED
                      c:attr CDATA #IMPLIED>
        ]>
        <foo xmlns:a="http://example.org/wine"
             xmlns:b="http://Example.org/wine"
             xmlns:c="http://example.org/Wine">

        <bar a:attr="1" b:attr="2" c:attr="3"/>

        </foo>
        """;

    private const string RepeatedExpandedAttribute = """
        <?xml version="1.0"?>
        <!-- Namespace equality test: plain repetition -->
        <!DOCTYPE foo [
        <!ELEMENT foo ANY>
        <!ATTLIST foo xmlns:a CDATA #IMPLIED
                      xmlns:b CDATA #IMPLIED
                      xmlns:c CDATA #IMPLIED>
        <!ELEMENT bar ANY>
        <!ATTLIST bar a:attr CDATA #IMPLIED
                      b:attr CDATA #IMPLIED
                      c:attr CDATA #IMPLIED>
        ]>
        <foo xmlns:a="http://example.org/~wilbur"
             xmlns:b="http://example.org/~wilbur">

        <bar a:attr="1" b:attr="2"/>

        </foo>
        """;

    private const string NormalizedNamespaceEquality = """
        <?xml version="1.0"?>
        <!-- Namespace inequality test: equal after attribute value normalization -->
        <!DOCTYPE foo [
        <!ELEMENT foo ANY>
        <!ATTLIST foo xmlns:a CDATA #IMPLIED
                      xmlns:b NMTOKEN #IMPLIED
                      xmlns:c CDATA #IMPLIED>
        <!ELEMENT bar ANY>
        <!ATTLIST bar a:attr CDATA #IMPLIED
                      b:attr CDATA #IMPLIED
                      c:attr CDATA #IMPLIED>
        ]>
        <foo xmlns:a="urn:xyzzy"
             xmlns:b=" urn:xyzzy ">

        <bar a:attr="1" b:attr="2"/>

        </foo>
        """;

    private const string DefaultNamespaceReset = """
        <foo xmlns="http://example.org/namespace">
         <foo xmlns=""/>
        </foo>
        """;

    [Test]
    public void DifferentUriCapitalizationRemainsDistinct()
    {
        var root = MarkupParser.ParseXml(DifferentCapitalization).DocumentElement!;
        var bar = root.ChildNodes.OfType<Element>().Single();
        bar.Attributes.Select(a => a.NamespaceUri).Where(uri => uri != Namespaces.Xmlns).Should()
            .Equal("http://example.org/wine", "http://Example.org/wine", "http://example.org/Wine");
    }

    [TestCase(RepeatedExpandedAttribute)]
    [TestCase(NormalizedNamespaceEquality)]
    public void EqualExpandedNamesAreRejected(string source)
    {
        var error = Assert.Throws<MarkupParseException>(() => MarkupParser.ParseXml(source));
        error!.Code.Should().Be("xml/duplicate-attribute");
    }

    [Test]
    public void DefaultNamespaceCanBeReset()
    {
        var root = MarkupParser.ParseXml(DefaultNamespaceReset).DocumentElement!;
        root.NamespaceUri.Should().Be("http://example.org/namespace");
        root.ChildNodes.OfType<Element>().Single().NamespaceUri.Should().BeNull();
    }
}
