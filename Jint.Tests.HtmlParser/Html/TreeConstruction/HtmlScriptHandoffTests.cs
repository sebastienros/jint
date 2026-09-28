#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html.TreeConstruction;

// Living HTML Standard, last updated 2026-09-25, checked 2026-09-25:
// https://html.spec.whatwg.org/multipage/parsing.html#parsing-main-incdata
// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#document-write-steps
// These are native host-driver fixtures, not claims of Browser scheduling conformance.
public class HtmlScriptHandoffTests
{
    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void CustomElementInsertionsYieldBeforeChildrenRegardlessOfQuota(int quota)
    {
        var session = Session("<body><x-first id=first><x-second id=second>tail</x-second></x-first><button is=x-button id=button>click</button>",
            scriptingEnabled: true);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.CustomElementReactions);
        var first = session.Document.GetElementById("first")!;
        first.ChildCount.Should().Be(0);
        session.Document.GetElementById("second").Should().BeNull();
        first.AppendChild(session.Document.CreateTextNode("host"));

        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.CustomElementReactions);
        var second = session.Document.GetElementById("second")!;
        second.ChildCount.Should().Be(0);
        second.ParentNode.Should().BeSameAs(first);
        session.Document.GetElementById("button").Should().BeNull();

        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.CustomElementReactions);
        session.Document.GetElementById("button")!.ChildCount.Should().Be(0);
        second.TextContent().Should().Be("tail");
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.Complete);
        first.TextContent().Should().Be("hosttail");
        session.Document.GetElementById("button")!.TextContent().Should().Be("click");
    }

    [TestCase(1)]
    [TestCase(10000)]
    public void ReconstructedCustomFormattingYieldsBeforeInsertingText(int quota)
    {
        var session = Session("<body><p><b is=x-bold id=bold><i is=x-italic id=italic></p>tail", scriptingEnabled: true);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.CustomElementReactions);
        session.Document.GetElementById("bold")!.ChildCount.Should().Be(0);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.CustomElementReactions);
        session.Document.GetElementById("italic")!.ChildCount.Should().Be(0);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.CustomElementReactions);
        var reconstructed = (Element) session.Document.DocumentElement!.LastChild!.LastChild!;
        reconstructed.LocalName.Should().Be("b");
        reconstructed.ChildCount.Should().Be(0);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.CustomElementReactions);
        reconstructed.FirstChild!.ChildCount.Should().Be(0);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.Complete);
        reconstructed.TextContent().Should().Be("tail");
    }

    [TestCase((int) HtmlParserScriptingMode.Normal, false)]
    [TestCase((int) HtmlParserScriptingMode.Disabled, true)]
    [TestCase((int) HtmlParserScriptingMode.Inert, true)]
    [TestCase((int) HtmlParserScriptingMode.Fragment, true)]
    public void NonexecutingParsesDoNotRequestCustomElementReactions(int mode, bool enableScriptRequests)
    {
        var session = new HtmlParserSession(Document.CreateHtml(), enableScriptRequests: enableScriptRequests,
            scriptingMode: (HtmlParserScriptingMode) mode);
        session.AppendInput("<x-one><button is=x-button>text</button></x-one>", true);
        Drive(session, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
    }

    [Test]
    public void CancellationAtCustomElementBoundaryInvalidatesTheSession()
    {
        var session = Session("<x-one id=one>unread</x-one>", scriptingEnabled: true);
        Drive(session, 10000).Kind.Should().Be(HtmlParseStepKind.CustomElementReactions);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Drive(10000, cancellation.Token));
        session.Document.GetElementById("one")!.ChildCount.Should().Be(0);
        Assert.Throws<InvalidOperationException>(() => session.Drive(10000, default));
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void CheckpointThenWriteQueryUsesSameTreeBeforeUnreadTail(int quota)
    {
        var session = Session("<body><script>outer</script><p id=tail>tail</p>");
        var checkpoint = Request(Drive(session, quota), HtmlHostRequestKind.MicrotaskCheckpoint);
        var script = checkpoint.Script;
        script.GetHtmlState()!.Script!.ParserDocument.Should().BeSameAs(session.Document);
        script.GetHtmlState()!.Script!.ForceAsync.Should().BeFalse();
        session.Document.GetElementById("tail").Should().BeNull();
        // A host checkpoint may mutate or remove the exact script. The parser's
        // stack is authoritative; parent links are not used to reconstruct it.
        script.ParentNode!.RemoveChild(script);
        session.CompleteHostRequest(checkpoint.Id, HtmlHostRequestOutcome.Finished);
        var preparation = Request(Drive(session, quota), HtmlHostRequestKind.PrepareScript);
        preparation.Script.Should().BeSameAs(script);
        var frame = preparation.Frame!;
        Write(session, frame, "<span id=written>now</span>", quota);
        session.Document.GetElementById("written")!.TextContent().Should().Be("now");
        script.ChildCount.Should().Be(1); // popped exactly once before writing
        session.Document.GetElementById("tail").Should().BeNull();
        session.CompleteHostRequest(preparation.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.Complete);
        session.Document.GetElementById("tail")!.TextContent().Should().Be("tail");
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void NestedInlineFramesPreserveWriteOrderAndRestoreOuterMarker(int quota)
    {
        var session = Session("<body><script>outer</script><b id=tail>T</b>");
        var outer = Prepare(session, quota);
        session.InsertInput(outer.Frame!, "<i id=first>A</i><script>inner</script><i id=last>C</i>", default);
        var inner = Request(Drive(session, quota, outer.Frame), HtmlHostRequestKind.PrepareScript);
        inner.Script.TextContent().Should().Be("inner");
        inner.Script.Should().NotBeSameAs(outer.Script);
        Write(session, inner.Frame!, "<i id=middle>B</i>", quota);
        session.Document.GetElementById("middle").Should().NotBeNull();
        session.Document.GetElementById("last").Should().BeNull();
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished));
        session.CompleteHostRequest(inner.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.InsertionBoundary);
        Write(session, outer.Frame!, "<i id=after>D</i>", quota);
        session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = (Element) session.Document.DocumentElement!.LastChild!;
        body.TextContent().Should().Be("outerAinnerBCDT");
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void NestedExternalPendingBlockerAbortsWritesUntilOuterPreparationReturns(int quota)
    {
        var session = Session("<body><script>outer</script><p id=tail>T</p>");
        var outer = Prepare(session, quota);
        session.InsertInput(outer.Frame!, "<script src=external></script><i id=suffix>S</i>", default);
        var nested = Request(Drive(session, quota, outer.Frame), HtmlHostRequestKind.PrepareScript);
        session.CompleteHostRequest(nested.Id, HtmlHostRequestOutcome.PendingParsingBlockingScript);
        Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.PendingBlocker);
        session.InsertInput(outer.Frame!, "<i id=second>W</i>", default);
        Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.PendingBlocker);
        session.Document.GetElementById("suffix").Should().BeNull();
        session.Document.GetElementById("second").Should().BeNull();
        session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished);
        var wait = Request(Drive(session, quota), HtmlHostRequestKind.WaitForPendingScript);
        wait.NestingLevel.Should().Be(0);
        wait.ParserDocument.Should().BeSameAs(session.Document);
        session.Drive(quota, default).HostRequest.Should().BeSameAs(wait);
        session.CompleteHostRequest(wait.Id, HtmlHostRequestOutcome.Finished);
        var execution = Request(Drive(session, quota), HtmlHostRequestKind.ExecutePendingScript);
        execution.Script.Should().BeSameAs(nested.Script);
        // Repeated polls while resources are unready return the same identity.
        session.Drive(quota, default).HostRequest.Should().BeSameAs(execution);
        Write(session, execution.Frame!, "<i id=external-write>E</i>", quota);
        session.Document.GetElementById("external-write").Should().NotBeNull();
        session.Document.GetElementById("suffix").Should().BeNull();
        session.CompleteHostRequest(execution.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.Complete);
        ((Element) session.Document.DocumentElement!.LastChild!).TextContent().Should().Be("outerESWT");
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void FinalInputCanStillReceiveSplitWritesWithoutPrematureEof(int quota)
    {
        var session = Session("<body><script>outer</script>");
        var outer = Prepare(session, quota);
        foreach (var part in new[] { "<p id=split", ">A&am", "p;B</p><scr", "ipt>child</scr", "ipt>" })
        {
            session.InsertInput(outer.Frame!, part, default);
            var step = Drive(session, quota, outer.Frame);
            if (step.Kind == HtmlParseStepKind.HostRequest)
            {
                var child = Request(step, HtmlHostRequestKind.PrepareScript);
                child.Script.TextContent().Should().Be("child");
                session.CompleteHostRequest(child.Id, HtmlHostRequestOutcome.Finished);
                Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.InsertionBoundary);
            }
            else step.Kind.Should().Be(HtmlParseStepKind.InsertionBoundary);
        }
        session.Document.GetElementById("split")!.TextContent().Should().Be("A&B");
        session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.Complete);
    }

    [Test]
    public void StaleForeignAndOutOfOrderCapabilitiesCannotChangeSession()
    {
        var session = Session("<script>x</script><p id=tail>T</p>");
        var checkpoint = Request(Drive(session, 1), HtmlHostRequestKind.MicrotaskCheckpoint);
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(checkpoint.Id, HtmlHostRequestOutcome.PendingParsingBlockingScript));
        session.CompleteHostRequest(checkpoint.Id, HtmlHostRequestOutcome.Finished);
        var outer = Request(Drive(session, 1), HtmlHostRequestKind.PrepareScript);
        var other = Session("<script>y</script>");
        var foreign = Prepare(other, 1);
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(foreign.Id, HtmlHostRequestOutcome.Finished));
        Assert.Throws<InvalidOperationException>(() => session.InsertInput(foreign.Frame!, "bad", default));
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(checkpoint.Id, HtmlHostRequestOutcome.Finished));
        session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished);
        Assert.Throws<InvalidOperationException>(() => session.InsertInput(outer.Frame!, "bad", default));
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished));
        Drive(session, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
        Assert.Throws<InvalidOperationException>(() => session.DriveInsertedInput(outer.Frame!, 1, default));
    }

    [Test]
    public void CancelWhileWaitingForResourceInvalidatesCompletionAndFrames()
    {
        var session = Session("<script src=external></script><p id=tail>T</p>");
        var preparation = Prepare(session, 1);
        session.CompleteHostRequest(preparation.Id, HtmlHostRequestOutcome.PendingParsingBlockingScript);
        var execution = Request(Drive(session, 1), HtmlHostRequestKind.WaitForPendingScript);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.CompleteHostRequest(execution.Id, HtmlHostRequestOutcome.Finished, cancellation.Token));
        session.Document.GetElementById("tail").Should().BeNull();
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(execution.Id, HtmlHostRequestOutcome.Finished));
        Assert.Throws<InvalidOperationException>(() => session.InsertInput(preparation.Frame!, "bad", default));
    }

    [Test]
    public void GrammarFlagDoesNotGrantExecutionAndUnterminatedScriptIsAlreadyStarted()
    {
        var session = new HtmlParserSession(Document.CreateHtml(), new HtmlParseOptions { ScriptingEnabled = true });
        session.AppendInput("<script>closed</script><script>unterminated", true);
        Drive(session, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
        var head = (Element) session.Document.DocumentElement!.FirstChild!;
        var closed = (Element) head.FirstChild!;
        var unterminated = (Element) head.LastChild!;
        unterminated.GetHtmlState()!.Script!.AlreadyStarted.Should().BeTrue();
        closed.GetHtmlState()!.Script!.ParserInserted.Should().BeTrue();
        var clone = (Element) unterminated.CloneNode(true);
        clone.GetHtmlState()!.Script!.AlreadyStarted.Should().BeTrue();
        clone.GetHtmlState()!.Script!.ParserInserted.Should().BeFalse();
        clone.GetHtmlState()!.Script!.ForceAsync.Should().BeTrue();
        var executable = Session("<body><script>unterminated");
        Drive(executable, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
        ((Element) executable.Document.DocumentElement!.LastChild!.LastChild!).GetHtmlState()!.Script!.AlreadyStarted.Should().BeTrue();
    }

    [Test]
    public void TemplateScriptsAreInertAndAdoptedOpenElementsKeepParserIdentity()
    {
        var session = Session("<body><template><script>inert</script></template><div id=open><script>outer</script><p id=later>L</p></div>");
        var inert = Prepare(session, 1);
        inert.Script.TextContent().Should().Be("inert");
        inert.Script.OwnerDocument.Should().NotBeSameAs(session.Document);
        session.CompleteHostRequest(inert.Id, HtmlHostRequestOutcome.Finished);
        var outer = Prepare(session, 1);
        outer.Script.TextContent().Should().Be("outer");
        var open = session.Document.GetElementById("open")!;
        var other = Document.CreateHtml();
        other.AdoptNode(open);
        outer.Script.GetHtmlState()!.Script!.ParserDocument.Should().BeSameAs(session.Document);
        outer.Script.OwnerDocument.Should().BeSameAs(other);
        Write(session, outer.Frame!, "<span id=inserted>I</span>", 1);
        ((Element) open.LastChild!).OwnerDocument.Should().BeSameAs(other);
        session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
        open.LastChild!.TextContent().Should().Be("L");
    }

    [Test]
    public void InsertedInputLimitsAndCancellationRemainTerminal()
    {
        const string source = "<script>x</script>";
        var session = new HtmlParserSession(Document.CreateHtml(), new HtmlParseOptions
        {
            Limits = new ParseLimits { MaxInputCharacters = source.Length + 1 }
        }, enableScriptRequests: true);
        session.AppendInput(source, true);
        var outer = Prepare(session, 1);
        Write(session, outer.Frame!, "a", 1);
        Assert.Throws<ParseLimitException>(() => session.InsertInput(outer.Frame!, "b", default));
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished));

        var canceled = Session(source);
        var request = Prepare(canceled, 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => canceled.InsertInput(request.Frame!, "bad", cancellation.Token));
        Assert.Throws<InvalidOperationException>(() => canceled.Drive(1, default));
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void SvgProcessingPausesWritesUntilFrameUnwinds(int quota)
    {
        var session = Session("<body><svg><script>svg</script><script/></svg><p id=tail>T</p>");
        var svg = Request(Drive(session, quota), HtmlHostRequestKind.ProcessSvgScript);
        svg.Script.NamespaceUri.Should().Be(Namespaces.Svg);
        svg.NestingLevel.Should().Be(1);
        session.InsertInput(svg.Frame!, "<g id=written></g>", default);
        Drive(session, quota, svg.Frame).Kind.Should().Be(HtmlParseStepKind.ParserPaused);
        session.Document.GetElementById("written").Should().BeNull();
        session.CompleteHostRequest(svg.Id, HtmlHostRequestOutcome.Finished);
        var selfClosing = Request(Drive(session, quota), HtmlHostRequestKind.ProcessSvgScript);
        session.Document.GetElementById("written").Should().NotBeNull();
        selfClosing.Script.Should().NotBeSameAs(svg.Script);
        session.CompleteHostRequest(selfClosing.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.Complete);
    }

    [Test]
    public void AsyncAdditionClearsForceAsyncPermanentlyIncludingClonesAndParserBatches()
    {
        var document = Document.CreateHtml();
        var script = document.CreateElement("script");
        var state = script.GetHtmlState()!.Script!;
        state.ForceAsync.Should().BeTrue();
        script.SetAttributeNS("urn:test", "t:async", "");
        state.ForceAsync.Should().BeTrue();
        script.SetAttribute("async", "");
        state.ForceAsync.Should().BeFalse();
        script.RemoveAttribute("async");
        state.ForceAsync.Should().BeFalse();
        script.SetAttribute("async", "");
        state.AlreadyStarted = true;
        state.ParserDocument = document;
        state.PreparationTimeDocument = document;
        var clone = (Element) script.CloneNode(false);
        var copy = clone.GetHtmlState()!.Script!;
        copy.AlreadyStarted.Should().BeTrue();
        copy.ForceAsync.Should().BeFalse();
        copy.ParserDocument.Should().BeNull();
        copy.PreparationTimeDocument.Should().BeNull();
        var unstarted = (Element) document.CreateElement("script").CloneNode(false);
        unstarted.GetHtmlState()!.Script!.AlreadyStarted.Should().BeFalse();
        var parsed = Session("<script async>x</script>");
        var request = Prepare(parsed, 1);
        ((Element) request.Script.CloneNode(false)).GetHtmlState()!.Script!.ForceAsync.Should().BeFalse();
    }

    [TestCase((int) HtmlParserScriptingMode.Normal, true, false)]
    [TestCase((int) HtmlParserScriptingMode.Disabled, true, false)]
    [TestCase((int) HtmlParserScriptingMode.Inert, true, true)]
    [TestCase((int) HtmlParserScriptingMode.Fragment, false, false)]
    public void ScriptingModesPreserveProvenanceWithoutExecutionPermission(
        int mode, bool parserInserted, bool alreadyStarted)
    {
        var session = new HtmlParserSession(Document.CreateHtml(), scriptingMode: (HtmlParserScriptingMode) mode);
        session.AppendInput("<script>x</script>", true);
        Drive(session, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
        var state = ((Element) session.Document.DocumentElement!.FirstChild!.FirstChild!).GetHtmlState()!.Script!;
        state.ParserInserted.Should().Be(parserInserted);
        state.AlreadyStarted.Should().Be(alreadyStarted);
        state.ForceAsync.Should().BeFalse();
    }

    [Test]
    public void AbortAtCheckpointAndDuringNestedPreparationInvalidatesEveryCapability()
    {
        var session = Session("<script>x</script><p id=tail>T</p>");
        var checkpoint = Request(Drive(session, 1), HtmlHostRequestKind.MicrotaskCheckpoint);
        var other = Session("<script>other</script>");
        var foreign = Prepare(other, 1);
        Assert.Throws<InvalidOperationException>(() => session.DriveInsertedInput(foreign.Frame!, 1, default));
        session.Abort(1).Kind.Should().Be(HtmlParseStepKind.Complete);
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(checkpoint.Id, HtmlHostRequestOutcome.Finished));
        session.Document.GetElementById("tail").Should().BeNull();
        var nested = Session("<script>outer</script>");
        var outer = Prepare(nested, 1);
        nested.InsertInput(outer.Frame!, "<script>child</script>", default);
        var child = Request(Drive(nested, 1, outer.Frame), HtmlHostRequestKind.PrepareScript);
        nested.Abort(1).Kind.Should().Be(HtmlParseStepKind.Yielded);
        Assert.Throws<InvalidOperationException>(() => nested.CompleteHostRequest(child.Id, HtmlHostRequestOutcome.Finished));
        Assert.Throws<InvalidOperationException>(() => nested.InsertInput(outer.Frame!, "bad", default));
        nested.Abort(1).Kind.Should().Be(HtmlParseStepKind.Complete);
        outer.Frame!.Completed.Should().BeTrue();
        child.Frame!.Completed.Should().BeTrue();
    }

    [Test]
    public void ExecutingBlockerCanCreateAnotherPendingBlockerWithoutOverwritingIt()
    {
        var session = Session("<body><script src=first></script><p id=tail>T</p>");
        var preparation = Prepare(session, 1);
        session.CompleteHostRequest(preparation.Id, HtmlHostRequestOutcome.PendingParsingBlockingScript);
        var wait = Request(Drive(session, 1), HtmlHostRequestKind.WaitForPendingScript);
        var other = Document.CreateHtml();
        other.AdoptNode(wait.Script);
        wait.ParserDocument.Should().BeSameAs(session.Document);
        wait.Script.OwnerDocument.Should().BeSameAs(other);
        session.CompleteHostRequest(wait.Id, HtmlHostRequestOutcome.Finished);
        var execute = Request(Drive(session, 1), HtmlHostRequestKind.ExecutePendingScript);
        session.InsertInput(execute.Frame!, "<script src=second></script><p id=suffix>S</p>", default);
        var second = Request(Drive(session, 1, execute.Frame), HtmlHostRequestKind.PrepareScript);
        second.NestingLevel.Should().Be(2);
        session.CompleteHostRequest(second.Id, HtmlHostRequestOutcome.PendingParsingBlockingScript);
        Drive(session, 1, execute.Frame).Kind.Should().Be(HtmlParseStepKind.PendingBlocker);
        session.CompleteHostRequest(execute.Id, HtmlHostRequestOutcome.Finished);
        var secondWait = Request(Drive(session, 1), HtmlHostRequestKind.WaitForPendingScript);
        secondWait.Script.Should().BeSameAs(second.Script);
        session.CompleteHostRequest(secondWait.Id, HtmlHostRequestOutcome.Finished);
        var secondExecute = Request(Drive(session, 1), HtmlHostRequestKind.ExecutePendingScript);
        session.CompleteHostRequest(secondExecute.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
    }

    [Test]
    public void MovingOpenTemplateScriptDoesNotPermanentlyVetoPreparation()
    {
        var session = new HtmlParserSession(Document.CreateHtml(), enableScriptRequests: true);
        session.AppendInput("<body><template><script>x");
        Drive(session, 1).Kind.Should().Be(HtmlParseStepKind.NeedInput);
        var body = (Element) session.Document.DocumentElement!.LastChild!;
        var template = (Element) body.FirstChild!;
        var script = (Element) template.TemplateContent!.FirstChild!;
        body.AppendChild(script);
        session.AppendInput("</script></template>", true);
        var request = Prepare(session, 1);
        request.Script.Should().BeSameAs(script);
        request.Script.OwnerDocument.Should().BeSameAs(session.Document);
        session.CompleteHostRequest(request.Id, HtmlHostRequestOutcome.Finished);
        Drive(session, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(10000)]
    public void NestedSvgPausePersistsThroughOuterPreparationUntilNestingZero(int quota)
    {
        var session = Session("<body><script>outer</script><p id=tail>T</p>");
        var outer = Prepare(session, quota);
        session.InsertInput(outer.Frame!, "<svg><script/></svg><p id=late>L</p>", default);
        var svg = Request(Drive(session, quota, outer.Frame), HtmlHostRequestKind.ProcessSvgScript);
        svg.NestingLevel.Should().Be(2);
        session.InsertInput(svg.Frame!, "<g id=svg-write/>", default);
        session.CompleteHostRequest(svg.Id, HtmlHostRequestOutcome.Finished);
        session.ParserPaused.Should().BeTrue();
        Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.ParserPaused);
        session.Document.GetElementById("late").Should().BeNull();
        session.InsertInput(outer.Frame!, "<p id=another>A</p>", default);
        Drive(session, quota, outer.Frame).Kind.Should().Be(HtmlParseStepKind.ParserPaused);
        session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished);
        session.ParserPaused.Should().BeFalse();
        Drive(session, quota).Kind.Should().Be(HtmlParseStepKind.Complete);
        session.Document.GetElementById("late").Should().NotBeNull();
        session.Document.GetElementById("another").Should().NotBeNull();
    }

    [Test]
    public void CanceledAbortInvalidatesImmediatelyAndCanLaterDrainMarkers()
    {
        var session = Session("<script>outer</script>");
        var outer = Prepare(session, 1);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => session.Abort(1, canceled.Token));
        Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(outer.Id, HtmlHostRequestOutcome.Finished));
        Assert.Throws<InvalidOperationException>(() => session.InsertInput(outer.Frame!, "bad", default));
        session.Abort(1).Kind.Should().Be(HtmlParseStepKind.Complete);
        outer.Frame!.Completed.Should().BeTrue();
    }

    [Test]
    public void CancellationCleanupHasBoundedWorkEvenBeyondTwoHundredFiftySixFrames()
    {
        var session = Session("<script>first</script>");
        var requests = new List<HtmlHostRequest> { Prepare(session, 1) };
        for (var i = 1; i < 300; i++)
        {
            session.InsertInput(requests[^1].Frame!, "<script>nested</script>", default);
            requests.Add(Request(Drive(session, 1, requests[^1].Frame), HtmlHostRequestKind.PrepareScript));
        }
        session.ScriptNestingLevel.Should().Be(300);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var before = session.WorkCount;
        Assert.Throws<OperationCanceledException>(() => session.DriveInsertedInput(requests[^1].Frame!, 1, canceled.Token));
        (session.WorkCount - before).Should().Be(256);
        session.ScriptNestingLevel.Should().Be(44);
        foreach (var request in requests)
            Assert.Throws<InvalidOperationException>(() => session.CompleteHostRequest(request.Id, HtmlHostRequestOutcome.Finished));
        var drained = 0;
        HtmlParseStep step;
        do
        {
            before = session.WorkCount;
            step = session.Abort(3);
            (session.WorkCount - before).Should().BeLessThanOrEqualTo(3);
            drained++;
        } while (step.Kind == HtmlParseStepKind.Yielded);
        drained.Should().Be(15);
        requests.Should().OnlyContain(request => request.Frame!.Completed);
    }

    [Test]
    public void CloneAndImportCopyOnlyStartedStateThroughTemplatesAndClonableShadows()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var template = document.CreateElement("template");
        var original = document.CreateElement("script");
        original.SetAttribute("async", "");
        var state = original.GetHtmlState()!.Script!;
        state.AlreadyStarted = true;
        state.ParserDocument = document;
        state.PreparationTimeDocument = document;
        template.TemplateContent!.AppendChild(original);
        host.AppendChild(template);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open, Clonable: true), default);
        var unstarted = document.CreateElement("script");
        unstarted.GetHtmlState()!.Script!.ParserDocument = document;
        unstarted.GetHtmlState()!.Script!.ForceAsync = false;
        shadow.AppendChild(unstarted);
        var destination = Document.CreateHtml();
        foreach (var copy in new[] { (Element) host.CloneNode(true), (Element) destination.ImportNode(host, true) })
        {
            var templateCopy = (Element) copy.FirstChild!;
            var startedCopy = ((Element) templateCopy.TemplateContent!.FirstChild!).GetHtmlState()!.Script!;
            startedCopy.AlreadyStarted.Should().BeTrue();
            startedCopy.ParserDocument.Should().BeNull();
            startedCopy.PreparationTimeDocument.Should().BeNull();
            startedCopy.ForceAsync.Should().BeFalse();
            var shadowCopy = ((Element) copy.AttachedShadowRoot!.FirstChild!).GetHtmlState()!.Script!;
            shadowCopy.AlreadyStarted.Should().BeFalse();
            shadowCopy.ParserDocument.Should().BeNull();
            shadowCopy.PreparationTimeDocument.Should().BeNull();
            shadowCopy.ForceAsync.Should().BeTrue();
        }
    }

    [Test]
    public void ReplacementAndSecondaryDocumentWritesUseTheirOwnSessionUntilClose()
    {
        var old = Session("<script>old</script><p id=unread>T</p>");
        var oldPreparation = Prepare(old, 1);
        old.Abort(1).Kind.Should().Be(HtmlParseStepKind.Complete);
        var replacement = new HtmlParserSession(Document.CreateHtml(), enableScriptRequests: true);
        replacement.AppendInput("<body><script>new</script>");
        var script = Prepare(replacement, 1);
        Assert.Throws<InvalidOperationException>(() => replacement.CompleteHostRequest(oldPreparation.Id, HtmlHostRequestOutcome.Finished));
        Assert.Throws<InvalidOperationException>(() => replacement.InsertInput(oldPreparation.Frame!, "bad", default));
        Write(replacement, script.Frame!, "<p id=secondary>S</p>", 1);
        replacement.Document.GetElementById("secondary").Should().NotBeNull();
        replacement.CompleteHostRequest(script.Id, HtmlHostRequestOutcome.Finished);
        Drive(replacement, 1).Kind.Should().Be(HtmlParseStepKind.NeedInput);
        replacement.AppendInput("<p id=close>C</p>", true);
        Drive(replacement, 1).Kind.Should().Be(HtmlParseStepKind.Complete);
        old.Document.GetElementById("unread").Should().BeNull();
        replacement.Document.GetElementById("close").Should().NotBeNull();
    }

    private static HtmlParserSession Session(string text, bool scriptingEnabled = false)
    {
        var session = new HtmlParserSession(Document.CreateHtml(), new HtmlParseOptions { ScriptingEnabled = scriptingEnabled },
            enableScriptRequests: true);
        session.AppendInput(text, true);
        return session;
    }

    private static HtmlHostRequest Prepare(HtmlParserSession session, int quota)
    {
        var checkpoint = Request(Drive(session, quota), HtmlHostRequestKind.MicrotaskCheckpoint);
        session.CompleteHostRequest(checkpoint.Id, HtmlHostRequestOutcome.Finished);
        return Request(Drive(session, quota), HtmlHostRequestKind.PrepareScript);
    }

    private static HtmlHostRequest Request(HtmlParseStep step, HtmlHostRequestKind kind)
    {
        step.Kind.Should().Be(HtmlParseStepKind.HostRequest);
        step.HostRequest!.Kind.Should().Be(kind);
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

    private static void Write(HtmlParserSession session, HtmlScriptFrame frame, string text, int quota)
    {
        session.InsertInput(frame, text, default);
        Drive(session, quota, frame).Kind.Should().Be(HtmlParseStepKind.InsertionBoundary);
    }
}

internal static class HtmlScriptHostQueries
{
    internal static Element? GetElementById(this Document document, string id)
        => NodeTraversal.DescendantElements(document, default).FirstOrDefault(element => element.GetAttribute("id") == id);

    internal static string TextContent(this Node node)
    {
        var text = new System.Text.StringBuilder();
        var pending = new Stack<Node>();
        foreach (var child in node.ChildNodes.Reverse()) pending.Push(child);
        while (pending.TryPop(out var child))
        {
            if (child is Text value) text.Append(value.Data);
            else foreach (var descendant in child.ChildNodes.Reverse()) pending.Push(descendant);
        }
        return text.ToString();
    }
}
