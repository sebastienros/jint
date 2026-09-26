using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Files;
using Jint.Browser.Events;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Events;

public sealed class BrowserSelectorControlHelperTests
{
    [TestCase("<input id=i type=hidden value=unread>")]
    [TestCase("<input id=i type=reset value=unread>")]
    [TestCase("<input id=i type=date readonly value=unread>")]
    [TestCase("<input id=i disabled value=unread>")]
    [TestCase("<datalist><input id=i value=unread></datalist>")]
    public void CandidateRejectionLeavesNativeInputValueCold(string html)
    {
        using var fixture = DomTestFixture.Create(html);
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        input.ExistingInputValueState.Should().BeNull();
        BrowserControlValidation.WillValidate(DomRealm.Of(fixture.Engine), input).Should().BeFalse();
        input.ExistingInputValueState.Should().BeNull();
    }

    [Test]
    public void CustomValidityRevisionChangesOnlyWhenItsMessageChanges()
    {
        using var fixture = DomTestFixture.Create("<input id=i>");
        var document = fixture.Document;
        var input = ContentDom.ElementById(document, "i")!;
        var stamp = document.MutationStamp;
        BrowserSelectorSemanticRevision.Read(document).Should().Be(0UL);
        BrowserControlValidation.SetCustomValidity(input, "error");
        BrowserSelectorSemanticRevision.Read(document).Should().Be(1UL);
        BrowserControlValidation.SetCustomValidity(input, "error");
        BrowserSelectorSemanticRevision.Read(document).Should().Be(1UL);
        BrowserControlValidation.SetCustomValidity(input, "");
        BrowserSelectorSemanticRevision.Read(document).Should().Be(2UL);
        document.MutationStamp.Should().Be(stamp, "custom validity is Browser state rather than a native tree write");
        input.ExistingInputValueState.Should().BeNull();
    }

    [Test]
    public void DesignModeRevisionChangesOnlyOnAnEffectiveTransition()
    {
        using var fixture = DomTestFixture.Create("<p>text</p>");
        var realm = DomRealm.Of(fixture.Engine);
        var document = fixture.Document;
        DomDocumentEditing.Set(realm, document, "off");
        BrowserSelectorSemanticRevision.Read(document).Should().Be(0UL);
        DomDocumentEditing.Set(realm, document, "on");
        BrowserSelectorSemanticRevision.Read(document).Should().Be(1UL);
        DomDocumentEditing.Set(realm, document, "on");
        BrowserSelectorSemanticRevision.Read(document).Should().Be(1UL);
        DomDocumentEditing.Set(realm, document, "invalid");
        BrowserSelectorSemanticRevision.Read(document).Should().Be(1UL);
        DomDocumentEditing.Set(realm, document, "off");
        BrowserSelectorSemanticRevision.Read(document).Should().Be(2UL);
    }

    [Test]
    public void FileListIdentityWritesAdvanceRevisionAndSelfAssignmentDoesNot()
    {
        using var fixture = DomTestFixture.Create("<input id=i type=file>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var files = FileTransferRealm.Of(fixture.Engine);
        var first = files.NewFileList();
        files.SetInputFiles(input, first);
        BrowserSelectorSemanticRevision.Read(fixture.Document).Should().Be(1UL);
        files.SetInputFiles(input, first);
        BrowserSelectorSemanticRevision.Read(fixture.Document).Should().Be(1UL);
        files.SetInputFiles(input, files.NewFileList());
        BrowserSelectorSemanticRevision.Read(fixture.Document).Should().Be(2UL);
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void CancelledFileCleanupPreservesTypeHistoryForRetry(int cancelAt)
    {
        using var fixture = DomTestFixture.Create("<input id=i type=file>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var files = FileTransferRealm.Of(fixture.Engine);
        var shared = files.NewFileList();
        shared.Add(new Jint.WebApi.Files.JsFile(fixture.Engine, new byte[] { 1 }, "text/plain", "kept.txt", 0));
        files.SetInputFiles(input, shared);
        input.SetAttribute("type", "text");
        input.SetAttribute("type", "file");
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new DomReadWork(_ =>
        {
            if (++checks == cancelAt) cancellation.Cancel();
        }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => files.InputFiles(input, work));
        // Retry must detach the obsolete selection even though the current type is file again.
        files.InputFiles(input, new DomReadWork(null, default)).Should().BeNull();
        shared.Length.Should().Be(1, "clearing an input must preserve its externally assigned list");
        files.InputFiles(input, create: true)!.Length.Should().Be(0);
    }

    [Test]
    public void DefaultButtonUsesActualOwnerTreeOrderAndIncludesDisabledExternalImageInputs()
    {
        using var fixture = DomTestFixture.Create("""
            <input id=external type=image disabled form=f>
            <form id=f><button id=inside>Inside</button></form>
            """);
        var form = ContentDom.ElementById(fixture.Document, "f")!;
        var external = ContentDom.ElementById(fixture.Document, "external")!;
        BrowserFormDefaults.DefaultButton(DomRealm.Of(fixture.Engine), form).Should().BeSameAs(external);
        external.ExistingInputValueState.Should().BeNull();
    }

    [TestCase("<button command=show-popover></button>", false)]
    [TestCase("<button commandfor=target></button>", false)]
    [TestCase("<button type=invalid></button>", true)]
    [TestCase("<button type=submit command=show-popover></button>", true)]
    public void SubmitClassificationPreservesModernAutoExclusions(string html, bool expected)
    {
        using var fixture = DomTestFixture.Create(html);
        var button = BrowserFormDefaults.InclusiveElements(fixture.Document, new DomReadWork(null, default))
            .Single(element => element.LocalName == "button");
        BrowserFormDefaults.IsSubmitButton(button).Should().Be(expected);
    }

    [Test]
    public void AutoButtonDirectlyParentedByASelectIsNotASubmitButton()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var button = document.CreateElement("button");
        select.AppendChild(button);
        BrowserFormDefaults.IsSubmitButton(button).Should().BeFalse();
        button.SetAttribute("type", "submit");
        BrowserFormDefaults.IsSubmitButton(button).Should().BeTrue();
    }

    [Test]
    public void DefaultButtonWalkPollsAcrossNonElementRuns()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var index = 0; index < 2048; index++) root.AppendChild(document.CreateTextNode("text"));
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new DomReadWork(_ =>
        {
            if (++checks == 2) cancellation.Cancel();
        }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => BrowserFormDefaults.InclusiveElements(root, work).ToArray());
        checks.Should().Be(2);
    }
}
