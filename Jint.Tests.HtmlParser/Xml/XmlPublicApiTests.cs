#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlPublicApiTests
{
    [Test]
    public void GenericXmlAcceptsAnyRootAndPreservesDefaultMetadata()
    {
        var document = MarkupParser.ParseXml("<?xml version='1.0'?><anything/>");
        document.Kind.Should().Be(DocumentKind.Xml);
        document.ContentType.Should().Be("application/xml");
        document.CharacterSet.Should().Be("UTF-8");
        document.DocumentElement!.LocalName.Should().Be("anything");
    }

    [Test]
    public void SvgRequiresExactNamespaceAndRootName()
    {
        var accepted = MarkupParser.ParseSvg("<s:svg xmlns:s='http://www.w3.org/2000/svg'><s:path/></s:svg>");
        accepted.ContentType.Should().Be("image/svg+xml");
        accepted.DocumentElement!.NamespaceUri.Should().Be(Namespaces.Svg);

        foreach (var input in new[]
        {
            "<svg/>",
            "<svg xmlns='urn:wrong'/>",
            "<rect xmlns='http://www.w3.org/2000/svg'/>"
        })
        {
            var error = Assert.Throws<MarkupParseException>(() => MarkupParser.ParseSvg(input));
            error!.Code.Should().Be("xml/svg-root-required");
            error.Offset.Should().Be(0);
        }
    }

    [Test]
    public void PublicFragmentKeepsContextUntouchedAndHasNoDtdInheritance()
    {
        var document = MarkupParser.ParseXml("<!DOCTYPE r [<!ENTITY e 'declared'>]><r/>");
        var context = document.DocumentElement!;
        var fragment = MarkupParser.ParseXmlFragment("<x/>text", context);
        fragment.OwnerDocument.Should().BeSameAs(document);
        fragment.ChildCount.Should().Be(2);
        context.ChildCount.Should().Be(0);
        var error = Assert.Throws<MarkupParseException>(() => MarkupParser.ParseXmlFragment("&e;", context));
        error!.Code.Should().Be("xml/undeclared-entity");
        context.ChildCount.Should().Be(0);
    }

    [Test]
    public void PublicLimitsAndCancellationKeepTheirExceptionTypes()
    {
        var options = new XmlParseOptions { Limits = new ParseLimits { MaxInputCharacters = 3 } };
        var limit = Assert.Throws<ParseLimitException>(() => MarkupParser.ParseXml("<r/>", options));
        limit!.Kind.Should().Be(ParseLimitKind.InputCharacters);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => MarkupParser.ParseXml("<r/>", cancellationToken: cancellation.Token));
    }
}
