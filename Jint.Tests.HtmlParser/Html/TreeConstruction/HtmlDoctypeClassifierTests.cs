#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public class HtmlDoctypeClassifierTests
{
    [TestCase("html", null, null, false, "NoQuirks")]
    [TestCase("HTML", null, null, false, "NoQuirks")]
    [TestCase(null, null, null, false, "Quirks")]
    [TestCase("other", null, null, false, "Quirks")]
    [TestCase("html", null, null, true, "Quirks")]
    [TestCase("html", "HTML", null, false, "Quirks")]
    [TestCase("html", "-//W3O//DTD W3 HTML Strict 3.0//EN//", null, false, "Quirks")]
    [TestCase("html", "-/W3C/DTD HTML 4.0 Transitional/EN", null, false, "Quirks")]
    [TestCase("html", null, "http://www.ibm.com/data/dtd/v11/ibmxhtml1-transitional.dtd", false, "Quirks")]
    [TestCase("html", "-//IETF//DTD HTML 2.0 Strict//EN", null, false, "Quirks")]
    [TestCase("html", "-//Microsoft//DTD Internet Explorer 3.0 Tables//EN", null, false, "Quirks")]
    [TestCase("html", "-//W3C//DTD HTML 3.2 Final//EN", null, false, "Quirks")]
    [TestCase("html", "-//WEBTECHS//DTD MOZILLA HTML//EN", null, false, "Quirks")]
    [TestCase("html", "-//W3C//DTD HTML 4.01 Transitional//EN", null, false, "Quirks")]
    [TestCase("html", "-//W3C//DTD HTML 4.01 Transitional//EN", "", false, "Quirks")]
    [TestCase("html", "-//W3C//DTD HTML 4.01 Transitional//EN", "x", false, "LimitedQuirks")]
    [TestCase("html", "-//W3C//DTD HTML 4.01 Frameset//EN", "x", false, "LimitedQuirks")]
    [TestCase("html", "-//W3C//DTD XHTML 1.0 Transitional//EN", null, false, "LimitedQuirks")]
    [TestCase("html", "-//W3C//DTD XHTML 1.0 Frameset//EN", null, false, "LimitedQuirks")]
    [TestCase("html", "-//W3C//DTD XHTML 1.0 Strict//EN", null, false, "NoQuirks")]
    [TestCase("html", "-//W3C//DTD HTML 4.01 Strict//EN", null, false, "NoQuirks")]
    [TestCase("html", "-//W3C//DTD HTML 4.01 TransitionalX//EN", "x", false, "NoQuirks")]
    public void InitialDoctypeClassificationCoversConditionFamilies(string? name, string? publicId,
        string? systemId, bool forceQuirks, string expected)
    {
        var token = new HtmlToken(HtmlTokenKind.Doctype, name: name, publicIdentifier: publicId,
            systemIdentifier: systemId, forceQuirks: forceQuirks);
        HtmlDoctypeClassifier.Classify(token).Should().Be(Enum.Parse<DocumentMode>(expected));
    }

    [Test]
    public void SourceContextCanPreserveDocumentMode()
    {
        var locked = Document.CreateHtml();
        locked.SetParserMode(DocumentMode.Quirks);
        var lockedSession = new HtmlParserSession(locked, context: new HtmlDocumentContext(CannotChangeMode: true));
        lockedSession.AppendInput("<!doctype html>", isFinal: true);
        DriveToEnd(lockedSession).Kind.Should().Be(HtmlParseStepKind.Complete);
        locked.Mode.Should().Be(DocumentMode.Quirks);

        var srcdoc = Document.CreateHtml();
        var srcdocSession = new HtmlParserSession(srcdoc, context: new HtmlDocumentContext(IsSrcdoc: true));
        srcdocSession.AppendInput("text", isFinal: true);
        DriveToEnd(srcdocSession).Kind.Should().Be(HtmlParseStepKind.Complete);
        srcdoc.Mode.Should().Be(DocumentMode.NoQuirks);
    }

    [Test]
    public void MissingAndEmptyIdentifiersBothPersistAsEmptyButAreClassifiedFromToken()
    {
        var missing = Document.CreateHtml();
        var missingSession = new HtmlParserSession(missing);
        missingSession.AppendInput("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Frameset//EN'>", isFinal: true);
        DriveToEnd(missingSession).Kind.Should().Be(HtmlParseStepKind.Complete);
        missing.Doctype!.SystemId.Should().BeEmpty();
        missing.Mode.Should().Be(DocumentMode.Quirks);

        var present = Document.CreateHtml();
        var presentSession = new HtmlParserSession(present);
        presentSession.AppendInput("<!doctype html PUBLIC '-//W3C//DTD HTML 4.01 Frameset//EN' 'about:blank'>", isFinal: true);
        DriveToEnd(presentSession).Kind.Should().Be(HtmlParseStepKind.Complete);
        present.Doctype!.SystemId.Should().Be("about:blank");
        present.Mode.Should().Be(DocumentMode.LimitedQuirks);
    }

    private static HtmlParseStep DriveToEnd(HtmlParserSession session)
    {
        HtmlParseStep step;
        do { step = session.Drive(10_000, CancellationToken.None); } while (step.Kind == HtmlParseStepKind.Yielded);
        return step;
    }
}
