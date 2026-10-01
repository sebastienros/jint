#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Xml;

public class XmlPiNameTests
{
    // W3C XML Fifth Edition errata-4e corpus: ibm89n06 through ibm89n12.
    [TestCase('\u0EC7')]
    [TestCase('\u3006')]
    [TestCase('\u3030')]
    [TestCase('\u3036')]
    [TestCase('\u309C')]
    [TestCase('\u309F')]
    [TestCase('\u30FF')]
    public void FifthEditionNameCharactersSurvivePiCreationAndCloning(char nameCharacter)
    {
        var target = "_" + nameCharacter;
        var source = "<?" + target + " data?><IllegalExtender" + nameCharacter + "/>";
        var document = MarkupParser.ParseXml(source);
        var instruction = (ProcessingInstruction) document.FirstChild!;
        instruction.Target.Should().Be(target);
        document.DocumentElement!.LocalName.Should().Be("IllegalExtender" + nameCharacter);
        ((ProcessingInstruction) instruction.CloneNode()).Target.Should().Be(target);
        ((ProcessingInstruction) Document.CreateXml().ImportNode(instruction)).Target.Should().Be(target);
    }

    [TestCase("p:q", "xml/namespace-error")]
    [TestCase("xMl", "xml/invalid-declaration")]
    public void XmlParserAppliesItsPiTargetRestrictionsBeyondThePublicFactory(string target, string code)
    {
        Document.CreateXml().CreateProcessingInstruction(target, "data").Target.Should().Be(target);
        var source = "<r/><?" + target + " data?>";
        var error = Assert.Throws<MarkupParseException>(() => MarkupParser.ParseXml(source));
        error!.Code.Should().Be(code);
    }

    [Test]
    public void LongPiTargetPollsCancellationDuringNameScan()
    {
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var source = "<?" + new string('x', 12_000) + " data?><r/>";
        Assert.Throws<OperationCanceledException>(() => XmlTreeParser.ParseDocument(source, ParseLimits.Unbounded,
            () => { polls++; cancellation.Cancel(); }, cancellation.Token));
        polls.Should().Be(1);
    }
}
