using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Files;
using Jint.Browser.Events;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.Browser.Events;

public sealed class BrowserSelectorControlFactsTests
{
    [TestCase("<input id=i required pattern='[' value=unread>", "read-write")]
    [TestCase("<input id=i type=checkbox placeholder=hint>", "placeholder")]
    [TestCase("<input id=i type=text min=1 max=2 value=unread>", "range")]
    [TestCase("<input id=i required disabled value=unread>", "validity")]
    public void DemandAndApplicabilityRejectBeforeValueState(string html, string family)
    {
        using var fixture = DomTestFixture.Create(html);
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var mask = family switch
        {
            "read-write" => SelectorControlFactMask.ReadWrite,
            "placeholder" => SelectorControlFactMask.PlaceholderShown,
            "range" => SelectorControlFactMask.Range,
            _ => SelectorControlFactMask.Validity,
        };
        Read(fixture, input, mask);
        input.ExistingInputValueState.Should().BeNull();
    }

    [Test]
    public void InvalidNumberPresentationDoesNotShowThePlaceholder()
    {
        using var fixture = DomTestFixture.Create("<input id=i type=number placeholder=hint>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var state = input.GetHtmlState()!.GetInputValueState(default)!;
        state.ApplyNumberUserValue("1e", new HtmlTextSelection(2, 2, HtmlSelectionDirection.None), null, default);
        state.GetValue(default).Should().BeEmpty();
        state.GetEditingValue(default).Should().Be("1e");
        Read(fixture, input, SelectorControlFactMask.PlaceholderShown).PlaceholderShown.Should().BeFalse();
    }

    [TestCase("<input id=i type=number>", "NotApplicable")]
    [TestCase("<input id=i type=number min=invalid max=invalid>", "NotApplicable")]
    [TestCase("<input id=i type=number min=0 max=10>", "InRange")]
    [TestCase("<input id=i type=number min=0 max=10 step=3 value=1>", "InRange")]
    [TestCase("<input id=i type=number min=0 max=10 value=11>", "OutOfRange")]
    [TestCase("<input id=i type=range value=1000>", "InRange")]
    [TestCase("<input id=i type=time min=23:00 max=01:00 value=00:30>", "InRange")]
    [TestCase("<input id=i type=time min=23:00 max=01:00 value=12:00>", "OutOfRange")]
    public void RangeKeepsNativeLimitsEmptyStepAndReversedTimeFacts(string html, string expected)
    {
        using var fixture = DomTestFixture.Create(html);
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        Read(fixture, input, SelectorControlFactMask.Range).Range.ToString().Should().Be(expected);
    }

    [Test]
    public void ValidityUsesActualFormOwnershipAndFieldsetDescendantsWithoutInvalidEvents()
    {
        using var fixture = DomTestFixture.Create("""
            <input id=external required form=f>
            <form id=f><input required form=g></form><form id=g></form>
            <fieldset id=empty><input required disabled></fieldset>
            <fieldset id=nested><fieldset><input required></fieldset></fieldset>
            """);
        fixture.Execute("var invalids=0; document.addEventListener('invalid',()=>invalids++,true);");
        var form = ContentDom.ElementById(fixture.Document, "f")!;
        var empty = ContentDom.ElementById(fixture.Document, "empty")!;
        var nested = ContentDom.ElementById(fixture.Document, "nested")!;
        Read(fixture, form, SelectorControlFactMask.Validity).Validity.Should().Be(SelectorControlValidity.Invalid);
        Read(fixture, empty, SelectorControlFactMask.Validity).Validity.Should().Be(SelectorControlValidity.Valid);
        Read(fixture, nested, SelectorControlFactMask.Validity).Validity.Should().Be(SelectorControlValidity.Invalid);
        fixture.Number("invalids").Should().Be(0);
    }

    [Test]
    public void CachedFactsRejectCustomValidityChangesAtTheNextCheckpoint()
    {
        using var fixture = DomTestFixture.Create("<input id=i value=valid>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var factory = BrowserSelectorControlFacts.Factory;
        var realm = DomRealm.Of(fixture.Engine);
        var source = factory.Create(realm, fixture.Document, factory.ReadRevision(realm, fixture.Document));
        Action? change = null;
        var work = new SelectorMatchWork(input, default, () => change?.Invoke());
        source.Read(input, SelectorControlFactMask.Validity, ref work).Validity.Should().Be(SelectorControlValidity.Valid);
        change = () => BrowserControlValidation.SetCustomValidity(input, "invalid now");
        Action read = () => source.Read(input, SelectorControlFactMask.Validity, ref work);
        read.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
    }

    [Test]
    public void FirstPreparedReadReconcilesFileHistoryBeforeCapturingTheSelectorView()
    {
        using var fixture = DomTestFixture.Create("<input id=i type=file required>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var realm = DomRealm.Of(fixture.Engine);
        var files = FileTransferRealm.Of(fixture.Engine);
        var shared = files.NewFileList();
        shared.Add(new Jint.WebApi.Files.JsFile(fixture.Engine, new byte[] { 1 }, "text/plain", "old.txt", 0));
        files.SetInputFiles(input, shared);
        input.SetAttribute("type", "text");
        input.SetAttribute("type", "file");
        BrowserSelectorControlFacts.PrepareControlFactsRead(realm, realm.NativeReadCheckpoint, realm.CancellationToken);
        var stamp = fixture.Document.MutationStamp;
        var revision = BrowserSelectorSemanticRevision.Read(fixture.Document);
        var source = BrowserSelectorControlFacts.Factory.Create(realm, fixture.Document, revision);
        var work = new SelectorMatchWork(input, default, fixture.Engine.Constraints.Check);
        source.Read(input, SelectorControlFactMask.Validity, ref work).Validity.Should().Be(SelectorControlValidity.Invalid);
        fixture.Document.MutationStamp.Should().Be(stamp);
        BrowserSelectorSemanticRevision.Read(fixture.Document).Should().Be(revision);
        files.InputFiles(input, new DomReadWork(null, default)).Should().BeNull();
        shared.Length.Should().Be(1);
    }

    [Test]
    public void UnpreparedFileReadRejectsRatherThanRefreshingCapturedRevisions()
    {
        using var fixture = DomTestFixture.Create("<input id=i type=file>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var files = FileTransferRealm.Of(fixture.Engine);
        files.SetInputFiles(input, files.NewFileList());
        input.SetAttribute("type", "text");
        input.SetAttribute("type", "file");
        var stamp = fixture.Document.MutationStamp;
        var revision = BrowserSelectorSemanticRevision.Read(fixture.Document);
        Action read = () => files.InputFiles(input, new DomReadWork(null, default));
        read.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
        fixture.Document.MutationStamp.Should().Be(stamp);
        BrowserSelectorSemanticRevision.Read(fixture.Document).Should().Be(revision);
    }

    [Test]
    public void SaturatedCapturedRevisionCannotPublishFacts()
    {
        using var fixture = DomTestFixture.Create("<input id=i>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var source = BrowserSelectorControlFacts.Factory.Create(DomRealm.Of(fixture.Engine), fixture.Document, ulong.MaxValue);
        var work = new SelectorMatchWork(input, default);
        Action read = () => source.Read(input, SelectorControlFactMask.ReadWrite, ref work);
        read.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
        input.ExistingInputValueState.Should().BeNull();
    }

    [Test]
    public void CachedFactsRejectAdoptionIntoAnotherDocument()
    {
        using var fixture = DomTestFixture.Create("<input id=i>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var realm = DomRealm.Of(fixture.Engine);
        var source = BrowserSelectorControlFacts.Factory.Create(realm, fixture.Document,
            BrowserSelectorSemanticRevision.Read(fixture.Document));
        var work = new SelectorMatchWork(input, default);
        source.Read(input, SelectorControlFactMask.ReadWrite, ref work).ReadWrite.Should().BeTrue();
        Document.CreateHtml().AdoptNode(input);
        Action read = () => source.Read(input, SelectorControlFactMask.ReadWrite, ref work);
        read.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
        input.ExistingInputValueState.Should().BeNull();
    }

    [Test]
    public void CachedFactsStillCheckCancellationAfterTheCallback()
    {
        using var fixture = DomTestFixture.Create("<input id=i>");
        var input = ContentDom.ElementById(fixture.Document, "i")!;
        var realm = DomRealm.Of(fixture.Engine);
        var source = BrowserSelectorControlFacts.Factory.Create(realm, fixture.Document,
            BrowserSelectorSemanticRevision.Read(fixture.Document));
        using var cancellation = new CancellationTokenSource();
        var cancel = false;
        var work = new SelectorMatchWork(input, cancellation.Token, () =>
        {
            if (cancel) cancellation.Cancel();
        });
        source.Read(input, SelectorControlFactMask.ReadWrite, ref work).ReadWrite.Should().BeTrue();
        cancel = true;
        Action read = () => source.Read(input, SelectorControlFactMask.ReadWrite, ref work);
        read.Should().Throw<OperationCanceledException>();
        input.ExistingInputValueState.Should().BeNull();
    }

    [Test]
    public void DefaultSubmitUsesActualOwnerAndLeavesUnrelatedValueStatesCold()
    {
        using var fixture = DomTestFixture.Create("""
            <input id=first type=image disabled form=f>
            <form id=f><button id=second>Second</button><input id=unrelated required pattern='[' value=unread></form>
            """);
        var realm = DomRealm.Of(fixture.Engine);
        var source = BrowserSelectorControlFacts.Factory.Create(realm, fixture.Document,
            BrowserSelectorSemanticRevision.Read(fixture.Document));
        var work = new SelectorMatchWork(fixture.Document, default);
        var first = ContentDom.ElementById(fixture.Document, "first")!;
        var second = ContentDom.ElementById(fixture.Document, "second")!;
        source.Read(first, SelectorControlFactMask.DefaultSubmit, ref work).DefaultSubmit.Should().BeTrue();
        source.Read(second, SelectorControlFactMask.DefaultSubmit, ref work).DefaultSubmit.Should().BeFalse();
        first.ExistingInputValueState.Should().BeNull();
        ContentDom.ElementById(fixture.Document, "unrelated")!.ExistingInputValueState.Should().BeNull();
    }

    private static SelectorControlFacts Read(DomTestFixture fixture, Element element, SelectorControlFactMask mask)
    {
        var realm = DomRealm.Of(fixture.Engine);
        var factory = BrowserSelectorControlFacts.Factory;
        var source = factory.Create(realm, fixture.Document, factory.ReadRevision(realm, fixture.Document));
        var work = new SelectorMatchWork(element, default, fixture.Engine.Constraints.Check);
        return source.Read(element, mask, ref work);
    }
}
