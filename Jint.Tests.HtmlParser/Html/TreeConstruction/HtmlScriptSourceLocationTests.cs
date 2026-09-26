#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

public class HtmlScriptSourceLocationTests
{
    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void EverySplitPreservesOriginalCoordinates(int quota)
    {
        foreach (var newline in new[] { "\n", "\r", "\r\n" })
        {
            var prefix = "<!DOCTYPE html>" + newline + "<!--x-->" + newline + "<script" + newline + " data-x='a" + newline + "b'>";
            var source = prefix + "x</script>";
            for (var split = 0; split <= source.Length; split++)
            {
                var session = Session();
                session.AppendInput(source[..split]);
                var first = Drive(session, quota);
                session.AppendInput(source[split..], true);
                var request = Prepare(session, quota, first);
                Location(request).Should().Be(new HtmlSourceLocation(HtmlSourceKind.Primary, 0, prefix.Length, 5, 3));
                ((Element) request.Script.CloneNode(true)).GetHtmlState()!.Script!.ParserSourceLocation.Should().BeNull();
                var destination = Document.CreateHtml();
                ((Element) destination.ImportNode(request.Script, true)).GetHtmlState()!.Script!.ParserSourceLocation.Should().BeNull();
                destination.AdoptNode(request.Script);
                Location(request).Should().Be(new HtmlSourceLocation(HtmlSourceKind.Primary, 0, prefix.Length, 5, 3));
            }
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void NestedWritesResumePrimaryAndOuterSourceCoordinates(int quota)
    {
        var session = Session();
        session.AppendInput("<script>outer</script>\r\n<script>primary</script>", true);
        var outer = Prepare(session, quota);
        session.InsertInput(outer.Frame!, "\n\n<script>inner</script>\n<script>suffix</script>", default);
        var inner = Prepare(session, quota, frame: outer.Frame);
        Location(inner).Should().Be(new HtmlSourceLocation(HtmlSourceKind.Inserted, 1, 10, 3, 8));
        session.InsertInput(inner.Frame!, "\n\n\n<script>nested</script>", default);
        var nested = Prepare(session, quota, frame: inner.Frame);
        Location(nested).Should().Be(new HtmlSourceLocation(HtmlSourceKind.Inserted, 2, 11, 4, 8));
        Finish(session, nested);
        Drive(session, quota, inner.Frame).Kind.Should().Be(HtmlParseStepKind.InsertionBoundary);
        Finish(session, inner);
        var suffix = Prepare(session, quota, frame: outer.Frame);
        Location(suffix).Should().Be(new HtmlSourceLocation(HtmlSourceKind.Inserted, 1, 33, 4, 8));
        Finish(session, suffix);
        Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.InsertionBoundary);
        Finish(session, outer);
        var primary = Prepare(session, quota);
        Location(primary).Should().Be(new HtmlSourceLocation(HtmlSourceKind.Primary, 0, 32, 2, 8));
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void StartTagAndBodyAcrossWritesAreExplicitlyMixed(int quota)
    {
        foreach (var first in new[] { "<scr", "<script>bo" })
        {
            var session = Session();
            session.AppendInput("<script>outer</script>", true);
            var outer = Prepare(session, quota);
            session.InsertInput(outer.Frame!, first, default);
            Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.InsertionBoundary);
            session.InsertInput(outer.Frame!, first == "<scr" ? "ipt>body</script>" : "dy</script>", default);
            var mixed = Prepare(session, quota, frame: outer.Frame);
            Location(mixed).Kind.Should().Be(HtmlSourceKind.Mixed);
            Location(mixed).SourceUnitId.Should().Be(first == "<scr" ? 2 : 1);
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void InsertedStartTagCompletedByPrimaryTailIsMixed(int quota)
    {
        var session = Session();
        session.AppendInput("<script>outer</script>ipt>body</script>", true);
        var outer = Prepare(session, quota);
        session.InsertInput(outer.Frame!, "<scr", default);
        Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.InsertionBoundary);
        Finish(session, outer);
        var mixed = Prepare(session, quota);
        Location(mixed).Kind.Should().Be(HtmlSourceKind.Mixed);
        Location(mixed).SourceUnitId.Should().Be(0);
    }

    private static HtmlSourceLocation Location(HtmlHostRequest request) => request.Script.GetHtmlState()!.Script!.ParserSourceLocation!.Value;
    private static HtmlParserSession Session() => new(Document.CreateHtml(), enableScriptRequests: true);
    private static void Finish(HtmlParserSession session, HtmlHostRequest request) => session.CompleteHostRequest(request.Id, HtmlHostRequestOutcome.Finished);

    private static HtmlHostRequest Prepare(HtmlParserSession session, int quota, HtmlParseStep? initial = null, HtmlScriptFrame? frame = null)
    {
        var step = initial ?? Drive(session, quota, frame);
        if (step.Kind == HtmlParseStepKind.NeedInput) step = Drive(session, quota, frame);
        step.Kind.Should().Be(HtmlParseStepKind.HostRequest);
        if (step.HostRequest!.Kind == HtmlHostRequestKind.MicrotaskCheckpoint)
        {
            Finish(session, step.HostRequest);
            step = Drive(session, quota, frame);
        }
        step.HostRequest!.Kind.Should().Be(HtmlHostRequestKind.PrepareScript);
        return step.HostRequest;
    }

    private static HtmlParseStep Drive(HtmlParserSession session, int quota, HtmlScriptFrame? frame = null)
    {
        for (var attempts = 0; attempts < 100000; attempts++)
        {
            var step = frame is null ? session.Drive(quota, default) : session.DriveInsertedInput(frame, quota, default);
            if (step.Kind != HtmlParseStepKind.Yielded) return step;
        }
        throw new AssertionException("Parser did not reach a boundary within the deterministic work ceiling.");
    }
}
