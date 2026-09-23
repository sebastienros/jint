#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlDtdReviewTests
{
    [Test]
    public void EntityCannotCloseCallerAndReopenToRestoreFinalDepth()
    {
        const string source = "<!DOCTYPE a [<!ENTITY e '</a><a>'>]><a>&e;</a>";
        var error = Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
        error!.Code.Should().Be("xml/invalid-markup");
        error.Offset.Should().Be(source.IndexOf("&e;", StringComparison.Ordinal));
    }

    [Test]
    public void NestedAttributeEntityCanResolveLocalCatalog()
    {
        const string source = "<!DOCTYPE r PUBLIC '-//W3C//DTD XHTML 1.0 Strict//EN' 'unused' [<!ENTITY e '&nbsp;'>]><r a='&e;'/>";
        var root = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;
        root.GetAttribute("a").Should().Be("\u00A0");
    }

    [Test]
    public void ConstructedCharacterReferenceCrSurvivesInTextAndNormalizesTwiceInAttribute()
    {
        const string source = "<!DOCTYPE r [<!ENTITY e '&#13;&#10;'>]><r a='&e;'>&e;<![CDATA[&#13;]]></r>";
        var root = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;
        root.GetAttribute("a").Should().Be("  ");
        ((Text) root.FirstChild!).Data.Should().Be("\r\n");
    }

    [Test]
    public void DoctypeNameMismatchIsValidityOnly()
    {
        var document = XmlTreeParser.ParseDocument("<!DOCTYPE a><b/>", ParseLimits.Unbounded, default);
        document.Doctype!.Name.Should().Be("a");
        document.DocumentElement!.LocalName.Should().Be("b");
    }

    [Test]
    public void MixedModelCanHaveZeroNamedAlternatives()
    {
        var document = XmlTreeParser.ParseDocument("<!DOCTYPE r [<!ELEMENT r (#PCDATA)*>]><r/>",
            ParseLimits.Unbounded, default);
        document.DocumentElement.Should().NotBeNull();
    }

    [Test]
    public void NotationEnumerationRequiresXmlNames()
    {
        const string source = "<!DOCTYPE r [<!ATTLIST r n NOTATION (1|valid) #IMPLIED>]><r/>";
        var error = Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
        error!.Code.Should().Be("xml/invalid-declaration");
    }

    [Test]
    public void ParameterReferenceInsideInternalSubsetDeclarationIsFatal()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % p 'x'><!ENTITY e '%p;'>]><r/>";
        var error = Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
        error!.Code.Should().Be("xml/invalid-declaration");
    }

    [Test]
    public void UnknownAttributeEntityMayBeSkippedAfterUnreadExternalSubset()
    {
        const string source = "<!DOCTYPE r SYSTEM 'missing.dtd'><r a='x&unknown;y'/>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        document.DocumentElement!.GetAttribute("a").Should().Be("xy");
        document.SkippedXmlEntities.Select(x => x.Kind).Should()
            .Equal(XmlSkippedEntityKind.ExternalSubset, XmlSkippedEntityKind.General);
        document.SkippedXmlEntities[1].PublicId.Should().BeNull();
        document.SkippedXmlEntities[1].SystemId.Should().BeNull();
    }

    [Test]
    public void CatalogEntityChargesItsEffectiveXmlReplacementLength()
    {
        const string source = "<!DOCTYPE r PUBLIC '-//W3C//DTD XHTML 1.0 Strict//EN' 'unused'><r>&LT;</r>";
        var limit = Assert.Throws<ParseLimitException>(() => XmlTreeParser.ParseDocument(source,
            new ParseLimits { MaxEntityExpansionCharacters = 5 }, default));
        limit!.Kind.Should().Be(ParseLimitKind.EntityExpansionCharacters);
        limit.Observed.Should().Be(6);
        ((Text) XmlTreeParser.ParseDocument(source, new ParseLimits { MaxEntityExpansionCharacters = 6 },
            default).DocumentElement!.FirstChild!).Data.Should().Be("<");
    }

    [TestCase("<!DOCTYPE r [<!ATTLIST unused a CDATA '<'>]><r/>")]
    [TestCase("<!DOCTYPE r [<!ATTLIST unused a CDATA '&later;'><!ENTITY later 'x'>]><r/>")]
    public void UnusedDefaultsStillEnforceDeclarationTimeWellFormedness(string source)
    {
        Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
    }

    [TestCase("<!DOCTYPE r [<!ENTITY a:b 'x'>]><r/>")]
    [TestCase("<!DOCTYPE r [<!NOTATION a:b SYSTEM 'n'>]><r/>")]
    [TestCase("<!DOCTYPE r [<!ELEMENT a::b EMPTY>]><r/>")]
    [TestCase("<!DOCTYPE r [<!ATTLIST a::b attr CDATA #IMPLIED>]><r/>")]
    [TestCase("<!DOCTYPE r [<!ATTLIST unused a::b CDATA #IMPLIED>]><r/>")]
    public void NamespaceChecksCoverUnusedDtdNames(string source)
    {
        var error = Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
        error!.Code.Should().Be("xml/namespace-error");
    }

    [Test]
    public void SpecifiedTokenizedNamespaceDeclarationNormalizesBeforeResolution()
    {
        const string source = "<!DOCTYPE r [<!ATTLIST r xmlns NMTOKEN #IMPLIED>]><r xmlns=' urn:x '/>";
        var root = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default).DocumentElement!;
        root.NamespaceUri.Should().Be("urn:x");
        root.GetAttribute("xmlns").Should().Be("urn:x");
    }

    [Test]
    public void AReadParameterEntityMakesMissingGeneralEntityAValidityIssue()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % p ''>%p;]><r>&missing;</r>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        document.SkippedXmlEntities.Should().ContainSingle();
        document.SkippedXmlEntities[0].Name.Should().Be("missing");
        document.SkippedXmlEntities[0].PublicId.Should().BeNull();
    }

    [Test]
    public void StandaloneDocumentCannotUseGeneralDeclarationFromParameterEntity()
    {
        const string source = "<?xml version='1.0' standalone='yes'?><!DOCTYPE r [<!ENTITY % p '<!ENTITY e \"x\">'>%p;]><r>&e;</r>";
        var error = Assert.Throws<MarkupParseException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default));
        error!.Code.Should().Be("xml/undeclared-entity");
    }

    [Test]
    public void UnknownEntityInKnownCatalogSubsetCanStillBeReported()
    {
        const string source = "<!DOCTYPE r PUBLIC '-//W3C//DTD XHTML 1.0 Strict//EN' 'unused'><r>&missing;</r>";
        var document = XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded, default);
        document.SkippedXmlEntities.Should().ContainSingle();
        document.SkippedXmlEntities[0].Kind.Should().Be(XmlSkippedEntityKind.General);
        document.SkippedXmlEntities[0].Name.Should().Be("missing");
    }

    [Test]
    public void CancelsDuringNestedAttributeExpansion()
    {
        const string ten = "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;";
        const string source = "<!DOCTYPE r [<!ENTITY a 'abcdefghij'><!ENTITY b '" + ten +
            "'><!ENTITY c '&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;'>" +
            "<!ENTITY d '&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;'>]><r a='&d;'/>";
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        Assert.Throws<OperationCanceledException>(() => XmlTreeParser.ParseDocument(source,
            ParseLimits.Unbounded, () => { polls++; cancellation.Cancel(); }, cancellation.Token));
        polls.Should().Be(1);
    }

    [Test]
    public void ParameterExpansionBudgetAppliesBeforePaddedCopy()
    {
        const string source = "<!DOCTYPE r [<!ENTITY % p ''>%p;]><r/>";
        var limit = Assert.Throws<ParseLimitException>(() => XmlTreeParser.ParseDocument(source,
            new ParseLimits { MaxEntityExpansionCharacters = 1 }, default));
        limit!.Kind.Should().Be(ParseLimitKind.EntityExpansionCharacters);
        limit.Observed.Should().Be(2);
    }
}
